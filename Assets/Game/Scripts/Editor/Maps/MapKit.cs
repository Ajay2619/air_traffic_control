using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClearToLand.EditorTools
{
    /// <summary>Roof styles for <see cref="MapKit.Building"/>.</summary>
    internal enum MapRoof
    {
        Flat,
        Gable,
        Arched,
    }

    /// <summary>A tree crown to paint: world position, world radius and base color.</summary>
    internal struct MapTree
    {
        public Vector2 Pos;
        public float Radius;
        public Color Color;

        public MapTree(Vector2 pos, float radius, Color color)
        {
            Pos = pos;
            Radius = radius;
            Color = color;
        }
    }

    /// <summary>Rounded, oriented rectangle in world units (airfield zones, keep-out areas). Circles are a special case.</summary>
    internal struct MapZone
    {
        public Vector2 Center;
        public Vector2 Axis;
        public float HalfLength;
        public float HalfWidth;
        public float Radius;

        public MapZone(Vector2 center, float angleDeg, float halfLength, float halfWidth, float radius)
        {
            Center = center;
            Axis = MapKit.Dir(angleDeg);
            HalfLength = halfLength;
            HalfWidth = halfWidth;
            Radius = radius;
        }

        /// <summary>Signed distance in world units (negative inside).</summary>
        public float Sdf(float x, float y)
        {
            float rx = x - Center.x, ry = y - Center.y;
            float u = rx * Axis.x + ry * Axis.y;
            float v = -rx * Axis.y + ry * Axis.x;
            return MapKit.BoxSdf(u, v, HalfLength, HalfWidth, Radius);
        }

        public float Sdf(Vector2 p) => Sdf(p.x, p.y);

        /// <summary>Zone covering a landing spot grown by <paramref name="margin"/> (plus <paramref name="extraBehind"/> before a threshold).</summary>
        public static MapZone AroundSpot(LandingSpotDef s, float margin, float extraBehind)
        {
            if (!LandingColors.IsDirectional(s.kind))
            {
                float r = s.width * 0.5f + margin;
                return new MapZone(s.position, 0f, r, r, r);
            }
            Vector2 center = s.position + s.Direction * ((s.length - extraBehind) * 0.5f);
            return new MapZone(center, s.angleDeg, (s.length + extraBehind) * 0.5f + margin, s.width * 0.5f + margin, margin);
        }

        public static MapZone FromRect(Rect r, float margin)
            => new MapZone(r.center, 0f, r.width * 0.5f + margin, r.height * 0.5f + margin, margin);

        public static MapZone Circle(Vector2 center, float radius) => new MapZone(center, 0f, radius, radius, radius);

        /// <summary>Capsule-like zone around segment a-b.</summary>
        public static MapZone Segment(Vector2 a, Vector2 b, float halfWidth)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            float ang = len > 1e-5f ? Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg : 0f;
            return new MapZone((a + b) * 0.5f, ang, len * 0.5f + halfWidth, halfWidth, halfWidth);
        }

        /// <summary>Union (minimum) of the zone distances; +infinity for an empty list.</summary>
        public static float Min(MapZone[] zones, float x, float y)
        {
            float m = float.MaxValue;
            for (int i = 0; i < zones.Length; i++)
            {
                float d = zones[i].Sdf(x, y);
                if (d < m) m = d;
            }
            return m;
        }
    }

    /// <summary>
    /// Fractal noise precomputed on a coarse pixel grid and bilinearly interpolated, so terrain loops can sample
    /// several smooth noise layers per pixel cheaply. Values follow Unity's Mathf.PerlinNoise normalization:
    /// roughly [0, 1], centered near 0.47 (thresholds in the painters are tuned for that distribution).
    /// </summary>
    internal sealed class MapNoiseField
    {
        private readonly float[] _v;
        private readonly int _gw;
        private readonly int _gh;
        private readonly float _invStep;

        /// <param name="scalePx">Feature size in pixels (larger = smoother).</param>
        /// <param name="step">Grid spacing in pixels (keep well below <paramref name="scalePx"/>).</param>
        public MapNoiseField(int width, int height, float scalePx, int octaves, int seed, int step = 4)
        {
            step = Mathf.Max(1, step);
            _invStep = 1f / step;
            _gw = width / step + 3;
            _gh = height / step + 3;
            _v = new float[_gw * _gh];
            float ox = MapKit.SeedOffset(seed, 101), oy = MapKit.SeedOffset(seed, 202);
            float k = step / Mathf.Max(1f, scalePx);
            for (int gy = 0; gy < _gh; gy++)
            {
                for (int gx = 0; gx < _gw; gx++)
                    _v[gy * _gw + gx] = PixelCanvas.Fbm(gx * k + ox, gy * k + oy, octaves);
            }
        }

        /// <summary>Bilinear sample at pixel coordinates (clamped to the grid).</summary>
        public float Sample(float px, float py)
        {
            float gx = px * _invStep, gy = py * _invStep;
            if (gx < 0f) gx = 0f;
            else if (gx > _gw - 2.001f) gx = _gw - 2.001f;
            if (gy < 0f) gy = 0f;
            else if (gy > _gh - 2.001f) gy = _gh - 2.001f;
            int ix = (int)gx, iy = (int)gy;
            float fx = gx - ix, fy = gy - iy;
            int i = iy * _gw + ix;
            float a = _v[i] + (_v[i + 1] - _v[i]) * fx;
            float b = _v[i + _gw] + (_v[i + _gw + 1] - _v[i + _gw]) * fx;
            return a + (b - a) * fy;
        }
    }

    /// <summary>
    /// Shared helpers for painting map backgrounds. World/pixel mapping follows the contract:
    /// px = (world + (12.8, 7.2)) * 80. The sun is at the top-left, so shadows fall toward +x / -y.
    /// </summary>
    internal static class MapKit
    {
        public const float Ppu = GameConstants.BackgroundPixelsPerUnit;
        public const int Width = GameConstants.BackgroundPixelWidth;
        public const int Height = GameConstants.BackgroundPixelHeight;
        public const float HalfW = GameConstants.BackgroundWidth * 0.5f;
        public const float HalfH = GameConstants.BackgroundHeight * 0.5f;

        /// <summary>Unit direction shadows fall in (away from a top-left sun).</summary>
        public static readonly Vector2 ShadowDir = new Vector2(0.6f, -0.8f);

        /// <summary>Tint used for all cast shadows (cool, dark).</summary>
        public static readonly Color ShadowTint = new Color(0.05f, 0.08f, 0.12f, 1f);

        /// <summary>Sea foam / wave crest color.</summary>
        public static readonly Color Foam = new Color(0.95f, 0.98f, 1f, 1f);

        // Normalized 3D sun vector for spherical shading (x right, y up, z toward the viewer).
        private const float L3x = -0.45f, L3y = 0.6f, L3z = 0.66f;

        // ------------------------------------------------------------------ coordinates

        public static Vector2 Px(Vector2 world) => new Vector2((world.x + HalfW) * Ppu, (world.y + HalfH) * Ppu);
        public static Vector2 Px(float wx, float wy) => new Vector2((wx + HalfW) * Ppu, (wy + HalfH) * Ppu);
        public static float Len(float world) => world * Ppu;
        public static float WorldX(int px) => (px + 0.5f) / Ppu - HalfW;
        public static float WorldY(int py) => (py + 0.5f) / Ppu - HalfH;

        public static Vector2 Dir(float angleDeg)
        {
            float r = angleDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        public static Vector2 Perp(Vector2 d) => new Vector2(-d.y, d.x);

        /// <summary>Point (x, y) of a local frame at <paramref name="origin"/> rotated by <paramref name="angleDeg"/>.</summary>
        public static Vector2 Local(Vector2 origin, float angleDeg, float x, float y)
            => origin + Sdf.Rotate(new Vector2(x, y), angleDeg);

        /// <summary>Pixel offset of the shadow cast by something <paramref name="heightWorld"/> units tall.</summary>
        public static Vector2 ShadowOffset(float heightWorld) => ShadowDir * Len(heightWorld);

        // ------------------------------------------------------------------ color / math

        public static Color Rgb(int hex)
            => new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f, 1f);

        /// <summary>Multiplies RGB by <paramref name="k"/> (alpha kept, no clamping).</summary>
        public static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        /// <summary>Linear blend with t clamped to [0, 1] (all four channels).</summary>
        public static Color Mix(Color a, Color b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        }

        public static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        public static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Rounded box SDF for a point (u, v) relative to the box center.</summary>
        public static float BoxSdf(float u, float v, float hu, float hv, float r)
        {
            r = Mathf.Clamp(r, 0f, Mathf.Min(hu, hv));
            float dx = Mathf.Abs(u) - (hu - r);
            float dy = Mathf.Abs(v) - (hv - r);
            float ox = dx > 0f ? dx : 0f, oy = dy > 0f ? dy : 0f;
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f) - r;
        }

        // ------------------------------------------------------------------ hashing / randomness

        public static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u + 0x7F4A7C15u;
                h ^= (uint)x * 0x85EBCA6Bu;
                h = (h << 13) | (h >> 19);
                h ^= (uint)y * 0xC2B2AE35u;
                h = (h << 17) | (h >> 15);
                h *= 0x27D4EB2Fu;
                h ^= h >> 15;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>Deterministic value in [0, 1).</summary>
        public static float Hash01(int x, int y, int seed) => (Hash(x, y, seed) & 0xFFFFFFu) / 16777216f;

        /// <summary>FNV-1a hash of a string (stable across runs and platforms, unlike string.GetHashCode).</summary>
        public static int StableSeed(string s)
        {
            unchecked
            {
                uint h = 2166136261u;
                if (s != null)
                {
                    foreach (char ch in s)
                    {
                        h ^= ch;
                        h *= 16777619u;
                    }
                }
                return (int)(h & 0x7FFFFFFFu);
            }
        }

        /// <summary>Perlin-space offset derived from a seed (keeps coordinates small for float precision).</summary>
        public static float SeedOffset(int seed, int salt) => Hash01(seed, salt, 911) * 157f + 11f;

        public static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        // ------------------------------------------------------------------ spots

        /// <summary>First spot of <paramref name="kind"/> in the map, or null.</summary>
        public static LandingSpotDef FindSpot(MapDefinition def, LandingKind kind)
        {
            if (def == null || def.landingSpots == null) return null;
            foreach (LandingSpotDef s in def.landingSpots)
            {
                if (s != null && s.kind == kind) return s;
            }
            return null;
        }

        /// <summary>Closest point to <paramref name="p"/> on a directional spot's centerline (threshold to far end).</summary>
        public static Vector2 ClosestOnCenterline(LandingSpotDef s, Vector2 p)
        {
            Vector2 d = s.Direction;
            float t = Mathf.Clamp(Vector2.Dot(p - s.position, d), 0f, s.length);
            return s.position + d * t;
        }

        // ------------------------------------------------------------------ geometry helpers

        public static bool Clip(PixelCanvas c, Rect r, out int x0, out int y0, out int x1, out int y1)
        {
            x0 = Mathf.Max(0, Mathf.FloorToInt(r.xMin));
            y0 = Mathf.Max(0, Mathf.FloorToInt(r.yMin));
            x1 = Mathf.Min(c.Width - 1, Mathf.CeilToInt(r.xMax));
            y1 = Mathf.Min(c.Height - 1, Mathf.CeilToInt(r.yMax));
            return x1 >= x0 && y1 >= y0;
        }

        /// <summary>Catmull-Rom spline through <paramref name="pts"/> (end points included).</summary>
        public static List<Vector2> Spline(IList<Vector2> pts, int samplesPerSegment)
        {
            List<Vector2> o = new List<Vector2>();
            if (pts == null || pts.Count == 0) return o;
            if (pts.Count == 1)
            {
                o.Add(pts[0]);
                return o;
            }
            samplesPerSegment = Mathf.Max(1, samplesPerSegment);
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector2 p0 = pts[Mathf.Max(i - 1, 0)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(i + 2, pts.Count - 1)];
                for (int s = 0; s < samplesPerSegment; s++)
                {
                    float t = s / (float)samplesPerSegment;
                    float t2 = t * t, t3 = t2 * t;
                    o.Add(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3));
                }
            }
            o.Add(pts[pts.Count - 1]);
            return o;
        }

        /// <summary>Converts world points to pixel points.</summary>
        public static List<Vector2> ToPx(IList<Vector2> world)
        {
            List<Vector2> o = new List<Vector2>(world.Count);
            for (int i = 0; i < world.Count; i++) o.Add(Px(world[i]));
            return o;
        }

        public static float DistToPolyline(Vector2 p, IList<Vector2> pts)
        {
            float best = float.MaxValue;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                float d = Sdf.Segment(p, pts[i], pts[i + 1], 0f);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>Polyline shifted sideways by <paramref name="dist"/> (positive = left of the travel direction).</summary>
        public static List<Vector2> OffsetPolyline(IList<Vector2> pts, float dist)
        {
            List<Vector2> o = new List<Vector2>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 t = pts[Mathf.Min(pts.Count - 1, i + 1)] - pts[Mathf.Max(0, i - 1)];
                if (t.sqrMagnitude < 1e-10f) t = Vector2.right;
                t.Normalize();
                o.Add(pts[i] + new Vector2(-t.y, t.x) * dist);
            }
            return o;
        }

        /// <summary>Transforms local points (x along the heading) into pixel space.</summary>
        public static Vector2[] Transform(IList<Vector2> local, Vector2 originPx, float angleDeg, float scaleX, float scaleY)
        {
            Vector2[] o = new Vector2[local.Count];
            for (int i = 0; i < local.Count; i++)
                o[i] = originPx + Sdf.Rotate(new Vector2(local[i].x * scaleX, local[i].y * scaleY), angleDeg);
            return o;
        }

        // ------------------------------------------------------------------ painting primitives

        /// <summary>
        /// Soft cast shadow of the shape <paramref name="sdf"/> (pixel space), displaced by <paramref name="offset"/> px
        /// with a penumbra of about 2 x <paramref name="blur"/> px. With <paramref name="extrude"/> the shape is swept
        /// along the offset, so tall objects (towers, buildings, cliffs) get a shadow attached to their base.
        /// </summary>
        public static void SoftShadow(PixelCanvas c, Func<Vector2, float> sdf, Rect shapeBounds, Vector2 offset, float blur,
            float alpha, bool extrude = false)
        {
            blur = Mathf.Max(0.75f, blur);
            float minX = shapeBounds.xMin + offset.x, minY = shapeBounds.yMin + offset.y;
            float maxX = shapeBounds.xMax + offset.x, maxY = shapeBounds.yMax + offset.y;
            if (extrude)
            {
                minX = Mathf.Min(minX, shapeBounds.xMin);
                minY = Mathf.Min(minY, shapeBounds.yMin);
                maxX = Mathf.Max(maxX, shapeBounds.xMax);
                maxY = Mathf.Max(maxY, shapeBounds.yMax);
            }
            Rect b = Rect.MinMaxRect(minX - blur - 1f, minY - blur - 1f, maxX + blur + 1f, maxY + blur + 1f);
            if (!Clip(c, b, out int x0, out int y0, out int x1, out int y1)) return;
            int steps = extrude ? Mathf.Clamp(Mathf.CeilToInt(offset.magnitude / 3f), 1, 24) : 0;
            Color col = Alpha(ShadowTint, alpha);
            float inv = 1f / (2f * blur);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float d = sdf(new Vector2(px - offset.x, py - offset.y));
                    for (int k = 0; k < steps && d > -blur; k++)
                    {
                        float f = k / (float)steps;
                        d = Mathf.Min(d, sdf(new Vector2(px - offset.x * f, py - offset.y * f)));
                    }
                    float t = Mathf.Clamp01((blur - d) * inv);
                    if (t <= 0f) continue;
                    c.BlendPixel(x, y, col, t * t * (3f - 2f * t));
                }
            }
        }

        /// <summary>
        /// Shaded round blob (tree crown, bush, boulder, buoy): optionally lumpy outline, spherical lighting from the
        /// top-left and a fine mottled texture. All sizes in pixels.
        /// </summary>
        public static void Blob(PixelCanvas c, Vector2 center, float radius, Color color, int seed,
            float lumpiness, float texture, float lighting = 1f)
        {
            if (radius <= 0.3f) return;
            float reach = radius * (1f + Mathf.Abs(lumpiness)) + 2f;
            if (!Clip(c, new Rect(center.x - reach, center.y - reach, reach * 2f, reach * 2f),
                    out int x0, out int y0, out int x1, out int y1)) return;
            float ph1 = Hash01(seed, 3, 11) * 6.2832f, ph2 = Hash01(seed, 5, 13) * 6.2832f;
            int lobes = 4 + (int)(Hash01(seed, 7, 17) * 4f);
            float inv = 1f / radius;
            float texScale = Mathf.Max(2.2f, radius * 0.32f);
            float tox = SeedOffset(seed, 21), toy = SeedOffset(seed, 22);
            for (int y = y0; y <= y1; y++)
            {
                float qy = y + 0.5f - center.y;
                for (int x = x0; x <= x1; x++)
                {
                    float qx = x + 0.5f - center.x;
                    float dist = Mathf.Sqrt(qx * qx + qy * qy);
                    float rr = radius;
                    if (lumpiness > 0f)
                    {
                        float ang = Mathf.Atan2(qy, qx);
                        rr *= 1f + lumpiness * (0.6f * Mathf.Sin(ang * lobes + ph1) + 0.4f * Mathf.Sin(ang * (lobes * 2 + 1) + ph2));
                    }
                    float d = dist - rr;
                    float cov = Mathf.Clamp01(0.5f - d);
                    if (cov <= 0f) continue;
                    float nx = qx * inv, ny = qy * inv;
                    float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx - ny * ny));
                    float diffuse = Mathf.Clamp01(nx * L3x + ny * L3y + nz * L3z);
                    float k = Mathf.Lerp(1f, 0.58f + 0.62f * diffuse, lighting);
                    if (texture > 0f)
                    {
                        k *= 1f + texture * ((Mathf.PerlinNoise(x / texScale + tox, y / texScale + toy) - 0.5f) * 0.9f
                                             + (Hash01(x, y, seed) - 0.5f) * 0.3f);
                    }
                    if (d > -1.5f) k *= 0.88f;
                    c.BlendPixel(x, y, Shade(color, k), cov);
                }
            }
        }

        /// <summary>Paints trees: all soft shadows first, then crowns from top to bottom of the map.</summary>
        public static void DrawTrees(PixelCanvas c, List<MapTree> trees, int seed)
        {
            trees.Sort((a, b) => b.Pos.y.CompareTo(a.Pos.y));
            for (int i = 0; i < trees.Count; i++)
            {
                Vector2 p = Px(trees[i].Pos);
                float r = Len(trees[i].Radius);
                SoftShadow(c, q => Sdf.Circle(q, p, r), PixelCanvas.Square(p, r), ShadowDir * (r * 0.8f), r * 0.3f, 0.36f);
            }
            for (int i = 0; i < trees.Count; i++)
                Blob(c, Px(trees[i].Pos), Len(trees[i].Radius), trees[i].Color, seed + i * 31, 0.07f, 0.5f);
        }

        /// <summary>
        /// Building seen from above: soft cast shadow, then a shaded roof (flat with parapet, gable with lit/unlit
        /// slopes and ridge, or arched hangar vault). World units for position/size/height.
        /// </summary>
        public static void Building(PixelCanvas c, Vector2 centerW, Vector2 sizeW, float angleDeg, Color roof,
            MapRoof kind, float heightW, int seed)
        {
            Vector2 cp = Px(centerW);
            Vector2 half = new Vector2(Len(sizeW.x), Len(sizeW.y)) * 0.5f;
            float rad = Mathf.Min(1.2f, Mathf.Min(half.x, half.y) * 0.25f);
            Rect bounds = PixelCanvas.Square(cp, half.magnitude);
            SoftShadow(c, p => Sdf.Box(p, cp, half, rad, angleDeg), bounds, ShadowOffset(heightW),
                Mathf.Max(1.2f, Len(heightW) * 0.18f), 0.42f, true);
            if (!Clip(c, bounds, out int x0, out int y0, out int x1, out int y1)) return;

            float a = -angleDeg * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            Vector2 sunLocal = Sdf.Rotate(-ShadowDir, -angleDeg);
            bool alongX = half.x >= half.y;
            float halfAcross = alongX ? half.y : half.x;
            float sunAcross = alongX ? sunLocal.y : sunLocal.x;
            float sunSign = sunAcross >= 0f ? 1f : -1f;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float rx = x + 0.5f - cp.x, ry = y + 0.5f - cp.y;
                    float qx = rx * ca - ry * sa, qy = rx * sa + ry * ca;
                    float d = BoxSdf(qx, qy, half.x, half.y, rad);
                    float cov = Mathf.Clamp01(0.5f - d);
                    if (cov <= 0f) continue;
                    float across = alongX ? qy : qx;
                    float along = alongX ? qx : qy;
                    float k;
                    switch (kind)
                    {
                        case MapRoof.Gable:
                        {
                            float t = Mathf.Clamp01(0.5f + across * sunSign * 0.9f);
                            k = Mathf.Lerp(0.8f, 1.07f, t);
                            if (Mathf.Abs(across) < 0.9f) k *= 1.06f;
                            k *= 1f + 0.035f * Mathf.Sin(across * 2.2f);
                            break;
                        }
                        case MapRoof.Arched:
                        {
                            float s = Mathf.Clamp(across / Mathf.Max(1f, halfAcross), -1f, 1f);
                            float nz = Mathf.Sqrt(1f - s * s);
                            k = 0.68f + 0.42f * Mathf.Clamp01(s * sunAcross * 0.9f + nz * 0.75f);
                            if (Mathf.Repeat(along, 6f) < 1f) k *= 0.9f;
                            break;
                        }
                        default:
                            k = d > -3f ? 1.07f : 1f;
                            break;
                    }
                    if (d > -1.2f) k *= 0.72f;
                    k *= 1f + (Hash01(x, y, seed) - 0.5f) * 0.05f;
                    c.BlendPixel(x, y, Shade(roof, k), cov);
                }
            }
        }

        /// <summary>
        /// Paved slab (apron, platform, plaza): rounded rect with subtle texture, panel joints and a faint
        /// ambient-occlusion rim. World units.
        /// </summary>
        public static void Slab(PixelCanvas c, Vector2 centerW, Vector2 sizeW, float angleDeg, float cornerW,
            Color color, float jointW, int seed, float aoAlpha = 0.18f)
        {
            Vector2 cp = Px(centerW);
            float hx = Len(sizeW.x) * 0.5f, hy = Len(sizeW.y) * 0.5f;
            float rad = Len(cornerW);
            float joint = Len(jointW);
            Rect b = PixelCanvas.Square(cp, Mathf.Sqrt(hx * hx + hy * hy) + 6f);
            if (!Clip(c, b, out int x0, out int y0, out int x1, out int y1)) return;
            float a = -angleDeg * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            float ox = SeedOffset(seed, 31), oy = SeedOffset(seed, 32);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float rx = x + 0.5f - cp.x, ry = y + 0.5f - cp.y;
                    float qx = rx * ca - ry * sa, qy = rx * sa + ry * ca;
                    float d = BoxSdf(qx, qy, hx, hy, rad);
                    if (d > 5.5f) continue;
                    if (d > -0.5f && aoAlpha > 0f)
                    {
                        float ao = 1f - Smooth(0f, 5f, d);
                        c.BlendPixel(x, y, new Color(0f, 0f, 0f, aoAlpha), ao * Mathf.Clamp01(d + 0.5f));
                    }
                    float cov = Mathf.Clamp01(0.5f - d);
                    if (cov <= 0f) continue;
                    float k = 1f + (Hash01(x, y, seed) - 0.5f) * 0.05f + (Mathf.PerlinNoise(qx / 30f + ox, qy / 30f + oy) - 0.5f) * 0.09f;
                    if (joint > 2f)
                    {
                        float jx = Mathf.Repeat(qx + hx, joint), jy = Mathf.Repeat(qy + hy, joint);
                        if (jx < 1f || jy < 1f) k *= 0.93f;
                    }
                    if (d > -1.5f) k *= 0.9f;
                    c.BlendPixel(x, y, Shade(color, k), cov);
                }
            }
        }

        /// <summary>
        /// Natural water body (pond / oasis): wobbly ellipse with a muddy bank, depth gradient and soft
        /// horizontal reflections. World units.
        /// </summary>
        public static void Pond(PixelCanvas c, Vector2 centerW, Vector2 radiiW, float angleDeg,
            Color shallow, Color deep, Color bank, float bankW, int seed)
        {
            Vector2 cp = Px(centerW);
            float rx = Len(radiiW.x), ry = Len(radiiW.y), bw = Len(bankW);
            float rmin = Mathf.Min(rx, ry);
            float ph1 = Hash01(seed, 1, 5) * 6.2832f, ph2 = Hash01(seed, 2, 5) * 6.2832f;
            Rect b = PixelCanvas.Square(cp, Mathf.Max(rx, ry) * 1.2f + bw + 4f);
            if (!Clip(c, b, out int x0, out int y0, out int x1, out int y1)) return;
            float a = -angleDeg * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            float ox = SeedOffset(seed, 41), oy = SeedOffset(seed, 42);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f - cp.x, py = y + 0.5f - cp.y;
                    float qx = px * ca - py * sa, qy = px * sa + py * ca;
                    float ex = qx / rx, ey = qy / ry;
                    float e = Mathf.Sqrt(ex * ex + ey * ey);
                    float ang = Mathf.Atan2(ey, ex);
                    float wob = 1f + 0.08f * Mathf.Sin(3f * ang + ph1) + 0.045f * Mathf.Sin(5f * ang + ph2);
                    float d = (e - wob) * rmin;
                    if (d > bw + 1f) continue;
                    float bankA = 1f - Smooth(bw * 0.35f, bw + 0.5f, d);
                    if (bankA > 0f) c.BlendPixel(x, y, bank, bankA * 0.9f);
                    float cov = Mathf.Clamp01(0.5f - d);
                    if (cov <= 0f) continue;
                    float depth = Smooth(0f, rmin * 0.65f, -d);
                    Color col = Mix(shallow, deep, depth);
                    float refl = Smooth(0.58f, 0.85f, Mathf.PerlinNoise(x / 34f + ox, y / 7f + oy));
                    col = Mix(col, new Color(0.85f, 0.95f, 1f, 1f), refl * 0.16f);
                    // the bank shades the upper-left rim of the water
                    float rim = 1f - Smooth(0f, 6f, -d);
                    float facing = Mathf.Clamp01(-(qx * ShadowDir.x + qy * ShadowDir.y) / Mathf.Max(1f, Mathf.Sqrt(qx * qx + qy * qy)));
                    col = Shade(col, 1f - 0.22f * rim * facing);
                    c.BlendPixel(x, y, col, cov);
                }
            }
        }

        /// <summary>Palm tree from above: radiating fronds with a soft shadow. World units.</summary>
        public static void Palm(PixelCanvas c, Vector2 centerW, float radiusW, int seed)
        {
            Vector2 cp = Px(centerW);
            float r = Len(radiusW);
            int fronds = 7 + (int)(Hash01(seed, 1, 3) * 3f);
            float rot = Hash01(seed, 2, 3) * 360f;
            Vector2[] ctr = new Vector2[fronds];
            float[] ang = new float[fronds];
            Vector2 radii = new Vector2(r * 0.52f, r * 0.17f);
            for (int i = 0; i < fronds; i++)
            {
                ang[i] = rot + i * 360f / fronds + (Hash01(seed, i, 9) - 0.5f) * 20f;
                ctr[i] = cp + Dir(ang[i]) * (r * 0.5f);
            }
            Func<Vector2, float> sdf = p =>
            {
                float m = float.MaxValue;
                for (int k = 0; k < ctr.Length; k++) m = Mathf.Min(m, Sdf.Ellipse(p, ctr[k], radii, ang[k]));
                return m;
            };
            SoftShadow(c, sdf, PixelCanvas.Square(cp, r), ShadowDir * (r * 1.15f), 1.5f, 0.3f);
            float tint = 0.9f + 0.2f * Hash01(seed, 4, 3);
            Color dark = Shade(new Color(0.2f, 0.4f, 0.17f, 1f), tint);
            Color light = Shade(new Color(0.37f, 0.58f, 0.25f, 1f), tint);
            for (int i = 0; i < fronds; i++) c.FillEllipse(ctr[i], radii, ang[i], dark);
            for (int i = 0; i < fronds; i++)
                c.FillEllipse(ctr[i] + Dir(ang[i] + 90f) * 0.6f, new Vector2(radii.x * 0.88f, radii.y * 0.42f), ang[i], light);
            c.FillCircle(cp, r * 0.15f, new Color(0.47f, 0.37f, 0.22f, 1f));
        }

        /// <summary>
        /// Small boat from above (pointed bow toward <paramref name="angleDeg"/>), optionally with a sail.
        /// World units for position and size.
        /// </summary>
        public static void Boat(PixelCanvas c, Vector2 centerW, float angleDeg, float lengthW, float beamW,
            Color hull, Color deck, Color cabin, bool sail, int seed)
        {
            float L = Len(lengthW), B = Len(beamW);
            Vector2 cp = Px(centerW);
            Vector2[] local =
            {
                new Vector2(-0.5f, -0.4f), new Vector2(0.12f, -0.5f), new Vector2(0.36f, -0.38f), new Vector2(0.5f, 0f),
                new Vector2(0.36f, 0.38f), new Vector2(0.12f, 0.5f), new Vector2(-0.5f, 0.4f),
            };
            Vector2[] outer = Transform(local, cp, angleDeg, L, B);
            Vector2[] inner = Transform(local, cp, angleDeg, Mathf.Max(1f, L - 3f), Mathf.Max(1f, B - 3f));
            Vector2[] deckPts = Transform(local, cp + Sdf.Rotate(new Vector2(-0.04f * L, 0f), angleDeg), angleDeg, L * 0.7f, B * 0.6f);
            Rect b = Sdf.Bounds(outer, 2f);
            SoftShadow(c, p => Sdf.Polygon(p, outer), b, ShadowOffset(0.05f), 1.8f, 0.32f);
            c.FillSdf(p => Sdf.Polygon(p, outer), b, Shade(hull, 0.68f));
            c.FillSdf(p => Sdf.Polygon(p, inner), b, hull);
            c.FillSdf(p => Sdf.Polygon(p, deckPts), b, deck);
            Vector2 cabinC = cp + Sdf.Rotate(new Vector2(-0.08f * L, 0f), angleDeg);
            c.FillBox(cabinC, new Vector2(0.3f * L, 0.5f * B), 1.2f, angleDeg, Shade(cabin, 0.78f));
            c.FillBox(cabinC - ShadowDir * 0.8f, new Vector2(0.24f * L, 0.38f * B), 1f, angleDeg, cabin);
            if (!sail) return;
            Vector2 mast = cp + Sdf.Rotate(new Vector2(0.1f * L, 0f), angleDeg);
            float boomAngle = angleDeg + 180f - 25f + 50f * Hash01(seed, 3, 7);
            Vector2 sc = mast + Dir(boomAngle) * (0.3f * L);
            Vector2 sr = new Vector2(0.32f * L, Mathf.Max(1.6f, 0.07f * L));
            SoftShadow(c, p => Sdf.Ellipse(p, sc, sr, boomAngle), PixelCanvas.Square(sc, sr.x), ShadowOffset(0.22f), 2f, 0.22f);
            c.FillEllipse(sc, sr, boomAngle, new Color(0.88f, 0.89f, 0.9f, 1f));
            c.FillEllipse(sc - ShadowDir * 0.6f, new Vector2(sr.x * 0.9f, sr.y * 0.5f), boomAngle, new Color(1f, 1f, 1f, 1f));
            c.FillCircle(mast, 1.4f, new Color(0.3f, 0.3f, 0.32f, 1f));
        }

        /// <summary>
        /// Foam wake behind a vessel: turbulent centre trail plus two faint Kelvin arms. Pixel units;
        /// <paramref name="headingDeg"/> is the vessel's heading (the wake trails the opposite way).
        /// </summary>
        public static void Wake(PixelCanvas c, Vector2 sternPx, float headingDeg, float lengthPx, float halfWidthPx,
            float strength, int seed)
        {
            Vector2 back = -Dir(headingDeg);
            Vector2 side = Perp(back);
            const float spread = 0.354f; // tan(19.5 deg)
            float maxHalf = halfWidthPx + lengthPx * spread + 4f;
            Vector2[] corners =
            {
                sternPx + side * maxHalf, sternPx - side * maxHalf,
                sternPx + back * lengthPx + side * maxHalf, sternPx + back * lengthPx - side * maxHalf,
                sternPx - back * 4f,
            };
            if (!Clip(c, Sdf.Bounds(corners, 2f), out int x0, out int y0, out int x1, out int y1)) return;
            float ox = SeedOffset(seed, 51), oy = SeedOffset(seed, 52);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float rx = x + 0.5f - sternPx.x, ry = y + 0.5f - sternPx.y;
                    float t = rx * back.x + ry * back.y;
                    if (t < -2f || t > lengthPx) continue;
                    float s = Mathf.Abs(rx * side.x + ry * side.y);
                    float tt = Mathf.Max(t, 0f) / lengthPx;
                    float fade = (1f - tt) * (1f - tt) * Mathf.Clamp01((t + 2f) / 4f);
                    float w = halfWidthPx * (0.8f + 1.6f * tt);
                    float trail = Mathf.Clamp01(1f - s / w);
                    trail *= trail;
                    float n = Mathf.PerlinNoise(t / 15f + ox, s / 5f + oy);
                    float foam = trail * (0.35f + 0.9f * n) * fade;
                    float armPos = halfWidthPx * 0.7f + t * spread;
                    float armD = Mathf.Abs(s - armPos);
                    float armFade = Mathf.Clamp01(1f - tt * 1.8f);
                    float arm = Mathf.Clamp01(1.0f + t * 0.008f + 0.5f - armD) * armFade * armFade * 0.42f
                                * (0.4f + 0.8f * Mathf.PerlinNoise(t / 9f + oy, s / 9f + ox));
                    float aOut = Mathf.Clamp01(Mathf.Max(foam, arm)) * strength;
                    if (aOut > 0.004f) c.BlendPixel(x, y, Foam, aOut);
                }
            }
        }

        /// <summary>
        /// Stylized wave crest marks: coverage in [0, 1] of a short crescent placed (with probability
        /// <paramref name="density"/>) in the grid cell containing pixel (px, py).
        /// </summary>
        public static float WaveMark(float px, float py, float cell, float density, int seed)
        {
            int cx = Mathf.FloorToInt(px / cell), cy = Mathf.FloorToInt(py / cell);
            uint h = Hash(cx, cy, seed);
            if ((h & 0xFFFFu) / 65536f > density) return 0f;
            float jx = ((h >> 16) & 0xFFu) / 255f, jy = ((h >> 24) & 0xFFu) / 255f;
            float half = cell * (0.15f + 0.14f * Hash01(cx, cy, seed + 1));
            float mx = (cx + 0.5f) * cell + (jx - 0.5f) * (cell - half * 2f - 2f);
            float my = (cy + 0.5f) * cell + (jy - 0.5f) * (cell * 0.5f);
            float dx = px - mx, dy = py - my;
            float t = Mathf.Clamp(dx / half, -1f, 1f);
            float arc = dy + t * t * cell * 0.07f;
            float ex = Mathf.Max(Mathf.Abs(dx) - half, 0f);
            float dist = Mathf.Sqrt(ex * ex + arc * arc);
            float thick = 1.05f * (1f - 0.6f * t * t);
            return Mathf.Clamp01(thick + 0.5f - dist);
        }

        /// <summary>
        /// Final pass over the whole canvas: subtle grain (fine + medium noise), a gentle vignette outside the
        /// safe area, clamping and full opacity.
        /// </summary>
        public static void Finish(PixelCanvas c, int seed, float grain, float vignette)
        {
            MapNoiseField mid = new MapNoiseField(c.Width, c.Height, 24f, 2, seed + 7001, 2);
            float safeHalfW = GameConstants.SafeWidth * 0.5f - 1.2f, safeHalfH = GameConstants.SafeHeight * 0.5f - 1.0f;
            float spanW = HalfW - safeHalfW, spanH = HalfH - safeHalfH;
            for (int y = 0; y < c.Height; y++)
            {
                float vy = Mathf.Max(0f, Mathf.Abs(WorldY(y)) - safeHalfH) / spanH;
                for (int x = 0; x < c.Width; x++)
                {
                    float vx = Mathf.Max(0f, Mathf.Abs(WorldX(x)) - safeHalfW) / spanW;
                    float v = Mathf.Clamp01(vx * vx + vy * vy);
                    float k = (1f - vignette * v)
                              * (1f + (Hash01(x, y, seed) - 0.5f) * grain + (mid.Sample(x, y) - 0.5f) * grain * 1.6f);
                    Color col = c[x, y];
                    c[x, y] = new Color(Mathf.Clamp01(col.r * k), Mathf.Clamp01(col.g * k), Mathf.Clamp01(col.b * k), 1f);
                }
            }
        }
    }
}
