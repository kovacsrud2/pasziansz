using Pasziansz.Game;
using Pasziansz.Models;
using Xunit;

namespace Pasziansz.Tests;

/// <summary>Az áthelyezés (Move) tesztjei kézzel felépített táblákon.</summary>
public class MoveTests
{
    [Fact]
    public void Move_SingleCardToTableau_SucceedsAndMovesCard()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "5S");
        BoardSetup.SetTableau(game, 1, 0, "6H");

        var result = game.Move(PileRef.Tableau(0), 0, PileRef.Tableau(1));

        Assert.True(result.Success);
        Assert.Empty(game.Board.Tableau[0].Pile.Cards);
        Assert.Equal(2, game.Board.Tableau[1].Pile.Count);
        Assert.Equal(Cards.C("5S"), game.Board.Tableau[1].Pile.TopCard);
        Assert.Equal(1, game.MoveCount);
        Assert.NotNull(result.Record);
        Assert.Equal(MoveKind.Transfer, result.Record!.Kind);
        Assert.Equal(PileRef.Tableau(0), result.Record.From);
        Assert.Equal(PileRef.Tableau(1), result.Record.To);
    }

    [Fact]
    public void Move_WholeRunMovesTogether()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "9C", "8H", "7S");
        BoardSetup.SetTableau(game, 1, 0, "TD"); // piros tízes a fekete kilences alá

        var result = game.Move(PileRef.Tableau(0), 0, PileRef.Tableau(1));

        Assert.True(result.Success);
        Assert.Empty(game.Board.Tableau[0].Pile.Cards);
        Assert.Equal(4, game.Board.Tableau[1].Pile.Count);
        Assert.Equal(3, result.Record!.Cards.Count);
    }

    [Fact]
    public void Move_PartOfRunMovesFromIndex()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "9C", "8H", "7S");
        BoardSetup.SetTableau(game, 1, 0, "9S");

        var result = game.Move(PileRef.Tableau(0), 1, PileRef.Tableau(1));

        Assert.True(result.Success);
        Assert.Equal(Cards.C("9C"), game.Board.Tableau[0].Pile.TopCard);
        Assert.Equal(1, game.Board.Tableau[0].Pile.Count);
        Assert.Equal(3, game.Board.Tableau[1].Pile.Count);
    }

    [Fact]
    public void Move_ToFoundation_OnlyAceAndInOrder()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "AH");

        var result = game.Move(PileRef.Tableau(0), 0, PileRef.Foundation(2));

        Assert.True(result.Success);
        Assert.Equal(Cards.C("AH"), game.Board.Foundation[2].Pile.TopCard);
        Assert.Equal(Scoring.Foundation, game.Score);
    }

    [Fact]
    public void Move_ToWrongFoundation_IsRejected()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "AH");

        var result = game.Move(PileRef.Tableau(0), 0, PileRef.Foundation(0)); // treff gyűjtőhely

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        Assert.Empty(game.Board.Foundation[0].Pile.Cards);
        Assert.Single(game.Board.Tableau[0].Pile.Cards); // a lap a helyén marad
    }

    [Fact]
    public void Move_MultipleCardsToFoundation_IsRejected()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "2H", "AH");

        var result = game.Move(PileRef.Tableau(0), 0, PileRef.Foundation(2));

        Assert.False(result.Success);
        Assert.Equal(2, game.Board.Tableau[0].Pile.Count);
    }

    [Fact]
    public void Move_ToEmptyColumnOnlyKing()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "QH");

        var rejected = game.Move(PileRef.Tableau(0), 0, PileRef.Tableau(4));
        Assert.False(rejected.Success);
        Assert.Empty(game.Board.Tableau[4].Pile.Cards);
        Assert.Single(game.Board.Tableau[0].Pile.Cards);

        BoardSetup.SetTableau(game, 0, 0, "KH");
        var accepted = game.Move(PileRef.Tableau(0), 0, PileRef.Tableau(4));
        Assert.True(accepted.Success);
        Assert.Equal(Cards.C("KH"), game.Board.Tableau[4].Pile.TopCard);
    }

    [Fact]
    public void Move_WrongColorOrder_IsRejected()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "5H");
        BoardSetup.SetTableau(game, 1, 0, "6D");

        var result = game.Move(PileRef.Tableau(0), 0, PileRef.Tableau(1));

        Assert.False(result.Success);
        Assert.Equal("5H", DeckBuilder.CodeOf(game.Board.Tableau[0].Pile.TopCard!));
    }

    [Fact]
    public void Move_FaceDownCard_IsRejected()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 2, "5H", "4S");
        BoardSetup.SetTableau(game, 1, 0, "6D");

        var result = game.Move(PileRef.Tableau(0), 0, PileRef.Tableau(1));

        Assert.False(result.Success);
        Assert.Equal(2, game.Board.Tableau[0].Pile.Count);
    }

    [Fact]
    public void Move_FromStock_IsRejected()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetStock(game, "5H", "4S");
        BoardSetup.SetTableau(game, 0, 0, "6D");

        var result = game.Move(PileRef.Stock(), 1, PileRef.Tableau(0));

        Assert.False(result.Success);
        Assert.Equal(2, game.Board.Stock.Pile.Count);
    }

    [Fact]
    public void Move_FromWasteToTableau_ScoresFive()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetWaste(game, "7D");
        BoardSetup.SetTableau(game, 0, 0, "8C");

        var result = game.Move(PileRef.Waste(), 0, PileRef.Tableau(0));

        Assert.True(result.Success);
        Assert.Empty(game.Board.Waste.Pile.Cards);
        Assert.Equal(Scoring.WasteToTableau, game.Score);
    }

    [Fact]
    public void Move_FromFoundationBackToTableau_PenaltyApplied()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "3S");
        BoardSetup.SetTableau(game, 1, 0, "2H");
        BoardSetup.SetTableau(game, 2, 0, "AC");
        BoardSetup.SetFoundation(game, 2, "AH");

        // Előbb szerezzünk 20 pontot, hogy a -15 büntetés látszódjon.
        Assert.True(game.Move(PileRef.Tableau(2), 0, PileRef.Foundation(0)).Success);
        Assert.True(game.Move(PileRef.Tableau(1), 0, PileRef.Foundation(2)).Success);
        Assert.Equal(2 * Scoring.Foundation, game.Score);

        var back = game.Move(PileRef.Foundation(2), 1, PileRef.Tableau(0));

        Assert.True(back.Success);
        Assert.Equal((2 * Scoring.Foundation) + Scoring.FoundationToTableau, game.Score);
    }

    [Fact]
    public void Move_ToSamePile_ReturnsNothing()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "5S");

        var result = game.Move(PileRef.Tableau(0), 0, PileRef.Tableau(0));

        Assert.False(result.Success);
        Assert.Empty(result.Message);
        Assert.Equal(0, game.MoveCount);
        Assert.False(game.CanUndo);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void Move_InvalidSourceIndex_IsRejected(int index)
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "5S");
        BoardSetup.SetTableau(game, 1, 0, "6H");

        var result = game.Move(PileRef.Tableau(0), index, PileRef.Tableau(1));

        Assert.False(result.Success);
        Assert.Equal("Érvénytelen lap.", result.Message);
    }

    [Fact]
    public void Move_IntoWaste_IsRejected()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "5S");

        var result = game.Move(PileRef.Tableau(0), 0, PileRef.Waste());

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public void Move_FlipsExposedCardAndScoresFifteen()
    {
        var game = BoardSetup.EmptyGame();
        var pile0 = BoardSetup.SetTableau(game, 0, 1, "8C", "7H");
        BoardSetup.SetTableau(game, 1, 0, "8S");

        var result = game.Move(PileRef.Tableau(0), 1, PileRef.Tableau(1));

        Assert.True(result.Success);
        Assert.Equal(0, pile0.FaceDownCount);
        Assert.True(pile0.IsFaceUp(0));
        Assert.Equal(Scoring.FlipCard, game.Score);
        Assert.Equal(Cards.C("8C"), result.Record!.FlippedCard);
    }

    [Fact]
    public void Move_ErrorMessageIsUserFriendly_Hungarian()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "5H");
        BoardSetup.SetTableau(game, 1, 0, "6D");

        var result = game.Move(PileRef.Tableau(0), 0, PileRef.Tableau(1));

        Assert.False(result.Success);
        Assert.Contains("5", result.Message);   // az elvárt lap értéke
        Assert.Contains("színnel", result.Message);
    }

    [Fact]
    public void Move_FiresMovePerformedAndGameChanged()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "5S");
        BoardSetup.SetTableau(game, 1, 0, "6H");

        MoveRecord? recorded = null;
        int changed = 0;
        game.MovePerformed += (_, record) => recorded = record;
        game.GameChanged += (_, _) => changed++;

        game.Move(PileRef.Tableau(0), 0, PileRef.Tableau(1));

        Assert.NotNull(recorded);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Move_RejectedMoveFiresNoEvents()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "5H");
        BoardSetup.SetTableau(game, 1, 0, "6D");

        int performed = 0;
        int changed = 0;
        game.MovePerformed += (_, _) => performed++;
        game.GameChanged += (_, _) => changed++;

        game.Move(PileRef.Tableau(0), 0, PileRef.Tableau(1));

        Assert.Equal(0, performed);
        Assert.Equal(0, changed);
    }

    [Fact]
    public void Move_LastActionDescribesTheMove()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "5S");
        BoardSetup.SetTableau(game, 1, 0, "6H");

        game.Move(PileRef.Tableau(0), 0, PileRef.Tableau(1));

        Assert.Contains("5♠", game.LastAction);
        Assert.Contains("oszlop", game.LastAction);
    }

    [Fact]
    public void Move_ScoreNeverGoesBelowZero()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetFoundation(game, 2, "AH", "2H");
        BoardSetup.SetTableau(game, 0, 0, "3S");

        var result = game.Move(PileRef.Foundation(2), 1, PileRef.Tableau(0));

        Assert.True(result.Success);
        Assert.Equal(0, game.Score); // -15 helyett 0
    }
}
