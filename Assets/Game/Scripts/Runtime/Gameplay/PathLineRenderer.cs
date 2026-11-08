using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClearToLand
{
    /// <summary>
    /// Draws an aircraft's remaining flight path (aircraft position followed by the path points) with a
    /// LineRenderer, plus a faint dark under-stroke so the line reads on light and dark maps alike.
    /// Reuses one Vector3 buffer; no per-frame allocations.
    /// </summary>
    public sealed class PathLineRenderer : MonoBehaviour
    {
        private const float LineWidth = 0.06f;
        private const float UnderStrokeWidth = 0.11f;
        private const int InitialCapacity = 64;

        private static readonly Color NormalColor = new Color(1f, 1f, 1f, 0.75f);
        private static readonly Color UnderStrokeColor = new Color(0f, 0f, 0f, 0.22f);
        private static Material s_fallbackMaterial;

        private Aircraft _aircraft;
        private LineRenderer _line;
        private LineRenderer _under;
        private Vector3[] _buffer = new Vector3[InitialCapacity];
        private int _colorMode = -1;

        /// <summary>Binds to <paramref name="aircraft"/>. A null material falls back to a shared Sprites/Default material.</summary>
        public void Init(Aircraft aircraft, Material material)
        {
            _aircraft = aircraft;
            Material mat = material != null ? material : GetFallbackMaterial();

            if (_line == null) _line = GetComponent<LineRenderer>();
            if (_line == null) _line = gameObject.AddComponent<LineRenderer>();
            Configure(_line, mat, LineWidth, SortOrder.PathLine);

            if (_under == null)
            {
                GameObject underGo = new GameObject("UnderStroke");
                underGo.transform.SetParent(transform, false);
                _under = underGo.AddComponent<LineRenderer>();
            }
            Configure(_under, mat, UnderStrokeWidth, SortOrder.PathLine - 1);
            _under.startColor = UnderStrokeColor;
            _under.endColor = UnderStrokeColor;

            _colorMode = -1;
        }

        private static void Configure(LineRenderer lr, Material mat, float width, int sortingOrder)
        {
            lr.useWorldSpace = true;
            lr.positionCount = 0;
            lr.widthMultiplier = 1f;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 2;
            lr.alignment = LineAlignment.View;
            lr.textureMode = LineTextureMode.Stretch;
            lr.sortingOrder = sortingOrder;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.lightProbeUsage = LightProbeUsage.Off;
            lr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            lr.allowOcclusionWhenDynamic = false;
            if (mat != null) lr.sharedMaterial = mat;
        }

        private static Material GetFallbackMaterial()
        {
            if (s_fallbackMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader != null)
                {
                    s_fallbackMaterial = new Material(shader);
                    s_fallbackMaterial.name = "PathLine (runtime)";
                }
            }
            return s_fallbackMaterial;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_fallbackMaterial = null;
        }

        private void LateUpdate()
        {
            if (_line == null) return;

            Aircraft a = _aircraft;
            FlightPath path = a != null ? a.Path : null;
            if (a == null || path == null || path.IsEmpty || !a.IsAirborne)
            {
                Hide();
                return;
            }

            IReadOnlyList<Vector2> points = path.Points;
            int pointCount = points.Count;
            int count = pointCount + 1;
            EnsureCapacity(count);

            Vector2 p0 = a.Position;
            _buffer[0] = new Vector3(p0.x, p0.y, 0f);
            for (int i = 0; i < pointCount; i++)
            {
                Vector2 q = points[i];
                _buffer[i + 1] = new Vector3(q.x, q.y, 0f);
            }

            _line.positionCount = count;
            _line.SetPositions(_buffer);
            if (_under != null)
            {
                _under.positionCount = count;
                _under.SetPositions(_buffer);
            }

            int mode = path.IsLanding ? 1 : 0;
            if (mode != _colorMode)
            {
                _colorMode = mode;
                Color c;
                if (mode == 1)
                {
                    Color landing = a.Type != null ? a.Type.Color : Color.white;
                    c = GameplayUtil.WithAlpha(Color.Lerp(landing, Color.white, 0.3f), 0.95f);
                }
                else
                {
                    c = NormalColor;
                }
                _line.startColor = c;
                _line.endColor = c;
            }
        }

        private void Hide()
        {
            if (_line.positionCount != 0) _line.positionCount = 0;
            if (_under != null && _under.positionCount != 0) _under.positionCount = 0;
        }

        private void EnsureCapacity(int count)
        {
            if (_buffer.Length >= count) return;
            int size = _buffer.Length;
            while (size < count) size *= 2;
            _buffer = new Vector3[size];
        }
    }
}
