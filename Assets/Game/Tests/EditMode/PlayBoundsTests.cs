using NUnit.Framework;
using UnityEngine;

namespace ClearToLand.Tests
{
    /// <summary><see cref="PlayBounds"/>: orthographic fit keeps the safe area visible; rect helpers.</summary>
    public sealed class PlayBoundsTests
    {
        private const float Eps = 1e-3f;
        private static readonly Rect SafeRect = new Rect(-GameConstants.SafeWidth * 0.5f, -GameConstants.SafeHeight * 0.5f,
            GameConstants.SafeWidth, GameConstants.SafeHeight);

        private GameObject _cameraObject;

        [TearDown]
        public void TearDown()
        {
            if (_cameraObject != null) Object.DestroyImmediate(_cameraObject);
            _cameraObject = null;
        }

        private Camera CreateCamera(float aspect, Vector3 position)
        {
            _cameraObject = new GameObject("PlayBoundsTestCamera");
            Camera cam = _cameraObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.aspect = aspect;
            cam.orthographicSize = PlayBounds.FitOrthographicSize(aspect);
            cam.transform.position = position;
            return cam;
        }

        // ---------------------------------------------------------------- FitOrthographicSize

        [TestCase(16f, 9f, 5.4f)]
        [TestCase(4f, 3f, 7.2f)]
        [TestCase(21f, 9f, 5.4f)]
        [TestCase(2f, 1f, 5.4f)]
        public void FitOrthographicSize_KnownAspects(float w, float h, float expected)
        {
            Assert.That(PlayBounds.FitOrthographicSize(w / h), Is.EqualTo(expected).Within(Eps));
        }

        [TestCase(16f / 9f)]
        [TestCase(4f / 3f)]
        [TestCase(21f / 9f)]
        [TestCase(2f)]
        [TestCase(3f / 2f)]
        [TestCase(19.5f / 9f)]
        [TestCase(9f / 16f)]
        public void FitOrthographicSize_KeepsSafeAreaVisible_AndIsTight(float aspect)
        {
            float size = PlayBounds.FitOrthographicSize(aspect);
            float visibleH = size * 2f;
            float visibleW = visibleH * aspect;
            Assert.That(visibleW, Is.GreaterThanOrEqualTo(GameConstants.SafeWidth - Eps));
            Assert.That(visibleH, Is.GreaterThanOrEqualTo(GameConstants.SafeHeight - Eps));
            bool tight = Mathf.Abs(visibleW - GameConstants.SafeWidth) < Eps || Mathf.Abs(visibleH - GameConstants.SafeHeight) < Eps;
            Assert.That(tight, Is.True, "One dimension should match the safe area exactly (no wasted zoom).");
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void FitOrthographicSize_InvalidAspect_FallsBackTo16By9(float aspect)
        {
            Assert.That(PlayBounds.FitOrthographicSize(aspect), Is.EqualTo(5.4f).Within(Eps));
        }

        // ---------------------------------------------------------------- ViewRect

        [Test]
        public void ViewRect_MatchesCameraSizeAndPosition()
        {
            Camera cam = CreateCamera(16f / 9f, new Vector3(1f, 2f, -10f));
            Rect r = PlayBounds.ViewRect(cam);
            Assert.That(r.width, Is.EqualTo(19.2f).Within(Eps));
            Assert.That(r.height, Is.EqualTo(10.8f).Within(Eps));
            Vec2Assert.Near(new Vector2(1f, 2f), r.center, Eps);
        }

        [TestCase(16f / 9f)]
        [TestCase(4f / 3f)]
        [TestCase(21f / 9f)]
        [TestCase(2f)]
        public void ViewRect_OfFittedCamera_ContainsSafeArea(float aspect)
        {
            Camera cam = CreateCamera(aspect, new Vector3(0f, 0f, -10f));
            Rect r = PlayBounds.ViewRect(cam);
            Assert.That(r.xMin, Is.LessThanOrEqualTo(SafeRect.xMin + Eps));
            Assert.That(r.xMax, Is.GreaterThanOrEqualTo(SafeRect.xMax - Eps));
            Assert.That(r.yMin, Is.LessThanOrEqualTo(SafeRect.yMin + Eps));
            Assert.That(r.yMax, Is.GreaterThanOrEqualTo(SafeRect.yMax - Eps));
        }

        [Test]
        public void ViewRect_OfFittedCamera_StaysInsideMapBackgroundUpTo21By9()
        {
            Camera cam = CreateCamera(21f / 9f, new Vector3(0f, 0f, -10f));
            Rect r = PlayBounds.ViewRect(cam);
            Assert.That(r.width, Is.LessThanOrEqualTo(GameConstants.BackgroundWidth + Eps));
            Assert.That(r.height, Is.LessThanOrEqualTo(GameConstants.BackgroundHeight + Eps));
        }

        // ---------------------------------------------------------------- Inset / ClampInside

        [Test]
        public void Inset_Positive_ShrinksAroundCenter()
        {
            Rect r = PlayBounds.Inset(new Rect(0f, 0f, 10f, 4f), 1f);
            Assert.That(r.xMin, Is.EqualTo(1f).Within(Eps));
            Assert.That(r.yMin, Is.EqualTo(1f).Within(Eps));
            Assert.That(r.width, Is.EqualTo(8f).Within(Eps));
            Assert.That(r.height, Is.EqualTo(2f).Within(Eps));
        }

        [Test]
        public void Inset_Negative_Grows()
        {
            Rect r = PlayBounds.Inset(new Rect(0f, 0f, 10f, 4f), -0.5f);
            Assert.That(r.xMin, Is.EqualTo(-0.5f).Within(Eps));
            Assert.That(r.width, Is.EqualTo(11f).Within(Eps));
            Assert.That(r.height, Is.EqualTo(5f).Within(Eps));
        }

        [Test]
        public void Inset_LargerThanHalfSize_CollapsesToCenterWithoutNegativeSize()
        {
            Rect r = PlayBounds.Inset(new Rect(0f, 0f, 10f, 4f), 3f);
            Assert.That(r.width, Is.EqualTo(4f).Within(Eps));
            Assert.That(r.height, Is.EqualTo(0f).Within(Eps));
            Vec2Assert.Near(new Vector2(5f, 2f), r.center, Eps);
        }

        [Test]
        public void ClampInside_PointInside_IsUnchanged()
        {
            Vec2Assert.Near(new Vector2(1f, -2f), PlayBounds.ClampInside(SafeRect, new Vector2(1f, -2f), 0.1f));
        }

        [Test]
        public void ClampInside_PointOutside_IsClampedToInsetRect()
        {
            Vec2Assert.Near(new Vector2(9.5f, -5.3f), PlayBounds.ClampInside(SafeRect, new Vector2(20f, -30f), 0.1f), Eps);
            Vec2Assert.Near(new Vector2(-9.6f, 5.4f), PlayBounds.ClampInside(SafeRect, new Vector2(-20f, 30f)), Eps);
        }
    }
}
