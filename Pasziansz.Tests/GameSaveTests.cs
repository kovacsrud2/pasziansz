using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pasziansz.Game;
using Pasziansz.Models;
using Xunit;

namespace Pasziansz.Tests;

/// <summary>A parti mentésének és folytatásának tesztjei (fájl nélkül, memóriában).</summary>
public class GameSaveTests
{
    /// <summary>Egy játszott parti (húzásokkal és lépésekkel).</summary>
    private static KlondikeGame PlayedGame(int seed, int steps = 25)
    {
        var game = new KlondikeGame(new Random(seed));
        game.NewGame(seed % 2 == 0 ? 1 : 3);
        var random = new Random(seed + 1);

        for (int i = 0; i < steps; i++)
        {
            var moves = LegalMoves.For(game);
            if (moves.Count > 0 && random.Next(2) == 0)
            {
                var move = moves[random.Next(moves.Count)];
                game.Move(move.From, move.Index, move.To);
            }
            else
            {
                game.DrawFromStock();
            }
        }

        return game;
    }

    [Fact]
    public void Capture_RecordsCountersAndAllFiftyTwoCards()
    {
        var game = PlayedGame(3);

        var saved = GameSaveStore.Capture(game);

        Assert.Equal(1, saved.Version);
        Assert.Equal(game.DrawCount, saved.DrawCount);
        Assert.Equal(game.Score, saved.Score);
        Assert.Equal(game.MoveCount, saved.Moves);
        Assert.Equal(game.RecycleCount, saved.Recycles);
        Assert.Equal(7, saved.Tableau.Count);
        Assert.Equal(4, saved.Foundation.Count);
        Assert.True(GameSaveStore.IsValid(saved));

        int cards = saved.Tableau.Sum(p => p.Cards.Count) + saved.Foundation.Sum(p => p.Cards.Count)
            + saved.Stock.Cards.Count + saved.Waste.Cards.Count;
        Assert.Equal(Deck.FullDeckSize, cards);
    }

    [Fact]
    public void Capture_KeepsFaceDownCounts()
    {
        var game = new KlondikeGame(new Random(1));
        game.NewGame(1);

        var saved = GameSaveStore.Capture(game);

        for (int i = 0; i < 7; i++)
        {
            Assert.Equal(i, saved.Tableau[i].FaceDown);
            Assert.Equal(i + 1, saved.Tableau[i].Cards.Count);
        }

        Assert.Equal(24, saved.Stock.Cards.Count);
        Assert.Equal(24, saved.Stock.FaceDown);
        Assert.Empty(saved.Waste.Cards);
    }

    [Fact]
    public void Capture_ThenApplyTo_RestoresIdenticalState()
    {
        var game = PlayedGame(5);

        var saved = GameSaveStore.Capture(game);

        var restored = new KlondikeGame(new Random(999));
        saved.ApplyTo(restored);

        Assert.Equal(Snap.Of(game), Snap.Of(restored));
        Assert.Equal(game.Score, restored.Score);
        Assert.Equal(game.MoveCount, restored.MoveCount);
        Assert.Equal(game.RecycleCount, restored.RecycleCount);
        Assert.Equal(game.DrawCount, restored.DrawCount);
        Assert.Equal(game.CardsOnFoundation, restored.CardsOnFoundation);
    }

    [Fact]
    public void ApplyTo_RestoresGameStartedAt()
    {
        var game = PlayedGame(7);
        var saved = GameSaveStore.Capture(game);

        var restored = new KlondikeGame(new Random(1));
        saved.ApplyTo(restored);

        Assert.Equal(game.GameStartedAt, restored.GameStartedAt);
    }

    [Fact]
    public void ApplyTo_RestoresFaceDownFlags()
    {
        // Középső állapot: az első oszlop teteje lefordított lap, felette felfordított.
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 2, "9C", "8H", "7S");
        BoardSetup.SetTableau(game, 1, 1, "KD", "QC");
        BoardSetup.SetWaste(game, "5H");
        BoardSetup.SetStock(game, "2C", "3D", "4S");
        var saved = GameSaveStore.Capture(game);

        var restored = new KlondikeGame(new Random(1));
        saved.ApplyTo(restored);

