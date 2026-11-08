using TMPro;
using UnityEngine;

namespace ClearToLand
{
    /// <summary>Floating world-space score text ("+1") that pops, rises 0.6 units and fades over 0.9 s (scaled time).</summary>
    public static class ScorePopup
    {
        private const float FontSize = 5f;
        private const float OutlineWidth = 0.22f;
        private static readonly Color OutlineColor = new Color(0.05f, 0.06f, 0.1f, 0.9f);

        private static Material s_outlineMaterial;
        private static Material s_outlineSource;

        /// <summary>Spawns a popup showing <paramref name="text"/> in <paramref name="color"/> at <paramref name="position"/>.</summary>
        public static void Play(Vector2 position, string text, Color color)
        {
            GameObject go = new GameObject("ScorePopup");
            go.transform.position = new Vector3(position.x, position.y, 0f);

            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text ?? string.Empty;
            tmp.fontSize = FontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.richText = false;
            tmp.color = color;
            tmp.rectTransform.sizeDelta = new Vector2(4f, 1.5f);
            ApplyOutline(tmp);
            tmp.sortingOrder = SortOrder.ScorePopup;

            ScorePopupRunner runner = go.AddComponent<ScorePopupRunner>();
            runner.Init(tmp, position, color);
        }

        /// <summary>
        /// Shares one outline material per font material (no per-popup material instances). Uses the OUTLINE_ON variant
        /// of the TMP SDF shader, which builds keep because TMP's Resources include the outline preset material.
        /// </summary>
        private static void ApplyOutline(TextMeshPro tmp)
        {
            Material source = tmp.fontSharedMaterial;
            if (source == null) return;
            if (source == s_outlineMaterial)
                return;

            if (s_outlineMaterial == null || s_outlineSource != source)
            {
                Material m = new Material(source);
                m.name = source.name + " (ScorePopup)";
                m.EnableKeyword(ShaderUtilities.Keyword_Outline);
                if (m.HasProperty(ShaderUtilities.ID_OutlineWidth)) m.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
                if (m.HasProperty(ShaderUtilities.ID_OutlineColor)) m.SetColor(ShaderUtilities.ID_OutlineColor, OutlineColor);
                ShaderUtilities.UpdateShaderRatios(m);
                s_outlineMaterial = m;
                s_outlineSource = source;
            }
            tmp.fontSharedMaterial = s_outlineMaterial;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_outlineMaterial = null;
            s_outlineSource = null;
        }
    }
}
