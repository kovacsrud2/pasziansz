using Pasziansz.Game;
using Pasziansz.Models;
using Xunit;

namespace Pasziansz.Tests;

/// <summary>Húzás (DrawFromStock) és újrakeverés (RecycleWaste) tesztek.</summary>
public class DrawTests
{
    [Fact]
    public void DrawFromStock_OneCardMode_MovesTopCardToWaste()
    {
        var game = BoardSetup.EmptyGame(drawCount: 1);
        BoardSetup.SetStock(game, "5H", "4S", "3D");

        var result = game.DrawFromStock();

        Assert.True(result.Success);
        Assert.Equal(2, game.Board.Stock.Pile.Count);
        Assert.Single(game.Board.Waste.Pile.Cards);
        Assert.Equal(Cards.C("3D"), game.Board.Waste.Pile.TopCard); // a pakli teteje
        Assert.Equal(1, game.MoveCount);
        Assert.Equal(0, game.Score); // a húzásért nincs pont
    }

    [Fact]
    public void DrawFromStock_ThreeCardMode_MovesThreeCards()
    {
        var game = BoardSetup.EmptyGame(drawCount: 3);
        BoardSetup.SetStock(game, "5H", "4S", "3D", "2C", "AH");

        game.DrawFromStock();

        Assert.Equal(2, game.Board.Stock.Pile.Count);
        Assert.Equal(3, game.Board.Waste.Pile.Count);
        // A pakli tetejéről húzunk: AH, majd 2C, végül 3D kerül a talonra.
        Assert.Equal(Cards.C("AH"), game.Board.Waste.Pile.Cards[0]);
        Assert.Equal(Cards.C("2C"), game.Board.Waste.Pile.Cards[1]);
        Assert.Equal(Cards.C("3D"), game.Board.Waste.Pile.TopCard);
    }

    [Fact]
    public void DrawFromStock_ThreeCardMode_NearEndDrawsRemaining()
    {
        var game = BoardSetup.EmptyGame(drawCount: 3);
        BoardSetup.SetStock(game, "5H", "4S");

        game.DrawFromStock();

        Assert.Empty(game.Board.Stock.Pile.Cards);
        Assert.Equal(2, game.Board.Waste.Pile.Count);
    }

    [Fact]
    public void DrawFromStock_EmptyStock_RecyclesWaste()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetWaste(game, "5H", "4S", "3D");

        var result = game.DrawFromStock();

