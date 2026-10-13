using UnityEngine;

namespace FRC6874
{
    /// <summary>
    /// Generates every <see cref="GameSound"/> procedurally (44.1 kHz mono), so the project ships
    /// without audio assets. Oscillators are band-limited (PolyBLEP) and every sound has a short
    /// attack and release so nothing clicks. Clips are normalized; loudness is set per sound by
    /// <see cref="GameAudioSynth"/>.
    /// </summary>
    public static class SoundSynthesizer
    {
        public const int SampleRate = 44100;
        const float TwoPi = Mathf.PI * 2f;

        /// <summary>Builds a ready-to-play clip for <paramref name="sound"/>.</summary>
        public static AudioClip CreateClip(GameSound sound)
        {
            float[] data = Generate(sound);
            Normalize(data, 0.9f);
            AudioClip clip = AudioClip.Create("FRC6874 " + sound, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Raw samples for <paramref name="sound"/> (not normalized).</summary>
        public static float[] Generate(GameSound sound)
        {
            switch (sound)
            {
                case GameSound.MatchStart: return Horn();
                case GameSound.AutoEnd: return Buzzer(0.75f, 110f, 0.07f);
                case GameSound.TeleopStart: return Bell();
                case GameSound.EndgameWarning: return Beeps(3, 1318.5f, 0.13f, 0.075f);
                case GameSound.MatchEnd: return Buzzer(1.7f, 92f, 0.35f);
                case GameSound.Score: return Blip();
                case GameSound.Shoot: return Thunk();
                case GameSound.Intake: return Whoosh();
                case GameSound.Climb: return Whine();
                case GameSound.UIClick: return Tick();
                case GameSound.Countdown: return Beeps(1, 880f, 0.2f, 0f);
                default: return Beeps(1, 660f, 0.12f, 0f);
            }
        }

        // ---- Sounds -------------------------------------------------------------------------------

        /// <summary>Arena start horn: a rich F-major saw/square chord with a brassy attack (~1.15 s).</summary>
        static float[] Horn()
        {
            const float duration = 1.15f;
            float[] data = Buffer(duration);
            float[] notes = { 174.61f, 220.00f, 261.63f, 349.23f, 440.00f };
            float[] detune = { 0.997f, 1.003f };
            var phases = new float[notes.Length * detune.Length];
            float low = 0f, low2 = 0f;

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float vibrato = 1f + 0.004f * Mathf.Sin(TwoPi * 5.5f * t) * Mathf.Clamp01(t / 0.3f);
                float scoop = 1f - 0.035f * Mathf.Exp(-t * 20f);   // pitch settles up into the note like a horn
                float sample = 0f;
                int p = 0;
                for (int n = 0; n < notes.Length; n++)
                {
                    for (int d = 0; d < detune.Length; d++, p++)
                    {
                        float step = notes[n] * detune[d] * vibrato * scoop / SampleRate;
                        sample += 0.6f * Saw(phases[p], step) + 0.4f * Square(phases[p], step);
                        phases[p] = Wrap(phases[p] + step);
                    }
                }
                sample /= phases.Length;

                // Brass-like brightness: the filter opens over the first 120 ms.
                float cutoff = 1100f + 2600f * Mathf.Clamp01(t / 0.12f);
                float k = OnePole(cutoff);
                low += k * (sample - low);
                low2 += k * (low - low2);
                data[i] = low2 * Envelope(t, duration, 0.035f, 0.3f);
            }
            return data;
        }

        /// <summary>Classic buzzer: two detuned low squares beating against each other plus an octave saw.</summary>
        static float[] Buzzer(float duration, float frequency, float release)
        {
            float[] data = Buffer(duration);
            float p1 = 0f, p2 = 0f, p3 = 0f, low = 0f;
            float k = OnePole(2400f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float s1 = frequency / SampleRate;
                float s2 = frequency * 1.059f / SampleRate;
                float s3 = frequency * 2.007f / SampleRate;
                float sample = 0.45f * Square(p1, s1) + 0.35f * Square(p2, s2) + 0.25f * Saw(p3, s3);
                p1 = Wrap(p1 + s1);
                p2 = Wrap(p2 + s2);
                p3 = Wrap(p3 + s3);
                low += k * (sample - low);
                data[i] = low * Envelope(t, duration, 0.012f, release);
            }
            return data;
        }

        /// <summary>Bright bell "ding" from inharmonic, individually decaying partials.</summary>
        static float[] Bell()
        {
            const float duration = 1.6f;
            const float fundamental = 987.77f;   // B5
            float[] data = Buffer(duration);
            float[] ratios = { 1f, 2.32f, 4.25f, 6.63f, 9.38f };
            float[] amplitudes = { 1f, 0.6f, 0.35f, 0.2f, 0.1f };
            float[] decays = { 2.2f, 3.4f, 5.2f, 7.5f, 10f };

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float sample = 0f;
                for (int n = 0; n < ratios.Length; n++)
                    sample += amplitudes[n] * Mathf.Exp(-decays[n] * t) * Mathf.Sin(TwoPi * fundamental * ratios[n] * t);
                data[i] = sample * Envelope(t, duration, 0.002f, 0.08f);
            }
            return data;
        }

        /// <summary><paramref name="count"/> clean beeps (sine plus a little third harmonic).</summary>
        static float[] Beeps(int count, float frequency, float beepLength, float gap)
        {
            float duration = count * beepLength + (count - 1) * gap;
            float[] data = Buffer(duration);
            float period = beepLength + gap;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float local = count > 1 ? t % period : t;
                if (local > beepLength) continue;
                float tone = Mathf.Sin(TwoPi * frequency * t) + 0.2f * Mathf.Sin(TwoPi * frequency * 3f * t);
                data[i] = tone * Envelope(local, beepLength, 0.005f, 0.04f);
            }
            return data;
        }

