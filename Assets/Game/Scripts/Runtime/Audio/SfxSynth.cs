using System;
using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// Procedural sound-effect synthesizer. Every effect is rendered into a mono float buffer at
    /// <see cref="SampleRate"/> with smooth attack/release envelopes (no clicks), band-limited partials
    /// (no aliasing) and a per-effect peak level so the set is balanced before any mixer volume is applied.
    /// Rendering is pure, deterministic math (no Unity audio API), so it also runs in EditMode tests;
    /// <see cref="CreateClip"/> wraps samples in a non-streamed AudioClip, which is WebGL-safe.
    /// </summary>
    public static class SfxSynth
    {
        /// <summary>Sample rate of every rendered buffer and created clip, in Hz.</summary>
        public const int SampleRate = 44100;

        private const float TwoPi = 2f * Mathf.PI;

        // Final peak levels (linear, before the AudioManager master volume). Tuned so that the frequent
        // cues (warning, select, click, alert) sit well below the rare, important ones (landed, crash).
        private const float AlertPeak = 0.50f;
        private const float SelectPeak = 0.36f;
        private const float LockPeak = 0.46f;
        private const float LandedPeak = 0.58f;
        private const float CrashPeak = 0.90f;
        private const float ClickPeak = 0.30f;
        private const float WarningPeak = 0.22f;

        /// <summary>One sine partial of a tone: frequency ratio, relative amplitude and decay-time multiplier.</summary>
        private readonly struct Partial
        {
            public readonly float Ratio;
            public readonly float Amplitude;
            public readonly float DecayScale;

            public Partial(float ratio, float amplitude, float decayScale)
            {
                Ratio = ratio;
                Amplitude = amplitude;
                DecayScale = decayScale;
            }
        }

        // Timbres. Upper partials decay faster than the fundamental, which keeps every sound soft and round.
        private static readonly Partial[] PureTone = { new Partial(1f, 1f, 1f) };

        private static readonly Partial[] ChimeTone =
        {
            new Partial(1f, 1f, 1f),
            new Partial(2f, 0.22f, 0.5f),
            new Partial(3f, 0.07f, 0.3f)
        };

        private static readonly Partial[] BlipTone =
        {
            new Partial(1f, 1f, 1f),
            new Partial(2f, 0.12f, 0.6f)
        };

        private static readonly Partial[] SoftTriangleTone =
        {
            new Partial(1f, 1f, 1f),
            new Partial(2f, 0.08f, 0.6f),
            new Partial(3f, 0.11f, 0.5f)
        };

        private static readonly Partial[] MalletTone =
        {
            new Partial(1f, 1f, 1f),
            new Partial(2f, 0.30f, 0.45f),
            new Partial(3f, 0.08f, 0.3f),
            new Partial(4f, 0.035f, 0.2f)
        };

        // Odd harmonics rolled off faster than a true square (1/n): square-like character without harshness.
        private static readonly Partial[] SoftSquareTone =
        {
            new Partial(1f, 1f, 1f),
            new Partial(3f, 0.20f, 1f),
            new Partial(5f, 0.07f, 1f),
            new Partial(7f, 0.03f, 1f)
        };

        /// <summary>Renders the given effect as mono samples in [-1, 1]. Returns a new array on every call.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="Sfx"/>.</exception>
        public static float[] Render(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Alert: return RenderAlert();
                case Sfx.Select: return RenderSelect();
                case Sfx.Lock: return RenderLock();
                case Sfx.Landed: return RenderLanded();
                case Sfx.Crash: return RenderCrash();
                case Sfx.Click: return RenderClick();
                default: throw new ArgumentOutOfRangeException(nameof(sfx), sfx, "Unknown sound effect.");
            }
        }

        /// <summary>
        /// Renders the proximity warning beep: 0.12 s soft square tone at ~950 Hz, quiet because it repeats
        /// every ~0.45 s while aircraft are too close. Mono samples in [-1, 1]; new array on every call.
        /// </summary>
        public static float[] RenderWarningBeep()
        {
            float[] buffer = Allocate(0.12f);
            AddTone(buffer, 0f, 0.12f, 950f, 950f, 1f, 0.008f, 0.35f, 0.035f, SoftSquareTone);
            return Finish(buffer, 0.001f, 0.005f, WarningPeak);
        }

        /// <summary>
        /// Creates a mono, non-streamed AudioClip at <see cref="SampleRate"/> filled with <paramref name="samples"/>
        /// (AudioClip.Create + SetData, which works on every platform including WebGL).
        /// An empty array yields a one-sample silent clip. The caller owns (and should eventually destroy) the clip.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="samples"/> is null.</exception>
        public static AudioClip CreateClip(string name, float[] samples)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));

            int length = samples.Length > 0 ? samples.Length : 1;
            AudioClip clip = AudioClip.Create(string.IsNullOrEmpty(name) ? "Sfx" : name, length, 1, SampleRate, false);
            if (samples.Length > 0) clip.SetData(samples, 0);
            return clip;
        }

        // ------------------------------------------------------------------------------------------------
        // Effects
        // ------------------------------------------------------------------------------------------------

        /// <summary>Two-tone chime E5 (660 Hz) then A5 (880 Hz); the first note keeps ringing under the second.</summary>
        private static float[] RenderAlert()
        {
            float[] buffer = Allocate(0.35f);
            AddTone(buffer, 0.00f, 0.35f, 660f, 660f, 0.80f, 0.006f, 0.11f, 0.04f, ChimeTone);
            AddTone(buffer, 0.11f, 0.24f, 880f, 880f, 0.85f, 0.006f, 0.13f, 0.05f, ChimeTone);
            return Finish(buffer, 0.001f, 0.03f, AlertPeak);
        }

        /// <summary>Short upward-gliding blip (1250 -> 1550 Hz), 60 ms.</summary>
        private static float[] RenderSelect()
        {
            float[] buffer = Allocate(0.06f);
            AddTone(buffer, 0f, 0.06f, 1250f, 1550f, 1f, 0.003f, 0.03f, 0.018f, BlipTone);
            return Finish(buffer, 0.0005f, 0.006f, SelectPeak);
        }

        /// <summary>Rising perfect fifth G5 -> D6, 180 ms: "path locked".</summary>
        private static float[] RenderLock()
        {
            float[] buffer = Allocate(0.18f);
            AddTone(buffer, 0.000f, 0.085f, 783.99f, 783.99f, 0.75f, 0.004f, 0.07f, 0.025f, SoftTriangleTone);
            AddTone(buffer, 0.065f, 0.115f, 1174.66f, 1174.66f, 0.90f, 0.004f, 0.08f, 0.04f, SoftTriangleTone);
            return Finish(buffer, 0.0005f, 0.01f, LockPeak);
        }

        /// <summary>Mallet-like C major arpeggio C5-E5-G5 with a faint C6 sparkle on the last note, 450 ms.</summary>
        private static float[] RenderLanded()
        {
            float[] buffer = Allocate(0.45f);
            AddTone(buffer, 0.000f, 0.450f, 523.25f, 523.25f, 0.70f, 0.005f, 0.16f, 0.06f, MalletTone);
            AddTone(buffer, 0.075f, 0.375f, 659.26f, 659.26f, 0.70f, 0.005f, 0.16f, 0.06f, MalletTone);
            AddTone(buffer, 0.150f, 0.300f, 783.99f, 783.99f, 0.80f, 0.005f, 0.20f, 0.07f, MalletTone);
            AddTone(buffer, 0.150f, 0.300f, 1046.50f, 1046.50f, 0.16f, 0.005f, 0.12f, 0.07f, PureTone);
            return Finish(buffer, 0.0005f, 0.04f, LandedPeak);
        }

        /// <summary>
        /// Mid-air collision, 1.0 s: white noise through a two-pole low-pass sweeping from bright to a dull rumble,
        /// a pitch-dropping sine thump (with a 2nd harmonic so it survives phone speakers) and a few debris crackles,
        /// then DC removal and gentle saturation for punch.
        /// </summary>
        private static float[] RenderCrash()
        {
            float[] buffer = Allocate(1.0f);
            int count = buffer.Length;
            uint rng = 0x6D2B79F5u;

            // Body: swept low-passed noise. The sweep lowers the noise level, so it is partly compensated.
            const float startCutoff = 3200f;
            const float endCutoff = 240f;
            float logCutoffRatio = Mathf.Log(endCutoff / startCutoff);
            int noiseAttack = Samples(0.004f);
            float lowPass1 = 0f;
            float lowPass2 = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float sweep = Mathf.Clamp01(t / 0.7f);
                float cutoff = startCutoff * Mathf.Exp(logCutoffRatio * sweep);
                float coefficient = OnePoleCoefficient(cutoff);
                lowPass1 += coefficient * (NextNoise(ref rng) - lowPass1);
                lowPass2 += coefficient * (lowPass1 - lowPass2);

                float compensation = Mathf.Pow(startCutoff / cutoff, 0.35f);
                float envelope = 0.7f * Mathf.Exp(-t / 0.12f) + 0.3f * Mathf.Exp(-t / 0.45f);
                if (i < noiseAttack) envelope *= SmoothStep01(i / (float)noiseAttack);
                buffer[i] += lowPass2 * envelope * compensation * 1.8f;
            }

            // Thump: 140 Hz falling quickly towards 48 Hz.
            int thumpAttack = Samples(0.003f);
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float frequency = 48f + 92f * Mathf.Exp(-t / 0.07f);
                float envelope = Mathf.Exp(-t / 0.2f);
                if (i < thumpAttack) envelope *= SmoothStep01(i / (float)thumpAttack);
                float angle = phase * TwoPi;
                buffer[i] += (Mathf.Sin(angle) + 0.35f * Mathf.Sin(2f * angle)) * envelope * 0.75f;

                phase += frequency / SampleRate;
                if (phase >= 1f) phase -= 1f;
            }

            // Debris: short band-passed crackles scattered through the first half second.
            for (int k = 0; k < 7; k++)
            {
                float when = 0.10f + 0.50f * Next01(ref rng);
                float amplitude = 0.25f + 0.25f * Next01(ref rng);
                float length = 0.04f + 0.05f * Next01(ref rng);
                uint seed = NextRandom(ref rng) | 1u;
                AddNoiseBurst(buffer, when, length, amplitude, 0.001f, 0.012f, 0.01f, 500f, 3500f, seed);
            }

            RemoveDc(buffer, 20f);
            SoftClip(buffer, 1.3f);
            return Finish(buffer, 0.001f, 0.15f, CrashPeak);
        }

        /// <summary>UI tick, 30 ms: a very short falling sine ping plus a breath of band-passed noise.</summary>
        private static float[] RenderClick()
        {
            float[] buffer = Allocate(0.03f);
            AddTone(buffer, 0f, 0.03f, 1900f, 1500f, 0.7f, 0.0006f, 0.007f, 0.008f, PureTone);
            AddNoiseBurst(buffer, 0f, 0.03f, 0.6f, 0.0004f, 0.004f, 0.008f, 1000f, 7000f, 0x1234567u);
            return Finish(buffer, 0.0003f, 0.004f, ClickPeak);
        }

        // ------------------------------------------------------------------------------------------------
        // Building blocks
        // ------------------------------------------------------------------------------------------------

        private static float[] Allocate(float seconds)
        {
            return new float[Mathf.Max(1, Mathf.CeilToInt(seconds * SampleRate))];
        }

        private static int Samples(float seconds)
        {
            return Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
        }

        /// <summary>
        /// Adds a note made of sine partials. Frequency glides exponentially from <paramref name="frequencyStart"/>
        /// to <paramref name="frequencyEnd"/>; amplitude = smooth attack * exponential decay (tau seconds, &lt;= 0
        /// for none) * smooth release that always reaches zero by the end of the note (or of the buffer).
        /// </summary>
        private static void AddTone(float[] buffer, float start, float length, float frequencyStart, float frequencyEnd,
            float amplitude, float attack, float decayTau, float release, Partial[] partials)
        {
            int first = Mathf.Clamp(Mathf.RoundToInt(start * SampleRate), 0, buffer.Length);
            int last = Mathf.Clamp(first + Mathf.RoundToInt(length * SampleRate), first, buffer.Length);
            int total = last - first;
            if (total <= 0 || frequencyStart <= 0f || frequencyEnd <= 0f) return;

            int attackSamples = Samples(attack);
            int releaseSamples = Samples(release);
            float glide = total > 1 && !Mathf.Approximately(frequencyStart, frequencyEnd)
                ? Mathf.Pow(frequencyEnd / frequencyStart, 1f / (total - 1))
                : 1f;
            const float maxFrequency = SampleRate * 0.45f;

            for (int p = 0; p < partials.Length; p++)
            {
                Partial partial = partials[p];
                float tau = decayTau * partial.DecayScale;
                float decay = tau > 0f ? Mathf.Exp(-1f / (tau * SampleRate)) : 1f;
                float level = amplitude * partial.Amplitude;
                float frequency = frequencyStart * partial.Ratio;
                float phase = 0f;

                for (int i = 0; i < total; i++)
                {
                    if (frequency < maxFrequency)
                    {
                        buffer[first + i] += Mathf.Sin(phase * TwoPi) * level * Gate(i, total, attackSamples, releaseSamples);
                    }

                    level *= decay;
                    phase += frequency / SampleRate;
                    if (phase >= 1f) phase -= Mathf.Floor(phase);
                    frequency *= glide;
                }
            }
        }

        /// <summary>
        /// Adds band-passed white noise (one-pole high cut, optional one-pole low cut) with the same envelope
        /// shape as <see cref="AddTone"/>. Deterministic for a given seed.
        /// </summary>
        private static void AddNoiseBurst(float[] buffer, float start, float length, float amplitude, float attack,
            float decayTau, float release, float lowCutHz, float highCutHz, uint seed)
        {
            int first = Mathf.Clamp(Mathf.RoundToInt(start * SampleRate), 0, buffer.Length);
            int last = Mathf.Clamp(first + Mathf.RoundToInt(length * SampleRate), first, buffer.Length);
            int total = last - first;
            if (total <= 0) return;

            uint state = seed != 0u ? seed : 0x9E3779B9u;
            int attackSamples = Samples(attack);
            int releaseSamples = Samples(release);
            float decay = decayTau > 0f ? Mathf.Exp(-1f / (decayTau * SampleRate)) : 1f;
            float highCoefficient = OnePoleCoefficient(highCutHz);
            float lowCoefficient = lowCutHz > 0f ? OnePoleCoefficient(lowCutHz) : 0f;
            float highCut = 0f;
            float lowCut = 0f;
            float level = amplitude;

            for (int i = 0; i < total; i++)
            {
                highCut += highCoefficient * (NextNoise(ref state) - highCut);
                lowCut += lowCoefficient * (highCut - lowCut);
                buffer[first + i] += (highCut - lowCut) * level * Gate(i, total, attackSamples, releaseSamples);
                level *= decay;
            }
        }

        /// <summary>Attack/release gain for sample <paramref name="index"/> of a note <paramref name="total"/> samples long.</summary>
        private static float Gate(int index, int total, int attackSamples, int releaseSamples)
        {
            float gain = 1f;
            if (index < attackSamples) gain = SmoothStep01(index / (float)attackSamples);
            int remaining = total - 1 - index;
            if (remaining < releaseSamples) gain *= SmoothStep01(remaining / (float)releaseSamples);
            return gain;
        }

        /// <summary>Raised-cosine ramp: 0 at x &lt;= 0, 1 at x &gt;= 1, zero slope at both ends.</summary>
        private static float SmoothStep01(float x)
        {
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;
            return 0.5f - 0.5f * Mathf.Cos(x * Mathf.PI);
        }

        /// <summary>Coefficient for y += a * (x - y), a one-pole low-pass at <paramref name="cutoffHz"/>.</summary>
        private static float OnePoleCoefficient(float cutoffHz)
        {
            float cutoff = Mathf.Clamp(cutoffHz, 1f, SampleRate * 0.49f);
            return 1f - Mathf.Exp(-TwoPi * cutoff / SampleRate);
        }

        /// <summary>One-pole high-pass (DC blocker) applied in place.</summary>
        private static void RemoveDc(float[] buffer, float cutoffHz)
        {
            float r = Mathf.Exp(-TwoPi * cutoffHz / SampleRate);
            float previousInput = 0f;
            float previousOutput = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                float input = buffer[i];
                float output = input - previousInput + r * previousOutput;
                previousInput = input;
                previousOutput = output;
                buffer[i] = output;
            }
        }

        /// <summary>tanh saturation in place; rounds off peaks so a loud mix keeps its punch after normalizing.</summary>
        private static void SoftClip(float[] buffer, float drive)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (float)Math.Tanh(drive * buffer[i]);
            }
        }

        /// <summary>Applies the safety edge fades (first and last sample become exactly 0) and normalizes to <paramref name="peak"/>.</summary>
        private static float[] Finish(float[] buffer, float fadeIn, float fadeOut, float peak)
        {
            int length = buffer.Length;

            int fadeInSamples = Mathf.Min(length, Samples(fadeIn));
            for (int i = 0; i < fadeInSamples; i++)
            {
                buffer[i] *= SmoothStep01(i / (float)fadeInSamples);
            }

            int fadeOutSamples = Mathf.Min(length, Samples(fadeOut));
            for (int i = 0; i < fadeOutSamples; i++)
            {
                buffer[length - 1 - i] *= SmoothStep01(i / (float)fadeOutSamples);
            }

            Normalize(buffer, peak);
            return buffer;
        }

        /// <summary>Scales the buffer so its largest absolute sample equals <paramref name="peak"/>; scrubs NaN/Infinity.</summary>
        private static void Normalize(float[] buffer, float peak)
        {
            float max = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                float value = buffer[i];
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    buffer[i] = 0f;
                    continue;
                }

                float magnitude = Mathf.Abs(value);
                if (magnitude > max) max = magnitude;
            }

            if (max < 1e-6f) return;

            float scale = Mathf.Clamp01(peak) / max;
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = Mathf.Clamp(buffer[i] * scale, -1f, 1f);
            }
        }

        // xorshift32: tiny, deterministic and allocation-free (System.Random is not needed here).
        private static uint NextRandom(ref uint state)
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        /// <summary>Uniform white noise in [-1, 1].</summary>
        private static float NextNoise(ref uint state)
        {
            return (NextRandom(ref state) >> 8) * (2f / 16777215f) - 1f;
        }

        /// <summary>Uniform value in [0, 1].</summary>
        private static float Next01(ref uint state)
        {
            return (NextRandom(ref state) >> 8) * (1f / 16777215f);
        }
    }
}
