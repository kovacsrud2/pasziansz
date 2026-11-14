using System.Collections.Generic;
using System.Linq;

namespace Pasziansz.Models
{
    /// <summary>Egy rakás azonosítása (típus + sorszám).</summary>
    public readonly record struct PileRef(PileKind Kind, int Index)
    {
        public static PileRef Tableau(int i) => new PileRef(PileKind.Tableau, i);
        public static PileRef Foundation(int i) => new PileRef(PileKind.Foundation, i);
        public static PileRef Waste() => new PileRef(PileKind.Waste, 0);
        public static PileRef Stock() => new PileRef(PileKind.Stock, 0);

        public override string ToString() => Kind switch
        {
            PileKind.Tableau => $"oszlop {Index + 1}",
            PileKind.Foundation => $"gyűjtőhely {Index + 1}",
            PileKind.Waste => "talon",
            _ => "húzópakli"
        };
    }

    /// <summary>Egy lépés típusa – a visszavonáshoz (Undo).</summary>
    public enum MoveKind
    {
        /// <summary>Kártya(lapok) áthelyezése egyik rakásból a másikba.</summary>
        Transfer,

        /// <summary>A húzópakli újrakeverése a talonból.</summary>
        RecycleStock,

        /// <summary>Húzás a húzópakliból a talonba.</summary>
        DrawFromStock,

        /// <summary>Lap felfordítása egy rakásban (az automatikus felpakolás külön lépése).</summary>
        Flip,

        /// <summary>Új osztás (nem vonható vissza).</summary>
        Deal
    }

    /// <summary>
    /// Egy megtörtént lépés leírása. A visszavonáshoz minden szükséges információt
    /// tárolunk: honnan hová, mely lapok, mit fordított fel a lépés, és mekkora volt
    /// a pontszám/lépésszám előtte.
    /// </summary>
    public sealed class MoveRecord
    {
        public MoveRecord(MoveKind kind)
        {
            Kind = kind;
        }

        public MoveKind Kind { get; init; }

        public PileRef From { get; init; }

        public PileRef To { get; init; }

        /// <summary>Az áthelyezett lapok (alacsonyabbtól a legfelsőig).</summary>
        public IReadOnlyList<Card> Cards { get; init; } = new List<Card>();

        /// <summary>A forrásrakásban felfordított lap (ha volt ilyen).</summary>
        public Card? FlippedCard { get; init; }

        /// <summary>A célrakás tartalma a lépés előtt (újrakeveréshez).</summary>
        public IReadOnlyList<Card> TargetCardsBefore { get; init; } = new List<Card>();

        /// <summary>A lépés előtti pontszám, a visszavonás pontos visszaállításához.</summary>
        public int ScoreBefore { get; init; }

        /// <summary>A lépés előtti lépésszám.</summary>
        public int MovesBefore { get; init; }

        public string Description => Kind switch
        {
            MoveKind.Transfer => Cards.Count > 1
                ? $"{Cards.Count} lap: {From} → {To}"
                : $"{Cards[0].ShortCode}: {From} → {To}",
            MoveKind.RecycleStock => "A talon újrakeverése",
            MoveKind.DrawFromStock => Cards.Count > 1 ? $"{Cards.Count} lap húzása" : $"{Cards[0].ShortCode} húzása",
            MoveKind.Flip => $"{FlippedCard?.ShortCode} felfordítása",
            _ => "Új osztás"
        };

        public override string ToString() => $"{Kind}: {Description}";

        public static MoveRecord Transfer(PileRef from, PileRef to, IEnumerable<Card> cards, Card? flipped,
            int scoreBefore, int movesBefore) => new MoveRecord(MoveKind.Transfer)
        {
            From = from,
            To = to,
            Cards = cards.ToList(),
            FlippedCard = flipped,
            ScoreBefore = scoreBefore,
            MovesBefore = movesBefore
        };
    }
}
