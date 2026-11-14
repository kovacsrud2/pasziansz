using System;
using System.Collections.Generic;
using System.Linq;

namespace Pasziansz.Models
{
    /// <summary>Egy 52 lapos francia pakli létrehozása, keverése és kiosztása.</summary>
    public static class Deck
    {
        public const int FullDeckSize = 52;

        /// <summary>Új, szabályos (rendezett) 52 lapos pakli.</summary>
        public static List<Card> CreateOrdered()
        {
            var cards = new List<Card>(FullDeckSize);
            foreach (Suit suit in Enum.GetValues<Suit>())
            {
                for (int rank = 1; rank <= 13; rank++)
                {
                    cards.Add(new Card(suit, rank));
                }
            }

            return cards;
        }

        /// <summary>Megkevert pakli. A keverés kriptográfiailag jó minőségű véletlenszámokat használ.</summary>
        public static List<Card> CreateShuffled(Random? random = null) => Shuffle(CreateOrdered(), random);

        /// <summary>Fisher–Yates keverés.</summary>
        public static List<Card> Shuffle(List<Card> cards, Random? random = null)
        {
            var rng = random ?? Random.Shared;
            var result = new List<Card>(cards);
            for (int i = result.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }

            return result;
        }

        /// <summary>A pakli érvényessége: 52 különböző lap.</summary>
        public static bool IsCompleteDeck(IEnumerable<Card> cards)
        {
            var list = cards.ToList();
            return list.Count == FullDeckSize && list.Distinct().Count() == FullDeckSize;
        }
    }
}
