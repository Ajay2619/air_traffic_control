using System.Collections.Generic;
using ClearToLand.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace ClearToLand.Tests
{
    /// <summary><see cref="AircraftCatalog"/>: ids and per-type stats (sprites may be missing in a fresh checkout).</summary>
    public sealed class AircraftCatalogTests
    {
        private readonly List<AircraftType> _created = new List<AircraftType>();

        [TearDown]
        public void TearDown()
        {
            foreach (AircraftType t in _created) if (t != null) Object.DestroyImmediate(t);
            _created.Clear();
        }

        private AircraftType Build(string id)
        {
            AircraftType t = ScriptableObject.CreateInstance<AircraftType>();
            _created.Add(t);
            AircraftCatalog.Apply(t, id);
            return t;
        }

        [Test]
        public void Ids_AreTheFiveContractTypes()
        {
            Assert.That(AircraftCatalog.Ids, Is.EqualTo(new[] { "prop", "airliner", "fighter", "helicopter", "seaplane" }));
        }

        [TestCase("prop", LandingKind.ShortRunway, 0.72f, 0.24f, "Prop plane")]
        [TestCase("airliner", LandingKind.LongRunway, 0.9f, 0.36f, "Airliner")]
        [TestCase("fighter", LandingKind.LongRunway, 1.15f, 0.28f, "Jet fighter")]
        [TestCase("helicopter", LandingKind.Helipad, 0.55f, 0.26f, "Helicopter")]
        [TestCase("seaplane", LandingKind.WaterLane, 0.68f, 0.27f, "Seaplane")]
        public void Apply_SetsContractStats(string id, LandingKind kind, float speed, float radius, string displayName)
        {
            AircraftType t = Build(id);
            Assert.That(t.id, Is.EqualTo(id));
            Assert.That(t.landingKind, Is.EqualTo(kind));
            Assert.That(t.speed, Is.EqualTo(speed).Within(1e-4f));
            Assert.That(t.radius, Is.EqualTo(radius).Within(1e-4f));
            Assert.That(t.displayName, Is.EqualTo(displayName));
            Assert.That(t.scoreValue, Is.EqualTo(1));
        }

        [Test]
        public void Apply_OnlyHelicopterIsHelicopter()
        {
            foreach (string id in AircraftCatalog.Ids)
                Assert.That(Build(id).IsHelicopter, Is.EqualTo(id == "helicopter"), id);
        }

        [Test]
        public void Apply_EveryLandingKindIsUsedByAtLeastOneType()
        {
            HashSet<LandingKind> kinds = new HashSet<LandingKind>();
            foreach (string id in AircraftCatalog.Ids) kinds.Add(Build(id).landingKind);
            foreach (LandingKind k in System.Enum.GetValues(typeof(LandingKind)))
                Assert.That(kinds.Contains(k), Is.True, k + " has no aircraft type");
        }

        [Test]
        public void Apply_IsIdempotent()
        {
            AircraftType t = Build("fighter");
            AircraftCatalog.Apply(t, "fighter");
            Assert.That(t.id, Is.EqualTo("fighter"));
            Assert.That(t.speed, Is.EqualTo(1.15f).Within(1e-4f));
        }
    }
}
