using System.Collections.Generic;
using UnityEngine;

namespace Doodgy.Data
{
    /// <summary>
    /// Registry mapping numeric item ids to <see cref="ItemData"/> assets, the
    /// mirror of TileDatabase. Saves and (later) network packets store item ids;
    /// this resolves them back to assets. Populated by the ContentBuilder.
    /// </summary>
    [CreateAssetMenu(menuName = "Doodgy/Items/Item Database", fileName = "ItemDatabase")]
    public sealed class ItemDatabase : ScriptableObject
    {
        [Tooltip("Every item in the game. Ids must be unique; 0 is reserved for 'no item'.")]
        [SerializeField] private List<ItemData> items = new List<ItemData>();

        private Dictionary<int, ItemData> _byId;

        private void BuildIfNeeded()
        {
            if (_byId != null) return;
            _byId = new Dictionary<int, ItemData>(items.Count);
            foreach (ItemData item in items)
            {
                if (item == null) continue;
                if (item.Id == 0)
                {
                    Debug.LogError($"[ItemDatabase] '{item.name}' uses reserved id 0 — skipped.", this);
                    continue;
                }
                if (_byId.ContainsKey(item.Id))
                {
                    Debug.LogError($"[ItemDatabase] Duplicate item id {item.Id} ('{item.name}') — skipped.", this);
                    continue;
                }
                _byId[item.Id] = item;
            }
        }

        /// <summary>Returns the item for an id, or null for 0 / unknown ids.</summary>
        public ItemData Get(int id)
        {
            if (id == 0) return null;
            BuildIfNeeded();
            return _byId.TryGetValue(id, out ItemData item) ? item : null;
        }

#if UNITY_EDITOR
        private void OnValidate() => _byId = null;
#endif
    }
}
