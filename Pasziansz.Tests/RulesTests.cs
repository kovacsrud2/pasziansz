using Pasziansz.Game;
using Pasziansz.Models;
using Xunit;

namespace Pasziansz.Tests;

/// <summary>A szabálymotor (Rules) tesztjei – felület nélkül, tisztán.</summary>
public class RulesTests
{
    // ------------------------------------------------------------------
    //  Oszlopra pakolás
    // ------------------------------------------------------------------

    [Fact]
    public void CanPlaceOnTableau_EmptyColumnAcceptsKingOnly()
    {
        Assert.True(Rules.CanPlaceOnTableau(Cards.C("KS"), null));

        foreach (var code in new[] { "AS", "QS", "2H", "TH" })
        {
            Assert.False(Rules.CanPlaceOnTableau(Cards.C(code), null));
        }
    }

    [Theory]
    [InlineData("6H", "7S")]    // piros hatos fekete hetesre
    [InlineData("6C", "7H")]    // fekete hatos piros hetesre
    [InlineData("AD", "2C")]
    [InlineData("QS", "KH")]
    public void CanPlaceOnTableau_AlternatingColorDescendingAccepted(string card, string top)
    {
        Assert.True(Rules.CanPlaceOnTableau(Cards.C(card), Cards.C(top)));
    }

    [Theory]
    [InlineData("6H", "7D")]    // azonos szín (piros)
    [InlineData("6C", "7S")]    // azonos szín (fekete)
    [InlineData("5H", "7S")]    // nem eggyel kisebb
    [InlineData("8H", "7S")]    // nem eggyel kisebb
    [InlineData("7S", "7H")]    // azonos érték
    [InlineData("KS", "AH")]    // király nem mehet ászra
    public void CanPlaceOnTableau_OtherCombinationsRejected(string card, string top)
    {
        Assert.False(Rules.CanPlaceOnTableau(Cards.C(card), Cards.C(top)));
    }

    [Fact]
    public void CanPlaceOnTableau_EmptyColumnRejectsNonKings()
    {
        Assert.False(Rules.CanPlaceOnTableau(Cards.C("KD"), Cards.C("AH")));
        Assert.False(Rules.CanPlaceOnTableau(Cards.C("QD"), null));
    }

    // ------------------------------------------------------------------
    //  Gyűjtőhelyre pakolás
    // ------------------------------------------------------------------

    [Fact]
    public void CanPlaceOnFoundation_AceOnEmptyPileOfItsSuit()
    {
        Assert.True(Rules.CanPlaceOnFoundation(Cards.C("AH"), null, Suit.Hearts));
        Assert.True(Rules.CanPlaceOnFoundation(Cards.C("AS"), null, Suit.Spades));
        Assert.True(Rules.CanPlaceOnFoundation(Cards.C("AD"), null, Suit.Diamonds));
        Assert.True(Rules.CanPlaceOnFoundation(Cards.C("AC"), null, Suit.Clubs));
    }

    [Fact]
    public void CanPlaceOnFoundation_OnlyAscendingSameSuit()
    {
        Assert.True(Rules.CanPlaceOnFoundation(Cards.C("2H"), Cards.C("AH"), Suit.Hearts));
        Assert.True(Rules.CanPlaceOnFoundation(Cards.C("KH"), Cards.C("QH"), Suit.Hearts));

        Assert.False(Rules.CanPlaceOnFoundation(Cards.C("2S"), Cards.C("AH"), Suit.Hearts)); // más szín
        Assert.False(Rules.CanPlaceOnFoundation(Cards.C("2H"), Cards.C("AH"), Suit.Diamonds)); // másik gyűjtőhely
        Assert.False(Rules.CanPlaceOnFoundation(Cards.C("3H"), Cards.C("AH"), Suit.Hearts)); // kimarad a kettes
        Assert.False(Rules.CanPlaceOnFoundation(Cards.C("AH"), Cards.C("AH"), Suit.Hearts)); // ász nem ismételhető
        Assert.False(Rules.CanPlaceOnFoundation(Cards.C("KH"), null, Suit.Hearts)); // üresre csak ász
    }

    [Fact]
    public void Boards_FoundationSuitMatchesSymbol()
    {
        var board = new Board();

        Assert.Equal(Suit.Clubs, board.Foundation[0].FoundationSuit);
        Assert.Equal(Suit.Diamonds, board.Foundation[1].FoundationSuit);
        Assert.Equal(Suit.Hearts, board.Foundation[2].FoundationSuit);
        Assert.Equal(Suit.Spades, board.Foundation[3].FoundationSuit);
        Assert.Null(board.Stock.FoundationSuit);
        Assert.Null(board.Waste.FoundationSuit);
    }

    [Fact]
    public void Boards_FindFoundationForReturnsMatchingSuit()
    {
        var board = new Board();

        Assert.Same(board.Foundation[2], board.FindFoundationFor(Cards.C("5H")));
        Assert.Same(board.Foundation[0], board.FindFoundationFor(Cards.C("AC")));
    }

