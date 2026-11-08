using System.Collections.Generic;
using ClearToLand.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace ClearToLand.Tests
{
    /// <summary>
    /// <see cref="MapCatalog"/> layouts: spots inside the allowed region, approach room, no overlaps, contract sizes,
    /// spawn tables that only use landable types. Aircraft come from <see cref="AircraftCatalog"/> (sprites may be null).
    /// </summary>
    public sealed class MapCatalogTests
    {
        // Allowed region for whole spots (safe area minus side margins and HUD bands).
        private static readonly Rect Allowed = Rect.MinMaxRect(-8.6f, -4.0f, 8.6f, 4.1f);
        private static readonly Rect Safe = Rect.MinMaxRect(-9.6f, -5.4f, 9.6f, 5.4f);
        private const float ApproachRoom = 2.5f;
        private const float Tol = 1e-3f;

        private readonly Dictionary<string, AircraftType> _aircraft = new Dictionary<string, AircraftType>();
        private readonly List<Object> _created = new List<Object>();

        private static IEnumerable<string> MapIds() => MapCatalog.Ids;

        [SetUp]
        public void SetUp()
        {
            foreach (string id in AircraftCatalog.Ids)
            {
                AircraftType t = ScriptableObject.CreateInstance<AircraftType>();
                AircraftCatalog.Apply(t, id);
                _aircraft[id] = t;
                _created.Add(t);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
            _aircraft.Clear();
        }

        private AircraftType Lookup(string id) => id != null && _aircraft.TryGetValue(id, out AircraftType t) ? t : null;

        private MapDefinition Build(string id)
        {
            MapDefinition def = ScriptableObject.CreateInstance<MapDefinition>();
            _created.Add(def);
            MapCatalog.Apply(def, id, Lookup);
            return def;
        }

        private string IdOf(AircraftType t)
        {
            foreach (KeyValuePair<string, AircraftType> kv in _aircraft) if (kv.Value == t) return kv.Key;
            return null;
        }

        // ---------------------------------------------------------------- catalog-level

        [Test]
        public void Ids_AreTheFourContractMaps()
        {
            Assert.That(MapCatalog.Ids, Is.EqualTo(new[] { "green_valley", "dune_sea", "harbor_bay", "carrier_group" }));
        }

        [TestCase("green_valley", 1)]
        [TestCase("dune_sea", 2)]
        [TestCase("harbor_bay", 3)]
        [TestCase("carrier_group", 4)]
        public void Apply_DifficultyMatchesContract(string id, int difficulty)
        {
            Assert.That(Build(id).difficulty, Is.EqualTo(difficulty));
        }

        [TestCase("green_valley", new[] { LandingKind.LongRunway, LandingKind.ShortRunway, LandingKind.Helipad })]
        [TestCase("dune_sea", new[] { LandingKind.LongRunway, LandingKind.ShortRunway, LandingKind.Helipad })]
        [TestCase("harbor_bay", new[] { LandingKind.LongRunway, LandingKind.ShortRunway, LandingKind.WaterLane, LandingKind.Helipad })]
        [TestCase("carrier_group", new[] { LandingKind.LongRunway, LandingKind.ShortRunway, LandingKind.WaterLane, LandingKind.Helipad })]
        public void Apply_HasContractSpotKinds(string id, LandingKind[] kinds)
        {
            MapDefinition def = Build(id);
            foreach (LandingKind k in kinds)
                Assert.That(def.landingSpots.Exists(s => s.kind == k), Is.True, id + " is missing a " + k);
        }

        [TestCase("green_valley", new[] { "prop", "airliner", "helicopter" })]
        [TestCase("dune_sea", new[] { "prop", "airliner", "helicopter", "fighter" })]
        [TestCase("harbor_bay", new[] { "prop", "airliner", "helicopter", "seaplane" })]
        [TestCase("carrier_group", new[] { "fighter", "helicopter", "prop", "seaplane" })]
        public void Apply_SpawnTableHasContractTypes(string id, string[] typeIds)
        {
            MapDefinition def = Build(id);
            HashSet<string> present = new HashSet<string>();
            foreach (SpawnEntry e in def.spawnTable) if (e != null && e.type != null) present.Add(IdOf(e.type));
            foreach (string t in typeIds) Assert.That(present.Contains(t), Is.True, id + " does not spawn " + t);
        }

        // ---------------------------------------------------------------- per map

        [TestCaseSource(nameof(MapIds))]
        public void Apply_FillsIdentityAndContent(string id)
        {
            MapDefinition def = Build(id);
            Assert.That(def.id, Is.EqualTo(id));
            Assert.That(string.IsNullOrEmpty(def.displayName), Is.False, "displayName");
            Assert.That(string.IsNullOrEmpty(def.tagline), Is.False, "tagline");
            Assert.That(def.difficulty, Is.InRange(1, 4));
            Assert.That(def.landingSpots.Count, Is.GreaterThan(0));
            Assert.That(def.spawnTable.Count, Is.GreaterThan(0));
        }

        [TestCaseSource(nameof(MapIds))]
        public void Apply_SpotsLieFullyInsideAllowedRegion(string id)
        {
            foreach (LandingSpotDef s in Build(id).landingSpots)
                foreach (Vector2 c in SpotGeometry.Corners(s))
                    Vec2Assert.Inside(Allowed, c, Tol, id + "/" + s.name + " (" + s.kind + ") corner");
        }

        [TestCaseSource(nameof(MapIds))]
        public void Apply_DirectionalSpotsHaveApproachRoomInsideSafeArea(string id)
        {
            foreach (LandingSpotDef s in Build(id).landingSpots)
            {
                if (!LandingColors.IsDirectional(s.kind)) continue;
                Vector2 approachStart = s.position - s.Direction * ApproachRoom;
                Vec2Assert.Inside(Safe, s.position, Tol, id + "/" + s.name + " threshold");
                Vec2Assert.Inside(Safe, approachStart, Tol, id + "/" + s.name + " needs " + ApproachRoom + " units before its threshold;");
            }
        }

        [TestCaseSource(nameof(MapIds))]
        public void Apply_SpotsDoNotOverlap(string id)
        {
            List<LandingSpotDef> spots = Build(id).landingSpots;
            for (int i = 0; i < spots.Count; i++)
                for (int j = i + 1; j < spots.Count; j++)
                    Assert.That(SpotGeometry.Overlap(spots[i], spots[j], 0.01f), Is.False,
                        id + ": " + spots[i].name + " overlaps " + spots[j].name);
        }

        [TestCaseSource(nameof(MapIds))]
        public void Apply_SpotSizesMatchContract(string id)
        {
            foreach (LandingSpotDef s in Build(id).landingSpots)
            {
                string label = id + "/" + s.name + " (" + s.kind + ")";
                switch (s.kind)
                {
                    case LandingKind.LongRunway:
                        Assert.That(s.length, Is.InRange(5.0f - 0.01f, 6.0f + 0.01f), label + " length");
                        Assert.That(s.width, Is.EqualTo(0.75f).Within(0.01f), label + " width");
                        break;
                    case LandingKind.ShortRunway:
                        Assert.That(s.length, Is.InRange(3.2f - 0.01f, 3.8f + 0.01f), label + " length");
                        Assert.That(s.width, Is.EqualTo(0.6f).Within(0.01f), label + " width");
                        break;
                    case LandingKind.WaterLane:
                        Assert.That(s.length, Is.InRange(4.0f - 0.01f, 4.6f + 0.01f), label + " length");
                        Assert.That(s.width, Is.EqualTo(0.8f).Within(0.01f), label + " width");
                        break;
                    case LandingKind.Helipad:
                        Assert.That(s.width, Is.EqualTo(1.0f).Within(0.01f), label + " diameter");
                        break;
                }
            }
        }

        [TestCaseSource(nameof(MapIds))]
        public void Apply_EverySpawnableTypeHasMatchingSpot(string id)
        {
            MapDefinition def = Build(id);
            foreach (SpawnEntry e in def.spawnTable)
            {
                if (e == null || e.type == null || e.weight <= 0f) continue;
                Assert.That(def.landingSpots.Exists(s => LandingMath.Accepts(s.kind, e.type.landingKind)), Is.True,
                    id + ": " + IdOf(e.type) + " spawns but no " + e.type.landingKind + " exists");
            }
        }

        [TestCaseSource(nameof(MapIds))]
        public void Apply_SpawnTableUsesLookupTypesAndValidValues(string id)
        {
            foreach (SpawnEntry e in Build(id).spawnTable)
            {
                Assert.That(e, Is.Not.Null);
                Assert.That(e.type != null, Is.True, id + ": null type in spawn table");
                Assert.That(IdOf(e.type), Is.Not.Null, id + ": spawn type does not come from the lookup");
                Assert.That(e.weight, Is.GreaterThan(0f), id + ": weight");
                Assert.That(e.unlockTime, Is.GreaterThanOrEqualTo(0f), id + ": unlockTime");
            }
        }

        [TestCaseSource(nameof(MapIds))]
        public void Apply_SomethingCanSpawnAtStart(string id)
        {
            Assert.That(Build(id).spawnTable.Exists(e => e.type != null && e.weight > 0f && e.unlockTime <= 0f), Is.True);
        }

        [TestCaseSource(nameof(MapIds))]
        public void Apply_TuningIsSane(string id)
        {
            MapDefinition d = Build(id);
            Assert.That(d.minSpawnInterval, Is.GreaterThan(0f));
            Assert.That(d.initialSpawnInterval, Is.GreaterThan(d.minSpawnInterval));
            Assert.That(d.rampDuration, Is.GreaterThan(0f));
            Assert.That(d.initialMaxAircraft, Is.GreaterThanOrEqualTo(1));
            Assert.That(d.maxAircraftCap, Is.GreaterThanOrEqualTo(d.initialMaxAircraft));
            Assert.That(d.maxAircraftStepSeconds, Is.GreaterThan(0f));
            Assert.That(d.speedMultiplier, Is.GreaterThan(0f));
        }

        [TestCaseSource(nameof(MapIds))]
        public void Apply_Twice_DoesNotDuplicateContent(string id)
        {
            MapDefinition def = Build(id);
            int spots = def.landingSpots.Count, spawns = def.spawnTable.Count;
            MapCatalog.Apply(def, id, Lookup);
            Assert.That(def.landingSpots.Count, Is.EqualTo(spots));
            Assert.That(def.spawnTable.Count, Is.EqualTo(spawns));
        }

        [TestCaseSource(nameof(MapIds))]
        public void Apply_WithMissingAircraft_SkipsNullTypes(string id)
        {
            MapDefinition def = ScriptableObject.CreateInstance<MapDefinition>();
            _created.Add(def);
            MapCatalog.Apply(def, id, _ => null);
            foreach (SpawnEntry e in def.spawnTable) Assert.That(e != null && e.type != null, Is.True);
            Assert.That(def.landingSpots.Count, Is.GreaterThan(0), "Layout does not depend on aircraft assets.");
        }

        // ---------------------------------------------------------------- validator self-checks

        [Test]
        public void SpotGeometry_DetectsCrossingRunways()
        {
            LandingSpotDef a = new LandingSpotDef { kind = LandingKind.LongRunway, position = new Vector2(-2f, 0f), length = 5f, width = 0.75f };
            LandingSpotDef b = new LandingSpotDef { kind = LandingKind.ShortRunway, position = new Vector2(0f, -1.5f), angleDeg = 90f, length = 3.5f, width = 0.6f };
            Assert.That(SpotGeometry.Overlap(a, b, 0.01f), Is.True);
        }

        [Test]
        public void SpotGeometry_SeparatesParallelRunways()
        {
            LandingSpotDef a = new LandingSpotDef { kind = LandingKind.LongRunway, position = new Vector2(-2f, 0f), length = 5f, width = 0.75f };
            LandingSpotDef b = new LandingSpotDef { kind = LandingKind.ShortRunway, position = new Vector2(-2f, 1.5f), length = 3.5f, width = 0.6f };
            Assert.That(SpotGeometry.Overlap(a, b, 0.01f), Is.False);
        }

        [Test]
        public void SpotGeometry_HandlesHelipads()
        {
            LandingSpotDef runway = new LandingSpotDef { kind = LandingKind.LongRunway, position = Vector2.zero, length = 5f, width = 0.75f };
            LandingSpotDef padOn = new LandingSpotDef { kind = LandingKind.Helipad, position = new Vector2(2f, 0.5f), width = 1f };
            LandingSpotDef padOff = new LandingSpotDef { kind = LandingKind.Helipad, position = new Vector2(2f, 1.5f), width = 1f };
            LandingSpotDef padNear = new LandingSpotDef { kind = LandingKind.Helipad, position = new Vector2(2.8f, 1.5f), width = 1f };
            Assert.That(SpotGeometry.Overlap(runway, padOn, 0.01f), Is.True);
            Assert.That(SpotGeometry.Overlap(padOff, runway, 0.01f), Is.False);
            Assert.That(SpotGeometry.Overlap(padOff, padNear, 0.01f), Is.True);
        }
    }
}