        Assert.True(result.Success);
        Assert.Equal(3, game.Board.Stock.Pile.Count);
        Assert.Empty(game.Board.Waste.Pile.Cards);
        Assert.Equal(1, game.RecycleCount);
        Assert.Equal("A talon újrakeverve", game.LastAction);
    }

    [Fact]
    public void DrawFromStock_EmptyStockAndWaste_FailsWithoutSideEffect()
    {
        var game = BoardSetup.EmptyGame();

        var result = game.DrawFromStock();

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        Assert.Equal(0, game.MoveCount);
        Assert.False(game.CanUndo);
    }

    [Fact]
    public void DrawFromStock_StockUpdatedFaceDownCount()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetStock(game, "5H", "4S", "3D");

        game.DrawFromStock();

        Assert.Equal(2, game.Board.Stock.Pile.FaceDownCount);
        Assert.Equal(0, game.Board.Waste.Pile.FaceDownCount);
    }

    [Fact]
    public void RecycleWaste_ReversesOrderOntoStock()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetWaste(game, "5H", "4S", "3D"); // 3D a talon teteje

        var result = game.RecycleWaste();

        Assert.True(result.Success);
        Assert.Empty(game.Board.Waste.Pile.Cards);
        Assert.Equal(3, game.Board.Stock.Pile.Count);
        // Az új pakli aljára a talon teteje kerül, így újra a régi legalsó lap
        // következik először – a húzási sorrend nem változik.
        Assert.Equal(Cards.C("5H"), game.Board.Stock.Pile.TopCard);
        Assert.Equal(Cards.C("3D"), game.Board.Stock.Pile.BottomCard);
        Assert.Equal(3, game.Board.Stock.Pile.FaceDownCount);
    }

    [Fact]
    public void RecycleWaste_KeepsDrawOrder()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetWaste(game, "5H", "4S", "3D"); // húzási sorrend: 5H, 4S, 3D

        game.RecycleWaste();
        game.DrawFromStock();

        Assert.Equal(Cards.C("5H"), game.Board.Waste.Pile.TopCard);
    }

    [Fact]
    public void RecycleWaste_ScoresMinusTwentyButNotBelowZero()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetWaste(game, "5H", "4S");

        game.RecycleWaste();

        Assert.Equal(0, game.Score); // -20 → 0

        // Két felpakolt ász (+20), utána az újrakeverés -20 → 0.
        var scorer = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(scorer, 0, 0, "AH");
        BoardSetup.SetTableau(scorer, 1, 0, "AC");
        scorer.Move(PileRef.Tableau(0), 0, PileRef.Foundation(2));
        scorer.Move(PileRef.Tableau(1), 0, PileRef.Foundation(0));
        Assert.Equal(2 * Scoring.Foundation, scorer.Score);
        BoardSetup.SetWaste(scorer, "5H", "4S");

        scorer.RecycleWaste();

        Assert.Equal(0, scorer.Score); // 20 - 20 = 0
    }

    [Fact]
    public void RecycleWaste_EmptyWaste_Fails()
    {
        var game = BoardSetup.EmptyGame();

        var result = game.RecycleWaste();

        Assert.False(result.Success);
        Assert.Contains("nincs mit", result.Message);
        Assert.Equal(0, game.RecycleCount);
        Assert.Equal(0, game.MoveCount);
    }

    [Fact]
    public void RecycleWaste_CountsMovesAndRecycles()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetWaste(game, "5H");

        game.RecycleWaste();

        Assert.Equal(1, game.RecycleCount);
        Assert.Equal(1, game.MoveCount);
    }

    [Fact]
    public void RepeatedFullGameCycle_KeepsAllFiftyTwoCards()
    {
        var game = new KlondikeGame(new System.Random(31));
        game.NewGame(3);

        for (int i = 0; i < 200; i++)
        {
            game.DrawFromStock();
            var cards = game.Board.AllCards();
            Assert.Equal(Deck.FullDeckSize, cards.Count);
            Assert.Equal(Deck.FullDeckSize, cards.Distinct().Count());
        }

        Assert.True(game.RecycleCount > 0);
    }

    [Fact]
    public void FlipTopCard_FaceDownTopCardIsFlipped()
    {
        var game = BoardSetup.EmptyGame();
        var pile = BoardSetup.SetTableau(game, 0, 1, "7C");

        var result = game.FlipTopCard(PileRef.Tableau(0));

        Assert.True(result.Success);
        Assert.Equal(0, pile.FaceDownCount);
        Assert.True(pile.IsFaceUp(0));
        Assert.Equal(1, game.MoveCount);
        Assert.Equal(0, game.Score); // a kézi felfordításért nincs pont
    }

    [Fact]
    public void FlipTopCard_FullyFaceDownColumn_FlipsOnlyTopCard()
    {
        var game = BoardSetup.EmptyGame();
        var pile = BoardSetup.SetTableau(game, 0, 3, "9C", "8H", "7S");

        var result = game.FlipTopCard(PileRef.Tableau(0));

        Assert.True(result.Success);
        Assert.Equal(2, pile.FaceDownCount);
        Assert.Equal(Cards.C("7S"), pile.TopCard);
        Assert.True(pile.IsFaceUp(2));
        Assert.False(pile.IsFaceUp(1));
    }

    [Fact]
    public void FlipTopCard_WithoutFaceDownCard_Fails()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetTableau(game, 0, 0, "7C");

        var result = game.FlipTopCard(PileRef.Tableau(0));

        Assert.False(result.Success);
        Assert.Equal(0, game.MoveCount);
    }

    [Fact]
    public void FlipTopCard_OnEmptyOrNonTableauPile_Fails()
    {
        var game = BoardSetup.EmptyGame();
        BoardSetup.SetStock(game, "7C");

        Assert.False(game.FlipTopCard(PileRef.Tableau(0)).Success);
        Assert.False(game.FlipTopCard(PileRef.Stock()).Success);
        Assert.False(game.FlipTopCard(PileRef.Waste()).Success);
    }
}
