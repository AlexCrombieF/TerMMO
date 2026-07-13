using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Character creation shown when Play starts: pick a hairstyle and skin /
    /// hair / eye colours from swatch palettes, with a live layered preview,
    /// then press Start. Gameplay input is disabled until confirmed. The choice
    /// persists in PlayerPrefs (pre-selected next session) and in the save file.
    /// Built entirely in code, like the other UI.
    /// </summary>
    [RequireComponent(typeof(PlayerAppearanceRenderer))]
    public sealed class CharacterCreationUI : MonoBehaviour
    {
        private const string PrefsKey = "doodgy_appearance";

        [Tooltip("Gameplay components disabled while the creator is open.")]
        [SerializeField] private Behaviour[] disableWhileOpen;

        private static readonly Color[] SkinTones =
        {
            new Color(0.98f, 0.86f, 0.74f), new Color(0.93f, 0.76f, 0.60f),
            new Color(0.82f, 0.62f, 0.45f), new Color(0.65f, 0.46f, 0.32f),
            new Color(0.48f, 0.33f, 0.22f), new Color(0.35f, 0.24f, 0.16f),
        };
        private static readonly Color[] HairTones =
        {
            new Color(0.12f, 0.12f, 0.12f), new Color(0.30f, 0.20f, 0.12f),
            new Color(0.48f, 0.32f, 0.18f), new Color(0.90f, 0.78f, 0.45f),
            new Color(0.80f, 0.42f, 0.18f), new Color(0.75f, 0.15f, 0.15f),
            new Color(0.25f, 0.45f, 0.85f), new Color(0.92f, 0.92f, 0.92f),
        };
        private static readonly Color[] EyeTones =
        {
            new Color(0.35f, 0.22f, 0.12f), new Color(0.25f, 0.45f, 0.85f),
            new Color(0.25f, 0.65f, 0.35f), new Color(0.55f, 0.55f, 0.58f),
            new Color(0.85f, 0.60f, 0.20f), new Color(0.55f, 0.35f, 0.75f),
        };

        private PlayerAppearanceRenderer _renderer;
        private PlayerAppearance _appearance;
        private Font _font;

        private GameObject _screen;
        private Image _prevBody, _prevClothes, _prevEyesBase, _prevEyes, _prevHair;
        private Text _hairLabel;

        private void Start()
        {
            _renderer = GetComponent<PlayerAppearanceRenderer>();

            // Restore the last choice so returning players start from theirs.
            _appearance = PlayerPrefs.HasKey(PrefsKey)
                ? JsonUtility.FromJson<PlayerAppearance>(PlayerPrefs.GetString(PrefsKey))
                : PlayerAppearance.Default;

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            BuildUI();
            SetGameplayEnabled(false);
            RefreshPreview();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private void SetGameplayEnabled(bool enabled)
        {
            if (disableWhileOpen == null) return;
            foreach (Behaviour b in disableWhileOpen)
                if (b != null) b.enabled = enabled;
        }

        private void OnDestroy()
        {
            if (_screen != null) Destroy(_screen); // no orphaned UI when the player is rebuilt
        }

        private void Confirm()
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(_appearance));
            PlayerPrefs.Save();
            _renderer.Apply(_appearance);
            SetGameplayEnabled(true);
            Destroy(_screen);
            Destroy(this);
        }

        // ------------------------------------------------------------------ UI

        private void BuildUI()
        {
            var canvasGo = new GameObject("CharacterCreationCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200; // above everything
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();
            _screen = canvasGo;

            // Dim the world behind the panel.
            var dim = new GameObject("Dim").AddComponent<Image>();
            Stretch(dim.rectTransform, canvasGo.transform);
            dim.color = new Color(0f, 0f, 0f, 0.75f);

            var panel = new GameObject("Panel").AddComponent<Image>();
            RectTransform prt = panel.rectTransform;
            prt.SetParent(canvasGo.transform, false);
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(560f, 640f);
            panel.color = new Color(0.07f, 0.08f, 0.11f, 0.97f);

            MakeText(prt, "CREATE YOUR CHARACTER", 22, TextAnchor.MiddleCenter, Color.white,
                     new Vector2(0f, 290f), new Vector2(520f, 30f));

            BuildPreview(prt, new Vector2(0f, 175f));

            // Hairstyle selector.
            _hairLabel = MakeText(prt, "", 17, TextAnchor.MiddleCenter, Color.white,
                                  new Vector2(0f, 55f), new Vector2(200f, 28f));
            MakeButton(prt, "<", new Vector2(-140f, 55f), new Vector2(44f, 34f), () => CycleHair(-1));
            MakeButton(prt, ">", new Vector2(140f, 55f), new Vector2(44f, 34f), () => CycleHair(1));

            // Swatch rows.
            BuildSwatchRow(prt, "Skin", SkinTones, 0f, c => { _appearance.skinColor = c; RefreshPreview(); });
            BuildSwatchRow(prt, "Hair", HairTones, -75f, c => { _appearance.hairColor = c; RefreshPreview(); });
            BuildSwatchRow(prt, "Eyes", EyeTones, -150f, c => { _appearance.eyeColor = c; RefreshPreview(); });

            MakeButton(prt, "START", new Vector2(0f, -265f), new Vector2(220f, 52f), Confirm, 20);
        }

        private void BuildPreview(RectTransform parent, Vector2 pos)
        {
            var holder = new GameObject("Preview").AddComponent<RectTransform>();
            holder.SetParent(parent, false);
            holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
            holder.anchoredPosition = pos;
            holder.sizeDelta = new Vector2(96f, 192f); // 16x32 art at 6x

            _prevBody = MakePreviewLayer(holder);
            _prevClothes = MakePreviewLayer(holder);
            _prevEyesBase = MakePreviewLayer(holder);
            _prevEyes = MakePreviewLayer(holder);
            _prevHair = MakePreviewLayer(holder);
        }

        private static Image MakePreviewLayer(RectTransform parent)
        {
            var img = new GameObject("Layer").AddComponent<Image>();
            RectTransform rt = img.rectTransform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            img.raycastTarget = false;
            img.enabled = false;
            return img;
        }

        private void BuildSwatchRow(RectTransform parent, string label, Color[] colors,
                                    float y, System.Action<Color> onPick)
        {
            MakeText(parent, label, 15, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.55f),
                     new Vector2(-215f, y), new Vector2(90f, 24f));

            const float size = 34f, gap = 8f;
            float startX = -150f;
            for (int i = 0; i < colors.Length; i++)
            {
                Color c = colors[i];
                var sw = new GameObject($"Swatch{i}").AddComponent<Image>();
                RectTransform srt = sw.rectTransform;
                srt.SetParent(parent, false);
                srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
                srt.sizeDelta = new Vector2(size, size);
                srt.anchoredPosition = new Vector2(startX + i * (size + gap), y);
                sw.color = c;
                var btn = sw.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(() => onPick(c));
            }
        }

        private void CycleHair(int dir)
        {
            int n = Mathf.Max(1, _renderer.HairStyleCount);
            _appearance.hairStyle = ((_appearance.hairStyle + dir) % n + n) % n;
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            // Push the current pick onto the actual player too — instant feedback
            // in the world behind the panel.
            _renderer.Apply(_appearance);

            _hairLabel.text = _renderer.HairStyleCount > 0
                ? $"Hair {_appearance.hairStyle + 1}/{_renderer.HairStyleCount}"
                : "Hair (no styles yet)";

            SetPreviewLayer(_prevBody, _renderer.BodySprite, _appearance.skinColor);
            SetPreviewLayer(_prevClothes, _renderer.ClothesSprite, Color.white);
            SetPreviewLayer(_prevEyesBase, _renderer.EyesBaseSprite, Color.white);
            SetPreviewLayer(_prevEyes, _renderer.EyesSprite, _appearance.eyeColor);
            SetPreviewLayer(_prevHair, _renderer.HairSprite(_appearance.hairStyle), _appearance.hairColor);

            // No body art drawn yet? Show a plain tinted block so colour picking
            // still previews something.
            if (_renderer.BodySprite == null)
            {
                _prevBody.enabled = true;
                _prevBody.sprite = null;
                _prevBody.color = _appearance.skinColor;
                _prevBody.rectTransform.sizeDelta = new Vector2(96f, 192f);
                _prevBody.rectTransform.anchoredPosition = Vector2.zero;
            }
        }

        private const float PreviewScale = 6f; // preview pixels per source pixel

        /// <summary>
        /// Lays a layer out at its true canvas position. The importer trims each
        /// sprite to its content (the eyes are a few pixels!) but adjusts pivots
        /// so they still share one canvas alignment point — so we size each image
        /// to its own rect, put its pivot at a shared spot, and everything stacks
        /// exactly like it does in the world.
        /// </summary>
        private void SetPreviewLayer(Image img, Sprite sprite, Color tint)
        {
            img.sprite = sprite;
            img.color = tint;
            img.enabled = sprite != null;
            if (sprite == null) return;

            Sprite body = _renderer.BodySprite;
            Vector2 bodyCenterPx = body != null
                ? (Vector2)body.bounds.center * body.pixelsPerUnit
                : Vector2.zero;

            RectTransform rt = img.rectTransform;
            rt.sizeDelta = sprite.rect.size * PreviewScale;
            rt.pivot = new Vector2(
                sprite.rect.width > 0f ? sprite.pivot.x / sprite.rect.width : 0.5f,
                sprite.rect.height > 0f ? sprite.pivot.y / sprite.rect.height : 0.5f);
            // Shared pivot point sits so the body's content is centred in the holder.
            rt.anchoredPosition = -bodyCenterPx * PreviewScale;
        }

        // ------------------------------------------------------------- helpers

        private static void Stretch(RectTransform rt, Transform parent)
        {
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private Text MakeText(RectTransform parent, string value, int size, TextAnchor anchor,
                              Color color, Vector2 pos, Vector2 dims)
        {
            var t = new GameObject("Text").AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = value;
            RectTransform rt = t.rectTransform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = dims;
            rt.anchoredPosition = pos;
            return t;
        }

        private void MakeButton(RectTransform parent, string label, Vector2 pos, Vector2 size,
                                UnityEngine.Events.UnityAction onClick, int fontSize = 16)
        {
            var img = new GameObject($"Btn_{label}").AddComponent<Image>();
            RectTransform rt = img.rectTransform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            img.color = new Color(0.2f, 0.35f, 0.25f, 1f);
            img.gameObject.AddComponent<Button>().onClick.AddListener(onClick);

            Text t = MakeText(rt, label, fontSize, TextAnchor.MiddleCenter, Color.white,
                              Vector2.zero, size);
            t.raycastTarget = false;
        }
    }
}
