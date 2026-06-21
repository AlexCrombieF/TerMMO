using UnityEngine;
using UnityEngine.InputSystem;
using Doodgy.Core;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Turns mouse input into validated tile-edit INTENTS:
    ///   - Left click  -> mine  (set target tile to air)
    ///   - Right click -> place (put the selected tile if the target is empty)
    ///
    /// This is the client-side intent producer. It validates locally (reach,
    /// occupancy) and then calls the authoritative <see cref="World.SetTile"/>.
    /// When netcode arrives, this same TryMine/TryPlace logic moves server-side:
    /// the client sends "I want to mine (x,y)", the server re-runs these checks,
    /// and only the server touches World. Tool/hardness gating is added in step 4.
    /// </summary>
    public sealed class WorldEditController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private World world;
        [SerializeField] private Camera worldCamera;

        [Tooltip("Tile placed on right-click. Assign any TileData asset.")]
        [SerializeField] private TileData placeTile;

        [Header("Reach")]
        [Tooltip("Optional origin the player must be near the target from. If null, " +
                 "reach is not enforced (handy before the player exists in step 4).")]
        [SerializeField] private Transform reachOrigin;
        [Tooltip("Max distance in tiles from reachOrigin to a valid edit.")]
        [SerializeField] private float reachTiles = 6f;

        private void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || world == null || worldCamera == null) return;

            Vector3 screen = mouse.position.ReadValue();
            Vector3 worldPos = worldCamera.ScreenToWorldPoint(screen);
            worldPos.z = 0f;
            Vector2Int tile = WorldCoords.WorldToTile(worldPos);

            if (mouse.leftButton.wasPressedThisFrame) TryMine(tile);
            else if (mouse.rightButton.wasPressedThisFrame) TryPlace(tile);
        }

        // --- Intents (validate, then call the authoritative apply) ---

        private void TryMine(Vector2Int tile)
        {
            if (!world.IsLoaded(tile) || !InReach(tile)) return;
            if (world.GetTile(tile) == WorldConstants.AirTileId) return; // nothing there

            world.SetTile(tile, WorldConstants.AirTileId);
        }

        private void TryPlace(Vector2Int tile)
        {
            if (placeTile == null) return;
            if (!world.IsLoaded(tile) || !InReach(tile)) return;
            if (world.GetTile(tile) != WorldConstants.AirTileId) return; // occupied

            world.SetTile(tile, placeTile.Id);
        }

        private bool InReach(Vector2Int tile)
        {
            if (reachOrigin == null) return true; // reach disabled until a player exists
            Vector2Int originTile = WorldCoords.WorldToTile(reachOrigin.position);
            return Vector2Int.Distance(originTile, tile) <= reachTiles;
        }
    }
}
