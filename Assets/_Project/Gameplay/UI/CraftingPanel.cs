using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Doodgy.Core;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Crafting UI: toggle with C, lists only the recipes currently AVAILABLE to
    /// the player — hand recipes always, workbench recipes only when standing near
    /// a placed workbench (so bench recipes "unlock" when you build one). Click a
    /// recipe to craft; unaffordable ones are greyed. Built in code.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class CraftingPanel : MonoBehaviour
    {
        [SerializeField] private Recipe[] recipes;
        [SerializeField] private World world;
        [SerializeField] private ushort workbenchTileId = 8;
        [SerializeField] private float stationRange = 5f;
        [SerializeField] private Sprite slotFrame;

        private const int RowH = 44;
        private const int Pad = 8;
        private const int Width = 340;

        private PlayerInventory _inv;
        private GameObject _panel;
        private RectTransform _panelRect;
        private bool _open;

        private readonly List<RectTransform> _rows = new List<RectTransform>();
        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<Image> _icons = new List<Image>();
        private readonly List<Text> _labels = new List<Text>();

        private void Awake() => _inv = GetComponent<PlayerInventory>();

        private void Start()
        {
            EnsureEventSystem();
            BuildUI();
            SetOpen(false);
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.cKey.wasPressedThisFrame) SetOpen(!_open);
            if (_open) Refresh();
        }

        private void SetOpen(bool open)
        {
            _open = open;
            if (_panel != null) _panel.SetActive(open);
            if (open) Refresh();
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
            var canvasGo = new GameObject("CraftingCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("CraftingPanel");
            _panelRect = _panel.AddComponent<RectTransform>();
            _panelRect.SetParent(canvasGo.transform, false);
            _panelRect.anchorMin = new Vector2(0f, 0.5f);
            _panelRect.anchorMax = new Vector2(0f, 0.5f);
            _panelRect.pivot = new Vector2(0f, 0.5f);
            _panelRect.anchoredPosition = new Vector2(20f, 0f);
            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            int n = recipes != null ? recipes.Length : 0;

            for (int i = 0; i < n; i++)
            {
                Recipe recipe = recipes[i];

                var rowGo = new GameObject($"Recipe{i}");
                var rrt = rowGo.AddComponent<RectTransform>();
                rrt.SetParent(_panelRect, false);
                rrt.anchorMin = new Vector2(0f, 1f);
                rrt.anchorMax = new Vector2(1f, 1f);
                rrt.pivot = new Vector2(0.5f, 1f);
                rrt.offsetMin = new Vector2(Pad, 0f);
                rrt.offsetMax = new Vector2(-Pad, 0f);
                rrt.sizeDelta = new Vector2(0f, RowH);
                _rows.Add(rrt);

                var btnImg = rowGo.AddComponent<Image>();
                btnImg.sprite = slotFrame;
                btnImg.color = new Color(1f, 1f, 1f, 0.9f);
                var btn = rowGo.AddComponent<Button>();
                int captured = i;
                btn.onClick.AddListener(() => TryCraft(recipes[captured]));
                _buttons.Add(btn);

                var icon = new GameObject("Icon").AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                RectTransform irt = icon.rectTransform;
                irt.SetParent(rrt, false);
                irt.anchorMin = new Vector2(0f, 0.5f);
                irt.anchorMax = new Vector2(0f, 0.5f);
                irt.pivot = new Vector2(0f, 0.5f);
                irt.sizeDelta = new Vector2(RowH - 8, RowH - 8);
                irt.anchoredPosition = new Vector2(6f, 0f);
                icon.sprite = recipe != null && recipe.output != null ? recipe.output.Icon : null;
                _icons.Add(icon);

                var label = new GameObject("Label").AddComponent<Text>();
                label.font = font;
                label.fontSize = 16;
                label.alignment = TextAnchor.MiddleLeft;
                label.color = Color.white;
                label.raycastTarget = false;
                RectTransform lrt = label.rectTransform;
                lrt.SetParent(rrt, false);
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(RowH, 2f);
                lrt.offsetMax = new Vector2(-4f, -2f);
                label.text = Describe(recipe);
                _labels.Add(label);
            }
        }

        private string Describe(Recipe r)
        {
            if (r == null || r.output == null) return "?";
            var sb = new System.Text.StringBuilder();
            sb.Append(r.outputCount).Append("x ").Append(r.output.DisplayName);
            if (r.inputs != null && r.inputs.Length > 0)
            {
                sb.Append("  (");
                for (int i = 0; i < r.inputs.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    Recipe.Ingredient ing = r.inputs[i];
                    sb.Append(ing.count).Append(' ').Append(ing.item != null ? ing.item.DisplayName : "?");
                }
                sb.Append(')');
            }
            return sb.ToString();
        }

        private void Refresh()
        {
            bool benchNear = WorkbenchNearby();
            int visible = 0;

            for (int i = 0; i < _rows.Count; i++)
            {
                Recipe r = recipes[i];
                bool unlocked = r != null && (!r.requiresWorkbench || benchNear);
                _rows[i].gameObject.SetActive(unlocked);
                if (!unlocked) continue;

                _rows[i].anchoredPosition = new Vector2(0f, -(Pad + visible * (RowH + Pad)));

                bool can = CanCraft(r, benchNear);
                _buttons[i].interactable = can;
                _labels[i].color = can ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                visible++;
            }

            _panelRect.sizeDelta = new Vector2(Width, visible * (RowH + Pad) + Pad);
        }

        private bool CanCraft(Recipe r, bool benchNear)
        {
            if (r == null || r.output == null) return false;
            if (r.requiresWorkbench && !benchNear) return false;
            if (r.inputs != null)
                foreach (Recipe.Ingredient ing in r.inputs)
                    if (ing.item == null || !_inv.Inventory.HasItems(ing.item, ing.count)) return false;
            return true;
        }

        private void TryCraft(Recipe r)
        {
            if (!CanCraft(r, WorkbenchNearby())) return;
            foreach (Recipe.Ingredient ing in r.inputs)
                _inv.Inventory.Consume(ing.item, ing.count);
            _inv.Inventory.Add(r.output, r.outputCount);
            Refresh();
        }

        private bool WorkbenchNearby()
            => PlacedObject.AnyNear("Workbench", transform.position, stationRange);
    }
}
