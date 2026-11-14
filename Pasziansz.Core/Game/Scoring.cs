using System;
using Pasziansz.Models;

namespace Pasziansz.Game
{
    /// <summary>
    /// A Klondike pasziánsz klasszikus pontozása:
    ///  +10 a gyűjtőhelyre tett lapért,  +5 a talonból a játéktérre tett lapért,
    ///  +15 az oszlop aljának felfordításáért,  -15 ha a gyűjtőhelyről visszaveszünk,
    ///  -20 a talon újrakeveréséért. A pontszám soha nem csökken nulla alá.
    /// </summary>
    public static class Scoring
    {
        public const int Foundation = 10;
        public const int WasteToTableau = 5;
        public const int TableauToTableau = 0;
        public const int FlipCard = 15;
        public const int FoundationToTableau = -15;
        public const int RecycleStock = -20;
        public const int StockToWaste = 0;

        /// <summary>Pontváltozás egy áthelyezés alapján.</summary>
        public static int ForTransfer(PileRef from, PileRef to, bool cardFlipped, bool toFoundation)
        {
            int delta = 0;

            if (toFoundation)
            {
                delta += Foundation;
            }
            else if (to.Kind == PileKind.Tableau)
            {
                if (from.Kind == PileKind.Waste)
                {
                    delta += WasteToTableau;
                }
                else if (from.Kind == PileKind.Foundation)
                {
                    delta += FoundationToTableau;
                }
            }
            else if (to.Kind == PileKind.Waste && from.Kind == PileKind.Foundation)
            {
                // A gyűjtőhelyről a talonba nem lehet visszatenni, de biztonsági ág.
                delta += FoundationToTableau;
            }

            if (cardFlipped)
            {
                delta += FlipCard;
            }

            return delta;
        }

        /// <summary>A pontszám soha nem mehet nulla alá.</summary>
        public static int Clamp(int score) => Math.Max(0, score);

        /// <summary>Az egyes lépések pontértéke szövegesen (a súgóhoz).</summary>
        public static string RulesSummary =>
            "+10 pont a gyűjtőhelyre tett lapért\n" +
            "+5 pont a talonból az oszlopra tett lapért\n" +
            "+15 pont az oszlop felfordításáért\n" +
            "−15 pont ha a gyűjtőhelyről visszaveszel egy lapot\n" +
            "−20 pont a talon újrakeveréséért\n" +
            "A pontszám soha nem megy nulla alá.";
    }
}
