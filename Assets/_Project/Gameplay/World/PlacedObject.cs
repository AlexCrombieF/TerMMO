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

        private const float PixelsPerTile = 16f;

        private SpriteRenderer _sr;
        private int _w, _h;

        /// <summary>
        /// Shows a sprite pixel-true (16 px = 1 tile), bottom-centred on the
        /// footprint, pivot/trim-agnostic. Called on spawn and again by doors
        /// when they swap between open/closed art (which may trim differently).
        /// </summary>
        public void SetSprite(Sprite spr)
        {
            if (_sr == null || spr == null) return;
            _sr.sprite = spr;

            float s = spr.pixelsPerUnit / PixelsPerTile;
            _sr.transform.localScale = new Vector3(s, s, 1f);

            // Root sits at the footprint centre; seat the drawn content's bottom
            // on the footprint's bottom edge, centred horizontally.
            Bounds b = spr.bounds; // pivot-relative, unscaled
            var desiredCenter = new Vector3(0f, -_h * 0.5f + b.extents.y * s, 0f);
            _sr.transform.localPosition = desiredCenter - b.center * s;
        }

        /// <summary>
        /// Spawns an object from an item's object data over an <c>ObjectSize</c>
        /// footprint whose bottom-left is <paramref name="bottomLeftTile"/>. The
        /// root sits at the footprint centre (unscaled, for a correct collider);
        /// the sprite lives on a scaled child and renders at its natural pixel
        /// size (16 px = 1 tile) — the footprint only defines collision/clicks.
        /// </summary>
        public static GameObject Spawn(ItemData item, Vector2Int bottomLeftTile, int sortingOrder = 6)
        {
            int w = Mathf.Max(1, item.ObjectSize.x);
            int h = Mathf.Max(1, item.ObjectSize.y);

            var root = new GameObject(item.DisplayName);
            root.transform.position = new Vector3(bottomLeftTile.x + w * 0.5f, bottomLeftTile.y + h * 0.5f, 0f);

            var spriteGo = new GameObject("sprite");
            spriteGo.transform.SetParent(root.transform, false);
            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;

            var box = root.AddComponent<BoxCollider2D>();
            box.isTrigger = true; // clickable, but doesn't block movement
            box.size = new Vector2(w, h);

            var po = root.AddComponent<PlacedObject>();
            po.Kind = item.ObjectKind;
            po.Source = item;
            po._sr = sr;
            po._w = w;
            po._h = h;
            po.SetSprite(item.ObjectSprite);

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
