using UnityEngine;
using Doodgy.Core;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Spawns cave enemies in DARK underground air near the player: below the
    /// surface, standing room with ground beneath, light under the threshold
    /// (torch-lit areas stay safe), and never right on top of the player.
    /// Population capped; despawns enemies that end up far away.
    /// </summary>
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private World world;
        [SerializeField] private LightingSystem lighting;
        [SerializeField] private EnemyData enemyData;
        [SerializeField] private GameObject player;

        [Header("Rules")]
        [SerializeField] private int maxAlive = 4;
        [SerializeField] private float spawnInterval = 5f;
        [Tooltip("Spawn between these distances from the player (tiles).")]
        [SerializeField] private float minDistance = 10f;
        [SerializeField] private float maxDistance = 28f;
        [Tooltip("Only spawn where light is below this (0 = pitch black).")]
        [Range(0f, 1f)] [SerializeField] private float maxLight = 0.25f;
        [Tooltip("Must be at least this many tiles below the surface.")]
        [SerializeField] private int minDepth = 5;
        [SerializeField] private float despawnDistance = 45f;

        private float _timer;
        private readonly System.Random _rng = new System.Random();

        private void Update()
        {
            if (world == null || enemyData == null || player == null) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = spawnInterval;

            DespawnFar();
            if (Enemy.All.Count >= maxAlive) return;

            // A handful of random candidates per tick; give up quietly otherwise.
            for (int attempt = 0; attempt < 12; attempt++)
            {
                if (TrySpawnOnce()) break;
            }
        }

        private bool TrySpawnOnce()
        {
            Vector2Int p = WorldCoords.WorldToTile(player.transform.position);
            int dx = _rng.Next((int)minDistance, (int)maxDistance + 1) * (_rng.Next(2) == 0 ? -1 : 1);
            int dy = _rng.Next(-(int)maxDistance / 2, (int)maxDistance / 2);
            var tile = new Vector2Int(p.x + dx, p.y + dy);

            if (!world.IsLoaded(tile)) return false;

            // Needs standing room (2 air tiles) with solid ground beneath.
            if (world.GetTile(tile) != WorldConstants.AirTileId) return false;
            if (world.GetTile(tile + Vector2Int.up) != WorldConstants.AirTileId) return false;
            if (world.GetTile(tile + Vector2Int.down) == WorldConstants.AirTileId) return false;

            // Underground only: several tiles below the surface of this column.
            if (!IsUnderground(tile)) return false;

            // Dark only — torches keep areas safe.
            if (lighting != null && lighting.GetLight(tile) > maxLight) return false;

            Enemy.Spawn(enemyData, new Vector3(tile.x + 0.5f, tile.y + 0.5f, 0f), player);
            return true;
        }

        private bool IsUnderground(Vector2Int tile)
        {
            for (int y = tile.y + 1; y <= tile.y + minDepth; y++)
            {
                // Any solid ceiling within minDepth above counts as underground...
                if (world.GetTile(new Vector2Int(tile.x, y)) != WorldConstants.AirTileId)
                    return true;
            }
            return false; // ...open sky above = surface, don't spawn.
        }

        private void DespawnFar()
        {
            for (int i = Enemy.All.Count - 1; i >= 0; i--)
            {
                Enemy e = Enemy.All[i];
                if (e == null) continue;
                if (Vector2.Distance(e.transform.position, player.transform.position) > despawnDistance)
                    Destroy(e.gameObject);
            }
        }
    }
}
