using System.Collections.Generic;
using UnityEngine;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// A multi-tile object placed in the world (workbench, furniture...). Unlike
    /// tiles it isn't in the chunk grid — it's a free sprite sized to N x M tiles,
    /// with a trigger collider so it can be clicked to pick up. Keeps a static
    /// registry so systems (e.g. crafting) can ask "is a Workbench nearby?".
    /// </summary>
    public sealed class PlacedObject : MonoBehaviour
    {
        public string Kind;
        /// <summary>The item that placed this, returned when it's picked up.</summary>
        public ItemData Source;

        private static readonly List<PlacedObject> All = new List<PlacedObject>();

        /// <summary>Every live placed object (save system iterates this).</summary>
        public static IReadOnlyList<PlacedObject> Registry => All;

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public static bool AnyNear(string kind, Vector3 pos, float range)
        {
            float r2 = range * range;
            for (int i = 0; i < All.Count; i++)
            {
                PlacedObject o = All[i];
                if (o != null && o.Kind == kind &&
                    ((Vector2)(o.transform.position - pos)).sqrMagnitude <= r2)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Spawns an object from an item's object data over an <c>ObjectSize</c>
        /// footprint whose bottom-left is <paramref name="bottomLeftTile"/>. The
        /// root sits at the footprint centre (unscaled, for a correct collider);
        /// the sprite lives on a scaled child.
        /// </summary>
        public static GameObject Spawn(ItemData item, Vector2Int bottomLeftTile, int sortingOrder = 6)
        {
            Sprite spr = item.ObjectSprite;
            int w = Mathf.Max(1, item.ObjectSize.x);
            int h = Mathf.Max(1, item.ObjectSize.y);

            var root = new GameObject(item.DisplayName);
            root.transform.position = new Vector3(bottomLeftTile.x + w * 0.5f, bottomLeftTile.y + h * 0.5f, 0f);

            // Sprite on a scaled child so the collider on the (unscaled) root is exact.
            var spriteGo = new GameObject("sprite");
            spriteGo.transform.SetParent(root.transform, false);
            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sprite = spr;
            sr.sortingOrder = sortingOrder;
            Vector2 size = spr.bounds.size;
            float sx = size.x > 0f ? w / size.x : 1f;
            float sy = size.y > 0f ? h / size.y : 1f;
            spriteGo.transform.localScale = new Vector3(sx, sy, 1f);
            Vector3 c = spr.bounds.center; // centre the sprite over the footprint (pivot-independent)
            spriteGo.transform.localPosition = -new Vector3(c.x * sx, c.y * sy, 0f);

            var box = root.AddComponent<BoxCollider2D>();
            box.isTrigger = true; // clickable, but doesn't block movement
            box.size = new Vector2(w, h);

            var po = root.AddComponent<PlacedObject>();
            po.Kind = item.ObjectKind;
            po.Source = item;

            // Kind-specific behaviour. Data stays in ItemData; only the behaviour
            // component is chosen here.
            switch (po.Kind)
            {
                case "Chest": root.AddComponent<ChestObject>(); break;
                case "Door": root.AddComponent<DoorObject>(); break;
                case "Furnace": root.AddComponent<FurnaceObject>(); break;
            }

            return root;
        }
    }
}
