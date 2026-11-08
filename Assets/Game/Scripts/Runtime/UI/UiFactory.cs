using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ClearToLand
{
    /// <summary>References to the parts of a button built by <see cref="UiFactory"/>.</summary>
    public sealed class UiButtonParts
    {
        /// <summary>Root transform (sized, positioned by the caller).</summary>
        public RectTransform Rect;
        /// <summary>The uGUI button (navigation disabled, color-tint transition on <see cref="Background"/>).</summary>
        public Button Button;
        /// <summary>The visible fill that receives raycasts.</summary>
        public Image Background;
        /// <summary>Icon image, or null when the button has no icon.</summary>
        public Image Icon;
        /// <summary>Label (or fallback glyph when an icon sprite is missing), or null.</summary>
        public TextMeshProUGUI Label;
    }

    /// <summary>
    /// Code-only uGUI construction helpers: canvases, event system, rect placement, texts, images, panels, buttons and
    /// modal overlays, all styled with <see cref="UiTheme"/>. Everything is created on active GameObjects so TMP
    /// components initialise immediately.
    /// </summary>
    public static class UiFactory
    {
        /// <summary>CanvasScaler reference resolution width.</summary>
        public const float ReferenceWidth = 1920f;
        /// <summary>CanvasScaler reference resolution height.</summary>
        public const float ReferenceHeight = 1080f;

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        // ------------------------------------------------------------------ canvas / event system

        /// <summary>
        /// Creates a Screen Space Overlay canvas with a CanvasScaler (Scale With Screen Size, 1920x1080, Expand: the
        /// canvas is never smaller than the reference, so layouts designed for 1920x1080 fit 4:3 through 21:9 and
        /// portrait browser windows) and a GraphicRaycaster.
        /// </summary>
        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) go.layer = uiLayer;

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = false;
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1
                                               | AdditionalCanvasShaderChannels.Normal
                                               | AdditionalCanvasShaderChannels.Tangent;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>Creates an EventSystem with an InputSystemUIInputModule when the scene has none.</summary>
        public static void EnsureEventSystem()
        {
            EventSystem existing = EventSystem.current;
            if (existing == null) existing = Object.FindAnyObjectByType<EventSystem>();
            if (existing != null)
            {
                if (existing.GetComponent<InputSystemUIInputModule>() == null)
                {
                    // A legacy StandaloneInputModule would throw with the new Input System only; replace it.
                    BaseInputModule[] modules = existing.GetComponents<BaseInputModule>();
                    for (int i = 0; i < modules.Length; i++)
                    {
                        modules[i].enabled = false;
                        Object.Destroy(modules[i]);
                    }
                    existing.gameObject.AddComponent<InputSystemUIInputModule>();
                }
                return;
            }

            GameObject go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>Plays the UI click sound.</summary>
        public static void PlayClick() => AudioManager.Instance.Play(Sfx.Click);

        // ------------------------------------------------------------------ rect helpers

        /// <summary>Creates an empty child RectTransform (same layer as the parent).</summary>
        public static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.layer = parent.gameObject.layer;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Stretches <paramref name="rt"/> over its parent with the given insets (positive = inwards).</summary>
        public static RectTransform Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = Center;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Anchors <paramref name="rt"/> to one point of its parent with a fixed size.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }

        /// <summary>
        /// Stretches <paramref name="rt"/> horizontally across its parent (with side insets) and pins it
        /// <paramref name="top"/> px below the parent's top edge with a fixed height.
        /// </summary>
        public static RectTransform PlaceTopBand(RectTransform rt, float left, float right, float top, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -top - height);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        // ------------------------------------------------------------------ graphics

        /// <summary>Creates a TextMeshProUGUI label (no wrapping, not a raycast target).</summary>
        public static TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal,
            Material material = null)
        {
            RectTransform rt = CreateRect(name, parent);
            TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (material != null) tmp.fontSharedMaterial = material;
            tmp.text = text ?? string.Empty;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = style;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.richText = true;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>Creates an Image (not a raycast target). Sliced sprites use <see cref="Image.Type.Sliced"/>.</summary>
        public static Image CreateImage(Transform parent, string name, Sprite sprite, Color color, bool sliced = false,
            bool preserveAspect = false)
        {
            RectTransform rt = CreateRect(name, parent);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = sliced && sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.preserveAspect = preserveAspect;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Adds an upper-case caption with wide letter spacing.</summary>
        public static TextMeshProUGUI CreateCaption(Transform parent, string name, string text, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, Material material = null)
        {
            TextMeshProUGUI t = CreateText(parent, name, text, UiTheme.FontCaption, color, alignment, FontStyles.Bold, material);
            t.characterSpacing = UiTheme.CaptionSpacing;
            return t;
        }

        /// <summary>
        /// Adds a soft drop shadow as the first child of <paramref name="target"/> (so it draws behind siblings added later).
        /// Uses the soft rounded-rect sprite; does nothing when that sprite is missing.
        /// </summary>
        public static Image AddSoftShadow(RectTransform target, float spread, Vector2 offset, float alpha)
        {
            Sprite s = UiTheme.ShadowSprite;
            if (s == null) return null;
            Image img = CreateImage(target, "Shadow", s, new Color(0f, 0f, 0f, alpha), true);
            img.pixelsPerUnitMultiplier = Mathf.Clamp(12f / Mathf.Max(1f, spread), 0.05f, 4f);
            RectTransform rt = Stretch(img.rectTransform, -spread + offset.x, -spread + offset.y, -spread - offset.x, -spread - offset.y);
            rt.SetAsFirstSibling();
            return img;
        }

        /// <summary>Adds a soft round shadow (radial sprite) centered under a round element.</summary>
        public static Image AddRoundShadow(RectTransform target, float diameter, Vector2 offset, float alpha)
        {
            Sprite s = UiTheme.SoftCircleSprite;
            if (s == null) return null;
            Image img = CreateImage(target, "Shadow", s, new Color(0f, 0f, 0f, alpha));
            Place(img.rectTransform, Center, Center, offset, new Vector2(diameter, diameter));
            img.rectTransform.SetAsFirstSibling();
            return img;
        }

        /// <summary>Creates a rounded panel: optional soft shadow + 9-sliced panel fill. Returns the fill image.</summary>
        public static Image CreatePanel(RectTransform parent, Color color, bool shadow = true)
        {
            if (shadow) AddSoftShadow(parent, 34f, new Vector2(0f, -14f), 0.5f);
            Image bg = CreateImage(parent, "Background", UiTheme.PanelSprite, color, true);
            Stretch(bg.rectTransform);
            bg.raycastTarget = true;
            return bg;
        }

        /// <summary>Creates a thin horizontal divider line.</summary>
        public static Image CreateDivider(Transform parent, string name)
        {
            return CreateImage(parent, name, null, UiTheme.DividerColor);
        }

        // ------------------------------------------------------------------ buttons

        /// <summary>
        /// Applies the shared button feel: color tint (unscaled-time fades), no keyboard/gamepad navigation (so a
        /// clicked button is never re-triggered by Space/Enter) and a press-scale effect.
        /// </summary>
        public static void StyleButton(Button button, Graphic target)
        {
            button.targetGraphic = target;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock cb = button.colors;
            cb.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            cb.highlightedColor = Color.white;
            cb.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            cb.selectedColor = cb.normalColor;
            cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.5f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            button.colors = cb;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            if (button.GetComponent<UiPressScale>() == null) button.gameObject.AddComponent<UiPressScale>();
        }

        /// <summary>
        /// Wide rounded button with an optional icon left of the label (both centered as a group).
        /// The root has no graphic; children are Shadow (optional), Background (raycast target) and Content.
        /// </summary>
        public static UiButtonParts CreateButton(Transform parent, string name, string label, Sprite icon, Color fill,
            Color content, Vector2 size, Action onClick, bool clickSound = true, bool shadow = false)
        {
            RectTransform rt = CreateRect(name, parent);
            rt.sizeDelta = size;
            if (shadow) AddSoftShadow(rt, 20f, new Vector2(0f, -8f), 0.45f);

            Image bg = CreateImage(rt, "Background", UiTheme.ButtonSprite, fill, true);
            Stretch(bg.rectTransform);
            bg.raycastTarget = true;

            Button button = rt.gameObject.AddComponent<Button>();
            StyleButton(button, bg);

            RectTransform group = Stretch(CreateRect("Content", rt));
            HorizontalLayoutGroup h = group.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 18f;
            h.padding = new RectOffset(28, 28, 0, 0);
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;

            Image iconImg = null;
            if (icon != null)
            {
                iconImg = CreateImage(group, "Icon", icon, content, false, true);
                LayoutElement le = iconImg.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = UiTheme.ButtonIconSize;
                le.preferredHeight = UiTheme.ButtonIconSize;
            }

            TextMeshProUGUI text = null;
            if (!string.IsNullOrEmpty(label))
            {
                text = CreateText(group, "Label", label, UiTheme.FontButton, content, TextAlignmentOptions.Center, FontStyles.Bold);
                text.overflowMode = TextOverflowModes.Ellipsis;
            }

            button.onClick.AddListener(() =>
            {
                if (clickSound) PlayClick();
                onClick?.Invoke();
            });

            return new UiButtonParts { Rect = rt, Button = button, Background = bg, Icon = iconImg, Label = text };
        }

        /// <summary>
        /// Round icon button. <paramref name="hitSize"/> is the touch target (>= 120 recommended); the visible disc is
        /// <paramref name="visualSize"/>. When <paramref name="icon"/> is null, <paramref name="fallbackGlyph"/> is shown as text.
        /// </summary>
        public static UiButtonParts CreateRoundButton(Transform parent, string name, Sprite icon, string fallbackGlyph,
            float hitSize, float visualSize, Color fill, Color iconColor, Action onClick, bool clickSound = true,
            bool shadow = true, bool rim = true)
        {
            RectTransform rt = CreateRect(name, parent);
            rt.sizeDelta = new Vector2(hitSize, hitSize);

            // Transparent full-size hit area so the touch target can be larger than the visible disc.
            Image hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            hit.canvasRenderer.cullTransparentMesh = false; // a culled (invisible) graphic would get depth -1 and miss raycasts

            if (shadow) AddRoundShadow(rt, visualSize * 1.5f, new Vector2(0f, -visualSize * 0.06f), 0.55f);

            Image disc = CreateImage(rt, "Disc", UiTheme.CircleSprite, fill, false, true);
            Place(disc.rectTransform, Center, Center, Vector2.zero, new Vector2(visualSize, visualSize));

            if (rim && UiTheme.RingSprite != null)
            {
                Image ring = CreateImage(rt, "Rim", UiTheme.RingSprite, UiTheme.RimColor, false, true);
                Place(ring.rectTransform, Center, Center, Vector2.zero, new Vector2(visualSize, visualSize));
            }

            Image iconImg = null;
            TextMeshProUGUI glyph = null;
            float iconSize = visualSize * 0.48f;
            if (icon != null)
            {
                iconImg = CreateImage(rt, "Icon", icon, iconColor, false, true);
                Place(iconImg.rectTransform, Center, Center, Vector2.zero, new Vector2(iconSize, iconSize));
            }
            else if (!string.IsNullOrEmpty(fallbackGlyph))
            {
                glyph = CreateText(rt, "Glyph", fallbackGlyph, visualSize * 0.36f, iconColor, TextAlignmentOptions.Center, FontStyles.Bold);
                Place(glyph.rectTransform, Center, Center, Vector2.zero, new Vector2(visualSize, visualSize));
            }

            Button button = rt.gameObject.AddComponent<Button>();
            StyleButton(button, disc);

            button.onClick.AddListener(() =>
            {
                if (clickSound) PlayClick();
                onClick?.Invoke();
            });

            return new UiButtonParts { Rect = rt, Button = button, Background = disc, Icon = iconImg, Label = glyph };
        }

        // ------------------------------------------------------------------ overlays

        /// <summary>
        /// Creates a modal overlay: a full-screen dim (blocks input to the world), a safe-area child, and a centered
        /// panel of <paramref name="panelSize"/> that is scaled down to fit small screens and animated by the
        /// returned <see cref="UiFader"/>. Content goes into <paramref name="panel"/> (already has shadow + background).
        /// The overlay starts visible; call <see cref="UiFader.Hide"/> with instant = true after building.
        /// </summary>
        public static UiFader CreateOverlay(Transform parent, string name, Vector2 panelSize, out RectTransform panel,
            out Image panelBackground)
        {
            RectTransform root = Stretch(CreateRect(name, parent));
            Image dim = root.gameObject.AddComponent<Image>();
            dim.color = UiTheme.DimColor;
            dim.raycastTarget = true;
            root.gameObject.AddComponent<CanvasGroup>();

            RectTransform safe = Stretch(CreateRect("Safe", root));
            safe.gameObject.AddComponent<SafeAreaFitter>();

            RectTransform fit = Place(CreateRect("Fit", safe), Center, Center, Vector2.zero, panelSize);
            UiFitInParent fitter = fit.gameObject.AddComponent<UiFitInParent>();
            fitter.Padding = 20f;

            panel = Stretch(CreateRect("Panel", fit));
            panelBackground = CreatePanel(panel, UiTheme.PanelColor);

            UiFader fader = root.gameObject.AddComponent<UiFader>();
            fader.ScaleTarget = panel;
            return fader;
        }

        /// <summary>Creates a full-screen curtain (for scene fades). Starts fully visible.</summary>
        public static UiFader CreateCurtain(Transform parent, bool blocksInput)
        {
            RectTransform rt = Stretch(CreateRect("Curtain", parent));
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = UiTheme.Ink;
            img.raycastTarget = blocksInput;
            CanvasGroup group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            UiFader fader = rt.gameObject.AddComponent<UiFader>();
            fader.BlocksRaycastsWhenShown = blocksInput;
            fader.Duration = 0.3f;
            fader.Show(true);
            rt.SetAsLastSibling();
            return fader;
        }

        /// <summary>
        /// Adds a top (or bottom) vignette that darkens the screen edge for text contrast. Not a raycast target.
        /// </summary>
        public static Image CreateEdgeFade(Transform parent, string name, bool top, float height, float alpha)
        {
            Image img = CreateImage(parent, name, UiTheme.FadeSprite, UiTheme.WithAlpha(UiTheme.Ink, alpha));
            RectTransform rt = img.rectTransform;
            float edge = top ? 1f : 0f;
            rt.anchorMin = new Vector2(0f, edge);
            rt.anchorMax = new Vector2(1f, edge);
            rt.pivot = Center; // flip around the center so the bottom variant stays on screen
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, top ? -height * 0.5f : height * 0.5f);
            if (!top) rt.localScale = new Vector3(1f, -1f, 1f);
            return img;
        }
    }
}
