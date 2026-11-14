using System;
using System.Collections.Generic;
using System.Linq;
using Pasziansz.Game;
using Pasziansz.Models;
using Xunit;

namespace Pasziansz.Tests;

/// <summary>Pontozás (Scoring) tesztek.</summary>
public class ScoringTests
{
    [Fact]
    public void Constants_MatchDocumentedValues()
    {
        Assert.Equal(10, Scoring.Foundation);
        Assert.Equal(5, Scoring.WasteToTableau);
        Assert.Equal(0, Scoring.TableauToTableau);
        Assert.Equal(15, Scoring.FlipCard);
        Assert.Equal(-15, Scoring.FoundationToTableau);
        Assert.Equal(-20, Scoring.RecycleStock);
        Assert.Equal(0, Scoring.StockToWaste);
    }

    [Fact]
    public void Clamp_NeverGoesBelowZero()
    {
        Assert.Equal(0, Scoring.Clamp(-1));
        Assert.Equal(0, Scoring.Clamp(-1000));
        Assert.Equal(0, Scoring.Clamp(0));
        Assert.Equal(7, Scoring.Clamp(7));
    }

    [Fact]
    public void ForTransfer_ToFoundation_IsTen()
    {
        Assert.Equal(Scoring.Foundation,
            Scoring.ForTransfer(PileRef.Tableau(0), PileRef.Foundation(0), false, true));

        Assert.Equal(Scoring.Foundation,
            Scoring.ForTransfer(PileRef.Waste(), PileRef.Foundation(3), false, true));
    }

    [Fact]
    public void ForTransfer_WasteToTableau()
    {
        Assert.Equal(Scoring.WasteToTableau,
            Scoring.ForTransfer(PileRef.Waste(), PileRef.Tableau(0), false, false));
    }

    [Fact]
    public void ForTransfer_FoundationToTableau()
    {
        Assert.Equal(Scoring.FoundationToTableau,
            Scoring.ForTransfer(PileRef.Foundation(0), PileRef.Tableau(3), false, false));
    }

    [Fact]
    public void ForTransfer_TableauToTableau()
    {
        Assert.Equal(0, Scoring.ForTransfer(PileRef.Tableau(1), PileRef.Tableau(2), false, false));
    }

    [Fact]
    public void ForTransfer_FlipBonusIsAdded()
    {
        Assert.Equal(Scoring.Foundation + Scoring.FlipCard,
            Scoring.ForTransfer(PileRef.Tableau(0), PileRef.Foundation(1), true, true));

        Assert.Equal(Scoring.WasteToTableau + Scoring.FlipCard,
            Scoring.ForTransfer(PileRef.Waste(), PileRef.Tableau(0), true, false));
    }

    [Fact]
    public void RulesSummary_ListsTheMainScores()
    {
        var summary = Scoring.RulesSummary;

        Assert.Contains("+10", summary);
        Assert.Contains("+5", summary);
        Assert.Contains("+15", summary);
        Assert.Contains("-20", summary.Replace('−', '-'));
    }
}

/// <summary>Győzelem-figyelés: IsWon, CardsOnFoundation, GameWon esemény.</summary>
public class WinTests
{
    [Fact]
    public void IsWon_FalseWhileCardsRemain()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetFoundation(game, 2, "AH", "2H");
        BoardSetup.SetTableau(game, 0, 0, "KH");

        Assert.Equal(2, game.CardsOnFoundation);
        Assert.False(game.IsWon);

