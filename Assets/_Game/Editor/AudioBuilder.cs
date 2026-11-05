using System;
using System.IO;
using Game.Audio;
using Template.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Game audio (SPEC polish): Kenney CC0 sound effects (copied into Audio/Sfx) plus three sounds synthesized here,
    /// because no pack has them: the throw whoosh, the in-flight wind loop and the music loop (a calm C-major
    /// piece: pad, bass, plucked arpeggio, marimba melody, shaker, stereo echo; 16 bars at 84 BPM, seamless).
    /// Writes Configs/AudioConfig.asset with every clip. Deterministic; regenerated with force.
    /// </summary>
    internal static class AudioBuilder
    {
        private const string Folder = "Assets/_Game/Audio";
        private const string SfxFolder = Folder + "/Sfx";
        private const string GeneratedFolder = Folder + "/Generated";
        public const string ConfigPath = "Assets/_Game/Configs/AudioConfig.asset";

        public static AudioConfig Build(bool force)
        {
            EditorUtil.EnsureFolder(GeneratedFolder);
            var whoosh = Generated("Whoosh", force, 44100, 1, Whoosh);
            var wind = Generated("WindLoop", force, 32000, 1, WindLoop);
            var music = Generated("Music_Paper", force, 32000, 2, Music);

            var config = EditorUtil.LoadOrCreate<AudioConfig>(ConfigPath);
            var so = new SerializedObject(config);
            void Set(string field, AudioClip clip)
            {
                var p = so.FindProperty(field);
                if (clip == null) Log.Warn($"Audio: {field} has no clip.");
                p.objectReferenceValue = clip;
            }
            Set("_music", music);
            Set("_throw", whoosh);
            Set("_wind", wind);
            Set("_click", Sfx("click"));
            Set("_gem", Sfx("gem"));
            Set("_ring", Sfx("ring"));
            Set("_crash", Sfx("crash"));
            Set("_crashPaper", Sfx("crash_paper"));
            Set("_land", Sfx("land"));
            Set("_star", Sfx("star"));
            Set("_levelComplete", Sfx("level_complete"));
            Set("_gameOver", Sfx("game_over"));
            Set("_unlock", Sfx("unlock"));
            Set("_checkIn", Sfx("checkin"));
            Set("_respawn", Sfx("respawn"));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static AudioClip Sfx(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxFolder}/{name}.ogg");
            if (clip != null)
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath($"{SfxFolder}/{name}.ogg");
                var settings = importer.defaultSampleSettings;
                if (settings.loadType != AudioClipLoadType.DecompressOnLoad || !importer.forceToMono)
                {
                    settings.loadType = AudioClipLoadType.DecompressOnLoad; // short, played often: no decode hitch
                    importer.defaultSampleSettings = settings;
                    importer.forceToMono = true;
                    importer.SaveAndReimport();
                }
            }
            return clip;
        }

        // ---------- WAV writing ----------
        private static AudioClip Generated(string name, bool force, int rate, int channels, Func<int, float[][]> render)
        {
            var path = $"{GeneratedFolder}/{name}.wav";
            if (force || !File.Exists(path))
            {
                var data = render(rate);
                WriteWav(path, data, rate);
                AssetDatabase.ImportAsset(path);
            }
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            var isMusic = name.StartsWith("Music");
            settings.loadType = isMusic ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = isMusic ? 0.5f : 0.7f;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = isMusic;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static void WriteWav(string path, float[][] channels, int rate)
        {
            var n = channels[0].Length;
            var c = channels.Length;
            using var w = new BinaryWriter(File.Create(path));
            w.Write("RIFF".ToCharArray());
            w.Write(36 + n * c * 2);
            w.Write("WAVEfmt ".ToCharArray());
            w.Write(16);
            w.Write((short)1);
            w.Write((short)c);
            w.Write(rate);
            w.Write(rate * c * 2);
            w.Write((short)(c * 2));
            w.Write((short)16);
            w.Write("data".ToCharArray());
            w.Write(n * c * 2);
            for (var i = 0; i < n; i++)
                for (var ch = 0; ch < c; ch++)
                    w.Write((short)Mathf.RoundToInt(Mathf.Clamp(channels[ch][i], -1f, 1f) * 32767f));
        }

        private static void Normalize(float[][] channels, float peak)
        {
            var max = 0f;
            foreach (var ch in channels) foreach (var v in ch) max = Mathf.Max(max, Mathf.Abs(v));
            if (max <= 0f) return;
            var g = peak / max;
            foreach (var ch in channels) for (var i = 0; i < ch.Length; i++) ch[i] *= g;
        }

        // ---------- Whoosh: band-passed noise sweeping up then down, paper-light ----------
        private static float[][] Whoosh(int rate)
        {
            var n = (int)(0.55f * rate);
            var o = new float[n];
            var rng = new System.Random(7);
            float low = 0f, band = 0f;
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)n;
                var cutoff = Mathf.Lerp(500f, 2600f, Mathf.Sin(t * Mathf.PI * 0.85f));
                var f = 2f * Mathf.Sin(Mathf.PI * cutoff / rate);
                var x = (float)(rng.NextDouble() * 2.0 - 1.0);
                // State-variable filter, band output.
                low += f * band;
                var high = x - low - 0.6f * band;
                band += f * high;
                var env = Mathf.Pow(Mathf.Sin(Mathf.Clamp01(t * 1.15f) * Mathf.PI), 1.6f);
                o[i] = band * env;
            }
            var data = new[] { o };
            Normalize(data, 0.8f);
            return data;
        }

        // ---------- Wind loop: soft low-passed noise with slow gusts, seamless ----------
        private static float[][] WindLoop(int rate)
        {
            const float seconds = 4f;
            var n = (int)(seconds * rate);
            var tail = rate / 2;
            var buf = new float[n + tail];
            var rng = new System.Random(11);
            float lp1 = 0f, lp2 = 0f;
            for (var i = 0; i < buf.Length; i++)
            {
                var t = i / (float)rate;
                var x = (float)(rng.NextDouble() * 2.0 - 1.0);
                var cutoff = 380f + 220f * Mathf.Sin(t * Mathf.PI * 2f / seconds) + 90f * Mathf.Sin(t * 5.3f);
                var a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / rate);
                lp1 += a * (x - lp1);
                lp2 += a * (lp1 - lp2);
                var gust = 0.75f + 0.25f * Mathf.Sin(t * Mathf.PI * 2f / seconds * 2f + 1f);
                buf[i] = lp2 * gust;
            }
            // Crossfade the tail into the start so the loop point is inaudible.
            var o = new float[n];
            for (var i = 0; i < n; i++) o[i] = buf[i];
            for (var i = 0; i < tail; i++)
            {
                var k = i / (float)tail;
                o[i] = buf[n + i] * (1f - k) + buf[i] * k;
            }
            var data = new[] { o };
            Normalize(data, 0.7f);
            return data;
        }

        // ---------- Music ----------
        private const float Bpm = 84f;

        private static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        private struct Note
        {
            public int Bar, Eighth, Midi, Length;
            public Note(int bar, int eighth, int midi, int length) { Bar = bar; Eighth = eighth; Midi = midi; Length = length; }
        }

        private static float[][] Music(int rate)
        {
            var beat = 60f / Bpm;
            var bar = beat * 4f;
            const int bars = 16;
            var length = bar * bars;
            var tail = 3f;
            var n = (int)((length + tail) * rate);
            var l = new float[n];
            var r = new float[n];

            // C  Am  F  G  C  Em  F  G (twice)
            int[][] chords =
            {
                new[] { 60, 64, 67 }, new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 55, 59, 62 },
                new[] { 60, 64, 67 }, new[] { 52, 55, 59 }, new[] { 53, 57, 60 }, new[] { 55, 59, 62 },
            };
            int[] roots = { 48, 45, 41, 43, 48, 40, 41, 43 };

            for (var b = 0; b < bars; b++)
            {
                var chord = chords[b % 8];
                var t0 = b * bar;
                // Pad: soft sines, slow swell, held a little past the bar.
                foreach (var m in chord) Tone(l, r, rate, t0, bar + 0.6f, Hz(m), 0.055f, 0.7f, 0.9f, Pad, 0f);
                // Bass: beat 1 (long) and beat 3.
                Tone(l, r, rate, t0, beat * 1.8f, Hz(roots[b % 8]), 0.16f, 0.02f, 0.3f, Bass, 0f);
                Tone(l, r, rate, t0 + beat * 2f, beat * 1.2f, Hz(roots[b % 8]), 0.12f, 0.02f, 0.3f, Bass, 0f);
                // Plucked arpeggio in eighths, panned left/right.
                int[] pattern = { 0, 1, 2, 1, 0, 1, 2, 1 };
                for (var e = 0; e < 8; e++)
                {
                    if (b < 1 && e < 4) continue; // gentle start
                    var m = chord[pattern[e]] + 12;
                    var accent = e % 2 == 0 ? 1f : 0.7f;
                    Tone(l, r, rate, t0 + e * beat * 0.5f, beat * 1.2f, Hz(m), 0.045f * accent, 0.004f, 0.6f, Pluck, e % 2 == 0 ? -0.4f : 0.4f);
                }
                // Shaker on the off-beat sixteenths (from bar 3).
                if (b >= 2)
                    for (var s = 0; s < 16; s++)
                        if (s % 2 == 1) Shaker(l, r, rate, t0 + s * beat * 0.25f, s % 4 == 3 ? 0.018f : 0.01f, s);
            }

            // Marimba melody over the second half (and an echo of it in bars 5-8).
            Note[] melody =
            {
                new Note(8, 0, 76, 2), new Note(8, 2, 79, 2), new Note(8, 4, 81, 3),
                new Note(9, 0, 79, 2), new Note(9, 2, 76, 2), new Note(9, 4, 72, 4),
                new Note(10, 0, 81, 2), new Note(10, 2, 84, 2), new Note(10, 4, 81, 2), new Note(10, 6, 79, 2),
                new Note(11, 0, 79, 3), new Note(11, 3, 74, 1), new Note(11, 4, 76, 4),
                new Note(12, 0, 76, 2), new Note(12, 2, 79, 2), new Note(12, 4, 84, 3),
                new Note(13, 0, 83, 2), new Note(13, 2, 79, 2), new Note(13, 4, 76, 4),
                new Note(14, 0, 81, 2), new Note(14, 2, 79, 2), new Note(14, 4, 77, 2), new Note(14, 6, 76, 2),
                new Note(15, 0, 74, 6),
                new Note(4, 4, 76, 2), new Note(4, 6, 79, 2), new Note(5, 0, 76, 4),
                new Note(6, 4, 81, 2), new Note(6, 6, 79, 2), new Note(7, 0, 74, 6),
            };
            foreach (var note in melody)
            {
                var t = note.Bar * bar + note.Eighth * beat * 0.5f;
                Tone(l, r, rate, t, note.Length * beat * 0.5f + 0.4f, Hz(note.Midi), 0.11f, 0.004f, 0.45f, Marimba, 0.15f);
            }

            // Stereo echo (dotted eighth left, quarter right), gentle feedback.
            Echo(l, rate, beat * 0.75f, 0.28f, 0.22f);
            Echo(r, rate, beat, 0.28f, 0.22f);

            // Seamless loop: fold the tail back onto the start, then cut to the exact loop length.
            var loop = (int)(length * rate);
            var ol = new float[loop];
            var or = new float[loop];
            for (var i = 0; i < loop; i++) { ol[i] = l[i]; or[i] = r[i]; }
            for (var i = loop; i < n; i++) { ol[(i - loop) % loop] += l[i]; or[(i - loop) % loop] += r[i]; }

            // Warm master: one-pole low-pass, soft clip.
            LowPass(ol, rate, 7000f);
            LowPass(or, rate, 7000f);
            var data = new[] { ol, or };
            Normalize(data, 0.7f);
            for (var c = 0; c < 2; c++)
                for (var i = 0; i < loop; i++) data[c][i] = (float)Math.Tanh(data[c][i] * 1.2f) / 1.05f;
            return data;
        }

        private delegate float Timbre(float phase, float t);

        private static float Pad(float p, float t) => Mathf.Sin(p) + 0.25f * Mathf.Sin(2f * p) + 0.08f * Mathf.Sin(3f * p);
        private static float Bass(float p, float t) => (float)Math.Tanh(1.6f * (Mathf.Sin(p) + 0.3f * Mathf.Sin(2f * p))) * Mathf.Exp(-t * 1.6f);
        private static float Pluck(float p, float t) => (Mathf.Sin(p) + 0.3f * Mathf.Sin(2f * p) * Mathf.Exp(-t * 18f) + 0.15f * Mathf.Sin(3f * p)) * Mathf.Exp(-t * 6f);
        private static float Marimba(float p, float t) => (Mathf.Sin(p) + 0.45f * Mathf.Sin(4f * p) * Mathf.Exp(-t * 25f)) * Mathf.Exp(-t * 3.2f);

        /// <summary>Adds one note: attack / release envelope, timbre, pan (-1 left .. 1 right), slight vibrato for long notes.</summary>
        private static void Tone(float[] l, float[] r, int rate, float start, float duration, float freq, float amp,
            float attack, float release, Timbre timbre, float pan)
        {
            var i0 = (int)(start * rate);
            var count = (int)((duration + release) * rate);
            var gl = amp * Mathf.Sqrt(0.5f * (1f - pan));
            var gr = amp * Mathf.Sqrt(0.5f * (1f + pan));
            var phase = 0f;
            for (var k = 0; k < count && i0 + k < l.Length; k++)
            {
                var t = k / (float)rate;
                var env = Mathf.Clamp01(t / Mathf.Max(attack, 0.001f));
                if (t > duration) env *= Mathf.Clamp01(1f - (t - duration) / Mathf.Max(release, 0.001f));
                var vibrato = duration > 1f ? 1f + 0.002f * Mathf.Sin(t * 2f * Mathf.PI * 5f) : 1f;
                phase += 2f * Mathf.PI * freq * vibrato / rate;
                var v = timbre(phase, t) * env;
                l[i0 + k] += v * gl;
                r[i0 + k] += v * gr;
            }
        }

        private static void Shaker(float[] l, float[] r, int rate, float start, float amp, int seed)
        {
            var rng = new System.Random(1000 + seed);
            var i0 = (int)(start * rate);
            var count = (int)(0.07f * rate);
            var prev = 0f;
            for (var k = 0; k < count && i0 + k < l.Length; k++)
            {
                var x = (float)(rng.NextDouble() * 2.0 - 1.0);
                var hp = x - prev; // crude high-pass: bright, hissy
                prev = x;
                var t = k / (float)rate;
                var v = hp * Mathf.Exp(-t * 60f) * amp;
                l[i0 + k] += v * 0.8f;
                r[i0 + k] += v;
            }
        }

        private static void Echo(float[] x, int rate, float delay, float feedback, float mix)
        {
            var d = (int)(delay * rate);
            var wet = new float[x.Length];
            for (var i = d; i < x.Length; i++) wet[i] = (x[i - d] + wet[i - d] * feedback) * 1f;
            for (var i = 0; i < x.Length; i++) x[i] += wet[i] * mix;
        }

        private static void LowPass(float[] x, int rate, float cutoff)
        {
            var a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / rate);
            var y = 0f;
            for (var i = 0; i < x.Length; i++)
            {
                y += a * (x[i] - y);
                x[i] = y;
            }
        }
    }
}
