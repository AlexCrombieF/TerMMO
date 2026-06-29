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
    /// Simple crafting UI: toggle with C, lists recipes, click one to craft.
    /// Recipes requiring a workbench are only craftable when a workbench tile is
    /// within range of the player. Crafting consumes inputs and adds the output
    /// to the inventory. Panel is built in code (no manual scene setup).
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class CraftingPanel : MonoBehaviour
    {
        [SerializeField] private Recipe[] recipes;
        [SerializeField] private World world;
        [SerializeField] private ushort workbenchTileId = 8;
        [SerializeField] private float stationRange = 5f;
        [SerializeField] private Sprite slotFrame;

        private PlayerInventory _inv;
        private GameObject _panel;
        private bool _open;

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
            es.AddComponent<InputSystemUIInputModule>(); // required for the new Input System
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
            var prt = _panel.AddComponent<RectTransform>();
            prt.SetParent(canvasGo.transform, false);
            prt.anchorMin = new Vector2(0f, 0.5f);
            prt.anchorMax = new Vector2(0f, 0.5f);
            prt.pivot = new Vector2(0f, 0.5f);
            int rowH = 44, width = 320, pad = 8;
            int n = recipes != null ? recipes.Length : 0;
            prt.sizeDelta = new Vector2(width, n * (rowH + pad) + pad);
            prt.anchoredPosition = new Vector2(20f, 0f);

            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < n; i++)
            {
                Recipe recipe = recipes[i];

                var rowGo = new GameObject($"Recipe{i}");
                var rrt = rowGo.AddComponent<RectTransform>();
                rrt.SetParent(prt, false);
                rrt.anchorMin = new Vector2(0f, 1f);
                rrt.anchorMax = new Vector2(1f, 1f);
                rrt.pivot = new Vector2(0.5f, 1f);
                rrt.offsetMin = new Vector2(pad, 0f);
                rrt.offsetMax = new Vector2(-pad, 0f);
                rrt.sizeDelta = new Vector2(0f, rowH);
                rrt.anchoredPosition = new Vector2(0f, -(pad + i * (rowH + pad)));

                var btnImg = rowGo.AddComponent<Image>();
                btnImg.sprite = slotFrame;
                btnImg.color = new Color(1f, 1f, 1f, 0.9f);
                var btn = rowGo.AddComponent<Button>();
                int captured = i;
                btn.onClick.AddListener(() => TryCraft(recipes[captured]));
                _buttons.Add(btn);

                var iconGo = new GameObject("Icon");
                var icon = iconGo.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                RectTransform irt = icon.rectTransform;
                irt.SetParent(rrt, false);
                irt.anchorMin = new Vector2(0f, 0.5f);
                irt.anchorMax = new Vector2(0f, 0.5f);
                irt.pivot = new Vector2(0f, 0.5f);
                irt.sizeDelta = new Vector2(rowH - 8, rowH - 8);
                irt.anchoredPosition = new Vector2(6f, 0f);
                icon.sprite = recipe != null && recipe.output != null ? recipe.output.Icon : null;
                _icons.Add(icon);

                var labelGo = new GameObject("Label");
                var label = labelGo.AddComponent<Text>();
                label.font = font;
                label.fontSize = 16;
                label.alignment = TextAnchor.MiddleLeft;
                label.color = Color.white;
                label.raycastTarget = false;
                RectTransform lrt = label.rectTransform;
                lrt.SetParent(rrt, false);
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(rowH, 2f);
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
            if (r.requiresWorkbench) sb.Append(" [bench]");
            return sb.ToString();
        }

        private void Refresh()
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                bool can = CanCraft(recipes[i]);
                _buttons[i].interactable = can;
                _labels[i].color = can ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            }
        }

        private bool CanCraft(Recipe r)
        {
            if (r == null || r.output == null) return false;
            if (r.requiresWorkbench && !WorkbenchNearby()) return false;
            if (r.inputs != null)
                foreach (Recipe.Ingredient ing in r.inputs)
                    if (ing.item == null || !_inv.Inventory.HasItems(ing.item, ing.count)) return false;
            return true;
        }

        private void TryCraft(Recipe r)
        {
            if (!CanCraft(r)) return;
            foreach (Recipe.Ingredient ing in r.inputs)
                _inv.Inventory.Consume(ing.item, ing.count);
            _inv.Inventory.Add(r.output, r.outputCount);
            Refresh();
        }

        private bool WorkbenchNearby()
        {
            if (world == null) return false;
            Vector2Int c = WorldCoords.WorldToTile(transform.position);
            int r = Mathf.CeilToInt(stationRange);
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (world.GetTile(new Vector2Int(c.x + dx, c.y + dy)) == workbenchTileId)
                        return true;
            return false;
        }
    }
}