    // ------------------------------------------------------------------
    //  Mozgatható sorozat
    // ------------------------------------------------------------------

    [Fact]
    public void IsMovableRun_DetectsValidSequence()
    {
        var pile = new Pile(PileKind.Tableau, 0);
        pile.Add(Cards.C("5H"));
        pile.Add(Cards.C("4S"));
        pile.Add(Cards.C("3D"));
        pile.FaceDownCount = 0;

        Assert.True(Rules.IsMovableRun(pile, 0));
        Assert.True(Rules.IsMovableRun(pile, 1));
        Assert.True(Rules.IsMovableRun(pile, 2));
    }

    [Fact]
    public void IsMovableRun_RejectsWrongColorOrder()
    {
        var pile = new Pile(PileKind.Tableau, 0);
        pile.Add(Cards.C("5H"));
        pile.Add(Cards.C("4D")); // piros a pirosra
        pile.FaceDownCount = 0;

        Assert.False(Rules.IsMovableRun(pile, 0));
        Assert.True(Rules.IsMovableRun(pile, 1));
    }

    [Fact]
    public void IsMovableRun_RejectsNonDescendingOrGap()
    {
        var pileGap = new Pile(PileKind.Tableau, 0);
        pileGap.Add(Cards.C("5H"));
        pileGap.Add(Cards.C("3S")); // kimarad a négyes
        pileGap.FaceDownCount = 0;
        Assert.False(Rules.IsMovableRun(pileGap, 0));

        var pileSame = new Pile(PileKind.Tableau, 0);
        pileSame.Add(Cards.C("5H"));
        pileSame.Add(Cards.C("5S"));
        pileSame.FaceDownCount = 0;
        Assert.False(Rules.IsMovableRun(pileSame, 0));
    }

    [Fact]
    public void IsMovableRun_RejectsFaceDownCardsAndBadIndexes()
    {
        var pile = new Pile(PileKind.Tableau, 0);
        pile.Add(Cards.C("5H"));
        pile.Add(Cards.C("4S"));
        pile.FaceDownCount = 1; // az ötös lefordított

        Assert.False(Rules.IsMovableRun(pile, 0));
        Assert.True(Rules.IsMovableRun(pile, 1));

        Assert.False(Rules.IsMovableRun(pile, -1));
        Assert.False(Rules.IsMovableRun(pile, 2));
        Assert.False(Rules.IsMovableRun(pile, 99));
    }

    // ------------------------------------------------------------------
    //  Felvehetőség
    // ------------------------------------------------------------------

    [Fact]
    public void CanPickUp_StockIsNeverPickable()
    {
        var stock = new Pile(PileKind.Stock, 0);
        stock.Add(Cards.C("5H"));
        stock.FaceDownCount = 1;

        bool result = Rules.CanPickUp(stock, 0, out string reason);

        Assert.False(result);
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Fact]
    public void CanPickUp_WasteAndFoundationOnlyTopCard()
    {
        var waste = new Pile(PileKind.Waste, 0);
        waste.Add(Cards.C("5H"));
        waste.Add(Cards.C("9S"));

        Assert.False(Rules.CanPickUp(waste, 0, out _));
        Assert.True(Rules.CanPickUp(waste, 1, out _));

        var foundation = new Pile(PileKind.Foundation, 0);
        foundation.Add(Cards.C("AH"));
        foundation.Add(Cards.C("2H"));

        Assert.False(Rules.CanPickUp(foundation, 0, out _));
        Assert.True(Rules.CanPickUp(foundation, 1, out _));
    }

    [Fact]
    public void CanPickUp_TableauRequiresFaceUpMovableRun()
    {
        var pile = new Pile(PileKind.Tableau, 0);
        pile.Add(Cards.C("7C"));   // 0 – lefordított
        pile.Add(Cards.C("6H"));   // 1
        pile.Add(Cards.C("5S"));   // 2
        pile.Add(Cards.C("9D"));   // 3 – nem illeszkedik a sorozatba
        pile.FaceDownCount = 1;

        Assert.False(Rules.CanPickUp(pile, 0, out _)); // lefordított
        Assert.True(Rules.CanPickUp(pile, 1, out _));  // 6H 5S érvényes sorozat
        Assert.True(Rules.CanPickUp(pile, 2, out _));
        Assert.True(Rules.CanPickUp(pile, 3, out _));  // a legfelső lap mindig felvehető
        Assert.False(Rules.CanPickUp(pile, 4, out _)); // nincs ilyen lap
        Assert.False(Rules.CanPickUp(pile, -1, out _));
    }

    [Fact]
    public void CanPickUp_RejectsInvalidRunInsideColumn()
    {
        var pile = new Pile(PileKind.Tableau, 0);
        pile.Add(Cards.C("6H"));
        pile.Add(Cards.C("5D")); // piros a pirosra
        pile.FaceDownCount = 0;

        Assert.False(Rules.CanPickUp(pile, 0, out _));
        Assert.True(Rules.CanPickUp(pile, 1, out _));
    }
}
