using UnityEngine;
using Doodgy.Core;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Shows the selected hotbar item held by a swinging ARM, Terraria-style.
    /// Rig: a pivot at the shoulder rotates a front-arm sprite (skin-tinted)
    /// with the item gripped at the arm's end — arm and item swing as one
    /// while mining/chopping/attacking, and settle to a rest pose otherwise.
    ///
    /// Sizing: tools & weapons render larger than regular items (blocks, food)
    /// via per-category scales. Grip convention: the item art's bottom-left
    /// corner is the handle tip. Purely visual.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    [RequireComponent(typeof(PlayerController))]
    public sealed class HeldItemDisplay : MonoBehaviour
    {
        private const float PixelsPerTile = 16f;

        [Header("Art")]
        [Tooltip("Front arm (white/gray for skin tint), drawn VERTICAL hanging " +
                 "down, shoulder at the TOP-CENTRE of the canvas. Optional.")]
        [SerializeField] private Sprite armSprite;

        [Header("Rig")]
        [Tooltip("Shoulder position relative to the player centre.")]
        [SerializeField] private Vector2 shoulderOffset = new Vector2(0.06f, 0.3f);
        [Tooltip("Distance from shoulder to the grip (where the item sits).")]
        [SerializeField] private float armLength = 0.45f;

        [Header("Sizing (multiplied onto pixel-true 16px = 1 tile)")]
        [Tooltip("Tools and weapons — the stars of the swing.")]
        [SerializeField] private float toolScale = 1.0f;
        [Tooltip("Everything else held in hand (blocks, torches, food).")]
        [SerializeField] private float itemScale = 0.55f;

        [Header("Swing")]
        [Tooltip("Arc in degrees (start = raised behind, end = swung through).")]
        [SerializeField] private float swingStartAngle = 70f;
        [SerializeField] private float swingEndAngle = -80f;
        [Tooltip("Seconds per full swing.")]
        [SerializeField] private float swingPeriod = 0.3f;
        [Tooltip("Resting tilt when held but not in use.")]
        [SerializeField] private float restAngle = 15f;

        private PlayerInventory _inventory;
        private PlayerController _controller;
        private WorldEditController _editor;
        private PlayerAppearanceRenderer _appearance;

        private Transform _pivot;     // at the shoulder; mirrored by facing
        private SpriteRenderer _arm;
        private SpriteRenderer _item;
        private float _swingT;

        private void Start()
        {
            _inventory = GetComponent<PlayerInventory>();
            _controller = GetComponent<PlayerController>();
            _editor = GetComponent<WorldEditController>();
            _appearance = GetComponent<PlayerAppearanceRenderer>();

            _pivot = new GameObject("ArmPivot").transform;
            _pivot.SetParent(transform, false);
            _pivot.localPosition = new Vector3(shoulderOffset.x, shoulderOffset.y, 0f);

            // Item first (under the arm), then the arm on top so the hand/arm
            // visually grips over the handle.
            var itemGo = new GameObject("HeldItem");
            itemGo.transform.SetParent(_pivot, false);
            _item = itemGo.AddComponent<SpriteRenderer>();
            _item.sortingOrder = 15;

            var armGo = new GameObject("Arm");
            armGo.transform.SetParent(_pivot, false);
            _arm = armGo.AddComponent<SpriteRenderer>();
            _arm.sortingOrder = 16;
            LayoutArm();
        }

        private void LayoutArm()
        {
            _arm.sprite = armSprite;
            _arm.enabled = armSprite != null;
            if (armSprite == null) return;

            // Pin the art's TOP-CENTRE to the shoulder pivot, pixel-true.
            float s = armSprite.pixelsPerUnit / PixelsPerTile;
            _arm.transform.localScale = new Vector3(s, s, 1f);
            Bounds b = armSprite.bounds;
            _arm.transform.localPosition = new Vector3(
                -b.center.x * s,
                -(b.center.y + b.extents.y) * s,
                0f);
        }

        private void LateUpdate()
        {
            ItemStack held = _inventory.Held;
            Sprite icon = held.IsEmpty ? null : held.Item.Icon;
            _item.sprite = icon;
            _item.enabled = icon != null;

            // Arm matches the chosen skin colour.
            if (_arm.enabled && _appearance != null)
                _arm.color = _appearance.Current.skinColor;

            if (icon != null)
            {
                // Tools/weapons read bigger than carried odds and ends.
                bool isTool = held.Item.Category == ItemCategory.Tool
                              || held.Item.Category == ItemCategory.Weapon;
                float s = icon.pixelsPerUnit / PixelsPerTile * (isTool ? toolScale : itemScale);
                _item.transform.localScale = new Vector3(s, s, 1f);

                // Grip: pin the art's bottom-left (handle tip) to the arm's end.
                Bounds b = icon.bounds;
                _item.transform.localPosition = new Vector3(
                    -b.min.x * s,
                    -armLength - b.min.y * s,
                    0f);
            }

            // Swing while using; settle to rest otherwise. Arm + item together.
            float angle;
            bool showArm = icon != null; // arm only appears while holding something
            if (_editor != null && _editor.SwingActive)
            {
                _swingT += Time.deltaTime;
                float t = (_swingT % swingPeriod) / swingPeriod; // sawtooth chop
                angle = Mathf.Lerp(swingStartAngle, swingEndAngle, t);
            }
            else
            {
                _swingT = 0f;
                angle = restAngle;
            }
            _pivot.localRotation = Quaternion.Euler(0f, 0f, angle);
            _arm.enabled = showArm && armSprite != null;

            // Mirroring the pivot flips offsets, rotation direction and sprites.
            _pivot.localScale = new Vector3(_controller.Facing < 0 ? -1f : 1f, 1f, 1f);
        }
    }
}
