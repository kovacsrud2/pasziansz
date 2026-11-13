using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pasziansz.Services
{
    /// <summary>A kártyahátlap lehetséges stílusai.</summary>
    public enum CardBackStyle
    {
        Green,
        Blue,
        Red
    }

    /// <summary>
    /// A játék felhasználói beállításai. A program a felhasználói profil mappában
    /// (pl. %APPDATA%\ZoldPasziansz\settings.json) tárolja őket.
    /// </summary>
    public sealed class Settings
    {
        /// <summary>Egyszerre 1 vagy 3 lapot húzunk-e a talonból.</summary>
        public int DrawCount { get; set; } = 1;

        /// <summary>Engedélyezett-e a gyorsbillentyűvel való vonalhúzás (1-7).</summary>
        public bool EnableHotkeys { get; set; } = true;

        /// <summary>Automatikus játék: a felvehető lapok automatikus felpakolása.</summary>
        public bool AutoPlayAvailable { get; set; } = false;

        public CardBackStyle CardBackStyle { get; set; } = CardBackStyle.Green;

        /// <summary>Az ablak mérete és pozíciója.</summary>
        public double WindowWidth { get; set; } = 1180;
        public double WindowHeight { get; set; } = 800;
        public double? WindowLeft { get; set; }
        public double? WindowTop { get; set; }
        public bool WindowMaximized { get; set; }

        public double SoundVolume { get; set; } = 0.6;
        public bool SoundEnabled { get; set; } = true;

        /// <summary>Az éppen futó beállítás-példány.</summary>
        [JsonIgnore]
        public static Settings Current { get; set; } = new Settings();

        public Settings Clone() => (Settings)MemberwiseClone();
    }

    /// <summary>
    /// A beállítások betöltése és mentése JSON formátumban. Minden hiba esetén
    /// csendben az alapértelmezett értékekkel tér vissza, hogy a játék mindig elinduljon.
    /// </summary>
    public static class SettingsStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static string SettingsFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZoldPasziansz");

        public static string SettingsPath => Path.Combine(SettingsFolder, "settings.json");

        public static Settings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var loaded = JsonSerializer.Deserialize<Settings>(json, JsonOptions);
                    if (loaded != null)
                    {
                        Normalize(loaded);
                        Settings.Current = loaded;
                        return loaded;
                    }
                }
            }
            catch
            {
                // Sérült vagy olvashatatlan beállításfájl: visszaesünk az alapértékekre.
            }

            Settings.Current = new Settings();
            return Settings.Current;
        }

        public static void Save(Settings settings)
        {
            try
            {
                Directory.CreateDirectory(SettingsFolder);
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // A mentés hibája nem akadályozhatja a játékot.
            }
        }

        /// <summary>A betöltött értékek ésszerű tartományba szorítása.</summary>
        private static void Normalize(Settings s)
        {
            if (s.DrawCount != 1 && s.DrawCount != 3)
            {
                s.DrawCount = 1;
            }

            if (s.WindowWidth < 900) s.WindowWidth = 1180;
            if (s.WindowHeight < 650) s.WindowHeight = 800;
            if (s.WindowLeft is double left && (double.IsNaN(left) || double.IsInfinity(left))) s.WindowLeft = null;
            if (s.WindowTop is double top && (double.IsNaN(top) || double.IsInfinity(top))) s.WindowTop = null;

            s.SoundVolume = Math.Clamp(s.SoundVolume, 0, 1);
        }
    }
}
