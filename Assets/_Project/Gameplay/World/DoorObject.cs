using UnityEngine;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// A door: solid (blocks movement) when closed, walk-through when open.
    /// Right-click toggles it. Until a dedicated open sprite exists, "open" is
    /// shown by fading the door and disabling its solid collider.
    /// </summary>
    [RequireComponent(typeof(PlacedObject))]
    public sealed class DoorObject : MonoBehaviour
    {
        private const float OpenAlpha = 0.35f;

        private BoxCollider2D _solid;
        private SpriteRenderer _sprite;
        private Sprite _closedArt;
        private Sprite _openArt;   // from ItemData.ObjectAltSprite; null = fade instead
        private bool _open;

        public bool IsOpen => _open;

        private void Awake()
        {
            // The PlacedObject root already has a trigger collider (for clicks);
            // add a second, solid collider that actually blocks movement.
            var trigger = GetComponent<BoxCollider2D>();
            _solid = gameObject.AddComponent<BoxCollider2D>();
            _solid.size = trigger != null ? trigger.size : Vector2.one;
            _solid.isTrigger = false;

            _sprite = GetComponentInChildren<SpriteRenderer>();
            if (_sprite != null) _closedArt = _sprite.sprite;

            var po = GetComponent<PlacedObject>();
            if (po != null && po.Source != null) _openArt = po.Source.ObjectAltSprite;
        }

        public void Toggle() => SetOpen(!_open);

        public void SetOpen(bool open)
        {
            _open = open;
            _solid.enabled = !open;
            if (_sprite == null) return;

            if (_openArt != null)
            {
                // Real open/closed art.
                _sprite.sprite = open ? _openArt : _closedArt;
            }
            else
            {
                // No open sprite yet — fade to signal pass-through.
                Color c = _sprite.color;
                c.a = open ? OpenAlpha : 1f;
                _sprite.color = c;
            }
        }
    }
}
