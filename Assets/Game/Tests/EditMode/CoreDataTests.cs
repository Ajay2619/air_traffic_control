using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ClearToLand.Tests
{
    /// <summary>Foundation data types: LandingSpotDef geometry, LandingColors, AircraftType, GameSession, constants.</summary>
    public sealed class CoreDataTests
    {
        private const float Eps = 1e-4f;
        private readonly List<Object> _created = new List<Object>();
        private MapDefinition _savedSelectedMap;

        [SetUp]
        public void SetUp()
        {
            _savedSelectedMap = GameSession.SelectedMap;
        }

        [TearDown]
        public void TearDown()
        {
            GameSession.SelectedMap = _savedSelectedMap;
            foreach (Object o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private T Create<T>() where T : ScriptableObject
        {
            T so = ScriptableObject.CreateInstance<T>();
            _created.Add(so);
            return so;
        }

        // ---------------------------------------------------------------- LandingSpotDef

        [TestCase(0f, 1f, 0f)]
        [TestCase(90f, 0f, 1f)]
        [TestCase(180f, -1f, 0f)]
        [TestCase(-90f, 0f, -1f)]
        public void LandingSpotDef_Direction_FollowsAngle(float angle, float x, float y)
        {
            LandingSpotDef d = new LandingSpotDef { angleDeg = angle };
            Vec2Assert.Near(new Vector2(x, y), d.Direction);
        }

        [Test]
        public void LandingSpotDef_Runway_FarEndAndCenter()
        {
            LandingSpotDef d = new LandingSpotDef
            {
                kind = LandingKind.LongRunway, position = new Vector2(1f, 2f), angleDeg = 90f, length = 5f,
            };
            Vec2Assert.Near(new Vector2(1f, 7f), d.FarEnd);
            Vec2Assert.Near(new Vector2(1f, 4.5f), d.Center);
        }

        [Test]
        public void LandingSpotDef_Helipad_FarEndAndCenterArePosition()
        {
            LandingSpotDef d = new LandingSpotDef
            {
                kind = LandingKind.Helipad, position = new Vector2(-3f, 2f), angleDeg = 45f, length = 5f, width = 1f,
            };
            Vec2Assert.Near(new Vector2(-3f, 2f), d.FarEnd);
            Vec2Assert.Near(new Vector2(-3f, 2f), d.Center);
        }

        [Test]
        public void LandingSpotDef_Clone_IsIndependentCopy()
        {
            LandingSpotDef d = new LandingSpotDef
            {
                name = "Rwy", kind = LandingKind.WaterLane, position = new Vector2(1f, 1f), angleDeg = 30f, length = 4f, width = 0.8f,
            };
            LandingSpotDef c = d.Clone();
            Assert.That(c, Is.Not.SameAs(d));
            Assert.That(c.name, Is.EqualTo("Rwy"));
            Assert.That(c.kind, Is.EqualTo(LandingKind.WaterLane));
            Assert.That(c.length, Is.EqualTo(4f));
            c.position = Vector2.zero;
            Vec2Assert.Near(new Vector2(1f, 1f), d.position);
        }

        // ---------------------------------------------------------------- LandingColors

        [Test]
        public void LandingColors_EachKindHasDistinctOpaqueColorAndLabel()
        {
            LandingKind[] kinds = (LandingKind[])System.Enum.GetValues(typeof(LandingKind));
            for (int i = 0; i < kinds.Length; i++)
            {
                Color c = LandingColors.For(kinds[i]);
                Assert.That(c.a, Is.EqualTo(1f).Within(Eps), kinds[i].ToString());
                Assert.That(string.IsNullOrEmpty(LandingColors.Label(kinds[i])), Is.False, kinds[i].ToString());
                for (int j = i + 1; j < kinds.Length; j++)
                    Assert.That(LandingColors.For(kinds[j]), Is.Not.EqualTo(c), kinds[i] + " vs " + kinds[j]);
            }
        }

        [Test]
        public void LandingColors_OnlyHelipadIsNonDirectional()
        {
            Assert.That(LandingColors.IsDirectional(LandingKind.ShortRunway), Is.True);
            Assert.That(LandingColors.IsDirectional(LandingKind.LongRunway), Is.True);
            Assert.That(LandingColors.IsDirectional(LandingKind.WaterLane), Is.True);
            Assert.That(LandingColors.IsDirectional(LandingKind.Helipad), Is.False);
        }

        // ---------------------------------------------------------------- AircraftType

        [Test]
        public void AircraftType_ColorAndHelicopterFlag_FollowLandingKind()
        {
            AircraftType t = Create<AircraftType>();
            t.landingKind = LandingKind.Helipad;
            Assert.That(t.IsHelicopter, Is.True);
            Assert.That(t.Color, Is.EqualTo(LandingColors.Helipad));
            t.landingKind = LandingKind.WaterLane;
            Assert.That(t.IsHelicopter, Is.False);
            Assert.That(t.Color, Is.EqualTo(LandingColors.WaterLane));
        }

        // ---------------------------------------------------------------- GameSession

        [Test]
        public void ResolveMap_PrefersSelectedMap()
        {
            GameConfig config = Create<GameConfig>();
            MapDefinition first = Create<MapDefinition>();
            MapDefinition chosen = Create<MapDefinition>();
            config.maps.Add(first);
            GameSession.SelectedMap = chosen;
            Assert.That(GameSession.ResolveMap(config), Is.SameAs(chosen));
        }

        [Test]
        public void ResolveMap_WithoutSelection_FallsBackToFirstConfigMap()
        {
            GameConfig config = Create<GameConfig>();
            MapDefinition first = Create<MapDefinition>();
            config.maps.Add(first);
            config.maps.Add(Create<MapDefinition>());
            GameSession.SelectedMap = null;
            Assert.That(GameSession.ResolveMap(config), Is.SameAs(first));
        }

        [Test]
        public void ResolveMap_NothingAvailable_ReturnsNull()
        {
            GameSession.SelectedMap = null;
            Assert.That(GameSession.ResolveMap(null), Is.Null);
            Assert.That(GameSession.ResolveMap(Create<GameConfig>()), Is.Null);
        }

        // ---------------------------------------------------------------- Constants

        [Test]
        public void Constants_BackgroundPixelsMatchWorldSize()
        {
            Assert.That(GameConstants.BackgroundPixelWidth / GameConstants.BackgroundPixelsPerUnit,
                Is.EqualTo(GameConstants.BackgroundWidth).Within(Eps));
            Assert.That(GameConstants.BackgroundPixelHeight / GameConstants.BackgroundPixelsPerUnit,
                Is.EqualTo(GameConstants.BackgroundHeight).Within(Eps));
            Assert.That(GameConstants.BackgroundWidth, Is.GreaterThan(GameConstants.SafeWidth));
            Assert.That(GameConstants.BackgroundHeight, Is.GreaterThan(GameConstants.SafeHeight));
        }

        [Test]
        public void SortOrder_LayersAreStackedSensibly()
        {
            Assert.That(SortOrder.Background, Is.LessThan(SortOrder.SpotHighlight));
            Assert.That(SortOrder.SpotHighlight, Is.LessThan(SortOrder.PathLine));
            Assert.That(SortOrder.PathLine, Is.LessThan(SortOrder.Shadow));
            Assert.That(SortOrder.Shadow, Is.LessThan(SortOrder.LandingAircraft));
            Assert.That(SortOrder.LandingAircraft, Is.LessThan(SortOrder.Aircraft));
            Assert.That(SortOrder.Aircraft, Is.LessThan(SortOrder.AircraftRotor));
            Assert.That(SortOrder.AircraftRotor, Is.LessThan(SortOrder.AircraftOverlay));
            Assert.That(SortOrder.AircraftOverlay, Is.LessThan(SortOrder.Effects));
            Assert.That(SortOrder.Effects, Is.LessThan(SortOrder.ScorePopup));
            Assert.That(SortOrder.ScorePopup, Is.LessThan(SortOrder.EdgeWarning));
        }
    }
}
