using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Inventory UI hosting the player's backpack plus one open station panel
    /// (chest or furnace), all sharing a single cursor stack.
    ///
    /// Controls: E toggles the backpack. Right-clicking a chest/furnace in the
    /// world opens its panel above the backpack. Move items by CLICK (pick /
    /// place / swap; right-click splits half / places one) or by DRAG & DROP —
    /// drag a stack from any slot and release it over another; releasing
    /// anywhere else returns it to the source slot. Built entirely in code.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class InventoryUI : MonoBehaviour
    {
        public enum PanelKind { Player, Chest, Furnace }

        [SerializeField] private Sprite slotFrame;
        [SerializeField] private int columns = 10;
        [SerializeField] private int slotSize = 52;
        [SerializeField] private int spacing = 6;
        [Tooltip("Open station (chest/furnace) auto-closes beyond this distance (tiles).")]
        [SerializeField] private float stationRange = 6f;

        private static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.11f, 0.92f);
        private static readonly Color HeaderColor = new Color(1f, 1f, 1f, 0.55f);
        private const int Pad = 12;
        private const int HeaderH = 22;
        private const int HotbarGap = 10; // extra gap under the hotbar row

        private PlayerInventory _player;
        private Canvas _canvas;
        private Font _font;
        private bool _open;

        private GridPanel _playerGrid;
        private GridPanel _chestGrid;
        private GridPanel _furnaceGrid;
        private Text _furnaceStatus;

        private ChestObject _openChest;
        private FurnaceObject _openFurnace;

        // Cursor stack + drag state.
        private ItemStack _cursor;
        private RectTransform _ghost;
        private Image _ghostIcon;
        private Text _ghostCount;
        private bool _dragging;
        private PanelKind _dragSourcePanel;
        private int _dragSourceIndex;

        private sealed class GridPanel
        {
            public GameObject Root;
            public Image[] Icons;
            public Text[] Counts;
            public Inventory Bound;

            public void Refresh()
            {
                for (int i = 0; i < Icons.Length; i++)
                {
                    ItemStack s = Bound != null && i < Bound.Size ? Bound.Get(i) : ItemStack.Empty;
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

            _playerGrid = BuildGrid(PanelKind.Player, "Backpack", _player.Inventory.Size,
                                    columns, new Vector2(0f, -60f), hotbarGapAfterRow0: true);
            _playerGrid.Bound = _player.Inventory;

            _chestGrid = BuildGrid(PanelKind.Chest, "Chest", ChestObject.Slots,
                                   columns, new Vector2(0f, 190f), false);

            _furnaceGrid = BuildFurnacePanel(new Vector2(0f, 190f));

            BuildGhost();
            _player.Inventory.Changed += RefreshAll;
            SetOpen(false);
        }

        private void OnDestroy()
        {
            if (_player != null && _player.Inventory != null)
                _player.Inventory.Changed -= RefreshAll;
            UnbindStation();
            if (_canvas != null) Destroy(_canvas.gameObject); // no orphaned UI
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) SetOpen(!_open);

            // Walking away from an open station closes it.
            Transform station = _openChest != null ? _openChest.transform
                              : _openFurnace != null ? _openFurnace.transform : null;
            if (station != null &&
                Vector2.Distance(transform.position, station.position) > stationRange)
                CloseStation();

            if (_open)
            {
                if (Mouse.current != null)
                    _ghost.position = Mouse.current.position.ReadValue();
                UpdateGhost();
                if (_openFurnace != null) UpdateFurnaceStatus();
            }
        }

        // ---------------------------------------------------------- open/close

        public void ToggleChest(ChestObject chest)
        {
            if (_openChest == chest) { CloseStation(); return; }
            CloseStation();
            _openChest = chest;
            _chestGrid.Bound = chest.Inventory;
            chest.Inventory.Changed += RefreshAll;
            OpenWithStation();
        }

        public void ToggleFurnace(FurnaceObject furnace)
        {
            if (_openFurnace == furnace) { CloseStation(); return; }
            CloseStation();
            _openFurnace = furnace;
            _furnaceGrid.Bound = furnace.Inventory;
            furnace.Inventory.Changed += RefreshAll;
            OpenWithStation();
        }

        private void OpenWithStation()
        {
            if (!_open) SetOpen(true);
            _chestGrid.Root.SetActive(_openChest != null);
            _furnaceGrid.Root.SetActive(_openFurnace != null);
            RefreshAll();
        }

        private void CloseStation()
        {
            UnbindStation();
            _openChest = null;
            _openFurnace = null;
            _chestGrid.Bound = null;
            _furnaceGrid.Bound = null;
            if (_chestGrid?.Root != null) _chestGrid.Root.SetActive(false);
            if (_furnaceGrid?.Root != null) _furnaceGrid.Root.SetActive(false);
        }

        private void UnbindStation()
        {
            if (_openChest != null) _openChest.Inventory.Changed -= RefreshAll;
            if (_openFurnace != null) _openFurnace.Inventory.Changed -= RefreshAll;
        }

        private void SetOpen(bool open)
        {
            _open = open;
            _playerGrid.Root.SetActive(open);
            _ghost.gameObject.SetActive(open);
            if (!open)
            {
                CloseStation();
                if (!_cursor.IsEmpty)
                {
                    _player.Inventory.Add(_cursor.Item, _cursor.Count);
                    _cursor = ItemStack.Empty;
                }
                _dragging = false;
            }
            else
            {
                _chestGrid.Root.SetActive(_openChest != null);
                _furnaceGrid.Root.SetActive(_openFurnace != null);
                RefreshAll();
                UpdateGhost();
            }
        }

        // -------------------------------------------------------- interactions

        private Inventory GetInventory(PanelKind panel) => panel switch
        {
            PanelKind.Player => _player.Inventory,
            PanelKind.Chest => _openChest != null ? _openChest.Inventory : null,
            PanelKind.Furnace => _openFurnace != null ? _openFurnace.Inventory : null,
            _ => null,
        };

        public void OnSlotClicked(PanelKind panel, int i, bool rightButton)
        {
            Inventory inv = GetInventory(panel);
            if (inv == null || i >= inv.Size) return;
            if (rightButton) RightClick(inv, i); else LeftClick(inv, i);
            UpdateGhost();
        }

        public void OnSlotBeginDrag(PanelKind panel, int i)
        {
            Inventory inv = GetInventory(panel);
            if (inv == null || i >= inv.Size) return;
            if (!_cursor.IsEmpty) return; // already carrying via click — drag just moves it

            ItemStack slot = inv.Get(i);
            if (slot.IsEmpty) return;
            _cursor = slot;
            inv.SetSlot(i, ItemStack.Empty);
            _dragging = true;
            _dragSourcePanel = panel;
            _dragSourceIndex = i;
            UpdateGhost();
        }

        public void OnSlotDrop(PanelKind panel, int i)
        {
            Inventory inv = GetInventory(panel);
            if (inv == null || i >= inv.Size || _cursor.IsEmpty) return;
            LeftClick(inv, i); // place / merge / swap semantics
            _dragging = false;
            UpdateGhost();
        }

        public void OnSlotEndDrag()
        {
            // Released somewhere that wasn't a slot: send the stack home.
            if (_dragging && !_cursor.IsEmpty)
            {
                Inventory src = GetInventory(_dragSourcePanel);
                if (src != null) LeftClick(src, _dragSourceIndex);
                if (!_cursor.IsEmpty) // source got occupied meanwhile — any pocket
                {
                    _player.Inventory.Add(_cursor.Item, _cursor.Count);
                    _cursor = ItemStack.Empty;
                }
            }
            _dragging = false;
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

        // -------------------------------------------------------------- build

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

        private GridPanel BuildGrid(PanelKind kind, string title, int slots, int cols,
                                    Vector2 anchoredPos, bool hotbarGapAfterRow0)
        {
            var panel = new GridPanel();
            int rows = Mathf.CeilToInt(slots / (float)cols);

            float gridW = cols * slotSize + (cols - 1) * spacing;
            float gridH = rows * slotSize + (rows - 1) * spacing
                          + (hotbarGapAfterRow0 && rows > 1 ? HotbarGap : 0);

            RectTransform prt = MakePanelRoot(title, out panel.Root,
                new Vector2(gridW + Pad * 2, gridH + Pad * 2 + HeaderH), anchoredPos);

            panel.Icons = new Image[slots];
            panel.Counts = new Text[slots];

            for (int i = 0; i < slots; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float extraGap = hotbarGapAfterRow0 && row > 0 ? HotbarGap : 0f;
                var pos = new Vector2(
                    Pad + col * (slotSize + spacing),
                    -(Pad + HeaderH + row * (slotSize + spacing) + extraGap));
                BuildSlot(prt, kind, i, pos, panel);
            }

            return panel;
        }

        private GridPanel BuildFurnacePanel(Vector2 anchoredPos)
        {
            var panel = new GridPanel();
            // 3 labelled slots: In | Fuel | Out, plus a status line beneath.
            int cols = 3;
            float gridW = cols * slotSize + (cols - 1) * (spacing + 14);
            float gridH = slotSize + 34; // captions above, status below

            RectTransform prt = MakePanelRoot("Furnace", out panel.Root,
                new Vector2(gridW + Pad * 2, gridH + Pad * 2 + HeaderH), anchoredPos);

            panel.Icons = new Image[3];
            panel.Counts = new Text[3];
            string[] captions = { "In", "Fuel", "Out" };

            for (int i = 0; i < 3; i++)
            {
                var pos = new Vector2(Pad + i * (slotSize + spacing + 14), -(Pad + HeaderH + 14));
                BuildSlot(prt, PanelKind.Furnace, i, pos, panel);

                Text cap = MakeText(prt, captions[i], 12, TextAnchor.MiddleCenter, HeaderColor);
                RectTransform crt = cap.rectTransform;
                crt.anchorMin = crt.anchorMax = new Vector2(0f, 1f);
                crt.pivot = new Vector2(0f, 1f);
                crt.sizeDelta = new Vector2(slotSize, 14);
                crt.anchoredPosition = new Vector2(pos.x, -(Pad + HeaderH - 2));
            }

            _furnaceStatus = MakeText(prt, "", 13, TextAnchor.MiddleLeft, new Color(1f, 0.8f, 0.4f));
            RectTransform srt = _furnaceStatus.rectTransform;
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(1f, 0f);
            srt.pivot = new Vector2(0.5f, 0f);
            srt.offsetMin = new Vector2(Pad, 6);
            srt.offsetMax = new Vector2(-Pad, 24);

            return panel;
        }

        private RectTransform MakePanelRoot(string title, out GameObject root,
                                            Vector2 size, Vector2 anchoredPos)
        {
            root = new GameObject(title + "Panel");
            var prt = root.AddComponent<RectTransform>();
            prt.SetParent(_canvas.transform, false);
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = size;
            prt.anchoredPosition = anchoredPos;
            root.AddComponent<Image>().color = PanelColor;

            Text header = MakeText(prt, title.ToUpperInvariant(), 13, TextAnchor.MiddleLeft, HeaderColor);
            RectTransform hrt = header.rectTransform;
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.offsetMin = new Vector2(Pad, -HeaderH - 4);
            hrt.offsetMax = new Vector2(-Pad, -4);
            return prt;
        }

        private void BuildSlot(RectTransform parent, PanelKind kind, int index,
                               Vector2 anchoredPos, GridPanel panel)
        {
            var frame = new GameObject($"Slot{index}").AddComponent<Image>();
            frame.sprite = slotFrame;
            RectTransform fr = frame.rectTransform;
            fr.SetParent(parent, false);
            fr.anchorMin = fr.anchorMax = new Vector2(0f, 1f);
            fr.pivot = new Vector2(0f, 1f);
            fr.sizeDelta = new Vector2(slotSize, slotSize);
            fr.anchoredPosition = anchoredPos;

            frame.gameObject.AddComponent<SlotView>().Init(this, kind, index);

            var icon = new GameObject("Icon").AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            RectTransform irt = icon.rectTransform;
            irt.SetParent(fr, false);
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(6, 6); irt.offsetMax = new Vector2(-6, -6);
            panel.Icons[index] = icon;

            Text count = MakeText(fr, "", 16, TextAnchor.LowerRight, Color.white);
            RectTransform crt = count.rectTransform;
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = new Vector2(2, 2); crt.offsetMax = new Vector2(-4, -2);
            panel.Counts[index] = count;
        }

        private Text MakeText(RectTransform parent, string value, int size,
                              TextAnchor anchor, Color color)
        {
            var t = new GameObject("Text").AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = value;
            t.rectTransform.SetParent(parent, false);
            return t;
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

            _ghostCount = MakeText(_ghost, "", 16, TextAnchor.LowerRight, Color.white);
            RectTransform gcr = _ghostCount.rectTransform;
            gcr.anchorMin = Vector2.zero; gcr.anchorMax = Vector2.one;
        }

        private void RefreshAll()
        {
            _playerGrid?.Refresh();
            _chestGrid?.Refresh();
            _furnaceGrid?.Refresh();
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

        private void UpdateFurnaceStatus()
        {
            if (_furnaceStatus == null || _openFurnace == null) return;
            if (_openFurnace.IsBurning)
            {
                int pct = Mathf.RoundToInt(_openFurnace.SmeltProgress01 * 100f);
                _furnaceStatus.text = pct > 0
                    ? $"Burning — smelting {pct}%"
                    : $"Burning ({Mathf.CeilToInt(_openFurnace.BurnRemaining)}s fuel left)";
            }
            else
            {
                _furnaceStatus.text = "Cold — add fuel (wood or coal) and something to smelt";
            }
        }
    }

    /// <summary>Per-slot pointer handler: forwards clicks and drag & drop to <see cref="InventoryUI"/>.</summary>
    public sealed class SlotView : MonoBehaviour, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        private InventoryUI _owner;
        private InventoryUI.PanelKind _panel;
        private int _index;

        public void Init(InventoryUI owner, InventoryUI.PanelKind panel, int index)
        {
            _owner = owner;
            _panel = panel;
            _index = index;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.dragging) return; // drags handle themselves
            _owner.OnSlotClicked(_panel, _index, e.button == PointerEventData.InputButton.Right);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left)
                _owner.OnSlotBeginDrag(_panel, _index);
        }

        public void OnDrag(PointerEventData e) { } // required for drag events to fire

        public void OnDrop(PointerEventData e) => _owner.OnSlotDrop(_panel, _index);

        public void OnEndDrag(PointerEventData e) => _owner.OnSlotEndDrag();
    }
}