        Assert.True(game.Move(PileRef.Tableau(0), 0, PileRef.Tableau(1)).Success);
        Assert.False(game.IsWon);
    }

    [Fact]
    public void IsWon_TrueWhenAllFiftyTwoCardsAreOnFoundations()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.FillFoundationExceptKings(game);
        BoardSetup.SetTableau(game, 0, 0, "KC");
        BoardSetup.SetTableau(game, 1, 0, "KD");
        BoardSetup.SetTableau(game, 2, 0, "KH");
        BoardSetup.SetTableau(game, 3, 0, "KS");

        Assert.Equal(48, game.CardsOnFoundation);
        Assert.False(game.IsWon);

        game.Move(PileRef.Tableau(0), 0, PileRef.Foundation(0));
        game.Move(PileRef.Tableau(1), 0, PileRef.Foundation(1));
        game.Move(PileRef.Tableau(2), 0, PileRef.Foundation(2));
        Assert.False(game.IsWon);
        Assert.Equal(51, game.CardsOnFoundation);

        var last = game.Move(PileRef.Tableau(3), 0, PileRef.Foundation(3));

        Assert.True(last.Success);
        Assert.Equal(52, game.CardsOnFoundation);
        Assert.True(game.IsWon);
    }

    [Fact]
    public void GameWon_FiresExactlyOnce()
    {
        var game = BoardSetup.EmptyGame();
        // 48 lap a gyűjtőhelyeken (színenként ász..dáma), a négy király az oszlopokban.
        BoardSetup.FillFoundationExceptKings(game);
        BoardSetup.SetTableau(game, 0, 0, "KC");
        BoardSetup.SetTableau(game, 1, 0, "KD");
        BoardSetup.SetTableau(game, 2, 0, "KH");
        BoardSetup.SetTableau(game, 3, 0, "KS");

        int won = 0;
        game.GameWon += (_, _) => won++;

        Assert.Equal(48, game.CardsOnFoundation);

        // 49., 50. és 51. lap: még nincs győzelem.
        Assert.True(game.Move(PileRef.Tableau(0), 0, PileRef.Foundation(0)).Success);
        Assert.True(game.Move(PileRef.Tableau(1), 0, PileRef.Foundation(1)).Success);
        Assert.True(game.Move(PileRef.Tableau(2), 0, PileRef.Foundation(2)).Success);
        Assert.Equal(51, game.CardsOnFoundation);
        Assert.False(game.IsWon);
        Assert.Equal(0, won);

        // Az 52. lap váltja ki a győzelmet – egyszer.
        Assert.True(game.Move(PileRef.Tableau(3), 0, PileRef.Foundation(3)).Success);
        Assert.True(game.IsWon);
        Assert.Equal(52, game.CardsOnFoundation);
        Assert.Equal(1, won);
    }

    [Fact]
    public void GameWon_DoesNotFireOnNormalMoves()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "AH");
        int won = 0;
        game.GameWon += (_, _) => won++;

        game.Move(PileRef.Tableau(0), 0, PileRef.Foundation(2));

        Assert.Equal(0, won);
    }

    [Fact]
    public void Game_CanBeWonByAutoPlay_AndFiresEvent()
    {
        // Olyan osztás, amelyben a húzópakli látható sorrendje színenként ász..
        // király, így az automatikus felpakolás a parti végére mindent felvisz.
        var play = new[] { "C", "D", "H", "S" }
            .SelectMany(suit => new[] { "A", "2", "3", "4", "5", "6", "7", "8", "9", "T", "J", "Q", "K" }
                .Select(rank => rank + suit))
            .ToArray();

        // 28 lap az oszlopokba (top-first darabolás, majd megfordítás), 24 a pakliba.
        var columns = new List<string[]>();
        int cursor = 0;
        for (int i = 0; i < 7; i++)
        {
            var chunk = play.Skip(cursor).Take(i + 1).ToArray();
            cursor += i + 1;
            columns.Add(chunk.Reverse().ToArray()); // alulról felfelé
        }

        var stockDrawOrder = play.Skip(cursor).ToArray();
        var deck = DeckBuilder.Build(columns, stockDrawOrder);

        var game = new KlondikeGame(new Random(0));
        int won = 0;
        game.GameWon += (_, _) => won++;
        game.NewGame(1, deck);

        for (int step = 0; step < 400 && !game.IsWon; step++)
        {
            if (game.AutoPlayToFoundation() > 0)
            {
                continue;
            }

            if (game.Board.Stock.Pile.Count > 0)
            {
                game.DrawFromStock();
                continue;
            }

            if (game.Board.Waste.Pile.Count > 0)
            {
                game.RecycleWaste();
                continue;
            }

            break;
        }

        // Ebben az osztásban a húzási sorrend színenként ász..király, így a parti
        // újrakeverés nélkül is kirakható (ezt a lépésszám alatti ellenőrzés mutatja).
        Assert.True(game.IsWon, "A felépített osztást ki kell rakni.");
        Assert.Equal(52, game.CardsOnFoundation);
        Assert.Equal(1, won);
        Assert.Equal(0, game.Board.Stock.Pile.Count);
        Assert.Equal(0, game.Board.Waste.Pile.Count);
    }
}