        Assert.Equal(Snap.Of(game), Snap.Of(restored));
        for (int i = 0; i < 7; i++)
        {
            Assert.Equal(game.Board.Tableau[i].Pile.FaceDownCount,
                restored.Board.Tableau[i].Pile.FaceDownCount);
        }
    }

    [Fact]
    public void ApplyTo_ClearsUndoHistory()
    {
        var game = PlayedGame(9);
        Assert.True(game.CanUndo);

        var saved = GameSaveStore.Capture(game);
        var restored = new KlondikeGame(new Random(1));
        saved.ApplyTo(restored);

        Assert.False(restored.CanUndo);
        Assert.Equal(0, restored.UndoDepth);
    }

    [Fact]
    public void ApplyTo_GameIsPlayableAfterwards()
    {
        var game = PlayedGame(11);
        var saved = GameSaveStore.Capture(game);

        var restored = new KlondikeGame(new Random(1));
        saved.ApplyTo(restored);

        // Húzás és visszavonás is működik a betöltött partiban.
        int stockBefore = restored.Board.Stock.Pile.Count;
        var draw = restored.DrawFromStock();
        Assert.True(draw.Success);
        Assert.True(restored.Undo().Success);
        Assert.Equal(stockBefore, restored.Board.Stock.Pile.Count);

        var move = LegalMoves.For(restored).Cast<LegalMoves.Move?>().FirstOrDefault();
        if (move is { } m)
        {
            Assert.True(restored.Move(m.From, m.Index, m.To).Success);
        }
    }

    [Fact]
    public void ApplyTo_KeepsEveryCardExactlyOnce()
    {
        var game = PlayedGame(13);
        var saved = GameSaveStore.Capture(game);
        var restored = new KlondikeGame(new Random(1));

        saved.ApplyTo(restored);

        var cards = restored.Board.AllCards();
        Assert.Equal(Deck.FullDeckSize, cards.Count);
        Assert.Equal(Deck.FullDeckSize, cards.Distinct().Count());
    }

    [Fact]
    public void ApplyTo_KeepsRestartPossible()
    {
        var game = PlayedGame(15);
        var saved = GameSaveStore.Capture(game);
        var restored = new KlondikeGame(new Random(1));

        saved.ApplyTo(restored);

        Assert.True(restored.CanRestart);
        Assert.True(restored.RestartCurrentDeal().Success);
        Assert.Equal(0, restored.Score);
        Assert.Equal(0, restored.MoveCount);
    }

    [Fact]
    public void ApplyTo_RestartDealsTheSameLayoutAsTheOriginalDeal()
    {
        // Az újraosztás az EREDETI osztást adja vissza (nem a félbehagyott állapotot):
        // ezért a mentésnek az osztás sorrendjét is tárolnia kell.
        var game = PlayedGame(17);
        var saved = GameSaveStore.Capture(game);

        var restored = new KlondikeGame(new Random(1));
        saved.ApplyTo(restored);

        Assert.Equal(Deck.FullDeckSize, saved.Deal.Count);

        Assert.True(restored.RestartCurrentDeal().Success);

        // Ilyenkor a klasszikus friss osztás áll elő: i+1 lap, i lefordítva.
        for (int i = 0; i < 7; i++)
        {
            var pile = restored.Board.Tableau[i].Pile;
            Assert.Equal(i + 1, pile.Count);
            Assert.Equal(i, pile.FaceDownCount);
        }

        Assert.Equal(24, restored.Board.Stock.Pile.Count);
        Assert.Empty(restored.Board.Waste.Pile.Cards);
        Assert.Equal(Deck.FullDeckSize, restored.Board.AllCards().Distinct().Count());
        Assert.Equal(0, restored.Score);
        Assert.Equal(0, restored.MoveCount);
    }

    [Fact]
    public void ApplyTo_RestartGivesTheSameCardsAsTheOriginalDeal()
    {
        // A visszaosztott lapok sorrendje pontosan az eredeti osztásé.
        var game = PlayedGame(31);
        var originalDeal = game.Deal!.Select(c => SavedGame.FormatCard(c)).ToList();

        var saved = GameSaveStore.Capture(game);
        var restored = new KlondikeGame(new Random(1));
        saved.ApplyTo(restored);

        restored.RestartCurrentDeal();

        var afterRestart = restored.Deal!.Select(c => SavedGame.FormatCard(c)).ToList();
        Assert.Equal(originalDeal, afterRestart);
    }

    [Fact]
    public void SaveAndLoad_KeepsTheOriginalDeal()
    {
        string path = TempSavePath();
        try
        {
            var game = PlayedGame(35);
            var originalDeal = game.Deal!.Select(c => SavedGame.FormatCard(c)).ToList();
            Assert.True(GameSaveStore.Save(game, path));

            var loaded = GameSaveStore.Load(path);
            Assert.NotNull(loaded);
            Assert.Equal(originalDeal, loaded!.Deal);

            var restored = new KlondikeGame(new Random(1));
            loaded.ApplyTo(restored);

            Assert.True(restored.CanRestart);
            Assert.True(restored.RestartCurrentDeal().Success);
            Assert.Equal(originalDeal, restored.Deal!.Select(c => SavedGame.FormatCard(c)).ToList());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Capture_StoresTheDealInDealOrder()
    {
        var game = new KlondikeGame(new Random(3));
        game.NewGame(1);

        var saved = GameSaveStore.Capture(game);

        // Az osztás sorrendje: az oszlopok alulról felfelé, majd a húzópakli alulról felfelé.
        var expected = game.Board.Tableau.SelectMany(l => l.Pile.Cards)
            .Concat(game.Board.Stock.Pile.Cards)
            .Select(SavedGame.FormatCard)
            .ToList();

        Assert.Equal(Deck.FullDeckSize, saved.Deal.Count);
        Assert.Equal(expected, saved.Deal);
    }

    [Fact]
    public void ApplyTo_WithoutDealInSave_RestartIsNotPossible()
    {
        // Régebbi (osztás nélküli) mentés: a parti folytatható, de újraosztani nem lehet.
        var saved = GameSaveStore.Capture(PlayedGame(33));
        saved.Deal.Clear();

        var restored = new KlondikeGame(new Random(1));
        saved.ApplyTo(restored);

        Assert.False(restored.CanRestart);
        Assert.False(restored.RestartCurrentDeal().Success);
        Assert.Equal(Deck.FullDeckSize, restored.Board.AllCards().Count);
    }

    // ------------------------------------------------------------------
    //  Fájlba mentés és betöltés (ideiglenes mappával, hogy a valódi
    //  felhasználói mentést ne írjuk felül)
    // ------------------------------------------------------------------

    [Fact]
    public void SaveAndLoad_RoundTripThroughFile()
    {
        string path = TempSavePath();
        try
        {
            var game = PlayedGame(23);
            Assert.True(GameSaveStore.Save(game, path));
            Assert.True(File.Exists(path));

            var loaded = GameSaveStore.Load(path);
            Assert.NotNull(loaded);

            var restored = new KlondikeGame(new Random(1));
            loaded!.ApplyTo(restored);

            Assert.Equal(Snap.Of(game), Snap.Of(restored));
            Assert.Equal(game.Score, restored.Score);
            Assert.True(GameSaveStore.HasSave(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        string path = TempSavePath();

        Assert.Null(GameSaveStore.Load(path));
        Assert.False(GameSaveStore.HasSave(path));
    }

    [Fact]
    public void Load_CorruptedFile_ReturnsNull()
    {
        string path = TempSavePath();
        try
        {
            File.WriteAllText(path, "{ ez nem érvényes json");
            Assert.Null(GameSaveStore.Load(path));

            File.WriteAllText(path, "{}");
            Assert.Null(GameSaveStore.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Delete_RemovesTheSave()
    {
        string path = TempSavePath();
        try
        {
            GameSaveStore.Save(PlayedGame(25), path);
            Assert.True(File.Exists(path));

            GameSaveStore.Delete(path);

            Assert.False(File.Exists(path));
            Assert.Null(GameSaveStore.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SaveTwice_OverwritesThePreviousSave()
    {
        string path = TempSavePath();
        try
        {
            var first = PlayedGame(27);
            GameSaveStore.Save(first, path);
            string firstLayout = Snap.Of(first);

            var second = PlayedGame(29);
            GameSaveStore.Save(second, path);
            string secondLayout = Snap.Of(second);

            Assert.NotEqual(firstLayout, secondLayout);

            var loaded = GameSaveStore.Load(path)!;
            var restored = new KlondikeGame(new Random(1));
            loaded.ApplyTo(restored);

            Assert.Equal(secondLayout, Snap.Of(restored));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string TempSavePath() =>
        Path.Combine(Path.GetTempPath(), $"pasziansz-test-{Guid.NewGuid():N}.json");

    // ------------------------------------------------------------------
    //  Érvényesség
    // ------------------------------------------------------------------

    [Fact]
    public void IsValid_RejectsBrokenSaves()
    {
        var saved = GameSaveStore.Capture(PlayedGame(19));

        Assert.True(GameSaveStore.IsValid(saved));

        var noTableau = GameSaveStore.Capture(PlayedGame(19));
        noTableau.Tableau.RemoveAt(0);
        Assert.False(GameSaveStore.IsValid(noTableau));

        var noFoundation = GameSaveStore.Capture(PlayedGame(19));
        noFoundation.Foundation.RemoveAt(0);
        Assert.False(GameSaveStore.IsValid(noFoundation));

        var missingCard = GameSaveStore.Capture(PlayedGame(19));
        missingCard.Stock.Cards.RemoveAt(0);
        Assert.False(GameSaveStore.IsValid(missingCard));

        var duplicated = GameSaveStore.Capture(PlayedGame(19));
        duplicated.Stock.Cards[0] = duplicated.Waste.Cards.Count > 0
            ? duplicated.Waste.Cards[0]
            : duplicated.Tableau[0].Cards[0];
        Assert.False(GameSaveStore.IsValid(duplicated));

        var wrongFaceDown = GameSaveStore.Capture(PlayedGame(19));
        wrongFaceDown.Tableau[0].FaceDown = 99;
        Assert.False(GameSaveStore.IsValid(wrongFaceDown));

        var badCode = GameSaveStore.Capture(PlayedGame(19));
        badCode.Stock.Cards[0] = "XX";
        Assert.False(GameSaveStore.IsValid(badCode));
    }

    [Fact]
    public void IsValid_AcceptsAFreshlyDealtGame()
    {
        var game = new KlondikeGame(new Random(21));
        game.NewGame(1);

        Assert.True(GameSaveStore.IsValid(GameSaveStore.Capture(game)));
    }

    // ------------------------------------------------------------------
    //  Kártyakódok
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("AS", Suit.Spades, 1)]
    [InlineData("TH", Suit.Hearts, 10)]
    [InlineData("KD", Suit.Diamonds, 13)]
    [InlineData("2C", Suit.Clubs, 2)]
    [InlineData("10H", Suit.Hearts, 10)]
    public void ParseCard_ReadsCodes(string code, Suit suit, int rank)
    {
        var card = SavedGame.ParseCard(code);

        Assert.Equal(suit, card.Suit);
        Assert.Equal(rank, card.Rank);
    }

    [Theory]
    [InlineData("")]
    [InlineData("X")]
    [InlineData("AX")]
    [InlineData("0H")]
    [InlineData("14C")]
    [InlineData("ZZ")]
    public void ParseCard_RejectsInvalidCodes(string code)
    {
        Assert.Throws<FormatException>(() => SavedGame.ParseCard(code));
    }

    [Fact]
    public void FormatCard_MatchesParseCard()
    {
        foreach (var card in Deck.CreateOrdered())
        {
            Assert.Equal(card, SavedGame.ParseCard(SavedGame.FormatCard(card)));
        }
    }

    [Fact]
    public void FormatCard_UsesTheShortCodeOfTheCard()
    {
        // A mentés a játék rövid kódját használja, ott a tízes „10”.
        Assert.Equal("10H", SavedGame.FormatCard(new Card(Suit.Hearts, 10)));
        Assert.Equal("AS", SavedGame.FormatCard(new Card(Suit.Spades, 1)));
        Assert.Equal("KD", SavedGame.FormatCard(new Card(Suit.Diamonds, 13)));

        // A beolvasás a „T” jelölést is elfogadja (kézzel írt mentés).
        Assert.Equal(new Card(Suit.Hearts, 10), SavedGame.ParseCard("TH"));
        Assert.Equal(new Card(Suit.Hearts, 10), SavedGame.ParseCard("10H"));
    }
}
