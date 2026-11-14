using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Pasziansz.Models;
using Xunit;

namespace Pasziansz.Tests;

/// <summary>A rakás (Pile) modell tesztjei.</summary>
public class PileTests
{
    [Fact]
    public void NewPile_IsEmpty()
    {
        var pile = new Pile(PileKind.Tableau, 3);

        Assert.Equal(PileKind.Tableau, pile.Kind);
        Assert.Equal(3, pile.Index);
        Assert.Equal(0, pile.Count);
        Assert.True(pile.IsEmpty);
        Assert.True(pile.IsCompletelyEmpty);
        Assert.Null(pile.TopCard);
        Assert.Null(pile.BottomCard);
        Assert.Equal(0, pile.FaceDownCount);
        Assert.Equal(0, pile.FaceUpCount);
    }

    [Fact]
    public void Add_KeepsOrderAndTopCard()
    {
        var pile = new Pile(PileKind.Tableau, 0);
        pile.Add(Cards.C("5H"));
        pile.Add(Cards.C("4S"));
        pile.Add(Cards.C("3D"));

        Assert.Equal(3, pile.Count);
        Assert.Equal(Cards.C("5H"), pile.BottomCard);
        Assert.Equal(Cards.C("3D"), pile.TopCard);

        // A lapok alulról felfelé tárolódnak: 5H = 0, 4S = 1, 3D = 2.
        Assert.Equal(0, pile.IndexOf(Cards.C("5H")));
        Assert.Equal(1, pile.IndexOf(Cards.C("4S")));
        Assert.Equal(2, pile.IndexOf(Cards.C("3D")));
    }

    [Fact]
    public void AddRange_AppendsAll()
    {
        var pile = new Pile(PileKind.Waste, 0);

        pile.AddRange(Cards.Many("AH", "2H", "3H"));

        Assert.Equal(3, pile.Count);
        Assert.Equal(Cards.C("3H"), pile.TopCard);
    }

    [Fact]
    public void FaceDownCount_IsClampedToCardCount()
    {
        var pile = new Pile(PileKind.Tableau, 0);
        pile.Add(Cards.C("5H"));
        pile.Add(Cards.C("4S"));

        pile.FaceDownCount = 10;
        Assert.Equal(2, pile.FaceDownCount);

        pile.FaceDownCount = -3;
        Assert.Equal(0, pile.FaceDownCount);
    }

    [Fact]
    public void FaceDownCount_ShrinksWhenCardsRemoved()
    {
        var pile = new Pile(PileKind.Tableau, 0);
        pile.AddRange(Cards.Many("5H", "4S", "3D"));
        pile.FaceDownCount = 3;

        pile.RemoveAt(2);

        Assert.Equal(2, pile.FaceDownCount);
        Assert.Equal(2, pile.Count);
    }

    [Fact]
    public void FaceDownCount_ShrinksWhenRemoveByReference()
    {
        var pile = new Pile(PileKind.Stock, 0);
        pile.AddRange(Cards.Many("5H", "4S"));
        pile.FaceDownCount = 2;

        Assert.True(pile.Remove(Cards.C("4S")));
        Assert.Equal(1, pile.FaceDownCount);

        Assert.False(pile.Remove(Cards.C("KS")));
        Assert.Equal(1, pile.Count);
    }

    [Fact]
    public void FaceUpAndFirstFaceUpIndex()
    {
        var pile = new Pile(PileKind.Tableau, 0);
        pile.AddRange(Cards.Many("7C", "6H", "5S"));
        pile.FaceDownCount = 1;

        Assert.False(pile.IsFaceUp(0));
        Assert.True(pile.IsFaceUp(1));
        Assert.True(pile.IsFaceUp(2));
        Assert.False(pile.IsFaceUp(3));  // nincs ilyen lap
        Assert.False(pile.IsFaceUp(-1));
        Assert.Equal(1, pile.FirstFaceUpIndex);
        Assert.Equal(2, pile.FaceUpCount);
    }

