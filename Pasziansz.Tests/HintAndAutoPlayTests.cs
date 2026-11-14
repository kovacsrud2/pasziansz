using System;
using System.Linq;
using Pasziansz.Game;
using Pasziansz.Models;
using Xunit;

namespace Pasziansz.Tests;

/// <summary>Tipp (FindHint) és automatikus felpakolás (AutoPlayToFoundation) tesztek.</summary>
public class HintAndAutoPlayTests
{
    // ------------------------------------------------------------------
    //  Tipp
    // ------------------------------------------------------------------

    [Fact]
    public void FindHint_PrefersWasteToFoundation()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetWaste(game, "AH");
        BoardSetup.SetTableau(game, 0, 0, "2S");
        BoardSetup.SetTableau(game, 1, 0, "KS");

        var hint = game.FindHint();

        Assert.NotNull(hint);
        Assert.Equal(PileRef.Waste(), hint!.From);
        Assert.Equal(PileRef.Foundation(2), hint.To);
        Assert.Equal(0, hint.SourceIndex);
        Assert.Contains("A♥", hint.Description);   // AH = ász kőr
        Assert.Contains("kőr", hint.Description);
    }

    [Fact]
    public void FindHint_ThenTableauToFoundation()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "AC");

        var hint = game.FindHint();

        Assert.NotNull(hint);
        Assert.Equal(PileRef.Tableau(0), hint!.From);
        Assert.Equal(PileRef.Foundation(0), hint.To);
    }

    [Fact]
    public void FindHint_EveryOfferedHintCanActuallyBePlayed()
    {
        for (int seed = 0; seed < 25; seed++)
        {
            var game = new KlondikeGame(new Random(seed));
            game.NewGame(seed % 2 == 0 ? 1 : 3);
            var random = new Random(seed + 100);

            for (int step = 0; step < 40; step++)
            {
                var hint = game.FindHint();

                if (hint is not null && hint.SourceIndex >= 0)
                {
                    var result = game.Move(hint.From, hint.SourceIndex, hint.To);
                    Assert.True(result.Success,
                        $"A tipp nem volt végrehajtható (seed {seed}, lépés {step}): " +
                        $"{hint.From} [{hint.SourceIndex}] → {hint.To}");
                }
                else if (hint is not null && hint.To.Kind == PileKind.Waste)
                {
                    Assert.True(game.Board.Stock.Pile.Count > 0, "Húzási tipp csak nem üres paklinál lehet.");
                    game.DrawFromStock();
                }
                else if (hint is not null)
                {
                    Assert.True(game.Board.Waste.Pile.Count > 0, "Újrakeverési tipp csak nem üres talonnál lehet.");
                    game.RecycleWaste();
                }
                else
                {
                    Assert.Empty(game.Board.Stock.Pile.Cards);
                    Assert.Empty(game.Board.Waste.Pile.Cards);
                    break;
                }

                // Haladjon is a játék, különben a ciklus ugyanazt a tippet adná.
                if (hint is not null && hint.SourceIndex < 0)
                {
                    continue;
                }

                if (random.Next(3) == 0)
                {
                    game.DrawFromStock();
                }
            }
        }
    }

    // ------------------------------------------------------------------
    //  Automatikus felpakolás
    // ------------------------------------------------------------------

    [Fact]
    public void AutoPlay_MovesAceFromWasteToFoundation()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetWaste(game, "5H", "AH");

        int placed = game.AutoPlayToFoundation();

        Assert.Equal(1, placed);
        Assert.Equal(Cards.C("AH"), game.Board.Foundation[2].Pile.TopCard);
        Assert.Equal(Cards.C("5H"), game.Board.Waste.Pile.TopCard);
    }

    [Fact]
    public void AutoPlay_DoesNotSkipACardInTheSequence()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetFoundation(game, 2, "AH");
        BoardSetup.SetWaste(game, "3H"); // a kettes hiányzik

        int placed = game.AutoPlayToFoundation();

        Assert.Equal(0, placed);
        Assert.Single(game.Board.Foundation[2].Pile.Cards);
        Assert.Equal(Cards.C("3H"), game.Board.Waste.Pile.TopCard);
    }

    [Fact]
    public void AutoPlay_ContinuesInChainWhenTheNextCardAppears()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetFoundation(game, 2, "AH");
        BoardSetup.SetTableau(game, 0, 0, "2H");
        BoardSetup.SetWaste(game, "3H");

        // A 2♥ felvitele után a talon 3♥-ja már sorra kerül, ezért két lap megy fel.
        int placed = game.AutoPlayToFoundation();

        Assert.Equal(2, placed);
        Assert.Equal(3, game.Board.Foundation[2].Pile.Count);
        Assert.Equal(Cards.C("3H"), game.Board.Foundation[2].Pile.TopCard);
        Assert.Empty(game.Board.Tableau[0].Pile.Cards);
        Assert.Empty(game.Board.Waste.Pile.Cards);
    }

    [Fact]
    public void AutoPlay_ChainsCardsUpToMaxLimit()
    {
        var game = BoardSetup.EmptyGame();
        // A talon teteje a lista vége: így a húzási sorrend ász, kettes, … ötös.
        BoardSetup.SetWaste(game, "5H", "4H", "3H", "2H", "AH");

        int placed = game.AutoPlayToFoundation();

        Assert.Equal(5, placed);
        Assert.Equal(5, game.Board.Foundation[2].Pile.Count);
        Assert.Equal(Cards.C("5H"), game.Board.Foundation[2].Pile.TopCard);
    }

    [Fact]
    public void AutoPlay_RespectsMaxCardsParameter()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetWaste(game, "5H", "4H", "3H", "2H", "AH");

        int placed = game.AutoPlayToFoundation(maxCards: 2);

        Assert.Equal(2, placed);
        Assert.Equal(2, game.Board.Foundation[2].Pile.Count);
    }

    [Fact]
    public void AutoPlay_DoesNothingWhenNoMoveIsAvailable()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetWaste(game, "5H", "9S");
        BoardSetup.SetTableau(game, 0, 0, "7D");

        int placed = game.AutoPlayToFoundation();

        Assert.Equal(0, placed);
        Assert.Equal(0, game.CardsOnFoundation);
    }

    [Fact]
    public void AutoPlay_ScoresTenPerCard()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "AH");

        int placed = game.AutoPlayToFoundation();

        Assert.Equal(1, placed);
        Assert.Equal(Scoring.Foundation, game.Score);
    }

    [Fact]
    public void HasAutoPlayMove_TrueWhenTopCardCanGoUp()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "AD");

        Assert.True(game.HasAutoPlayMove());

        game.AutoPlayToFoundation();

        Assert.False(game.HasAutoPlayMove());
    }

    [Fact]
    public void HasAutoPlayMove_FalseWhenNothingFits()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "5H");

        Assert.False(game.HasAutoPlayMove());
    }

    [Fact]
    public void AutoPlay_IsUndoable()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetWaste(game, "2H", "AH"); // ász a talon tetején
        string before = Snap.Of(game);

        int placed = game.AutoPlayToFoundation();
        Assert.Equal(2, placed);

        for (int i = 0; i < placed; i++)
        {
            Assert.True(game.Undo().Success);
        }

        Assert.Equal(before, Snap.Of(game));
        Assert.Equal(0, game.Score);
    }
}
