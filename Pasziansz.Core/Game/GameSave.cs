using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pasziansz.Models;

namespace Pasziansz.Game
{
    /// <summary>Egy rakás a mentett fájlban: a lapok kódjai és a lefordított lapok száma.</summary>
    public sealed class SavedPile
    {
        /// <summary>A lapok kódjai alulról felfelé, pl. <c>"AS"</c>, <c>"TH"</c>, <c>"KD"</c>.</summary>
        public List<string> Cards { get; set; } = new();

        public int FaceDown { get; set; }
    }

    /// <summary>
    /// Egy félbehagyott parti teljes állapota. A lapokat kártyakódokként tároljuk
    /// (pl. <c>"AS"</c> = ász pikk), így a fájl olvasható és stabil marad.
    /// </summary>
    public sealed class SavedGame
    {
        /// <summary>Formátum-verzió: a betöltés ebből tudja, mit várhat.</summary>
        public int Version { get; set; } = 1;

        public DateTime SavedAt { get; set; }

        public int DrawCount { get; set; } = 1;

        public int Score { get; set; }

        public int Moves { get; set; }

        public int Recycles { get; set; }

        /// <summary>A parti kezdete (az eltelt idő számításához).</summary>
        public DateTime StartedAt { get; set; }

        /// <summary>A hét oszlop, a 0. oszloptól.</summary>
        public List<SavedPile> Tableau { get; set; } = new();

        /// <summary>A húzópakli (alulról felfelé).</summary>
        public SavedPile Stock { get; set; } = new();

        /// <summary>A talon (alulról felfelé).</summary>
        public SavedPile Waste { get; set; } = new();

        /// <summary>A négy gyűjtőhely, a 0. gyűjtőhelytől (♣ ♦ ♥ ♠).</summary>
        public List<SavedPile> Foundation { get; set; } = new();

        /// <summary>A mentett állapot alapján felépített parti a játéknak.</summary>
        public void ApplyTo(KlondikeGame game)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            var tableau = new List<KlondikeGame.PileState>(7);
            for (int i = 0; i < Tableau.Count; i++)
            {
                tableau.Add(ToPileState(PileRef.Tableau(i), Tableau[i]));
            }

            var foundation = new List<KlondikeGame.PileState>(4);
            for (int i = 0; i < Foundation.Count; i++)
            {
                foundation.Add(ToPileState(PileRef.Foundation(i), Foundation[i]));
            }

            game.LoadState(
                DrawCount,
                Score,
                Moves,
                Recycles,
                StartedAt == default ? DateTime.Now : StartedAt,
                tableau,
                foundation,
                ToPileState(PileRef.Stock(), Stock),
                ToPileState(PileRef.Waste(), Waste));
        }

        private static KlondikeGame.PileState ToPileState(PileRef reference, SavedPile pile) =>
            new KlondikeGame.PileState(reference, ParseCards(pile.Cards).ToList(), pile.FaceDown);

        /// <summary>Kártyakód (pl. <c>"AS"</c>, <c>"TH"</c>) átalakítása lappá.</summary>
        public static Card ParseCard(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length < 2)
            {
                throw new FormatException($"Érvénytelen kártyakód: „{code}”.");
            }

            code = code.Trim().ToUpperInvariant();
            string rankPart = code[..^1];

            Suit suit = code[^1] switch
            {
                'C' => Suit.Clubs,
                'D' => Suit.Diamonds,
                'H' => Suit.Hearts,
                'S' => Suit.Spades,
                _ => throw new FormatException($"Ismeretlen szín a kódban: „{code}”.")
            };

            int rank = rankPart switch
            {
                "A" => 1,
                "J" => 11,
                "Q" => 12,
                "K" => 13,
                "T" => 10,
                _ => int.TryParse(rankPart, out int parsed) && parsed >= 1 && parsed <= 13
                    ? parsed
                    : throw new FormatException($"Ismeretlen érték a kódban: „{code}”.")
            };

