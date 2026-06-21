using System.Collections.Generic;
using UnityEngine;
using Doodgy.Core;

namespace Doodgy.Data
{
    /// <summary>
    /// Designer-authored registry mapping numeric tile ids to <see cref="TileData"/>
    /// assets. Chunks store ids; systems resolve id -> data through this database.
    /// One database asset for the project (drag every TileData into the list).
    ///
    /// The id -> data dictionary is built lazily and is NOT serialized, so it
    /// rebuilds cleanly after domain reload / editor changes.
    /// </summary>
    [CreateAssetMenu(menuName = "Doodgy/World/Tile Database", fileName = "TileDatabase")]
    public sealed class TileDatabase : ScriptableObject
    {
        [Tooltip("Every tile type in the game. Ids must be unique; 0 is reserved " +
                 "for air and must NOT appear here.")]
        [SerializeField] private List<TileData> tiles = new List<TileData>();

        private Dictionary<ushort, TileData> _byId;

        private void BuildIfNeeded()
        {
            if (_byId != null) return;
            _byId = new Dictionary<ushort, TileData>(tiles.Count);
            foreach (TileData t in tiles)
            {
                if (t == null) continue;
                if (t.Id == WorldConstants.AirTileId)
                {
                    Debug.LogError($"[TileDatabase] '{t.name}' uses reserved air id 0 — skipped.", this);
                    continue;
                }
                if (_byId.ContainsKey(t.Id))
                {
                    Debug.LogError($"[TileDatabase] Duplicate tile id {t.Id} ('{t.name}') — skipped.", this);
                    continue;
                }
                _byId[t.Id] = t;
            }
        }

        /// <summary>Returns the tile for an id, or null for air / unknown ids.</summary>
        public TileData Get(ushort id)
        {
            if (id == WorldConstants.AirTileId) return null;
            BuildIfNeeded();
            return _byId.TryGetValue(id, out TileData t) ? t : null;
        }

#if UNITY_EDITOR
        // Invalidate the cache when the list is edited in the inspector.
        private void OnValidate() => _byId = null;
#endif
    }
}
