using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ClearToLand.EditorTools
{
    /// <summary>
    /// The four original Clear to Land map layouts: landing spots, spawn tables and difficulty tuning.
    /// Every layout keeps each spot (whole rect) inside <see cref="SpotRegion"/>, gives every runway and water lane
    /// at least <see cref="MinApproachRoom"/> units of approach inside the safe area, and never overlaps spots
    /// or crosses another spot with an approach corridor. <see cref="Validate"/> checks all of this.
    /// </summary>
    public static class MapCatalog
    {
        /// <summary>Map ids in menu order.</summary>
        public static readonly string[] Ids = { "green_valley", "dune_sea", "harbor_bay", "carrier_group" };

        /// <summary>World rect every landing spot must fit inside (safe area minus side margins and the HUD bands).</summary>
        public static readonly Rect SpotRegion = Rect.MinMaxRect(-8.6f, -4.0f, 8.6f, 4.1f);

        /// <summary>The always-visible safe area (19.2 x 10.8 world units centered on the origin).</summary>
        public static readonly Rect SafeArea = Rect.MinMaxRect(
            -GameConstants.SafeWidth * 0.5f, -GameConstants.SafeHeight * 0.5f,
            GameConstants.SafeWidth * 0.5f, GameConstants.SafeHeight * 0.5f);

        /// <summary>Open approach length (world units) required before every runway / water-lane threshold.</summary>
        public const float MinApproachRoom = 2.5f;

        /// <summary>Long runway width (world units).</summary>
        public const float LongRunwayWidth = 0.75f;
        /// <summary>Short runway width (world units).</summary>
        public const float ShortRunwayWidth = 0.6f;
        /// <summary>Water lane width (world units).</summary>
        public const float WaterLaneWidth = 0.8f;
        /// <summary>Helipad diameter (world units).</summary>
        public const float HelipadDiameter = 1.0f;

        /// <summary>
        /// Fills <paramref name="def"/> with the layout for <paramref name="id"/>: id, display name, tagline, difficulty,
        /// camera color, landing spots, spawn table and tuning. Spawn entries whose aircraft type cannot be resolved
        /// through <paramref name="aircraftById"/> are skipped. Throws for unknown ids.
        /// </summary>
        public static void Apply(MapDefinition def, string id, Func<string, AircraftType> aircraftById)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Map id is empty.", nameof(id));
            // Reject unknown ids before touching the asset so a bad call never leaves it half-cleared.
            if (!IsKnownId(id))
                throw new ArgumentException(
                    $"[ClearToLand] Unknown map id '{id}'. Known ids: {string.Join(", ", Ids)}", nameof(id));

            def.id = id;
            if (def.landingSpots == null) def.landingSpots = new List<LandingSpotDef>();
            else def.landingSpots.Clear();
            if (def.spawnTable == null) def.spawnTable = new List<SpawnEntry>();
            else def.spawnTable.Clear();

            switch (id)
            {
                case "green_valley": GreenValley(def, aircraftById); break;
                case "dune_sea": DuneSea(def, aircraftById); break;
                case "harbor_bay": HarborBay(def, aircraftById); break;
                case "carrier_group": CarrierGroup(def, aircraftById); break;
                default:
                    throw new ArgumentException(
                        $"[ClearToLand] Unknown map id '{id}'. Known ids: {string.Join(", ", Ids)}", nameof(id));
            }
            EditorUtility.SetDirty(def);
        }

        /// <summary>True when <paramref name="id"/> is one of <see cref="Ids"/>.</summary>
        public static bool IsKnownId(string id) => Array.IndexOf(Ids, id) >= 0;

        // ------------------------------------------------------------------------------------------------
        // Layouts. Directional spots: position = threshold (approach end), angleDeg = landing direction.
        // ------------------------------------------------------------------------------------------------

        private static void GreenValley(MapDefinition def, Func<string, AircraftType> ac)
        {
            def.displayName = "Green Valley";
            def.tagline = "Quiet farmland strip \u2014 learn the ropes"; // em dash escaped so file encoding can't corrupt it
            def.difficulty = 1;
            def.cameraColor = new Color(0.32f, 0.45f, 0.23f);

            // Main runway lands east from the western fields; farm strip lands west-north-west from the east;
            // helipad sits on the hangar apron in the north-west. The middle of the map stays open.
            AddRunway(def, "Valley main runway", LandingKind.LongRunway, -5.2f, -1.9f, 8f, 5.6f);
            AddRunway(def, "Farm strip", LandingKind.ShortRunway, 6.4f, 1.6f, 165f, 3.4f);
            AddHelipad(def, "Hangar helipad", -3.4f, 2.5f);

            AddSpawn(def, ac, "prop", 3f, 0f);
            AddSpawn(def, ac, "airliner", 2f, 20f);
            AddSpawn(def, ac, "helicopter", 2f, 40f);
            SetTuning(def, 8.0f, 3.0f, 420f, 3, 10, 40f, 1.0f);
        }

        private static void DuneSea(MapDefinition def, Func<string, AircraftType> ac)
        {
            def.displayName = "Dune Sea";
            def.tagline = "Sun-baked strips and an oasis helipad";
            def.difficulty = 2;
            def.cameraColor = new Color(0.76f, 0.6f, 0.41f);

            // Long runway lands west from the eastern dunes; the short strip is set at an angle and lands
            // south-east out of the north-west; the helipad sits beside the oasis in the north-east.
            AddRunway(def, "Desert main runway", LandingKind.LongRunway, 5.6f, -2.2f, 172f, 5.8f);
            AddRunway(def, "Mesa strip", LandingKind.ShortRunway, -6.5f, 2.3f, 320f, 3.6f);
            AddHelipad(def, "Oasis helipad", 4.4f, 2.6f);

            AddSpawn(def, ac, "prop", 3f, 0f);
            AddSpawn(def, ac, "airliner", 3f, 10f);
            AddSpawn(def, ac, "helicopter", 2f, 25f);
            AddSpawn(def, ac, "fighter", 1.5f, 90f);
            SetTuning(def, 7.0f, 2.7f, 380f, 3, 11, 36f, 1.05f);
        }

        private static void HarborBay(MapDefinition def, Func<string, AircraftType> ac)
        {
            def.displayName = "Harbor Bay";
            def.tagline = "Busy coast: seaplanes join the traffic";
            def.difficulty = 3;
            def.cameraColor = new Color(0.15f, 0.39f, 0.56f);

            // Land in the north, bay in the south-east. Long runway along the western shore lands east;
            // short runway in the north-east lands west-south-west; the buoyed water lane in the bay lands
            // north-east out of the southern bay mouth; the helipad sits at the end of a pier.
            AddRunway(def, "Shore runway", LandingKind.LongRunway, -6.2f, 0.0f, 6f, 5.6f);
            AddRunway(def, "Hilltop strip", LandingKind.ShortRunway, 6.8f, 3.2f, 195f, 3.4f);
            AddWaterLane(def, "Bay water lane", 1.8f, -3.4f, 20f, 4.4f);
            AddHelipad(def, "Pier helipad", 7.4f, -0.5f);

            AddSpawn(def, ac, "prop", 3f, 0f);
            AddSpawn(def, ac, "airliner", 2.5f, 0f);
            AddSpawn(def, ac, "helicopter", 2f, 15f);
            AddSpawn(def, ac, "seaplane", 2.5f, 5f);
            SetTuning(def, 6.8f, 2.5f, 360f, 3, 12, 33f, 1.08f);
        }

        private static void CarrierGroup(MapDefinition def, Func<string, AircraftType> ac)
        {
            def.displayName = "Carrier Group";
            def.tagline = "Carrier deck, escort pad and a lonely atoll";
            def.difficulty = 4;
            def.cameraColor = new Color(0.09f, 0.3f, 0.47f);

            // Carrier angled deck lands east-north-east from the south-west; the escort's helipad is in the
            // north-west; the atoll strip lands west from the east; the lagoon lane lands north-east through
            // the reef pass in the south.
            AddRunway(def, "Carrier deck", LandingKind.LongRunway, -5.3f, -2.4f, 15f, 5.6f);
            AddHelipad(def, "Escort helipad", -4.6f, 2.4f);
            AddRunway(def, "Atoll strip", LandingKind.ShortRunway, 6.9f, 1.5f, 190f, 3.4f);
            AddWaterLane(def, "Lagoon lane", 4.4f, -3.4f, 25f, 4.2f);

            AddSpawn(def, ac, "fighter", 3f, 0f);
            AddSpawn(def, ac, "helicopter", 2.5f, 0f);
            AddSpawn(def, ac, "prop", 2f, 20f);
            AddSpawn(def, ac, "seaplane", 2f, 40f);
            SetTuning(def, 6.2f, 2.3f, 330f, 4, 12, 30f, 1.12f);
        }

        // ------------------------------------------------------------------------------------------------
        // Builders
        // ------------------------------------------------------------------------------------------------

        private static void AddRunway(MapDefinition def, string name, LandingKind kind, float x, float y, float angleDeg, float length)
        {
            def.landingSpots.Add(new LandingSpotDef
            {
                name = name,
                kind = kind,
                position = new Vector2(x, y),
                angleDeg = angleDeg,
                length = length,
                width = kind == LandingKind.LongRunway ? LongRunwayWidth : ShortRunwayWidth,
            });
        }

        private static void AddWaterLane(MapDefinition def, string name, float x, float y, float angleDeg, float length)
        {
            def.landingSpots.Add(new LandingSpotDef
            {
                name = name,
                kind = LandingKind.WaterLane,
                position = new Vector2(x, y),
                angleDeg = angleDeg,
                length = length,
                width = WaterLaneWidth,
            });
        }

        private static void AddHelipad(MapDefinition def, string name, float x, float y)
        {
            def.landingSpots.Add(new LandingSpotDef
            {
                name = name,
                kind = LandingKind.Helipad,
                position = new Vector2(x, y),
                angleDeg = 0f,
                length = 0f,
                width = HelipadDiameter,
            });
        }

        private static void AddSpawn(MapDefinition def, Func<string, AircraftType> ac, string typeId, float weight, float unlockTime)
        {
            AircraftType type = null;
            if (ac != null)
            {
                try { type = ac(typeId); }
                catch (KeyNotFoundException) { type = null; }
            }
            if (type == null) return;
            def.spawnTable.Add(new SpawnEntry { type = type, weight = weight, unlockTime = unlockTime });
        }

        private static void SetTuning(MapDefinition def, float initialInterval, float minInterval, float ramp,
            int initialMax, int cap, float stepSeconds, float speed)
        {
            def.initialSpawnInterval = initialInterval;
            def.minSpawnInterval = minInterval;
            def.rampDuration = ramp;
            def.initialMaxAircraft = initialMax;
            def.maxAircraftCap = cap;
            def.maxAircraftStepSeconds = stepSeconds;
            def.speedMultiplier = speed;
        }

        // ------------------------------------------------------------------------------------------------
        // Validation
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// Checks the layout rules: every spot inside <see cref="SpotRegion"/>, sizes within the allowed ranges,
        /// <see cref="MinApproachRoom"/> of in-safe-area approach before each directional threshold (corridor as wide
        /// as the spot), corridors not crossing other spots, no overlapping spots, and a matching spot for every
        /// spawnable aircraft. Appends human-readable problems to <paramref name="problems"/> (may be null).
        /// Returns true when nothing was found.
        /// </summary>
        public static bool Validate(MapDefinition def, List<string> problems)
        {
            List<string> list = problems ?? new List<string>();
            int start = list.Count;
            if (def == null)
            {
                list.Add("map definition is null");
                return false;
            }

            List<LandingSpotDef> spots = def.landingSpots ?? new List<LandingSpotDef>();
            if (spots.Count == 0) list.Add("has no landing spots");
            const float eps = 1e-4f;

            for (int i = 0; i < spots.Count; i++)
            {
                LandingSpotDef s = spots[i];
                if (s == null)
                {
                    list.Add($"spot {i} is null");
                    continue;
                }

                string label = $"spot {i} '{s.name}' ({s.kind})";
                CheckSize(s, label, list);
                Vector2[] outline = SpotOutline(s);
                foreach (Vector2 p in outline)
                {
                    if (!Contains(SpotRegion, p, eps))
                    {
                        list.Add($"{label} leaves the allowed region at ({p.x:0.###}, {p.y:0.###})");
                        break;
                    }
                }

                if (LandingColors.IsDirectional(s.kind))
                {
                    Vector2[] corridor = ApproachCorridor(s, MinApproachRoom);
                    foreach (Vector2 p in corridor)
                    {
                        if (!Contains(SafeArea, p, eps))
                        {
                            list.Add($"{label} has less than {MinApproachRoom} units of approach inside the safe area");
                            break;
                        }
                    }
                    for (int j = 0; j < spots.Count; j++)
                    {
                        if (j != i && spots[j] != null && ConvexOverlap(corridor, SpotOutline(spots[j])))
                            list.Add($"{label} approach corridor crosses spot {j} '{spots[j].name}'");
                    }
                }

                for (int j = i + 1; j < spots.Count; j++)
                {
                    if (spots[j] != null && ConvexOverlap(outline, SpotOutline(spots[j])))
                        list.Add($"{label} overlaps spot {j} '{spots[j].name}'");
                }
            }

            if (def.spawnTable != null)
            {
                foreach (SpawnEntry e in def.spawnTable)
                {
                    if (e == null || e.type == null) continue;
                    bool found = false;
                    foreach (LandingSpotDef s in spots)
                    {
                        if (s != null && LandingMath.Accepts(s.kind, e.type.landingKind))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        list.Add($"aircraft '{e.type.id}' can spawn but the map has no {LandingColors.Label(e.type.landingKind)}");
                }
            }

            return list.Count == start;
        }

        /// <summary>
        /// Convex outline of a spot in world units: the 4 corners of a runway / water-lane rect (threshold to far end),
        /// or a regular octagon circumscribing a helipad (same axis-aligned bounds as the pad circle).
        /// </summary>
        public static Vector2[] SpotOutline(LandingSpotDef spot)
        {
            if (spot == null) return new Vector2[0];
            if (!LandingColors.IsDirectional(spot.kind))
            {
                float r = spot.width * 0.5f / Mathf.Cos(22.5f * Mathf.Deg2Rad);
                Vector2[] oct = new Vector2[8];
                for (int k = 0; k < 8; k++)
                {
                    float a = (22.5f + 45f * k) * Mathf.Deg2Rad;
                    oct[k] = spot.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                }
                return oct;
            }
            return OrientedRect(spot, 0f, spot.length);
        }

        /// <summary>Rect in front of a directional spot's threshold: <paramref name="room"/> units long, as wide as the spot.</summary>
        public static Vector2[] ApproachCorridor(LandingSpotDef spot, float room)
            => OrientedRect(spot, -room, 0f);

        private static Vector2[] OrientedRect(LandingSpotDef s, float from, float to)
        {
            Vector2 d = s.Direction;
            Vector2 n = new Vector2(-d.y, d.x) * (s.width * 0.5f);
            Vector2 a = s.position + d * from;
            Vector2 b = s.position + d * to;
            return new[] { a - n, b - n, b + n, a + n };
        }

        private static void CheckSize(LandingSpotDef s, string label, List<string> list)
        {
            switch (s.kind)
            {
                case LandingKind.LongRunway:
                    CheckRange(s.length, 5.0f, 6.0f, label + " length", list);
                    CheckRange(s.width, LongRunwayWidth, LongRunwayWidth, label + " width", list);
                    break;
                case LandingKind.ShortRunway:
                    CheckRange(s.length, 3.2f, 3.8f, label + " length", list);
                    CheckRange(s.width, ShortRunwayWidth, ShortRunwayWidth, label + " width", list);
                    break;
                case LandingKind.WaterLane:
                    CheckRange(s.length, 4.0f, 4.6f, label + " length", list);
                    CheckRange(s.width, WaterLaneWidth, WaterLaneWidth, label + " width", list);
                    break;
                case LandingKind.Helipad:
                    CheckRange(s.width, HelipadDiameter, HelipadDiameter, label + " diameter", list);
                    break;
            }
        }

        private static void CheckRange(float value, float min, float max, string what, List<string> list)
        {
            if (value < min - 1e-4f || value > max + 1e-4f)
                list.Add($"{what} {value:0.###} is outside [{min:0.###}, {max:0.###}]");
        }

        private static bool Contains(Rect r, Vector2 p, float eps)
            => p.x >= r.xMin - eps && p.x <= r.xMax + eps && p.y >= r.yMin - eps && p.y <= r.yMax + eps;

        /// <summary>Separating-axis test for two convex polygons (touching edges count as not overlapping).</summary>
        private static bool ConvexOverlap(Vector2[] a, Vector2[] b)
        {
            if (a.Length < 3 || b.Length < 3) return false;
            return !HasSeparatingAxis(a, b) && !HasSeparatingAxis(b, a);
        }

        private static bool HasSeparatingAxis(Vector2[] poly, Vector2[] other)
        {
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 e = poly[(i + 1) % poly.Length] - poly[i];
                Vector2 axis = new Vector2(-e.y, e.x);
                if (axis.sqrMagnitude < 1e-12f) continue;
                Project(poly, axis, out float aMin, out float aMax);
                Project(other, axis, out float bMin, out float bMax);
                if (aMax <= bMin + 1e-5f || bMax <= aMin + 1e-5f) return true;
            }
            return false;
        }

        private static void Project(Vector2[] poly, Vector2 axis, out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            foreach (Vector2 p in poly)
            {
                float d = Vector2.Dot(p, axis);
                if (d < min) min = d;
                if (d > max) max = d;
            }
        }
    }
}
