using System;
using System.Collections.Generic;
using System.Linq;
using Pasziansz.Game;
using Pasziansz.Models;
using Xunit;

namespace Pasziansz.Tests;

/// <summary>Az osztás (NewGame) és az újrajátszás (RestartCurrentDeal) tesztjei.</summary>
public class DealTests
{
    [Fact]
    public void NewGame_SevenColumnsWithGrowingPiles()
    {
        var game = new KlondikeGame(new Random(1));

        game.NewGame(1);

        Assert.Equal(7, game.Board.Tableau.Count);
        for (int i = 0; i < 7; i++)
        {
            Assert.Equal(i + 1, game.Board.Tableau[i].Pile.Count);
        }
    }

    [Fact]
    public void NewGame_ColumnIFaceDownCardsAndTopCardFaceUp()
    {
        var game = new KlondikeGame(new Random(2));

        game.NewGame(1);

        for (int i = 0; i < 7; i++)
        {
            var pile = game.Board.Tableau[i].Pile;
            Assert.Equal(i, pile.FaceDownCount);
            Assert.Equal(1, pile.FaceUpCount);
            Assert.True(pile.IsFaceUp(pile.Count - 1));
            if (i > 0)
            {
                Assert.False(pile.IsFaceUp(0));
            }
        }
    }

    [Fact]
    public void NewGame_StockHas24CardsAndWasteIsEmpty()
    {
        var game = new KlondikeGame(new Random(3));

        game.NewGame(1);

        Assert.Equal(24, game.Board.Stock.Pile.Count);
        Assert.Equal(24, game.Board.Stock.Pile.FaceDownCount);
        Assert.Empty(game.Board.Waste.Pile.Cards);
        foreach (var foundation in game.Board.Foundation)
        {
            Assert.Empty(foundation.Pile.Cards);
        }
    }

    [Fact]
    public void NewGame_CountersAreZeroAndGameIsNotWon()
    {
        var game = new KlondikeGame(new Random(4));

        game.NewGame(1);

        Assert.Equal(0, game.Score);
        Assert.Equal(0, game.MoveCount);
        Assert.Equal(0, game.RecycleCount);
        Assert.Equal(0, game.CardsOnFoundation);
        Assert.False(game.IsWon);
        Assert.False(game.CanUndo);
        Assert.Equal(0, game.UndoDepth);
        Assert.Equal("Új osztás", game.LastAction);
    }

    [Fact]
    public void NewGame_AllFiftyTwoCardsArePresentExactlyOnce()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var game = new KlondikeGame(new Random(seed));
            game.NewGame(seed % 2 == 0 ? 1 : 3);

            var cards = game.Board.AllCards();
            Assert.Equal(Deck.FullDeckSize, cards.Count);
            Assert.Equal(Deck.FullDeckSize, cards.Distinct().Count());
        }
    }

    [Fact]
    public void NewGame_WithForcedDeck_DealsInDocumentedOrder()
    {
        // A rendezett pakli: treff ász..király, káró, kőr, pikk.
        var deck = DeckBuilder.OrderedDeck();
        var game = new KlondikeGame(new Random(99));

        game.NewGame(1, deck);

        int cursor = 0;
        for (int i = 0; i < 7; i++)
        {
            var pile = game.Board.Tableau[i].Pile;
            for (int j = 0; j <= i; j++)
            {
                Assert.Same(deck[cursor], pile.Cards[j]);
                cursor++;
            }
        }

        Assert.Equal(24, game.Board.Stock.Pile.Count);
        Assert.Same(deck[28], game.Board.Stock.Pile.Cards[0]);
        Assert.Same(deck[51], game.Board.Stock.Pile.TopCard);
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(52, 1)]
    public void NewGame_DrawCountOnlyOneOrThree(int requested, int expected)
    {
        var game = new KlondikeGame(new Random(5));

        game.NewGame(requested);

        Assert.Equal(expected, game.DrawCount);
    }

    [Fact]
    public void NewGame_InvalidForcedDeckFallsBackToFullDeck()
    {
        var game = new KlondikeGame(new Random(6));
        var tooShort = Deck.CreateOrdered().Take(10).ToList();

        game.NewGame(1, tooShort);

        var cards = game.Board.AllCards();
        Assert.Equal(Deck.FullDeckSize, cards.Count);
        Assert.Equal(Deck.FullDeckSize, cards.Distinct().Count());
        Assert.True(game.CanRestart);
    }

    [Fact]
    public void NewGame_TwoGamesWithSameSeedAreIdentical()
    {
        var first = new KlondikeGame(new Random(1234));
        var second = new KlondikeGame(new Random(1234));

        first.NewGame(1);
        second.NewGame(1);

        Assert.Equal(Snap.Of(first), Snap.Of(second));
    }

    [Fact]
    public void NewGame_DifferentSeedsUsuallyDealDifferently()
    {
        var first = new KlondikeGame(new Random(1));
        var second = new KlondikeGame(new Random(2));

        first.NewGame(1);
        second.NewGame(1);

        Assert.NotEqual(Snap.Of(first), Snap.Of(second));
    }

    [Fact]
    public void RestartCurrentDeal_RestoresSameLayoutWithCleanScore()
    {
        var game = new KlondikeGame(new Random(7));
        game.NewGame(1);
        string initialLayout = Snap.Of(game);

        // Játszunk néhány lépést: húzás + egy érvényes lépés, ha van.
        game.DrawFromStock();
        var moves = LegalMoves.For(game);
        if (moves.Count > 0)
        {
            game.Move(moves[0].From, moves[0].Index, moves[0].To);
        }

        Assert.True(game.Score > 0 || game.MoveCount > 0);

        var result = game.RestartCurrentDeal();

        Assert.True(result.Success);
        Assert.Equal(initialLayout, Snap.Of(game));
        Assert.Equal(0, game.Score);
        Assert.Equal(0, game.MoveCount);
        Assert.Equal(0, game.RecycleCount);
        Assert.False(game.CanUndo);
        Assert.Equal("Az osztás megismételve", game.LastAction);
    }

    [Fact]
    public void RestartCurrentDeal_KeepsDrawCount()
    {
        var game = new KlondikeGame(new Random(8));
        game.NewGame(3);

        game.RestartCurrentDeal();

        Assert.Equal(3, game.DrawCount);
        game.DrawFromStock();
        Assert.Equal(3, game.Board.Waste.Pile.Count);
    }

    [Fact]
    public void RestartCurrentDeal_FiresGameChanged()
    {
        var game = new KlondikeGame(new Random(9));
        game.NewGame(1);
        int changed = 0;
        game.GameChanged += (_, _) => changed++;

        game.RestartCurrentDeal();

        Assert.True(changed >= 1);
    }
}
