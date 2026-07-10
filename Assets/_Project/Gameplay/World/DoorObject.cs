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
        private PlacedObject _po;
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
            _po = GetComponent<PlacedObject>();
            if (_po != null && _po.Source != null)
            {
                _closedArt = _po.Source.ObjectSprite;
                _openArt = _po.Source.ObjectAltSprite;
            }
        }

        public void Toggle() => SetOpen(!_open);

        public void SetOpen(bool open)
        {
            _open = open;
            _solid.enabled = !open;

            if (_openArt != null && _po != null)
            {
                // Real open/closed art — re-laid-out per sprite, because the two
                // may be trimmed differently by the importer.
                _po.SetSprite(open ? _openArt : _closedArt);
            }
            else if (_sprite != null)
            {
                // No open sprite yet — fade to signal pass-through.
                Color c = _sprite.color;
                c.a = open ? OpenAlpha : 1f;
                _sprite.color = c;
            }
        }
    }
}
