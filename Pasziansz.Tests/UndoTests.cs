using System;
using System.Collections.Generic;
using System.Linq;
using Pasziansz.Game;
using Pasziansz.Models;
using Xunit;

namespace Pasziansz.Tests;

/// <summary>A visszavonás (Undo) tesztjei mind a négy lépéstípusra, plusz egy teljes parti.</summary>
public class UndoTests
{
    [Fact]
    public void Undo_WithoutMoves_Fails()
    {
        var game = BoardSetup.EmptyGame();

        var result = game.Undo();

        Assert.False(result.Success);
        Assert.Contains("Nincs visszavonható", result.Message);
    }

    [Fact]
    public void Undo_AfterTransfer_RestoresLayoutAndCounters()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "5S");
        BoardSetup.SetTableau(game, 1, 0, "6H");
        string before = Snap.Of(game);

        game.Move(PileRef.Tableau(0), 0, PileRef.Tableau(1));
        var result = game.Undo();

        Assert.True(result.Success);
        Assert.Equal(before, Snap.Of(game));
        Assert.Equal(0, game.Score);
        Assert.Equal(0, game.MoveCount);
        Assert.False(game.CanUndo);
        Assert.Equal("Lépés visszavonva", game.LastAction);
    }

    [Fact]
    public void Undo_AfterTransferWithFlip_RestoresFaceDownCard()
    {
        var game = BoardSetup.EmptyGame();
        var pile0 = BoardSetup.SetTableau(game, 0, 1, "8C", "7H");
        BoardSetup.SetTableau(game, 1, 0, "8S");
        string before = Snap.Of(game);

        game.Move(PileRef.Tableau(0), 1, PileRef.Tableau(1));
        Assert.Equal(0, pile0.FaceDownCount);
        Assert.Equal(Scoring.FlipCard, game.Score);

        game.Undo();

        Assert.Equal(1, pile0.FaceDownCount);
        Assert.Equal(Cards.C("7H"), pile0.TopCard);
        Assert.Equal(0, game.Score);
        Assert.Equal(before, Snap.Of(game));
    }

    [Fact]
    public void Undo_MovesOnlyPartOfRun_RestoresRunOrder()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "9C", "8H", "7S");
        BoardSetup.SetTableau(game, 1, 0, "9S");
        string before = Snap.Of(game);

        game.Move(PileRef.Tableau(0), 1, PileRef.Tableau(1));
        game.Undo();

        Assert.Equal(before, Snap.Of(game));
        Assert.Equal(Cards.C("7S"), game.Board.Tableau[0].Pile.TopCard);
    }

    [Fact]
    public void Undo_AfterMoveToFoundation_RestoresCardAndScore()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "AH");
        string before = Snap.Of(game);

        game.Move(PileRef.Tableau(0), 0, PileRef.Foundation(2));
        Assert.Equal(Scoring.Foundation, game.Score);

        game.Undo();

        Assert.Empty(game.Board.Foundation[2].Pile.Cards);
        Assert.Single(game.Board.Tableau[0].Pile.Cards);
        Assert.Equal(0, game.Score);
        Assert.Equal(before, Snap.Of(game));
    }

    [Fact]
    public void Undo_AfterDraw_ReturnsCardToStock()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetStock(game, "5H", "4S", "3D");
        string before = Snap.Of(game);

        game.DrawFromStock();
        Assert.Single(game.Board.Waste.Pile.Cards);

        var result = game.Undo();

        Assert.True(result.Success);
        Assert.Equal(before, Snap.Of(game));
        Assert.Equal(3, game.Board.Stock.Pile.Count);
        Assert.Equal(3, game.Board.Stock.Pile.FaceDownCount);
        Assert.Equal(0, game.MoveCount);
    }

    [Fact]
    public void Undo_AfterThreeCardDraw_ReturnsAllThreeInOrder()
    {
        var game = BoardSetup.EmptyGame(drawCount: 3);
        BoardSetup.SetStock(game, "5H", "4S", "3D", "2C", "AH");
        string before = Snap.Of(game);

        game.DrawFromStock();
        Assert.Equal(3, game.Board.Waste.Pile.Count);

        game.Undo();

        Assert.Equal(before, Snap.Of(game));
        Assert.Equal(Cards.C("AH"), game.Board.Stock.Pile.TopCard);
    }

    [Fact]
    public void Undo_AfterRecycle_RestoresWasteAndStockAndRecycleCount()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetWaste(game, "5H", "4S", "3D");
        string before = Snap.Of(game);

        game.RecycleWaste();
        Assert.Equal(1, game.RecycleCount);

        var result = game.Undo();

        Assert.True(result.Success);
        Assert.Equal(before, Snap.Of(game));
        Assert.Equal(0, game.RecycleCount);
        Assert.Equal(3, game.Board.Waste.Pile.Count);
        Assert.Equal(Cards.C("3D"), game.Board.Waste.Pile.TopCard);
    }

    [Fact]
    public void Undo_AfterFlip_RestoresFaceDownState()
    {
        var game = BoardSetup.EmptyGame();
        var pile = BoardSetup.SetTableau(game, 0, 3, "9C", "8H", "7S");
        string before = Snap.Of(game);

        game.FlipTopCard(PileRef.Tableau(0));
        Assert.Equal(2, pile.FaceDownCount);

        var result = game.Undo();

        Assert.True(result.Success);
        Assert.Equal(3, pile.FaceDownCount);
        Assert.Equal(before, Snap.Of(game));
        Assert.Equal(0, game.MoveCount);
    }

    [Fact]
    public void Undo_SeveralStepsInReverseOrder()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetStock(game, "5H", "4S");
        BoardSetup.SetTableau(game, 0, 0, "7C");
        BoardSetup.SetTableau(game, 1, 0, "6D");

        var snapshots = new List<string> { Snap.Of(game) };

        game.DrawFromStock();
        snapshots.Add(Snap.Of(game));

        game.Move(PileRef.Tableau(1), 0, PileRef.Tableau(0));
        snapshots.Add(Snap.Of(game));

        // Sikertelen lépés nem kerülhet a visszavonás-verembe.
        Assert.False(game.FlipTopCard(PileRef.Tableau(0)).Success); // nincs lefordított lap
        Assert.Equal(2, game.UndoDepth);

        game.Undo();
        Assert.Equal(snapshots[1], Snap.Of(game));

        game.Undo();
        Assert.Equal(snapshots[0], Snap.Of(game));

        Assert.False(game.CanUndo);
        Assert.Equal(0, game.MoveCount);
    }

    [Fact]
    public void Undo_StackIsCappedAndOldestStepsAreDropped()
    {
        var game = BoardSetup.EmptyGame();
        // 600 húzás: a korlát 500, tehát a legrégebbi 100 lépés kiesik.
        BoardSetup.SetStock(game, Enumerable.Repeat("AH", 600).ToArray());

        for (int i = 0; i < 600; i++)
        {
            Assert.True(game.DrawFromStock().Success);
        }

        // 600 megtett lépés, ebből 500 vonható vissza – a legrégebbi 100 kiesett.
        Assert.Equal(KlondikeGame.MaxUndoDepth, game.UndoDepth);
        Assert.Equal(600 - KlondikeGame.MaxUndoDepth, game.MoveCount - game.UndoDepth);

        int undone = 0;
        while (game.Undo().Success)
        {
            undone++;
        }

        Assert.Equal(KlondikeGame.MaxUndoDepth, undone);
        Assert.False(game.CanUndo);
    }

    [Fact]
    public void Undo_AfterLongGameStillRestoresCorrectly()
    {
        var game = BoardSetup.EmptyGame(drawCount: 3);
        BoardSetup.SetStock(game, DeckBuilder.AllCodes().Take(24).ToArray());

        for (int i = 0; i < 30; i++)
        {
            game.DrawFromStock();
        }

        string before = Snap.Of(game);
        game.DrawFromStock();
        game.Undo();

        Assert.Equal(before, Snap.Of(game));
    }

    [Fact]
    public void Undo_FiresGameChanged()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetStock(game, "5H");
        game.DrawFromStock();

        int changed = 0;
        game.GameChanged += (_, _) => changed++;

        game.Undo();

        Assert.Equal(1, changed);
    }

    // ------------------------------------------------------------------
    //  Teljes parti: minden lépés után visszavonás, az állapotnak
    //  pontosan az eredetivel kell megegyeznie.
    // ------------------------------------------------------------------

    [Fact]
    public void Undo_WholePlayedGame_RestoresEveryStateExactly()
    {
        for (int seed = 0; seed < 12; seed++)
        {
            var game = new KlondikeGame(new Random(seed));
            game.NewGame(seed % 2 == 0 ? 1 : 3);
            var random = new Random(seed * 7 + 3);

            var snapshots = new List<string> { Snap.Of(game) };
            var scores = new List<int> { game.Score };

            for (int step = 0; step < 40; step++)
            {
                var moves = LegalMoves.For(game);
                bool acted;

                if (moves.Count > 0 && random.NextDouble() < 0.7)
                {
                    var move = moves[random.Next(moves.Count)];
                    acted = game.Move(move.From, move.Index, move.To).Success;
                    Assert.True(acted, "A szabályok által felkínált lépésnek sikerülnie kell.");
                }
                else
                {
                    acted = game.DrawFromStock().Success;
                }

                if (!acted)
                {
                    break;
                }

                snapshots.Add(Snap.Of(game));
                scores.Add(game.Score);
            }

            Assert.True(snapshots.Count > 1, "A partiban történnie kell legalább egy lépésnek.");

            // Visszavonás a legutolsótól az elsőig.
            for (int i = snapshots.Count - 1; i >= 1; i--)
            {
                Assert.True(game.Undo().Success);
                Assert.Equal(snapshots[i - 1], Snap.Of(game));
                Assert.Equal(scores[i - 1], game.Score);
            }

            Assert.False(game.CanUndo);
            Assert.Equal(0, game.MoveCount);
            Assert.Equal(0, game.Score);
        }
    }

    [Fact]
    public void Undo_DoesNotDuplicateOrLoseCards()
    {
        var game = new KlondikeGame(new Random(77));
        game.NewGame(1);
        var random = new Random(5);

        for (int step = 0; step < 60; step++)
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

            var cards = game.Board.AllCards();
            Assert.Equal(Deck.FullDeckSize, cards.Count);
            Assert.Equal(Deck.FullDeckSize, cards.Distinct().Count());
        }

        while (game.CanUndo)
        {
            game.Undo();
            var cards = game.Board.AllCards();
            Assert.Equal(Deck.FullDeckSize, cards.Count);
            Assert.Equal(Deck.FullDeckSize, cards.Distinct().Count());
        }
    }
}
