using UnityEngine;
using UnityEngine.UI;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Minimal hotbar HUD built entirely in code (no manual Canvas setup needed).
    /// Renders one slot per inventory slot along the bottom of the screen, showing
    /// each item's icon + stack count and highlighting the selected slot. Refreshes
    /// from <see cref="PlayerInventory"/> events.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class HotbarHUD : MonoBehaviour
    {
        [SerializeField] private Sprite slotFrame;
        [SerializeField] private Sprite slotHighlight;
        [SerializeField] private int slotSize = 56;
        [SerializeField] private int spacing = 6;
        [SerializeField] private int bottomMargin = 16;

        private PlayerInventory _inv;
        private GameObject _canvas;
        private Image[] _frames;
        private Image[] _icons;
        private Text[] _counts;

        private void Awake() => _inv = GetComponent<PlayerInventory>();

        private void Start()
        {
            BuildUI();
            _inv.Inventory.Changed += Refresh;
            _inv.SelectionChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_inv != null)
            {
                if (_inv.Inventory != null) _inv.Inventory.Changed -= Refresh;
                _inv.SelectionChanged -= Refresh;
            }
            if (_canvas != null) Destroy(_canvas); // no orphaned UI when the player is rebuilt
        }

        private void BuildUI()
        {
            int n = _inv.HotbarSize;

            var canvasGo = new GameObject("HotbarCanvas");
            _canvas = canvasGo;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            var row = new GameObject("Hotbar").AddComponent<RectTransform>();
            row.SetParent(canvasGo.transform, false);
            // Anchor to the bottom-right corner.
            row.anchorMin = new Vector2(1f, 0f);
            row.anchorMax = new Vector2(1f, 0f);
            row.pivot = new Vector2(1f, 0f);
            float totalW = n * slotSize + (n - 1) * spacing;
            row.sizeDelta = new Vector2(totalW, slotSize);
            row.anchoredPosition = new Vector2(-bottomMargin, bottomMargin);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _frames = new Image[n];
            _icons = new Image[n];
            _counts = new Text[n];

            for (int i = 0; i < n; i++)
            {
                var frame = new GameObject($"Slot{i}").AddComponent<Image>();
                frame.sprite = slotFrame;
                frame.color = new Color(1f, 1f, 1f, 0.7f); // slightly transparent slots
                frame.raycastTarget = false;               // don't block world clicks
                RectTransform fr = frame.rectTransform;
                fr.SetParent(row, false);
                fr.anchorMin = new Vector2(0f, 0.5f);
                fr.anchorMax = new Vector2(0f, 0.5f);
                fr.pivot = new Vector2(0f, 0.5f);
                fr.sizeDelta = new Vector2(slotSize, slotSize);
                fr.anchoredPosition = new Vector2(i * (slotSize + spacing), 0f);
                _frames[i] = frame;

                var icon = new GameObject("Icon").AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                RectTransform ir = icon.rectTransform;
                ir.SetParent(fr, false);
                ir.anchorMin = Vector2.zero;
                ir.anchorMax = Vector2.one;
                ir.offsetMin = new Vector2(6f, 6f);
                ir.offsetMax = new Vector2(-6f, -6f);
                _icons[i] = icon;

                var count = new GameObject("Count").AddComponent<Text>();
                count.font = font;
                count.fontSize = 16;
                count.alignment = TextAnchor.LowerRight;
                count.color = Color.white;
                count.raycastTarget = false;
                count.horizontalOverflow = HorizontalWrapMode.Overflow;
                count.verticalOverflow = VerticalWrapMode.Overflow;
                RectTransform cr = count.rectTransform;
                cr.SetParent(fr, false);
                cr.anchorMin = Vector2.zero;
                cr.anchorMax = Vector2.one;
                cr.offsetMin = new Vector2(2f, 2f);
                cr.offsetMax = new Vector2(-4f, -2f);
                _counts[i] = count;
            }
        }

        private void Refresh()
        {
            if (_frames == null) return;
            for (int i = 0; i < _frames.Length; i++)
            {
                bool selected = i == _inv.Selected;
                _frames[i].sprite = (selected && slotHighlight != null) ? slotHighlight : slotFrame;

                ItemStack stack = _inv.Inventory.Get(i);
                if (stack.IsEmpty)
                {
                    _icons[i].enabled = false;
                    _counts[i].text = string.Empty;
                }
                else
                {
                    _icons[i].sprite = stack.Item.Icon;
                    _icons[i].enabled = stack.Item.Icon != null;
                    _counts[i].text = stack.Count > 1 ? stack.Count.ToString() : string.Empty;
                }
            }
        }
    }
}
