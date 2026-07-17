using UnityEngine;
using UnityEngine.UI;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Vertical health bar: custom frame art with a fill sprite that drains
    /// top-down as HP drops (fill origin at the bottom). Until the art exists it
    /// renders a generated dark casing + red fill so the system is testable.
    /// Anchored to the left edge of the screen. Built in code.
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class HealthBarHUD : MonoBehaviour
    {
        [Tooltip("Casing/border art. Same canvas as the fill sprite.")]
        [SerializeField] private Sprite frameSprite;
        [Tooltip("Fill at 100% HP, cropped vertically by current health.")]
        [SerializeField] private Sprite fillSprite;
        [Tooltip("UI pixels per art pixel (art is scaled up by this).")]
        [SerializeField] private float scale = 3f;
        [Tooltip("Inset from the top-right corner.")]
        [SerializeField] private Vector2 screenMargin = new Vector2(18f, 18f);

        private PlayerHealth _health;
        private GameObject _canvas;
        private Image _fill;
        private Text _label;

        private void Awake() => _health = GetComponent<PlayerHealth>();

        private PlayerXP _xp;

        private void Start()
        {
            _xp = GetComponent<PlayerXP>();
            BuildUI();
            _health.Changed += Refresh;
            if (_xp != null) _xp.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_health != null) _health.Changed -= Refresh;
            if (_xp != null) _xp.Changed -= Refresh;
            if (_canvas != null) Destroy(_canvas); // no orphaned UI when the player is rebuilt
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("HealthCanvas");
            _canvas = canvasGo;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // Bar size: from the art if present, otherwise a 16x64-ish default.
            Vector2 artSize = frameSprite != null ? frameSprite.rect.size : new Vector2(16f, 64f);
            Vector2 barSize = artSize * scale;

            var holder = new GameObject("HealthBar").AddComponent<RectTransform>();
            holder.SetParent(canvasGo.transform, false);
            holder.anchorMin = holder.anchorMax = new Vector2(1f, 1f); // top-right corner
            holder.pivot = new Vector2(1f, 1f);
            holder.sizeDelta = barSize;
            holder.anchoredPosition = new Vector2(-screenMargin.x, -screenMargin.y);

            // Fill goes UNDER the frame so the casing borders draw on top.
            _fill = new GameObject("Fill").AddComponent<Image>();
            RectTransform frt = _fill.rectTransform;
            frt.SetParent(holder, false);
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Vertical;
            _fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            _fill.raycastTarget = false;
            if (fillSprite != null)
            {
                _fill.sprite = fillSprite;
            }
            else
            {
                _fill.sprite = SolidSprite();          // placeholder red column
                _fill.color = new Color(0.85f, 0.15f, 0.15f);
                frt.offsetMin = new Vector2(3f, 3f);   // inset inside the fake casing
                frt.offsetMax = new Vector2(-3f, -3f);
            }

            var frame = new GameObject("Frame").AddComponent<Image>();
            RectTransform rrt = frame.rectTransform;
            rrt.SetParent(holder, false);
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = rrt.offsetMax = Vector2.zero;
            frame.raycastTarget = false;
            if (frameSprite != null)
            {
                frame.sprite = frameSprite;
            }
            else
            {
                frame.sprite = SolidSprite();
                frame.color = new Color(0.1f, 0.1f, 0.13f, 0.85f);
                rrt.SetAsFirstSibling(); // placeholder casing behind the fill
            }

            // Small numeric readout under the bar.
            _label = new GameObject("Label").AddComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 15;
            _label.alignment = TextAnchor.UpperCenter;
            _label.color = Color.white;
            _label.raycastTarget = false;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform lrt = _label.rectTransform;
            lrt.SetParent(holder, false);
            lrt.anchorMin = new Vector2(0.5f, 0f);
            lrt.anchorMax = new Vector2(0.5f, 0f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.anchoredPosition = new Vector2(0f, -6f);
        }

        private static Sprite SolidSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private void Refresh()
        {
            if (_fill != null) _fill.fillAmount = _health.Fraction;
            if (_label == null) return;

            string text = $"{Mathf.CeilToInt(_health.Current)}/{Mathf.CeilToInt(_health.Max)}";
            if (_xp != null)
                text += $"\nLv {_xp.Level}  {Mathf.FloorToInt(_xp.Xp)}/{Mathf.CeilToInt(_xp.XpToNext)}";
            _label.text = text;
        }
    }
}
