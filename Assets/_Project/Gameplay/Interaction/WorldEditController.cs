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

            // Don't mine/place/pick through open UI (backpack, crafting panel).
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null && es.IsPointerOverGameObject())
            {
                ResetMining();
                _chopTarget = null;
                return;
            }

            Vector2Int tile = MouseTile(mouse);

            // A single left-click on a placed object (workbench) picks it up.
            if (mouse.leftButton.wasPressedThisFrame && TryPickUpObject(mouse, tile))
                return;

            // Chopping a tree under the cursor takes precedence over tile mining.
            if (!HandleChop(mouse, tile))
                HandleMining(mouse, tile);

            if (mouse.rightButton.wasPressedThisFrame)
                TryPlace(tile);
        }

        private bool TryPickUpObject(Mouse mouse, Vector2Int tile)
        {
            Vector3 wp = worldCamera.ScreenToWorldPoint(mouse.position.ReadValue());
            wp.z = 0f;
            Collider2D col = Physics2D.OverlapPoint(wp);
            PlacedObject po = col != null ? col.GetComponent<PlacedObject>() : null;
            if (po == null) return false;
            if (!InReach(tile)) return true; // clicked but out of reach — consume the click

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

            ResetMining(); // we're on a tree, not a tile
            _chopTarget = null;
            if (InReach(tile))
            {
                GetToolStats(out ToolType toolType, out _, out float power);
                if (toolType == ToolType.Axe)
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
