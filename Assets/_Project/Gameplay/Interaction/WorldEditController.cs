using UnityEngine;
using UnityEngine.InputSystem;
using Doodgy.Core;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Turns mouse input into validated tile-edit INTENTS, originating from the
    /// player (attach this to the player GameObject):
    ///   - Hold left  -> mine the targeted tile (progress scales with hardness /
    ///                   mining power; gated by tool type + tier).
    ///   - Right click -> place the selected tile into an empty, in-reach cell.
    ///
    /// Client-side intent producer: validates locally (reach, occupancy, tool),
    /// then calls the authoritative <see cref="World.SetTile"/>. Under netcode the
    /// same TryMine/TryPlace checks run server-side; the client just requests.
    ///
    /// TOOL SOURCE: tool type/tier/power are serialized here for now. Once the
    /// inventory exists (step 5) these read from the equipped/held tool's ItemData
    /// instead — swap <see cref="_toolType"/> etc. for the held item's fields.
    /// </summary>
    public sealed class WorldEditController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private World world;
        [SerializeField] private Camera worldCamera;

        [Tooltip("Tile placed on right-click. Assign any TileData asset.")]
        [SerializeField] private TileData placeTile;

        [Header("Reach")]
        [Tooltip("If true, edits must be within reach of the origin (the player).")]
        [SerializeField] private bool enforceReach = true;
        [Tooltip("Origin to measure reach from. Defaults to this transform.")]
        [SerializeField] private Transform reachOrigin;
        [Tooltip("Max distance, in tiles, from the origin to a valid edit.")]
        [SerializeField] private float reachTiles = 6f;

        [Header("Tool (temporary — comes from equipped item in step 5)")]
        [SerializeField] private ToolType toolType = ToolType.Pickaxe;
        [SerializeField] private int toolTier = 1;
        [Tooltip("Mining speed. time-to-break = hardness / miningPower seconds.")]
        [Min(0.01f)] [SerializeField] private float miningPower = 4f;

        // Hold-to-mine progress state.
        private Vector2Int _miningTile;
        private bool _isMining;
        private float _miningProgress;

        private void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (reachOrigin == null) reachOrigin = transform;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || world == null || worldCamera == null) return;

            Vector2Int tile = MouseTile(mouse);

            HandleMining(mouse, tile);

            if (mouse.rightButton.wasPressedThisFrame)
                TryPlace(tile);
        }

        private Vector2Int MouseTile(Mouse mouse)
        {
            Vector3 worldPos = worldCamera.ScreenToWorldPoint(mouse.position.ReadValue());
            worldPos.z = 0f;
            return WorldCoords.WorldToTile(worldPos);
        }

        // --- Mining (hold; progress gated by hardness + tool) ---

        private void HandleMining(Mouse mouse, Vector2Int tile)
        {
            if (!mouse.leftButton.isPressed) { ResetMining(); return; }
            if (!world.IsLoaded(tile) || !InReach(tile)) { ResetMining(); return; }

            ushort id = world.GetTile(tile);
            if (id == WorldConstants.AirTileId) { ResetMining(); return; }

            TileData data = world.Tiles.Get(id);
            if (data == null || !data.CanBeMinedBy(toolType, toolTier)) { ResetMining(); return; }

            // Switching target resets accumulated progress.
            if (!_isMining || tile != _miningTile)
            {
                _isMining = true;
                _miningTile = tile;
                _miningProgress = 0f;
            }

            _miningProgress += Time.deltaTime * miningPower;
            if (_miningProgress >= data.Hardness)
            {
                world.SetTile(tile, WorldConstants.AirTileId);
                ResetMining();
            }
        }

        private void ResetMining()
        {
            _isMining = false;
            _miningProgress = 0f;
        }

        // --- Placing (instant on press) ---

        private void TryPlace(Vector2Int tile)
        {
            if (placeTile == null) return;
            if (!world.IsLoaded(tile) || !InReach(tile)) return;
            if (world.GetTile(tile) != WorldConstants.AirTileId) return; // occupied

            world.SetTile(tile, placeTile.Id);
        }

        private bool InReach(Vector2Int tile)
        {
            if (!enforceReach) return true;
            Vector3 origin = reachOrigin != null ? reachOrigin.position : transform.position;
            Vector2Int originTile = WorldCoords.WorldToTile(origin);
            return Vector2Int.Distance(originTile, tile) <= reachTiles;
        }
    }
}
