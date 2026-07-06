using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Inventory UI hosting up to two grids that share one cursor stack:
    ///   - the player's backpack (toggle with E)
    ///   - an open chest's contents (right-click a chest; shown above the backpack)
    /// Left-click picks up / places / swaps a whole stack, right-click splits half
    /// or places one — including across grids, which is how items move between the
    /// player and a chest. Built entirely in code.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class InventoryUI : MonoBehaviour
    {
        [SerializeField] private Sprite slotFrame;
        [SerializeField] private int columns = 10;
        [SerializeField] private int slotSize = 64;
        [SerializeField] private int spacing = 4;
        [Tooltip("Chest auto-closes beyond this distance (tiles).")]
        [SerializeField] private float chestRange = 6f;

        private PlayerInventory _player;
        private Canvas _canvas;
        private bool _open;

        private GridPanel _playerGrid;
        private GridPanel _chestGrid;
        private ChestObject _openChest;

        // The stack currently held on the cursor.
        private ItemStack _cursor;
        private RectTransform _ghost;
        private Image _ghostIcon;
        private Text _ghostCount;
        private Font _font;

        /// <summary>One grid panel bound to an Inventory.</summary>
        private sealed class GridPanel
        {
            public GameObject Root;
            public Image[] Icons;
            public Text[] Counts;
            public Inventory Bound;

            public void Refresh()
            {
                if (Bound == null) return;
                for (int i = 0; i < Icons.Length; i++)
                {
                    ItemStack s = i < Bound.Size ? Bound.Get(i) : ItemStack.Empty;
                    if (s.IsEmpty) { Icons[i].enabled = false; Counts[i].text = ""; }
                    else
                    {
                        Icons[i].enabled = s.Item.Icon != null;
                        Icons[i].sprite = s.Item.Icon;
                        Counts[i].text = s.Count > 1 ? s.Count.ToString() : "";
                    }
                }
            }
        }

        private void Awake() => _player = GetComponent<PlayerInventory>();

        private void Start()
        {
            EnsureEventSystem();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCanvas();

            _playerGrid = BuildGrid("Backpack", _player.Inventory.Size, anchorY: -80f);
            _playerGrid.Bound = _player.Inventory;
            _chestGrid = BuildGrid("Chest", ChestObject.Slots, anchorY: 190f);

            BuildGhost();
            _player.Inventory.Changed += RefreshAll;
            SetOpen(false);
        }

        private void OnDestroy()
        {
            if (_player != null && _player.Inventory != null)
                _player.Inventory.Changed -= RefreshAll;
            UnbindChest();
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) SetOpen(!_open);

            // Walking away from an open chest closes it.
            if (_openChest != null)
            {
                if (_openChest == null ||
                    Vector2.Distance(transform.position, _openChest.transform.position) > chestRange)
                    CloseChest();
            }

            if (_open && Mouse.current != null)
            {
                _ghost.position = Mouse.current.position.ReadValue();
                UpdateGhost();
            }
        }

        // ------------------------------------------------------------- open/close

        public void ToggleChest(ChestObject chest)
        {
            if (_openChest == chest) { CloseChest(); return; }

            UnbindChest();
            _openChest = chest;
            _chestGrid.Bound = chest.Inventory;
            chest.Inventory.Changed += RefreshAll;

            if (!_open) SetOpen(true);
            else _chestGrid.Root.SetActive(true);
            RefreshAll();
        }

        private void CloseChest()
        {
            UnbindChest();
            _openChest = null;
            _chestGrid.Bound = null;
            if (_chestGrid.Root != null) _chestGrid.Root.SetActive(false);
        }

        private void UnbindChest()
        {
            if (_openChest != null && _openChest.Inventory != null)
                _openChest.Inventory.Changed -= RefreshAll;
        }

        private void SetOpen(bool open)
        {
            _open = open;
            _playerGrid.Root.SetActive(open);
            _chestGrid.Root.SetActive(open && _openChest != null);
            _ghost.gameObject.SetActive(open);
            if (!open)
            {
                CloseChest();
                if (!_cursor.IsEmpty)
                {
                    // Return whatever is on the cursor to the player.
                    _player.Inventory.Add(_cursor.Item, _cursor.Count);
                    _cursor = ItemStack.Empty;
                }
            }
            if (open) { RefreshAll(); UpdateGhost(); }
        }

        // ---------------------------------------------------------------- clicks

        public void OnSlotClicked(bool chestGrid, int i, bool rightButton)
        {
            Inventory inv = chestGrid ? _chestGrid.Bound : _playerGrid.Bound;
            if (inv == null || i >= inv.Size) return;
            if (rightButton) RightClick(inv, i); else LeftClick(inv, i);
            UpdateGhost();
        }

        private void LeftClick(Inventory inv, int i)
        {
            ItemStack slot = inv.Get(i);
            if (_cursor.IsEmpty)
            {
                if (slot.IsEmpty) return;
                _cursor = slot;
                inv.SetSlot(i, ItemStack.Empty);
                return;
            }

            if (slot.IsEmpty)
            {
                inv.SetSlot(i, _cursor);
                _cursor = ItemStack.Empty;
            }
            else if (slot.Item == _cursor.Item)
            {
                int space = Mathf.Max(0, slot.Item.MaxStackSize - slot.Count);
                int move = Mathf.Min(space, _cursor.Count);
                if (move > 0)
                {
                    slot.Count += move;
                    inv.SetSlot(i, slot);
                    _cursor.Count -= move;
                    if (_cursor.Count <= 0) _cursor = ItemStack.Empty;
                }
                else { inv.SetSlot(i, _cursor); _cursor = slot; } // both full -> swap
            }
            else { inv.SetSlot(i, _cursor); _cursor = slot; } // different -> swap
        }

        private void RightClick(Inventory inv, int i)
        {
            ItemStack slot = inv.Get(i);
            if (_cursor.IsEmpty)
            {
                if (slot.IsEmpty) return;
                int half = Mathf.CeilToInt(slot.Count / 2f);
                _cursor = new ItemStack(slot.Item, half);
                slot.Count -= half;
                inv.SetSlot(i, slot.Count > 0 ? slot : ItemStack.Empty);
                return;
            }

            if (slot.IsEmpty)
            {
                inv.SetSlot(i, new ItemStack(_cursor.Item, 1));
                if (--_cursor.Count <= 0) _cursor = ItemStack.Empty;
            }
            else if (slot.Item == _cursor.Item && slot.Count < slot.Item.MaxStackSize)
            {
                slot.Count += 1;
                inv.SetSlot(i, slot);
                if (--_cursor.Count <= 0) _cursor = ItemStack.Empty;
            }
        }

        // ------------------------------------------------------------------ build

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("InventoryCanvas");
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 110;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        private GridPanel BuildGrid(string name, int slots, float anchorY)
        {
            var panel = new GridPanel();
            int rows = Mathf.CeilToInt(slots / (float)columns);
            int pad = 10;

            var root = new GameObject(name);
            panel.Root = root;
            var prt = root.AddComponent<RectTransform>();
            prt.SetParent(_canvas.transform, false);
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            float gridW = columns * slotSize + (columns - 1) * spacing;
            float gridH = rows * slotSize + (rows - 1) * spacing;
            prt.sizeDelta = new Vector2(gridW + pad * 2, gridH + pad * 2);
            prt.anchoredPosition = new Vector2(0f, anchorY);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);

            bool isChest = name == "Chest";
            panel.Icons = new Image[slots];
            panel.Counts = new Text[slots];

            for (int i = 0; i < slots; i++)
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

                frame.gameObject.AddComponent<SlotView>().Init(this, isChest, i);

                var icon = new GameObject("Icon").AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                RectTransform irt = icon.rectTransform;
                irt.SetParent(fr, false);
                irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
                irt.offsetMin = new Vector2(6, 6); irt.offsetMax = new Vector2(-6, -6);
                panel.Icons[i] = icon;

                var count = new GameObject("Count").AddComponent<Text>();
                count.font = _font; count.fontSize = 18; count.alignment = TextAnchor.LowerRight;
                count.color = Color.white; count.raycastTarget = false;
                count.horizontalOverflow = HorizontalWrapMode.Overflow;
                count.verticalOverflow = VerticalWrapMode.Overflow;
                RectTransform crt = count.rectTransform;
                crt.SetParent(fr, false);
                crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
                crt.offsetMin = new Vector2(2, 2); crt.offsetMax = new Vector2(-4, -2);
                panel.Counts[i] = count;
            }

            return panel;
        }

        private void BuildGhost()
        {
            var ghost = new GameObject("Ghost");
            _ghost = ghost.AddComponent<RectTransform>();
            _ghost.SetParent(_canvas.transform, false);
            _ghost.sizeDelta = new Vector2(slotSize, slotSize);

            _ghostIcon = new GameObject("GhostIcon").AddComponent<Image>();
            _ghostIcon.preserveAspect = true;
            _ghostIcon.raycastTarget = false;
            RectTransform gir = _ghostIcon.rectTransform;
            gir.SetParent(_ghost, false);
            gir.anchorMin = Vector2.zero; gir.anchorMax = Vector2.one;
            gir.offsetMin = new Vector2(6, 6); gir.offsetMax = new Vector2(-6, -6);

            _ghostCount = new GameObject("GhostCount").AddComponent<Text>();
            _ghostCount.font = _font; _ghostCount.fontSize = 18;
            _ghostCount.alignment = TextAnchor.LowerRight;
            _ghostCount.color = Color.white; _ghostCount.raycastTarget = false;
            _ghostCount.horizontalOverflow = HorizontalWrapMode.Overflow;
            _ghostCount.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform gcr = _ghostCount.rectTransform;
            gcr.SetParent(_ghost, false);
            gcr.anchorMin = Vector2.zero; gcr.anchorMax = Vector2.one;
        }

        private void RefreshAll()
        {
            _playerGrid?.Refresh();
            _chestGrid?.Refresh();
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
    }

    /// <summary>Per-slot click forwarder for <see cref="InventoryUI"/>.</summary>
    public sealed class SlotView : MonoBehaviour, IPointerClickHandler
    {
        private InventoryUI _owner;
        private bool _chestGrid;
        private int _index;

        public void Init(InventoryUI owner, bool chestGrid, int index)
        {
            _owner = owner;
            _chestGrid = chestGrid;
            _index = index;
        }

        public void OnPointerClick(PointerEventData e)
            => _owner.OnSlotClicked(_chestGrid, _index, e.button == PointerEventData.InputButton.Right);
    }
}