        /// <summary>Two quick rising tones: a bright "bli-ip".</summary>
        static float[] Blip()
        {
            const float duration = 0.17f;
            float[] data = Buffer(duration);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float first = t < 0.075f
                    ? Envelope(t, 0.075f, 0.003f, 0.02f) * Tone(1567.98f, t)
                    : 0f;
                float t2 = t - 0.055f;
                float second = t2 > 0f
                    ? Envelope(t2, duration - 0.055f, 0.003f, 0.06f) * Mathf.Exp(-t2 * 14f) * Tone(2093.0f, t2)
                    : 0f;
                data[i] = 0.8f * first + second;
            }
            return data;
        }

        /// <summary>Short low "thunk": a sine kick with a fast pitch drop and a puff of filtered noise.</summary>
        static float[] Thunk()
        {
            const float duration = 0.2f;
            float[] data = Buffer(duration);
            var random = new System.Random(6874);
            float phase = 0f, low = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float frequency = 55f + 150f * Mathf.Exp(-t * 28f);
                phase = Wrap(phase + frequency / SampleRate);
                float kick = Mathf.Sin(TwoPi * phase) * Mathf.Exp(-t * 16f);

                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                low += OnePole(300f + 900f * Mathf.Exp(-t * 30f)) * (noise - low);
                float puff = low * Mathf.Exp(-t * 38f) * 0.9f;

                data[i] = (kick + puff) * Envelope(t, duration, 0.002f, 0.03f);
            }
            return data;
        }

        /// <summary>Soft whoosh: band-passed noise whose center sweeps up and back down.</summary>
        static float[] Whoosh()
        {
            const float duration = 0.32f;
            float[] data = Buffer(duration);
            var random = new System.Random(1234);
            float low = 0f, floor = 0f;
            float floorK = OnePole(260f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float x = t / duration;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                float cutoff = 700f + 2200f * Mathf.Sin(Mathf.PI * x);
                low += OnePole(cutoff) * (noise - low);
                floor += floorK * (low - floor);
                float band = low - floor;
                float shape = Mathf.Pow(Mathf.Sin(Mathf.PI * x), 1.5f);
                data[i] = band * shape * Envelope(t, duration, 0.01f, 0.03f);
            }
            return data;
        }

        /// <summary>Rising motor whine for the climber.</summary>
        static float[] Whine()
        {
            const float duration = 1.6f;
            float[] data = Buffer(duration);
            float phase = 0f, low = 0f;
            float k = OnePole(2600f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float x = t / duration;
                float frequency = (180f + 420f * x * x * (3f - 2f * x)) * (1f + 0.01f * Mathf.Sin(TwoPi * 11f * t));
                float step = frequency / SampleRate;
                float sample = 0.5f * Saw(phase, step) + 0.3f * Mathf.Sin(TwoPi * phase * 2f) + 0.15f * Mathf.Sin(TwoPi * phase * 3f);
                phase = Wrap(phase + step);
                low += k * (sample - low);
                data[i] = low * Envelope(t, duration, 0.12f, 0.25f);
            }
            return data;
        }

        /// <summary>Tiny UI tick.</summary>
        static float[] Tick()
        {
            const float duration = 0.04f;
            float[] data = Buffer(duration);
            var random = new System.Random(42);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float tone = Mathf.Sin(TwoPi * 2400f * t) * Mathf.Exp(-t * 170f);
                float noise = (float)(random.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 400f) * 0.3f;
                data[i] = (tone + noise) * Envelope(t, duration, 0.0008f, 0.01f);
            }
            return data;
        }

        // ---- Building blocks -----------------------------------------------------------------------

        static float[] Buffer(float seconds) => new float[Mathf.Max(1, Mathf.CeilToInt(seconds * SampleRate))];

        /// <summary>Smooth attack and release (smoothstep ramps) so sounds start and end without clicks.</summary>
        static float Envelope(float t, float duration, float attack, float release)
        {
            float a = attack > 0f ? Mathf.Clamp01(t / attack) : 1f;
            float r = release > 0f ? Mathf.Clamp01((duration - t) / release) : 1f;
            return a * a * (3f - 2f * a) * r * r * (3f - 2f * r);
        }

        static float Tone(float frequency, float t) =>
            Mathf.Sin(TwoPi * frequency * t) + 0.3f * Mathf.Sin(TwoPi * frequency * 2f * t);

        static float Wrap(float phase) => phase >= 1f ? phase - Mathf.Floor(phase) : phase;

        /// <summary>One-pole low-pass coefficient for a cutoff in Hz.</summary>
        static float OnePole(float cutoff) => 1f - Mathf.Exp(-TwoPi * cutoff / SampleRate);

        static float PolyBlep(float t, float dt)
        {
            if (dt <= 0f) return 0f;
            if (t < dt)
            {
                t /= dt;
                return t + t - t * t - 1f;
            }
            if (t > 1f - dt)
            {
                t = (t - 1f) / dt;
                return t * t + t + t + 1f;
            }
            return 0f;
        }

        static float Saw(float phase, float step) => 2f * phase - 1f - PolyBlep(phase, step);

        static float Square(float phase, float step)
        {
            float value = phase < 0.5f ? 1f : -1f;
            value += PolyBlep(phase, step);
            value -= PolyBlep(Wrap(phase + 0.5f), step);
            return value;
        }

        static void Normalize(float[] data, float peak)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
            if (max < 1e-6f) return;
            float gain = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
        }
    }
}
