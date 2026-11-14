using System;
using System.Collections.Generic;
using System.Linq;
using Pasziansz.Models;
using Xunit;

namespace Pasziansz.Tests;

/// <summary>A kártya (Card) és a pakli (Deck) tesztjei.</summary>
public class CardAndDeckTests
{
    [Fact]
    public void Card_RejectsOutOfRangeRank()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Card(Suit.Hearts, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Card(Suit.Hearts, 14));
    }

    [Fact]
    public void Card_RankAndSuitAreStored()
    {
        var card = new Card(Suit.Spades, 12);

        Assert.Equal(Suit.Spades, card.Suit);
        Assert.Equal(12, card.Rank);
        Assert.True(card.IsBlack);
        Assert.False(card.IsRed);
    }

    [Theory]
    [InlineData(Suit.Diamonds, true)]
    [InlineData(Suit.Hearts, true)]
    [InlineData(Suit.Clubs, false)]
    [InlineData(Suit.Spades, false)]
    public void Card_ColorFollowsSuit(Suit suit, bool isRed)
    {
        var card = new Card(suit, 7);

        Assert.Equal(isRed, card.IsRed);
        Assert.Equal(!isRed, card.IsBlack);
    }

    [Fact]
    public void Card_EqualityIsValueBased()
    {
        var a = new Card(Suit.Hearts, 5);
        var b = new Card(Suit.Hearts, 5);
        var c = new Card(Suit.Hearts, 6);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, null);
    }

    [Theory]
    [InlineData(1, "A")]
    [InlineData(10, "T")]
    [InlineData(11, "J")]
    [InlineData(12, "Q")]
    [InlineData(13, "K")]
    [InlineData(7, "7")]
    public void Card_RankCodes(int rank, string expected)
    {
        Assert.Equal(expected, Card.RankToCode(rank));
    }

    [Theory]
    [InlineData(1, "ász")]
    [InlineData(11, "bubi")]
    [InlineData(12, "dáma")]
    [InlineData(13, "király")]
    [InlineData(9, "9")]
    public void Card_RankNamesAreHungarian(int rank, string expected)
    {
        Assert.Equal(expected, Card.RankToHungarian(rank));
    }

    [Fact]
    public void Card_DisplayNameAndShortCode()
    {
        var card = new Card(Suit.Diamonds, 11);

        Assert.Equal("bubi káró", card.DisplayName);
        Assert.Equal("J♦", card.ShortCode);
    }

    [Fact]
    public void Card_SuitHelpers()
    {
        Assert.Equal("treff", Suit.Clubs.ToHungarianName());
        Assert.Equal("káró", Suit.Diamonds.ToHungarianName());
        Assert.Equal("kőr", Suit.Hearts.ToHungarianName());
        Assert.Equal("pikk", Suit.Spades.ToHungarianName());

        Assert.Equal("clubs", Suit.Clubs.ToFileName());
        Assert.Equal("diamonds", Suit.Diamonds.ToFileName());
        Assert.Equal("hearts", Suit.Hearts.ToFileName());
        Assert.Equal("spades", Suit.Spades.ToFileName());
    }

    [Fact]
    public void Card_ImageResourceKeyIsStable()
    {
        // A kulcsot a ResourceDictionary használja, ezért nem változhat.
        Assert.Equal("Card.hearts.ace", new Card(Suit.Hearts, 1).ImageResourceKey);
        Assert.Equal("Card.spades.10", new Card(Suit.Spades, 10).ImageResourceKey);
        Assert.Equal("Card.clubs.king", new Card(Suit.Clubs, 13).ImageResourceKey);
    }

    // ------------------------------------------------------------------
    //  Pakli
    // ------------------------------------------------------------------

    [Fact]
    public void Deck_OrderedHasFiftyTwoDistinctCards()
    {
        var deck = Deck.CreateOrdered();

        Assert.Equal(Deck.FullDeckSize, deck.Count);
        Assert.Equal(Deck.FullDeckSize, deck.Distinct().Count());
        Assert.True(Deck.IsCompleteDeck(deck));
    }

    [Fact]
    public void Deck_OrderedStartsAtClubsAceAndEndsAtSpadesKing()
    {
        var deck = Deck.CreateOrdered();

        Assert.Equal(new Card(Suit.Clubs, 1), deck[0]);
        Assert.Equal(new Card(Suit.Spades, 13), deck[^1]);
    }

    [Fact]
    public void Deck_ShuffleKeepsAllCardsAndIsDeterministicPerSeed()
    {
        var source = Deck.CreateOrdered();

        var first = Deck.Shuffle(source, new Random(42));
        var second = Deck.Shuffle(source, new Random(42));
        var other = Deck.Shuffle(source, new Random(43));

        Assert.True(Deck.IsCompleteDeck(first));
        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        Assert.Equal(source, Deck.CreateOrdered()); // a forrás nem változik
    }

    [Fact]
    public void Deck_ShuffleActuallyChangesOrder()
    {
        var source = Deck.CreateOrdered();
        var shuffled = Deck.Shuffle(source, new Random(7));

        Assert.False(source.SequenceEqual(shuffled));
    }

    [Fact]
    public void Deck_IsCompleteDeckRejectsIncompleteOrDuplicated()
    {
        var full = Deck.CreateOrdered();

        Assert.False(Deck.IsCompleteDeck(full.Take(51)));
        Assert.False(Deck.IsCompleteDeck(full.Append(full[0])));
        Assert.False(Deck.IsCompleteDeck(Array.Empty<Card>()));
    }
}
