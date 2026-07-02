using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Full inventory grid (hotbar row + backpack rows), toggled with E. Uses a
    /// "cursor stack" model: left-click picks up / places / swaps a whole stack,
    /// right-click splits half / places one. Built in code; reads the same
    /// PlayerInventory the hotbar uses, so both stay in sync.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class InventoryUI : MonoBehaviour
    {
        [SerializeField] private Sprite slotFrame;
        [SerializeField] private int columns = 10;
        [SerializeField] private int slotSize = 64;
        [SerializeField] private int spacing = 4;

        private PlayerInventory _player;
        private Inventory _inv;
        private GameObject _panel;
        private bool _open;

        private Image[] _icons;
        private Text[] _counts;

        // The stack currently held on the cursor.
        private ItemStack _cursor;
        private Image _ghostIcon;
        private Text _ghostCount;

        private void Awake()
        {
            _player = GetComponent<PlayerInventory>();
        }

        private void Start()
        {
            _inv = _player.Inventory;
            EnsureEventSystem();
            BuildUI();
            _inv.Changed += RefreshGrid;
            SetOpen(false);
        }

        private void OnDestroy()
        {
            if (_inv != null) _inv.Changed -= RefreshGrid;
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) SetOpen(!_open);

            if (_open)
            {
                Mouse m = Mouse.current;
                if (m != null)
                    ((RectTransform)_ghostIcon.transform.parent).position = m.position.ReadValue();
                UpdateGhost();
            }
        }

        private void SetOpen(bool open)
        {
            _open = open;
            if (_panel != null) _panel.SetActive(open);

            if (!open && !_cursor.IsEmpty)
            {
                // Return whatever is on the cursor to the inventory.
                _inv.Add(_cursor.Item, _cursor.Count);
                _cursor = ItemStack.Empty;
            }
            if (open) { RefreshGrid(); UpdateGhost(); }
            if (_ghostIcon != null) _ghostIcon.transform.parent.gameObject.SetActive(open);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildUI()
        {
            int n = _inv.Size;
            int rows = Mathf.CeilToInt(n / (float)columns);

            var canvasGo = new GameObject("InventoryCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("InventoryPanel");
            var prt = _panel.AddComponent<RectTransform>();
            prt.SetParent(canvasGo.transform, false);
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            int pad = 10;
            float gridW = columns * slotSize + (columns - 1) * spacing;
            float gridH = rows * slotSize + (rows - 1) * spacing;
            prt.sizeDelta = new Vector2(gridW + pad * 2, gridH + pad * 2);
            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _icons = new Image[n];
            _counts = new Text[n];

            for (int i = 0; i < n; i++)
            {
                int col = i % columns;
                int rowFromTop = i / columns;

                var frame = new GameObject($"Slot{i}").AddComponent<Image>();
                frame.sprite = slotFrame;
                RectTransform fr = frame.rectTransform;
                fr.SetParent(prt, false);
                fr.anchorMin = fr.anchorMax = new Vector2(0f, 1f);
                fr.pivot = new Vector2(0f, 1f);
                fr.sizeDelta = new Vector2(slotSize, slotSize);
                fr.anchoredPosition = new Vector2(
                    pad + col * (slotSize + spacing),
                    -(pad + rowFromTop * (slotSize + spacing)));

                var slotView = frame.gameObject.AddComponent<SlotView>();
                slotView.Init(this, i);

                var icon = new GameObject("Icon").AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                RectTransform irt = icon.rectTransform;
                irt.SetParent(fr, false);
                irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
                irt.offsetMin = new Vector2(6, 6); irt.offsetMax = new Vector2(-6, -6);
                _icons[i] = icon;

                var count = new GameObject("Count").AddComponent<Text>();
                count.font = font; count.fontSize = 18; count.alignment = TextAnchor.LowerRight;
                count.color = Color.white; count.raycastTarget = false;
                count.horizontalOverflow = HorizontalWrapMode.Overflow;
                count.verticalOverflow = VerticalWrapMode.Overflow;
                RectTransform crt = count.rectTransform;
                crt.SetParent(fr, false);
                crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
                crt.offsetMin = new Vector2(2, 2); crt.offsetMax = new Vector2(-4, -2);
                _counts[i] = count;
            }

            // Cursor ghost (follows the mouse; never blocks raycasts).
            var ghost = new GameObject("Ghost");
            var ghrt = ghost.AddComponent<RectTransform>();
            ghrt.SetParent(canvasGo.transform, false);
            ghrt.sizeDelta = new Vector2(slotSize, slotSize);
            _ghostIcon = new GameObject("GhostIcon").AddComponent<Image>();
            _ghostIcon.preserveAspect = true; _ghostIcon.raycastTarget = false;
            RectTransform gir = _ghostIcon.rectTransform;
            gir.SetParent(ghrt, false);
            gir.anchorMin = Vector2.zero; gir.anchorMax = Vector2.one;
            gir.offsetMin = new Vector2(6, 6); gir.offsetMax = new Vector2(-6, -6);
            _ghostCount = new GameObject("GhostCount").AddComponent<Text>();
            _ghostCount.font = font; _ghostCount.fontSize = 18; _ghostCount.alignment = TextAnchor.LowerRight;
            _ghostCount.color = Color.white; _ghostCount.raycastTarget = false;
            _ghostCount.horizontalOverflow = HorizontalWrapMode.Overflow;
            _ghostCount.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform gcr = _ghostCount.rectTransform;
            gcr.SetParent(ghrt, false);
            gcr.anchorMin = Vector2.zero; gcr.anchorMax = Vector2.one;
        }

        private void RefreshGrid()
        {
            if (_icons == null) return;
            for (int i = 0; i < _icons.Length; i++)
            {
                ItemStack s = _inv.Get(i);
                if (s.IsEmpty) { _icons[i].enabled = false; _counts[i].text = ""; }
                else
                {
                    _icons[i].enabled = s.Item.Icon != null;
                    _icons[i].sprite = s.Item.Icon;
                    _counts[i].text = s.Count > 1 ? s.Count.ToString() : "";
                }
            }
        }

        private void UpdateGhost()
        {
            bool show = !_cursor.IsEmpty;
            _ghostIcon.enabled = show && _cursor.Item.Icon != null;
            if (show)
            {
                _ghostIcon.sprite = _cursor.Item.Icon;
                _ghostCount.text = _cursor.Count > 1 ? _cursor.Count.ToString() : "";
            }
            else _ghostCount.text = "";
        }

        // Called by SlotView on click.
        public void OnSlotClicked(int i, bool rightButton)
        {
            if (rightButton) RightClick(i); else LeftClick(i);
            UpdateGhost();
        }

        private void LeftClick(int i)
        {
            ItemStack slot = _inv.Get(i);
            if (_cursor.IsEmpty)
            {
                if (slot.IsEmpty) return;
                _cursor = slot;
                _inv.SetSlot(i, ItemStack.Empty);
                return;
            }

            if (slot.IsEmpty)
            {
                _inv.SetSlot(i, _cursor);
                _cursor = ItemStack.Empty;
            }
            else if (slot.Item == _cursor.Item)
            {
                int space = Mathf.Max(0, slot.Item.MaxStackSize - slot.Count);
                int move = Mathf.Min(space, _cursor.Count);
                if (move > 0)
                {
                    slot.Count += move;
                    _inv.SetSlot(i, slot);
                    _cursor.Count -= move;
                    if (_cursor.Count <= 0) _cursor = ItemStack.Empty;
                }
                else { _inv.SetSlot(i, _cursor); _cursor = slot; } // both full -> swap
            }
            else { _inv.SetSlot(i, _cursor); _cursor = slot; } // different -> swap
        }

        private void RightClick(int i)
        {
            ItemStack slot = _inv.Get(i);
            if (_cursor.IsEmpty)
            {
                if (slot.IsEmpty) return;
                int half = Mathf.CeilToInt(slot.Count / 2f);
                _cursor = new ItemStack(slot.Item, half);
                slot.Count -= half;
                _inv.SetSlot(i, slot.Count > 0 ? slot : ItemStack.Empty);
                return;
            }

            // Place one from the cursor.
            if (slot.IsEmpty)
            {
                _inv.SetSlot(i, new ItemStack(_cursor.Item, 1));
                if (--_cursor.Count <= 0) _cursor = ItemStack.Empty;
            }
            else if (slot.Item == _cursor.Item && slot.Count < slot.Item.MaxStackSize)
            {
                slot.Count += 1;
                _inv.SetSlot(i, slot);
                if (--_cursor.Count <= 0) _cursor = ItemStack.Empty;
            }
        }
    }

    /// <summary>Per-slot click forwarder for <see cref="InventoryUI"/>.</summary>
    public sealed class SlotView : MonoBehaviour, IPointerClickHandler
    {
        private InventoryUI _owner;
        private int _index;

        public void Init(InventoryUI owner, int index) { _owner = owner; _index = index; }

        public void OnPointerClick(PointerEventData e)
            => _owner.OnSlotClicked(_index, e.button == PointerEventData.InputButton.Right);
    }
}
