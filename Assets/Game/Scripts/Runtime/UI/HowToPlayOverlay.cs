using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClearToLand
{
    /// <summary>
    /// Builds the "How to play" modal: drawing instructions, a landing legend (aircraft sprite tinted with its color
    /// -> landing spot label) and tips (approach direction, collisions, fast-forward, pause). Closes via the close
    /// button, a click on the dim outside the panel, or Esc / Android back (handled by the owner).
    /// </summary>
    public static class HowToPlayOverlay
    {
        private const float PanelWidth = 1440f;
        private const float PanelHeight = 760f;
        private const float ColumnsTop = 236f;
        private const float LeftX = 64f;
        private const float LeftWidth = 560f;
        private const float RightX = 704f;
        private const float RightWidth = 672f;
        private const float RowHeight = 104f;

        private static readonly LandingKind[] KindOrder =
        {
            LandingKind.ShortRunway, LandingKind.LongRunway, LandingKind.Helipad, LandingKind.WaterLane
        };

        /// <summary>
        /// Creates the overlay under <paramref name="parent"/> (normally the canvas root) and returns its fader,
        /// already hidden. Show it with <see cref="UiFader.Show"/>.
        /// </summary>
        public static UiFader Build(Transform parent, GameConfig config)
        {
            UiFader fader = UiFactory.CreateOverlay(parent, "HowToPlay", new Vector2(PanelWidth, PanelHeight),
                out RectTransform panel, out Image panelBackground);

            // Clicks on the panel are swallowed; clicks on the dim around it close the overlay.
            panelBackground.gameObject.AddComponent<UiClickCatcher>();
            UiClickCatcher outside = fader.gameObject.AddComponent<UiClickCatcher>();
            outside.Clicked += () =>
            {
                UiFactory.PlayClick();
                fader.Hide();
            };

            TextMeshProUGUI title = UiFactory.CreateText(panel, "Title", "How to play", UiTheme.FontTitle, UiTheme.TextPrimary,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.PlaceTopBand(title.rectTransform, 160f, 160f, 40f, 76f);

            UiButtonParts close = UiFactory.CreateRoundButton(panel, "Close", UiTheme.IconClose, "X", UiTheme.MinTouch, 84f,
                UiTheme.ButtonColor, UiTheme.TextPrimary, () => fader.Hide(), true, false, false);
            UiFactory.Place(close.Rect, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-82f, -78f),
                new Vector2(UiTheme.MinTouch, UiTheme.MinTouch));

            TextMeshProUGUI intro = UiFactory.CreateText(panel, "Intro",
                "Press on an aircraft and drag to draw its flight path. Lead it to the landing spot painted in its color.",
                UiTheme.FontBody, UiTheme.TextSecondary);
            intro.textWrappingMode = TextWrappingModes.Normal;
            UiFactory.PlaceTopBand(intro.rectTransform, 140f, 140f, 124f, 84f);

            BuildLegend(panel, config);

            Image divider = UiFactory.CreateDivider(panel, "Divider");
            UiFactory.Place(divider.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 1f),
                new Vector2((LeftX + LeftWidth + RightX) * 0.5f, -ColumnsTop), new Vector2(2f, 4f * RowHeight + 46f));

            BuildTips(panel);

            fader.Hide(true);
            return fader;
        }

        // ------------------------------------------------------------------ legend

        private static void BuildLegend(RectTransform panel, GameConfig config)
        {
            TextMeshProUGUI header = UiFactory.CreateCaption(panel, "LegendHeader", "LAND BY COLOR", UiTheme.TextMuted,
                TextAlignmentOptions.Left);
            UiFactory.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(LeftX, -ColumnsTop),
                new Vector2(LeftWidth, 30f));

            // Group aircraft by landing kind, keeping config order inside each group.
            List<AircraftType>[] groups = new List<AircraftType>[KindOrder.Length];
            bool anyAircraft = false;
            if (config != null && config.aircraftTypes != null)
            {
                for (int i = 0; i < config.aircraftTypes.Count; i++)
                {
                    AircraftType t = config.aircraftTypes[i];
                    if (t == null) continue;
                    int k = System.Array.IndexOf(KindOrder, t.landingKind);
                    if (k < 0) continue;
                    if (groups[k] == null) groups[k] = new List<AircraftType>();
                    groups[k].Add(t);
                    anyAircraft = true;
                }
            }

            float top = ColumnsTop + 46f;
            for (int k = 0; k < KindOrder.Length; k++)
            {
                if (anyAircraft && groups[k] == null) continue;
                BuildLegendRow(panel, top, KindOrder[k], groups[k]);
                top += RowHeight;
            }
        }

        private static void BuildLegendRow(RectTransform panel, float top, LandingKind kind, List<AircraftType> types)
        {
            Color color = LandingColors.For(kind);
            RectTransform row = UiFactory.CreateRect("Legend_" + kind, panel);
            UiFactory.Place(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(LeftX, -top),
                new Vector2(LeftWidth, RowHeight - 8f));
            Vector2 midLeft = new Vector2(0f, 0.5f);

            // Aircraft icons, right-aligned in a 200 px slot.
            RectTransform planes = UiFactory.CreateRect("Aircraft", row);
            UiFactory.Place(planes, midLeft, midLeft, Vector2.zero, new Vector2(200f, RowHeight - 8f));
            HorizontalLayoutGroup h = planes.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleRight;
            h.spacing = 6f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            if (types != null)
            {
                for (int i = 0; i < types.Count && i < 3; i++) AddAircraftIcon(planes, types[i], types.Count > 2 ? 64f : 90f);
            }

            // Arrow.
            Sprite chevron = UiTheme.ChevronSprite;
            if (chevron != null)
            {
                Image arrow = UiFactory.CreateImage(row, "Arrow", chevron, UiTheme.TextMuted, false, true);
                UiFactory.Place(arrow.rectTransform, midLeft, new Vector2(0.5f, 0.5f), new Vector2(236f, 0f), new Vector2(30f, 30f));
            }
            else
            {
                TextMeshProUGUI arrowText = UiFactory.CreateText(row, "Arrow", "->", UiTheme.FontBody, UiTheme.TextMuted,
                    TextAlignmentOptions.Center, FontStyles.Bold);
                UiFactory.Place(arrowText.rectTransform, midLeft, new Vector2(0.5f, 0.5f), new Vector2(236f, 0f), new Vector2(60f, 40f));
            }

            // Color chip + label.
            Image chip = UiFactory.CreateImage(row, "Chip", UiTheme.CircleSprite, color, false, true);
            UiFactory.Place(chip.rectTransform, midLeft, new Vector2(0.5f, 0.5f), new Vector2(286f, 0f), new Vector2(28f, 28f));

            TextMeshProUGUI label = UiFactory.CreateText(row, "Label", LandingColors.Label(kind), 32f,
                Color.Lerp(color, Color.white, 0.18f), TextAlignmentOptions.Left, FontStyles.Bold);
            label.overflowMode = TextOverflowModes.Ellipsis;
            UiFactory.Place(label.rectTransform, midLeft, midLeft, new Vector2(314f, 0f), new Vector2(LeftWidth - 314f, 50f));
        }

        private static void AddAircraftIcon(Transform parent, AircraftType type, float maxBox)
        {
            Sprite body = type.bodySprite;
            float rel = body != null ? Mathf.Clamp(body.rect.width / 208f, 0.62f, 1f) : 0.5f;
            float box = maxBox * rel;

            Image img = UiFactory.CreateImage(parent, "Aircraft_" + type.id, body != null ? body : UiTheme.CircleSprite,
                type.Color, false, true);
            LayoutElement le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = box;
            le.preferredHeight = box;

            if (type.rotorSprite != null && body != null)
            {
                float rotorBox = box * (type.rotorSprite.rect.width / Mathf.Max(1f, body.rect.width));
                Image rotor = UiFactory.CreateImage(img.transform, "Rotor", type.rotorSprite,
                    UiTheme.WithAlpha(Color.Lerp(type.Color, Color.white, 0.45f), 0.95f), false, true);
                UiFactory.Place(rotor.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(rotorBox, rotorBox));
                rotor.rectTransform.localEulerAngles = new Vector3(0f, 0f, 18f);
            }
        }

        // ------------------------------------------------------------------ tips

        private static void BuildTips(RectTransform panel)
        {
            TextMeshProUGUI header = UiFactory.CreateCaption(panel, "TipsHeader", "TIPS", UiTheme.TextMuted,
                TextAlignmentOptions.Left);
            UiFactory.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(RightX, -ColumnsTop),
                new Vector2(RightWidth, 30f));

            float top = ColumnsTop + 46f;
            BuildTip(panel, top, UiTheme.ChevronSprite, UiTheme.AccentColor, ">",
                "Runways and water lanes are entered from the arrow end. Helipads accept any direction.");
            top += RowHeight;
            BuildTip(panel, top, UiTheme.RingSprite, UiTheme.DangerColor, "!",
                "Keep aircraft apart. A red ring means they are too close — a collision ends the game.");
            top += RowHeight;
            BuildTip(panel, top, UiTheme.IconFastForward, UiTheme.TextPrimary, ">>",
                "Fast-forward speeds up time while the sky is quiet.");
            top += RowHeight;
            BuildTip(panel, top, UiTheme.IconPause, UiTheme.TextPrimary, "II",
                "Pause any time with the pause button or Esc.");
        }

        private static void BuildTip(RectTransform panel, float top, Sprite icon, Color iconColor, string fallback, string text)
        {
            RectTransform row = UiFactory.CreateRect("Tip", panel);
            UiFactory.Place(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(RightX, -top),
                new Vector2(RightWidth, RowHeight - 8f));
            Vector2 midLeft = new Vector2(0f, 0.5f);

            if (icon != null)
            {
                Image img = UiFactory.CreateImage(row, "Icon", icon, iconColor, false, true);
                UiFactory.Place(img.rectTransform, midLeft, new Vector2(0.5f, 0.5f), new Vector2(32f, 0f), new Vector2(56f, 56f));
            }
            else
            {
                TextMeshProUGUI glyph = UiFactory.CreateText(row, "Icon", fallback, 34f, iconColor, TextAlignmentOptions.Center,
                    FontStyles.Bold);
                UiFactory.Place(glyph.rectTransform, midLeft, new Vector2(0.5f, 0.5f), new Vector2(32f, 0f), new Vector2(64f, 56f));
            }

            TextMeshProUGUI label = UiFactory.CreateText(row, "Text", text, UiTheme.FontSmall, UiTheme.TextPrimary,
                TextAlignmentOptions.Left);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            UiFactory.Place(label.rectTransform, midLeft, midLeft, new Vector2(88f, 0f), new Vector2(RightWidth - 88f, RowHeight - 8f));
        }
    }
}