            return new Card(suit, rank);
        }

        /// <summary>Több kártyakód átalakítása.</summary>
        public static IEnumerable<Card> ParseCards(IEnumerable<string> codes) => codes.Select(ParseCard);

        /// <summary>Kártya kódja a mentéshez (pl. <c>"AS"</c>, <c>"TH"</c>).</summary>
        public static string FormatCard(Card card) => Card.RankToCode(card.Rank) + SuitCode(card.Suit);

        /// <summary>A szín kódjele (C, D, H, S).</summary>
        public static string SuitCode(Suit suit) => suit switch
        {
            Suit.Clubs => "C",
            Suit.Diamonds => "D",
            Suit.Hearts => "H",
            _ => "S"
        };
    }

    /// <summary>
    /// A félbehagyott parti mentése és betöltése. Sérült vagy hiányos fájl esetén
    /// <c>null</c>-lal tér vissza, hogy a játék mindig elinduljon.
    /// </summary>
    public static class GameSaveStore
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        /// <summary>A mentés fájlja a felhasználói adatmappában.</summary>
        public static string FilePath => AppPaths.File("game-save.json");

        /// <summary>A tábla pillanatképének összeállítása a jelenlegi állapotból.</summary>
        public static SavedGame Capture(KlondikeGame game)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            var board = game.Board;

            return new SavedGame
            {
                Version = 1,
                SavedAt = DateTime.Now,
                DrawCount = game.DrawCount,
                Score = game.Score,
                Moves = game.MoveCount,
                Recycles = game.RecycleCount,
                StartedAt = game.GameStartedAt,
                Tableau = board.Tableau.Select(location => ToSavedPile(location.Pile)).ToList(),
                Foundation = board.Foundation.Select(location => ToSavedPile(location.Pile)).ToList(),
                Stock = ToSavedPile(board.Stock.Pile),
                Waste = ToSavedPile(board.Waste.Pile)
            };
        }

        /// <summary>A jelenlegi állapot mentése. Siker esetén <c>true</c>.</summary>
        public static bool Save(KlondikeGame game, string? filePath = null) =>
            Save(Capture(game), filePath);

        /// <summary>
        /// Egy pillanatkép mentése. A <paramref name="filePath"/> csak a tesztekhez kell,
        /// a játék az alapértelmezett helyre ment. Siker esetén <c>true</c>.
        /// </summary>
        public static bool Save(SavedGame saved, string? filePath = null)
        {
            string path = filePath ?? FilePath;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? AppPaths.DataFolder);
                File.WriteAllText(path, JsonSerializer.Serialize(saved, Options));
                return true;
            }
            catch
            {
                // A mentés hibája nem akadályozhatja a játékot.
                return false;
            }
        }

        /// <summary>A mentett parti betöltése, vagy <c>null</c>, ha nincs érvényes mentés.</summary>
        public static SavedGame? Load(string? filePath = null)
        {
            string path = filePath ?? FilePath;

            try
            {
                if (File.Exists(path))
                {
                    var saved = JsonSerializer.Deserialize<SavedGame>(File.ReadAllText(path), Options);
                    if (saved != null && IsValid(saved))
                    {
                        return saved;
                    }
                }
            }
            catch
            {
                // Sérült fájl: nincs folytatható parti.
            }

            return null;
        }

        /// <summary>Van-e érvényes, folytatható mentés.</summary>
        public static bool HasSave(string? filePath = null) => Load(filePath) != null;

        /// <summary>A mentés törlése (új játék indításakor).</summary>
        public static void Delete(string? filePath = null)
        {
            string path = filePath ?? FilePath;

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // nem kritikus
            }
        }

        /// <summary>
        /// A mentés érvényessége: hét oszlop, négy gyűjtőhely, és a táblán pontosan
        /// 52 különböző lap.
        /// </summary>
        public static bool IsValid(SavedGame saved)
        {
            if (saved.Version < 1 || saved.Tableau.Count != 7 || saved.Foundation.Count != 4)
            {
                return false;
            }

            var codes = new List<string>();
            foreach (var pile in saved.Tableau.Concat(saved.Foundation).Append(saved.Stock).Append(saved.Waste))
            {
                if (pile == null || pile.FaceDown < 0 || pile.FaceDown > pile.Cards.Count)
                {
                    return false;
                }

                codes.AddRange(pile.Cards);
            }

            if (codes.Count != Deck.FullDeckSize)
            {
                return false;
            }

            try
            {
                return SavedGame.ParseCards(codes).Distinct().Count() == Deck.FullDeckSize;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static SavedPile ToSavedPile(Pile pile) => new SavedPile
        {
            Cards = pile.Cards.Select(SavedGame.FormatCard).ToList(),
            FaceDown = pile.FaceDownCount
        };
    }
}
