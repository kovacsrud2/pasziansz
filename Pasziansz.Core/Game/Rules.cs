using System;
using System.Collections.Generic;
using System.Linq;
using Pasziansz.Models;

namespace Pasziansz.Game
{
    /// <summary>
    /// A Klondike pasziánsz szabályai. A osztály tisztán a szabályokat ismeri,
    /// a felületről független, így könnyen tesztelhető.
    /// </summary>
    public static class Rules
    {
        /// <summary>
        /// Egy kártya rátehető-e az oszlop tetejére. Az oszlopra csak váltakozó
        /// színnel és eggyel kisebb értékkel lehet pakolni; üres oszlopra csak király.
        /// </summary>
        public static bool CanPlaceOnTableau(Card card, Card? tableauTop)
        {
            if (tableauTop is null)
            {
                return card.Rank == 13;
            }

            return tableauTop.Rank == card.Rank + 1 && tableauTop.IsRed != card.IsRed;
        }

        /// <summary>Egy kártya rátehető-e a gyűjtőhelyre (ászra kettes, ... királyig, azonos szín).</summary>
        public static bool CanPlaceOnFoundation(Card card, Card? foundationTop, Suit foundationSuit)
        {
            if (card.Suit != foundationSuit)
            {
                return false;
            }

            return foundationTop is null ? card.Rank == 1 : foundationTop.Rank == card.Rank - 1;
        }

        /// <summary>
        /// A megadott indextől a rakás tetejéig tartó lapok szabályos, mozgatható
        /// sorozatot alkotnak-e (csökkenő érték, váltakozó szín, mind felfordítva).
        /// </summary>
        public static bool IsMovableRun(Pile pile, int startIndex)
        {
            if (startIndex < 0 || startIndex >= pile.Count)
            {
                return false;
            }

            if (!pile.IsFaceUp(startIndex))
            {
                return false;
            }

            for (int i = startIndex; i < pile.Count - 1; i++)
            {
                var upper = pile.Cards[i];
                var lower = pile.Cards[i + 1];
                if (!pile.IsFaceUp(i + 1) || upper.Rank != lower.Rank + 1 || upper.IsRed == lower.IsRed)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Mozgatható-e a kártya az adott rakásból (a kártya a felső lap vagy szabályos sorozat része).</summary>
        public static bool CanPickUp(Pile pile, int index, out string reason)
        {
            reason = string.Empty;
            if (index < 0 || index >= pile.Count)
            {
                reason = "Nincs ilyen lap.";
                return false;
            }

            switch (pile.Kind)
            {
                case PileKind.Stock:
                    reason = "A húzópakliból csak húzni lehet.";
                    return false;

                case PileKind.Waste:
                    if (index != pile.Count - 1)
                    {
                        reason = "A talonból csak a legfelső lap vehető fel.";
                        return false;
                    }

                    return true;

                case PileKind.Foundation:
                    if (index != pile.Count - 1)
                    {
                        reason = "A gyűjtőhelyről csak a legfelső lap vehető fel.";
                        return false;
                    }

                    return true;

                default:
                    if (!pile.IsFaceUp(index))
                    {
                        reason = "A lefordított lap nem vehető fel.";
                        return false;
                    }

                    if (!IsMovableRun(pile, index))
                    {
                        reason = "Ez a lapsorozat nem mozgatható (csökkenő sorrend, váltakozó szín kell).";
                        return false;
                    }

                    return true;
            }
        }
    }
}
