using UnityEngine;
using UnityEngine.InputSystem;
using Doodgy.Core;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Turns mouse input into validated tile-edit INTENTS, driven by the player's
    /// currently HELD hotbar item:
    ///   - Hold left  -> mine the targeted tile. A held Tool sets the tool type /
    ///                   tier / mining power; otherwise bare-hand defaults apply.
    ///                   Drops are added to the inventory.
    ///   - Right click -> if the held item is placeable, place its tile into an
    ///                   empty, in-reach cell and consume one.
    ///
    /// Client-side intent producer: validates locally (reach, occupancy, tool),
    /// then calls the authoritative <see cref="World.SetTile"/>. Under netcode the
    /// same checks run server-side and only the server applies + rolls drops.
    /// </summary>
    public sealed class WorldEditController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private World world;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private PlayerInventory inventory;

        [Header("Reach")]
        [SerializeField] private bool enforceReach = true;
        [SerializeField] private Transform reachOrigin;
        [Tooltip("Default reach (tiles) when not holding a tool that defines its own.")]
        [SerializeField] private float reachTiles = 6f;

        [Header("Bare-hand defaults (when not holding a tool)")]
        [SerializeField] private ToolType handToolType = ToolType.None;
        [SerializeField] private int handToolTier = 0;
        [Min(0.01f)] [SerializeField] private float handMiningPower = 2f;

        [Header("Combat VFX")]
        [SerializeField] private Sprite[] slashFrames;
        [SerializeField] private Sprite[] hitFrames;
        [SerializeField] private float vfxFps = 20f;

        // Hold-to-mine progress state.
        private Vector2Int _miningTile;
        private bool _isMining;
        private float _miningProgress;
        private float _miningHardness;

        /// <summary>True while actively mining a tile (not chopping).</summary>
        public bool IsMiningTile => _isMining;
        /// <summary>The tile currently being mined.</summary>
        public Vector2Int MiningTile => _miningTile;
        /// <summary>Mining completion [0..1] of the current tile (for crack visuals).</summary>
        public float MiningProgress01 => _miningHardness > 0f ? Mathf.Clamp01(_miningProgress / _miningHardness) : 0f;

        // Chop feedback state.
        private Choppable _chopTarget;
        private Vector2Int _chopTile;
        public bool IsChopping => _chopTarget != null;
        public Vector2Int ChopTile => _chopTile;
        public float ChopProgress01 => _chopTarget != null ? _chopTarget.Progress01 : 0f;

        // Seeded, server-side drop rolls (deterministic / replayable).
        private System.Random _rng;

        // Melee swing state.
        private float _swingTimer;
        private const float MeleeRange = 1.9f;   // tiles from the player's centre
        private const float MeleeHalfHeight = 1.4f;

        /// <summary>True while the held item should visibly swing (mining, chopping,
        /// attacking). Read by HeldItemDisplay — purely visual.</summary>
        public bool SwingActive { get; private set; }

        /// <summary>Tile under the cursor this frame (valid when HoverValid).</summary>
        public Vector2Int HoveredTile { get; private set; }
        /// <summary>True when the hovered tile is a legitimate target: in reach and
        /// either mineable, or empty while holding something placeable.</summary>
        public bool HoverValid { get; private set; }

        private void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (reachOrigin == null) reachOrigin = transform;
            _rng = new System.Random();
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || world == null || worldCamera == null) return;

            SwingActive = false;
            HoverValid = false;

            // Don't mine/place/pick through open UI (backpack, crafting panel).
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null && es.IsPointerOverGameObject())
            {
                ResetMining();
                _chopTarget = null;
                return;
            }

            Vector2Int tile = MouseTile(mouse);
            _swingTimer -= Time.deltaTime;

            // A single left-click on a placed object (workbench) picks it up.
            if (mouse.leftButton.wasPressedThisFrame && TryPickUpObject(mouse, tile))
                return;

            // Hover highlight: a solid tile you could mine, or an empty cell you
            // could place into — always within reach.
            HoveredTile = tile;
            if (world.IsLoaded(tile) && InReach(tile))
            {
                bool solid = world.GetTile(tile) != WorldConstants.AirTileId;
                ItemStack heldForHover = inventory != null ? inventory.Held : ItemStack.Empty;
                bool placeable = !heldForHover.IsEmpty
                                 && (heldForHover.Item.IsPlaceable || heldForHover.Item.IsPlaceableObject);
                HoverValid = solid || (!solid && placeable);
            }

            // Swing visual: any tool or weapon animates while left-click is held.
            ItemStack heldNow = inventory != null ? inventory.Held : ItemStack.Empty;
            SwingActive = mouse.leftButton.isPressed && !heldNow.IsEmpty
                          && (heldNow.Item.Category == ItemCategory.Tool
                              || heldNow.Item.Category == ItemCategory.Weapon);

            // Holding a weapon: left-click swings it instead of mining/chopping.
            if (!heldNow.IsEmpty && heldNow.Item.Category == ItemCategory.Weapon)
            {
                ResetMining();
                _chopTarget = null;
                if (mouse.leftButton.isPressed) TrySwing(heldNow.Item);
            }
            // Chopping a tree under the cursor takes precedence over tile mining.
            else if (!HandleChop(mouse, tile))
                HandleMining(mouse, tile);

            // Right-click: interact with doors/chests first, otherwise place.
            if (mouse.rightButton.wasPressedThisFrame && !TryInteractObject(mouse, tile))
                TryPlace(tile);
        }

        /// <summary>
        /// Melee swing: hits every enemy in a box in front of the player (facing
        /// side), applying the weapon's damage + knockback. Uses the enemy
        /// registry rather than physics queries. Server-side under netcode.
        /// </summary>
        private void TrySwing(ItemData weapon)
        {
            if (_swingTimer > 0f) return;
            _swingTimer = weapon.AttackCooldown;

            var pc = GetComponent<PlayerController>();
            int facing = pc != null ? pc.Facing : 1;
            Vector3 origin = transform.position;

            // Slash arc in front of the player.
            OneShotEffect.Spawn(slashFrames, vfxFps,
                origin + new Vector3(facing * 0.9f, 0.1f, 0f), sortingOrder: 16, flipX: facing < 0);

            for (int i = Enemy.All.Count - 1; i >= 0; i--)
            {
                Enemy e = Enemy.All[i];
                if (e == null) continue;
                Vector3 d = e.transform.position - origin;
                bool inFront = Mathf.Sign(d.x) == facing || Mathf.Abs(d.x) < 0.4f;
                if (!inFront || Mathf.Abs(d.x) > MeleeRange || Mathf.Abs(d.y) > MeleeHalfHeight)
                    continue;

                var knock = new Vector2(facing * weapon.WeaponKnockback, weapon.WeaponKnockback * 0.6f);
                e.TakeDamage(weapon.WeaponDamage, knock);
                OneShotEffect.Spawn(hitFrames, vfxFps, e.transform.position, sortingOrder: 18);
            }
        }

        /// <summary>Right-click interactions on placed objects (door toggle, chest open).</summary>
        private bool TryInteractObject(Mouse mouse, Vector2Int tile)
        {
            Vector3 wp = worldCamera.ScreenToWorldPoint(mouse.position.ReadValue());
            wp.z = 0f;
            Collider2D col = Physics2D.OverlapPoint(wp);
            if (col == null) return false;

            DoorObject door = col.GetComponent<DoorObject>();
            if (door != null)
            {
                if (InReach(tile)) door.Toggle();
                return true;
            }

            ChestObject chest = col.GetComponent<ChestObject>();
            if (chest != null)
            {
                if (InReach(tile))
                {
                    var ui = GetComponent<InventoryUI>();
                    if (ui != null) ui.ToggleChest(chest);
                }
                return true;
            }

            FurnaceObject furnace = col.GetComponent<FurnaceObject>();
            if (furnace != null)
            {
                if (InReach(tile))
                {
                    var ui = GetComponent<InventoryUI>();
                    if (ui != null) ui.ToggleFurnace(furnace);
                }
                return true;
            }

            return false;
        }

        private bool TryPickUpObject(Mouse mouse, Vector2Int tile)
        {
            Vector3 wp = worldCamera.ScreenToWorldPoint(mouse.position.ReadValue());
            wp.z = 0f;
            Collider2D col = Physics2D.OverlapPoint(wp);
            PlacedObject po = col != null ? col.GetComponent<PlacedObject>() : null;
            if (po == null) return false;
            if (!InReach(tile)) return true; // clicked but out of reach — consume the click

            // Storage objects (chest, furnace) must be emptied before pickup — no item loss.
            var holder = po.GetComponent<IHasInventory>();
            if (holder != null && !holder.Inventory.IsEmpty())
            {
                Debug.Log($"[World] Empty the {po.Kind} before picking it up.");
                return true;
            }

            if (po.Source != null && inventory != null)
                ItemPickup.Spawn(po.Source, 1, po.transform.position, inventory);
            Destroy(po.gameObject);
            return true;
        }

        // --- Chopping trees (left-hold with an axe) ---

        private bool HandleChop(Mouse mouse, Vector2Int tile)
        {
            if (!mouse.leftButton.isPressed) { _chopTarget = null; return false; }

            Vector3 wp = worldCamera.ScreenToWorldPoint(mouse.position.ReadValue());
            wp.z = 0f;
            Collider2D col = Physics2D.OverlapPoint(wp);
            Choppable tree = col != null ? col.GetComponent<Choppable>() : null;
            if (tree == null) { _chopTarget = null; return false; }

            ResetMining(); // we're on a tree/decoration, not a tile
            _chopTarget = null;
            if (InReach(tile))
            {
                GetToolStats(out ToolType toolType, out _, out float power);
                if (tree.CanChopWith(toolType))
                {
                    _chopTarget = tree;
                    _chopTile = tile;
                    tree.Chop(power * Time.deltaTime, inventory);
                }
            }
            return true; // tree handled the click; skip tile mining behind it
        }

        private Vector2Int MouseTile(Mouse mouse)
        {
            Vector3 worldPos = worldCamera.ScreenToWorldPoint(mouse.position.ReadValue());
            worldPos.z = 0f;
            return WorldCoords.WorldToTile(worldPos);
        }

        // --- Mining (hold; progress gated by hardness + held/hand tool) ---

        private void HandleMining(Mouse mouse, Vector2Int tile)
        {
            if (!mouse.leftButton.isPressed) { ResetMining(); return; }
            if (!world.IsLoaded(tile) || !InReach(tile)) { ResetMining(); return; }

            ushort id = world.GetTile(tile);
            if (id == WorldConstants.AirTileId) { ResetMining(); return; }

            TileData data = world.Tiles.Get(id);
            if (data == null) { ResetMining(); return; }

            GetToolStats(out ToolType toolType, out int toolTier, out float power);
            if (!data.CanBeMinedBy(toolType, toolTier)) { ResetMining(); return; }

            if (!_isMining || tile != _miningTile)
            {
                _isMining = true;
                _miningTile = tile;
                _miningProgress = 0f;
            }
            _miningHardness = data.Hardness;

            _miningProgress += Time.deltaTime * power;
            if (_miningProgress >= data.Hardness)
            {
                if (world.SetTile(tile, WorldConstants.AirTileId))
                    GrantDrops(data, tile);
                ResetMining();
            }
        }

        private void GrantDrops(TileData data, Vector2Int tile)
        {
            if (inventory == null || data.DropItem == null) return;
            int count = data.RollDropCount(_rng);
            if (count > 0)
            {
                Vector3 pos = new Vector3(tile.x + 0.5f, tile.y + 0.5f, 0f);
                ItemPickup.Spawn(data.DropItem, count, pos, inventory);
            }
        }

        private void ResetMining()
        {
            _isMining = false;
            _miningProgress = 0f;
        }

        // --- Placing (instant on press; consumes the held block) ---

        private void TryPlace(Vector2Int tile)
        {
            if (inventory == null) return;
            ItemStack held = inventory.Held;
            if (held.IsEmpty) return;

            // Edible held item: right-click eats it (only if hurt — no waste).
            if (held.Item.IsEdible)
            {
                var health = GetComponent<PlayerHealth>();
                if (health != null && health.Current < health.Max)
                {
                    health.Heal(held.Item.HealAmount);
                    inventory.ConsumeSelected(1);
                }
                return;
            }

            if (held.Item.IsPlaceableObject) { TryPlaceObject(tile, held.Item); return; }
            if (!held.Item.IsPlaceable) return;

            if (!world.IsLoaded(tile) || !InReach(tile)) return;
            if (world.GetTile(tile) != WorldConstants.AirTileId) return; // occupied

            if (world.SetTile(tile, held.Item.PlacesTile.Id))
                inventory.ConsumeSelected(1);
        }

        private void TryPlaceObject(Vector2Int tile, ItemData item)
        {
            if (!InReach(tile)) return;

            int w = Mathf.Max(1, item.ObjectSize.x);
            int h = Mathf.Max(1, item.ObjectSize.y);
            for (int dx = 0; dx < w; dx++)
                for (int dy = 0; dy < h; dy++)
                {
                    var t = new Vector2Int(tile.x + dx, tile.y + dy);
                    if (!world.IsLoaded(t) || world.GetTile(t) != WorldConstants.AirTileId) return; // needs clear space
                }

            // Furniture needs solid ground under its whole footprint — no
            // floating doors/chests.
            for (int dx = 0; dx < w; dx++)
                if (world.GetTile(new Vector2Int(tile.x + dx, tile.y - 1)) == WorldConstants.AirTileId)
                    return;

            PlacedObject.Spawn(item, tile);
            inventory.ConsumeSelected(1);
        }

        // --- Helpers ---

        private void GetToolStats(out ToolType toolType, out int toolTier, out float power)
        {
            ItemStack held = inventory != null ? inventory.Held : ItemStack.Empty;
            if (!held.IsEmpty && held.Item.Category == ItemCategory.Tool)
            {
                toolType = held.Item.ToolType;
                toolTier = held.Item.ToolTier;
                power = Mathf.Max(0.01f, held.Item.MiningPower);
            }
            else
            {
                toolType = handToolType;
                toolTier = handToolTier;
                power = handMiningPower;
            }
        }

        private bool InReach(Vector2Int tile)
        {
            if (!enforceReach) return true;

            // A held tool may extend reach.
            float reach = reachTiles;
            ItemStack held = inventory != null ? inventory.Held : ItemStack.Empty;
            if (!held.IsEmpty && held.Item.Category == ItemCategory.Tool && held.Item.Reach > 0f)
                reach = held.Item.Reach;

            Vector3 origin = reachOrigin != null ? reachOrigin.position : transform.position;
            Vector2Int originTile = WorldCoords.WorldToTile(origin);
            return Vector2Int.Distance(originTile, tile) <= reach;
        }
    }
}