    [Fact]
    public void TakeFrom_ReturnsCardsFromIndexWithoutRemoving()
    {
        var pile = new Pile(PileKind.Tableau, 0);
        pile.AddRange(Cards.Many("7C", "6H", "5S"));
        pile.FaceDownCount = 1;

        List<Card> taken = pile.TakeFrom(1);

        Assert.Equal(2, taken.Count);
        Assert.Equal(Cards.C("6H"), taken[0]);
        Assert.Equal(Cards.C("5S"), taken[1]);
        Assert.Equal(3, pile.Count); // a TakeFrom nem módosít

        Assert.Empty(pile.TakeFrom(3));
        Assert.Empty(pile.TakeFrom(99));
    }

    [Fact]
    public void Snapshot_IsIndependentCopy()
    {
        var pile = new Pile(PileKind.Waste, 0);
        pile.AddRange(Cards.Many("AH", "2H"));

        var snapshot = pile.Snapshot().ToList();
        pile.Add(Cards.C("3H"));

        Assert.Equal(2, snapshot.Count);
        Assert.Equal(3, pile.Count);
    }

    [Fact]
    public void Clear_EmptiesPileAndResetsFaceDownCount()
    {
        var pile = new Pile(PileKind.Tableau, 0);
        pile.AddRange(Cards.Many("5H", "4S"));
        pile.FaceDownCount = 2;

        pile.Clear();

        Assert.Equal(0, pile.Count);
        Assert.Equal(0, pile.FaceDownCount);
        Assert.True(pile.IsCompletelyEmpty);
        Assert.Null(pile.TopCard);
    }

    [Fact]
    public void Clear_NotifiesAllDependentProperties()
    {
        var pile = new Pile(PileKind.Tableau, 0);
        pile.AddRange(Cards.Many("5H", "4S"));
        pile.FaceDownCount = 2;

        var notified = new List<string>();
        pile.PropertyChanged += (_, e) => notified.Add(e.PropertyName ?? string.Empty);

        pile.Clear();

        foreach (var name in new[] { nameof(Pile.Count), nameof(Pile.TopCard), nameof(Pile.BottomCard),
                     nameof(Pile.FaceUpCount), nameof(Pile.FaceDownCount), nameof(Pile.FirstFaceUpIndex) })
        {
            Assert.Contains(name, notified);
        }
    }

    // ------------------------------------------------------------------
    //  A felület a PropertyChanged-re épül: ezek a regressziós tesztek
    //  a FaceDownCount-változás értesítéseire.
    // ------------------------------------------------------------------

    [Fact]
    public void FaceDownCountChange_NotifiesTopCardAndDerivedProperties()
    {
        var pile = new Pile(PileKind.Tableau, 0);
        pile.AddRange(Cards.Many("5H", "4S"));

        var notified = new List<string>();
        pile.PropertyChanged += (_, e) => notified.Add(e.PropertyName ?? string.Empty);

        pile.FaceDownCount = 1;

        Assert.Contains(nameof(Pile.FaceDownCount), notified);
        Assert.Contains(nameof(Pile.TopCard), notified);
        Assert.Contains(nameof(Pile.FaceUpCount), notified);
        Assert.Contains(nameof(Pile.FirstFaceUpIndex), notified);
    }

    [Fact]
    public void FaceDownCountChange_NoNotificationWhenValueUnchanged()
    {
        var pile = new Pile(PileKind.Tableau, 0);
        pile.AddRange(Cards.Many("5H", "4S"));
        pile.FaceDownCount = 1;

        int notifications = 0;
        pile.PropertyChanged += (_, _) => notifications++;

        pile.FaceDownCount = 1;

        Assert.Equal(0, notifications);
    }

    [Fact]
    public void AddingCard_NotifiesCount()
    {
        var pile = new Pile(PileKind.Waste, 0);
        var notified = new List<string>();
        pile.PropertyChanged += (_, e) => notified.Add(e.PropertyName ?? string.Empty);

        pile.Add(Cards.C("AH"));

        Assert.Contains(nameof(Pile.Count), notified);
        Assert.Contains(nameof(Pile.TopCard), notified);
    }

    [Fact]
    public void ToString_IsInformative()
    {
        var pile = new Pile(PileKind.Tableau, 2);
        pile.Add(Cards.C("5H"));

        Assert.Contains("1", pile.ToString());
        Assert.Contains("Tableau", pile.ToString());
    }
}
