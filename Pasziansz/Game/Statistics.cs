using System;
using System.IO;
using System.Text.Json;

namespace Pasziansz.Game
{
    /// <summary>
    /// A játékstatisztika (lejátszott/megnyert játszmák, legjobb idő, legjobb pontszám).
    /// A felhasználói profil mappában tárolódik.
    /// </summary>
    public sealed class Statistics
    {
        public int GamesStarted { get; set; }

        public int GamesWon { get; set; }

        public int WinsWithOneCardDraw { get; set; }

        public int WinsWithThreeCardDraw { get; set; }

        public int BestScore { get; set; }

        public int BestTimeSeconds { get; set; }

        public int FewestMovesInWin { get; set; }

        public DateTime? LastWinDate { get; set; }

        /// <summary>Megnyert játszmák aránya százalékban.</summary>
        public double WinRate => GamesStarted == 0 ? 0 : 100.0 * GamesWon / GamesStarted;

        public string BestTimeDisplay => BestTimeSeconds <= 0
            ? "–"
            : TimeSpan.FromSeconds(BestTimeSeconds).ToString(@"mm\:ss");

        public string FewestMovesDisplay => FewestMovesInWin <= 0 ? "–" : FewestMovesInWin.ToString();

        public string BestScoreDisplay => BestScore <= 0 ? "–" : BestScore.ToString();

        public void RegisterGameStarted() => GamesStarted++;

        /// <summary>Egy megnyert játszma rögzítése.</summary>
        public bool RegisterWin(int score, int elapsedSeconds, int moves, int drawCount)
        {
            GamesWon++;
            if (drawCount == 1)
            {
                WinsWithOneCardDraw++;
            }
            else
            {
                WinsWithThreeCardDraw++;
            }

            bool newRecord = false;
            if (score > BestScore)
            {
                BestScore = score;
                newRecord = true;
            }

            if (BestTimeSeconds == 0 || elapsedSeconds < BestTimeSeconds)
            {
                BestTimeSeconds = elapsedSeconds;
                newRecord = true;
            }

            if (FewestMovesInWin == 0 || moves < FewestMovesInWin)
            {
                FewestMovesInWin = moves;
                newRecord = true;
            }

            LastWinDate = DateTime.Now;
            return newRecord;
        }

        public void Reset()
        {
            GamesStarted = 0;
            GamesWon = 0;
            WinsWithOneCardDraw = 0;
            WinsWithThreeCardDraw = 0;
            BestScore = 0;
            BestTimeSeconds = 0;
            FewestMovesInWin = 0;
            LastWinDate = null;
        }

        public Statistics Clone() => (Statistics)MemberwiseClone();
    }

    /// <summary>A statisztika betöltése és mentése.</summary>
    public static class StatisticsStore
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public static string FilePath =>
            Path.Combine(Services.SettingsStore.SettingsFolder, "statistics.json");

        public static Statistics Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var stats = JsonSerializer.Deserialize<Statistics>(File.ReadAllText(FilePath), Options);
                    if (stats != null)
                    {
                        return stats;
                    }
                }
            }
            catch
            {
                // sérült fájl: új statisztikával indulunk
            }

            return new Statistics();
        }

        public static void Save(Statistics statistics)
        {
            try
            {
                Directory.CreateDirectory(Services.SettingsStore.SettingsFolder);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(statistics, Options));
            }
            catch
            {
                // nem kritikus
            }
        }
    }
}
