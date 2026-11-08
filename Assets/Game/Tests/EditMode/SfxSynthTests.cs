using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ClearToLand.Tests
{
    /// <summary><see cref="SfxSynth"/>: every procedural sound renders finite, in-range, audible, reasonably short samples.</summary>
    public sealed class SfxSynthTests
    {
        private const float MaxSeconds = 3f;

        private static IEnumerable<Sfx> AllSfx()
        {
            foreach (Sfx s in Enum.GetValues(typeof(Sfx))) yield return s;
        }

        private static void AssertValidSound(float[] samples, string label)
        {
            Assert.That(samples, Is.Not.Null, label);
            Assert.That(samples.Length, Is.GreaterThan(0), label + " is empty");
            Assert.That(samples.Length, Is.LessThanOrEqualTo((int)(SfxSynth.SampleRate * MaxSeconds)), label + " is too long");

            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float v = samples[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) Assert.Fail(label + ": sample " + i + " is not finite (" + v + ")");
                if (v < -1f || v > 1f) Assert.Fail(label + ": sample " + i + " = " + v + " is outside [-1, 1]");
                peak = Mathf.Max(peak, Mathf.Abs(v));
            }
            Assert.That(peak, Is.GreaterThan(0.01f), label + " is silent");
        }

        [Test]
        public void SampleRate_Is44100()
        {
            Assert.That(SfxSynth.SampleRate, Is.EqualTo(44100));
        }

        [TestCaseSource(nameof(AllSfx))]
        public void Render_EachSfx_IsValid(Sfx sfx)
        {
            AssertValidSound(SfxSynth.Render(sfx), sfx.ToString());
        }

        [Test]
        public void RenderWarningBeep_IsValid()
        {
            AssertValidSound(SfxSynth.RenderWarningBeep(), "Warning beep");
        }

        [Test]
        public void Render_ClickIsShorterThanCrash()
        {
            Assert.That(SfxSynth.Render(Sfx.Click).Length, Is.LessThan(SfxSynth.Render(Sfx.Crash).Length));
        }

        [Test]
        public void Render_StartsAndEndsQuietly()
        {
            // Attack/release envelopes: no hard click at either end of any clip.
            foreach (Sfx sfx in AllSfx())
            {
                float[] s = SfxSynth.Render(sfx);
                Assert.That(Mathf.Abs(s[0]), Is.LessThan(0.2f), sfx + " first sample");
                Assert.That(Mathf.Abs(s[s.Length - 1]), Is.LessThan(0.2f), sfx + " last sample");
            }
        }

        [Test]
        public void CreateClip_ProducesMonoClipWithAllSamples()
        {
            float[] samples = SfxSynth.Render(Sfx.Select);
            AudioClip clip = SfxSynth.CreateClip("test_select", samples);
            try
            {
                Assert.That(clip, Is.Not.Null);
                Assert.That(clip.name, Is.EqualTo("test_select"));
                Assert.That(clip.channels, Is.EqualTo(1));
                Assert.That(clip.frequency, Is.EqualTo(SfxSynth.SampleRate));
                Assert.That(clip.samples, Is.EqualTo(samples.Length));
            }
            finally
            {
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
            }
        }
    }
}
