using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Windows.Media;

namespace Pasziansz.Services
{
    /// <summary>
    /// Egyszerű, szintetizált hangeffektusok (kártya lerakása, húzás, hiba, győzelem).
    /// A hangok generált PCM hullámformákból készülnek, így nincs szükség külső
    /// audio-fájlokra. Az első használatkor WAV-fájl készül a temp mappába.
    /// </summary>
    public static class SoundPlayer
    {
        private enum Effect
        {
            Place,
            Draw,
            Invalid,
            Foundation,
            Win
        }

        private static readonly Dictionary<Effect, Uri> Cache = new Dictionary<Effect, Uri>();
        private static readonly object Sync = new object();
        private static MediaPlayer? _player;

        public static bool Enabled { get; set; } = true;

        /// <summary>0..1 közötti hangerő.</summary>
        public static double Volume { get; set; } = 0.6;

        public static void PlayCardPlace() => Play(Effect.Place);

        public static void PlayCardDraw() => Play(Effect.Draw);

        public static void PlayInvalid() => Play(Effect.Invalid);

        public static void PlayFoundation() => Play(Effect.Foundation);

        public static void PlayWin() => Play(Effect.Win);

        private static void Play(Effect effect)
        {
            if (!Enabled || Volume <= 0.001)
            {
                return;
            }

            try
            {
                Uri uri;
                lock (Sync)
                {
                    if (!Cache.TryGetValue(effect, out var cached))
                    {
                        cached = CreateWav(effect);
                        Cache[effect] = cached;
                    }

                    uri = cached;
                }

                _player ??= new MediaPlayer();
                _player.Close();
                _player.Open(uri);
                _player.Volume = Math.Clamp(Volume, 0, 1) * 0.9;
                _player.Play();
            }
            catch
            {
                // A hang nem kritikus: hiba esetén csendben folytatjuk.
            }
        }

        private static Uri CreateWav(Effect effect)
        {
            (double[] notes, double noteSeconds, double amplitude, bool square) recipe = effect switch
            {
                Effect.Draw => (new[] { 392.0, 523.25 }, 0.055, 0.28, false),
                Effect.Invalid => (new[] { 150.0, 118.0 }, 0.09, 0.22, true),
                Effect.Foundation => (new[] { 523.25, 659.25, 783.99 }, 0.06, 0.28, false),
                Effect.Win => (new[] { 523.25, 659.25, 783.99, 1046.5, 1318.5 }, 0.13, 0.32, false),
                _ => (new[] { 196.0, 261.63 }, 0.08, 0.30, false)
            };

            const int rate = 44100;
            int perNote = (int)(rate * recipe.noteSeconds);
            var samples = new short[perNote * recipe.notes.Length];

            for (int n = 0; n < recipe.notes.Length; n++)
            {
                int offset = n * perNote;
                for (int i = 0; i < perNote; i++)
                {
                    double t = (double)i / rate;
                    double envelope = Math.Exp(-9.0 * i / perNote);              // gyors lecsengés
                    double phase = 2.0 * Math.PI * recipe.notes[n] * t;
                    double wave = recipe.square
                        ? (Math.Sin(phase) >= 0 ? 1.0 : -1.0) * 0.5
                        : Math.Sin(phase) + 0.25 * Math.Sin(2 * phase);
                    samples[offset + i] = (short)(wave * envelope * recipe.amplitude * short.MaxValue);
                }
            }

            string path = Path.Combine(Path.GetTempPath(), "zold_pasziansz_" + effect.ToString().ToLowerInvariant() + ".wav");
            try
            {
                using var stream = File.Create(path);
                WriteWav(stream, samples, rate);
            }
            catch
            {
                // Ha nem írható a temp mappa, a lejátszás kimarad.
            }

            return new Uri(path, UriKind.Absolute);
        }

        private static void WriteWav(Stream stream, short[] samples, int rate)
        {
            using var writer = new BinaryWriter(stream);
            int dataBytes = samples.Length * 2;

            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);          // fmt chunk mérete
            writer.Write((short)1);    // PCM
            writer.Write((short)1);    // mono
            writer.Write(rate);
            writer.Write(rate * 2);    // byte/másodperc
            writer.Write((short)2);    // block align
            writer.Write((short)16);   // bit/minta
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);

            foreach (short sample in samples)
            {
                writer.Write(sample);
            }

            writer.Flush();
        }
    }
}
