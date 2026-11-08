using TMPro;
using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// Shared look of every runtime screen: palette, font sizes, spacing, the UI sprites taken from
    /// <see cref="GameConfig"/> and the TextMeshPro effect materials used for text drawn straight over map art.
    /// All sizes are reference pixels of the 1920x1080 CanvasScaler (Expand mode, so at least 1920x1080 is always available).
    /// </summary>
    public static class UiTheme
    {
        // ------------------------------------------------------------------ palette

        /// <summary>Near-black navy: text on bright fills, outlines, curtains.</summary>
        public static readonly Color Ink = new Color32(0x0A, 0x10, 0x1A, 0xFF);
        /// <summary>Semi-opaque dark panel fill (pause / game over / how-to panels).</summary>
        public static readonly Color PanelColor = new Color32(0x12, 0x1A, 0x28, 0xF4);
        /// <summary>Map card fill (slightly lighter than panels; the button tint darkens it a little at rest).</summary>
        public static readonly Color CardColor = new Color32(0x1C, 0x27, 0x3A, 0xF6);
        /// <summary>Secondary button fill.</summary>
        public static readonly Color ButtonColor = new Color32(0x2D, 0x3B, 0x54, 0xFF);
        /// <summary>Round HUD button fill drawn over the map.</summary>
        public static readonly Color HudButtonColor = new Color32(0x0B, 0x12, 0x1E, 0xC8);
        /// <summary>Primary accent (mint) - distinct from the four landing colors.</summary>
        public static readonly Color AccentColor = new Color32(0x3B, 0xD9, 0xA8, 0xFF);
        /// <summary>Errors and the crash headline.</summary>
        public static readonly Color DangerColor = new Color32(0xFF, 0x5D, 0x52, 0xFF);
        /// <summary>Stars, "new best" badge, best score highlight.</summary>
        public static readonly Color GoldColor = new Color32(0xFF, 0xCB, 0x45, 0xFF);
        /// <summary>Main text color.</summary>
        public static readonly Color TextPrimary = new Color32(0xF5, 0xF8, 0xFC, 0xFF);
        /// <summary>Secondary text (subtitles, descriptions).</summary>
        public static readonly Color TextSecondary = new Color32(0xB8, 0xC4, 0xD6, 0xFF);
        /// <summary>Captions and de-emphasised labels.</summary>
        public static readonly Color TextMuted = new Color32(0x84, 0x92, 0xA8, 0xFF);
        /// <summary>Full-screen dim behind modal panels.</summary>
        public static readonly Color DimColor = new Color(0.02f, 0.035f, 0.06f, 0.66f);
        /// <summary>Unlit star / disabled glyph.</summary>
        public static readonly Color StarDimColor = new Color(1f, 1f, 1f, 0.16f);
        /// <summary>Hairline dividers on panels.</summary>
        public static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.08f);
        /// <summary>Thin rim drawn around round HUD buttons for contrast on dark maps.</summary>
        public static readonly Color RimColor = new Color(1f, 1f, 1f, 0.16f);

        // ------------------------------------------------------------------ font sizes (reference px)

        /// <summary>Main menu title.</summary>
        public const float FontDisplay = 96f;
        /// <summary>Panel titles.</summary>
        public const float FontTitle = 62f;
        /// <summary>Card titles, large labels.</summary>
        public const float FontHeading = 40f;
        /// <summary>Button labels.</summary>
        public const float FontButton = 36f;
        /// <summary>Body text.</summary>
        public const float FontBody = 32f;
        /// <summary>Small body text (taglines, tips).</summary>
        public const float FontSmall = 27f;
        /// <summary>Upper-case captions ("SCORE", "TIPS").</summary>
        public const float FontCaption = 23f;
        /// <summary>HUD score value.</summary>
        public const float FontHudValue = 60f;
        /// <summary>HUD best value.</summary>
        public const float FontHudSecondary = 44f;

        /// <summary>Letter spacing (TMP units, 1/100 em) for upper-case captions.</summary>
        public const float CaptionSpacing = 14f;

        // ------------------------------------------------------------------ spacing / sizes

        /// <summary>Distance of HUD elements from the safe-area edges.</summary>
        public const float ScreenMargin = 28f;
        /// <summary>Default gap between stacked elements.</summary>
        public const float Gap = 16f;
        /// <summary>Gap between groups.</summary>
        public const float GapLarge = 32f;
        /// <summary>Minimum touch target edge (reference px).</summary>
        public const float MinTouch = 120f;
        /// <summary>Height of wide text buttons.</summary>
        public const float ButtonHeight = 120f;
        /// <summary>Icon edge inside wide buttons.</summary>
        public const float ButtonIconSize = 54f;

        /// <summary>Round HUD button edge; a little larger on phones / mobile browsers.</summary>
        public static float HudButtonSize => Application.isMobilePlatform ? 140f : 120f;

        // ------------------------------------------------------------------ sprites

        private static GameConfig s_config;

        /// <summary>Config whose UI sprites are used. Set by GameUi.Create / MainMenuUi.Create.</summary>
        public static GameConfig Config => s_config;

        /// <summary>Selects the config that provides UI sprites (null is allowed: plain rects are drawn).</summary>
        public static void Use(GameConfig config) => s_config = config;

        private static bool HasConfig => s_config != null;

        /// <summary>White 9-sliced rounded panel.</summary>
        public static Sprite PanelSprite => HasConfig ? s_config.panelSprite : null;
        /// <summary>White 9-sliced rounded button.</summary>
        public static Sprite ButtonSprite => HasConfig ? s_config.buttonSprite : null;
        /// <summary>White 9-sliced rounded card.</summary>
        public static Sprite CardSprite => HasConfig ? s_config.cardSprite : null;
        /// <summary>White filled circle.</summary>
        public static Sprite CircleSprite => HasConfig ? s_config.circleSprite : null;
        /// <summary>Title emblem.</summary>
        public static Sprite LogoSprite => HasConfig ? s_config.logoSprite : null;
        /// <summary>White radial soft disc (world sprite reused for soft round shadows / glows).</summary>
        public static Sprite SoftCircleSprite => HasConfig ? s_config.softCircleSprite : null;
        /// <summary>Soft-edged 9-sliced rounded rect (runway glow, reused for soft panel shadows).</summary>
        public static Sprite ShadowSprite => HasConfig ? s_config.spotGlowRunwaySprite : null;
        /// <summary>Thin white ring.</summary>
        public static Sprite RingSprite => HasConfig ? s_config.ringSprite : null;
        /// <summary>White chevron pointing +X.</summary>
        public static Sprite ChevronSprite => HasConfig ? s_config.chevronSprite : null;
        /// <summary>Pause glyph.</summary>
        public static Sprite IconPause => HasConfig ? s_config.iconPause : null;
        /// <summary>Play glyph.</summary>
        public static Sprite IconPlay => HasConfig ? s_config.iconPlay : null;
        /// <summary>Fast-forward glyph.</summary>
        public static Sprite IconFastForward => HasConfig ? s_config.iconFastForward : null;
        /// <summary>Sound on glyph.</summary>
        public static Sprite IconSoundOn => HasConfig ? s_config.iconSoundOn : null;
        /// <summary>Sound off glyph.</summary>
        public static Sprite IconSoundOff => HasConfig ? s_config.iconSoundOff : null;
        /// <summary>Home glyph.</summary>
        public static Sprite IconHome => HasConfig ? s_config.iconHome : null;
        /// <summary>Restart glyph.</summary>
        public static Sprite IconRestart => HasConfig ? s_config.iconRestart : null;
        /// <summary>Help glyph.</summary>
        public static Sprite IconHelp => HasConfig ? s_config.iconHelp : null;
        /// <summary>Close glyph.</summary>
        public static Sprite IconClose => HasConfig ? s_config.iconClose : null;
        /// <summary>Star glyph.</summary>
        public static Sprite IconStar => HasConfig ? s_config.iconStar : null;

        // ------------------------------------------------------------------ generated assets

        private static Sprite s_fadeSprite;
        private static Material s_hudTextMaterial;
        private static Material s_softShadowTextMaterial;

        /// <summary>
        /// Vertical alpha ramp generated at runtime (opaque at the top edge, transparent at the bottom).
        /// Flip it with localScale.y = -1 for a bottom vignette.
        /// </summary>
        public static Sprite FadeSprite
        {
            get
            {
                if (s_fadeSprite == null) s_fadeSprite = CreateFadeSprite();
                return s_fadeSprite;
            }
        }

        /// <summary>
        /// Text material with a dark outline and a soft drop shadow for labels drawn directly over map art (HUD).
        /// Null when no TMP default font exists (texts then keep the font's default material).
        /// </summary>
        public static Material HudTextMaterial
        {
            get
            {
                if (s_hudTextMaterial == null)
                {
                    s_hudTextMaterial = CreateTextMaterial("CTL HUD Text", 0.2f, new Color(0.03f, 0.05f, 0.09f, 0.92f),
                        new Color(0f, 0f, 0f, 0.55f), new Vector2(0.35f, -0.6f), 0.15f, 0.45f, 0.05f);
                }
                return s_hudTextMaterial;
            }
        }

        /// <summary>Text material with only a soft drop shadow (titles over the dimmed menu backdrop).</summary>
        public static Material SoftShadowTextMaterial
        {
            get
            {
                if (s_softShadowTextMaterial == null)
                {
                    s_softShadowTextMaterial = CreateTextMaterial("CTL Soft Shadow Text", 0f, new Color(0f, 0f, 0f, 0f),
                        new Color(0f, 0f, 0f, 0.6f), new Vector2(0.25f, -0.55f), 0.1f, 0.6f, 0f);
                }
                return s_softShadowTextMaterial;
            }
        }

        /// <summary>Returns a copy of <paramref name="c"/> with alpha <paramref name="a"/>.</summary>
        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>Smooth ease-out (cubic) for 0..1.</summary>
        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return 1f - u * u * u;
        }

        /// <summary>Ease-out with a small overshoot for 0..1 (pop-in animations).</summary>
        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        private static Sprite CreateFadeSprite()
        {
            const int height = 64;
            Texture2D tex = new Texture2D(1, height, TextureFormat.RGBA32, false);
            tex.name = "CTL UI Fade";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color32[] pixels = new Color32[height];
            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);          // 0 at the bottom row, 1 at the top row
                float a = t * t * (3f - 2f * t);            // smoothstep
                a *= a;                                     // keep the lower half very light
                pixels[y] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
            sprite.name = "CTL UI Fade";
            return sprite;
        }

        /// <summary>
        /// Clones the default font material and enables outline + underlay. The keyword set (OUTLINE_ON UNDERLAY_ON on
        /// the TMP SDF Mobile shader) matches TMP Essentials' "LiberationSans SDF - Drop Shadow" material, which lives
        /// in a Resources folder, so the shader variant is always part of player builds (WebGL / Android / Windows).
        /// </summary>
        private static Material CreateTextMaterial(string name, float outlineWidth, Color outlineColor, Color underlayColor,
            Vector2 underlayOffset, float underlayDilate, float underlaySoftness, float faceDilate)
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null || font.material == null) return null;

            Material m = new Material(font.material);
            m.name = name;
            m.EnableKeyword("OUTLINE_ON");
            m.EnableKeyword("UNDERLAY_ON");
            SetFloatIfPresent(m, "_OutlineWidth", outlineWidth);
            SetColorIfPresent(m, "_OutlineColor", outlineColor);
            SetFloatIfPresent(m, "_FaceDilate", faceDilate);
            SetColorIfPresent(m, "_UnderlayColor", underlayColor);
            SetFloatIfPresent(m, "_UnderlayOffsetX", underlayOffset.x);
            SetFloatIfPresent(m, "_UnderlayOffsetY", underlayOffset.y);
            SetFloatIfPresent(m, "_UnderlayDilate", underlayDilate);
            SetFloatIfPresent(m, "_UnderlaySoftness", underlaySoftness);
            ShaderUtilities.UpdateShaderRatios(m);
            return m;
        }

        private static void SetFloatIfPresent(Material m, string property, float value)
        {
            if (m.HasProperty(property)) m.SetFloat(property, value);
        }

        private static void SetColorIfPresent(Material m, string property, Color value)
        {
            if (m.HasProperty(property)) m.SetColor(property, value);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_config = null;
            s_fadeSprite = null;
            s_hudTextMaterial = null;
            s_softShadowTextMaterial = null;
        }
    }
}
