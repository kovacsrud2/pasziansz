using System;

namespace Pasziansz.Models
{
    /// <summary>A kártya színe (francia színek).</summary>
    public enum Suit
    {
        Clubs = 0,
        Diamonds = 1,
        Hearts = 2,
        Spades = 3
    }

    /// <summary>Kártya színsegéd-osztály.</summary>
    public static class SuitExtensions
    {
        public static bool IsRed(this Suit suit) => suit == Suit.Diamonds || suit == Suit.Hearts;

        public static bool IsBlack(this Suit suit) => !suit.IsRed();

        /// <summary>Magyar megnevezés.</summary>
        public static string ToHungarianName(this Suit suit) => suit switch
        {
            Suit.Clubs => "treff",
            Suit.Diamonds => "káró",
            Suit.Hearts => "kőr",
            _ => "pikk"
        };

        /// <summary>A képfájl nevében használt angol kisbetűs név.</summary>
        public static string ToFileName(this Suit suit) => suit switch
        {
            Suit.Clubs => "clubs",
            Suit.Diamonds => "diamonds",
            Suit.Hearts => "hearts",
            _ => "spades"
        };

        /// <summary>ResourceDictionary kulcs prefixe.</summary>
        public static string ToResourceKeyPart(this Suit suit) => suit.ToFileName();
    }

    /// <summary>Egy játékkártya. A lap azonosítója a szín és az érték (1..13).</summary>
    public sealed class Card : IEquatable<Card>
    {
        public Card(Suit suit, int rank)
        {
            if (rank < 1 || rank > 13)
            {
                throw new ArgumentOutOfRangeException(nameof(rank), "Az érték 1 és 13 között lehet.");
            }

            Suit = suit;
            Rank = rank;
        }

        public Suit Suit { get; }

        /// <summary>1 = ász, 11 = bubi, 12 = dáma, 13 = király.</summary>
        public int Rank { get; }

        public bool IsRed => Suit.IsRed();

        public bool IsBlack => Suit.IsBlack();

        /// <summary>Rövid magyar megnevezés, pl. "ász pikk".</summary>
        public string DisplayName => RankToHungarian(Rank) + " " + Suit.ToHungarianName();

        /// <summary>Rövid kód, pl. "A♠".</summary>
        public string ShortCode => RankToCode(Rank) + SuitSymbol;

        public string SuitSymbol => Suit switch
        {
            Suit.Clubs => "♣",
            Suit.Diamonds => "♦",
            Suit.Hearts => "♥",
            _ => "♠"
        };

        /// <summary>A kártya képe a ResourceDictionary kulcsa alapján.</summary>
        public string ImageResourceKey => $"Card.{Suit.ToResourceKeyPart()}.{RankToFileName(Rank)}";

        public static string RankToFileName(int rank) => rank switch
        {
            1 => "ace",
            11 => "jack",
            12 => "queen",
            13 => "king",
            _ => rank.ToString()
        };

        public static string RankToHungarian(int rank) => rank switch
        {
            1 => "ász",
            11 => "bubi",
            12 => "dáma",
            13 => "király",
            _ => rank.ToString()
        };

        public static string RankToCode(int rank) => rank switch
        {
            1 => "A",
            11 => "J",
            12 => "Q",
            13 => "K",
            _ => rank.ToString()
        };

        public bool Equals(Card? other) => other != null && other.Suit == Suit && other.Rank == Rank;

        public override bool Equals(object? obj) => Equals(obj as Card);

        public override int GetHashCode() => ((int)Suit * 100) + Rank;

        public override string ToString() => DisplayName;
    }
}
