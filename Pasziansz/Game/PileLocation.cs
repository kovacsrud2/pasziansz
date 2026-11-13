using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Pasziansz.Models;

namespace Pasziansz.Game
{
    /// <summary>
    /// A nézet számára kényelmes burkoló egy <see cref="Pile"/> körül. A rakás
    /// helyét a rácson (oszlop, sor) adja meg, valamint a kártyák közötti
    /// eltolásokat, amelyekből az egyes lapok helye kiszámítható.
    /// </summary>
    public sealed class PileLocation : INotifyPropertyChanged
    {
        private double _horizontalOffset;

        public PileLocation(Pile pile, int column, int row, double faceDownStep, double faceUpStep)
        {
            Pile = pile;
            Column = column;
            Row = row;
            FaceDownStep = faceDownStep;
            FaceUpStep = faceUpStep;
        }

        public Pile Pile { get; }

        public PileKind Kind => Pile.Kind;

        /// <summary>A rács oszlopa (0-tól).</summary>
        public int Column { get; }

        /// <summary>A rács sora (0-tól).</summary>
        public int Row { get; }

        /// <summary>Lehet-e kártyát tenni erre a rakásra.</summary>
        public bool CanDrop { get; init; } = true;

        /// <summary>Az átfedés mértéke a lefordított lapok között.</summary>
        public double FaceDownStep { get; set; }

        /// <summary>Az átfedés mértéke a felfordított lapok között.</summary>
        public double FaceUpStep { get; set; }

        /// <summary>Vízszintes eltolás (a talonnál a háromlapos rajongáshoz).</summary>
        public double HorizontalOffset
        {
            get => _horizontalOffset;
            set
            {
                if (Math.Abs(_horizontalOffset - value) > 0.01)
                {
                    _horizontalOffset = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Id => $"{Kind}_{Index}";

        public int Index => Pile.Index;

        /// <summary>Az oszlop fejléce (a felületen megjeleníthető angol/magyar rövidítés).</summary>
        public string ShortLabel => Kind switch
        {
            PileKind.Tableau => (Index + 1).ToString(),
            PileKind.Foundation => FoundationSymbol,
            PileKind.Waste => "T",
            _ => string.Empty
        };

        public string FoundationSymbol => Index switch
        {
            0 => "♣",
            1 => "♦",
            2 => "♥",
            3 => "♠",
            _ => string.Empty
        };

        /// <summary>A gyűjtőhely színe (a kártya színe szerint).</summary>
        public Suit? FoundationSuit => Kind == PileKind.Foundation
            ? Index switch { 0 => Suit.Clubs, 1 => Suit.Diamonds, 2 => Suit.Hearts, _ => Suit.Spades }
            : null;

        public bool IsRedFoundation => FoundationSuit?.IsRed() ?? false;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>A teljes játéktábla: a hét oszlop, négy gyűjtőhely, talon és húzópakli.</summary>
    public sealed class Board
    {
        public Board()
        {
            Tableau = new List<PileLocation>(7);
            Foundation = new List<PileLocation>(4);

            for (int i = 0; i < 7; i++)
            {
                Tableau.Add(new PileLocation(new Pile(PileKind.Tableau, i), i, 2, 14, 30));
            }

            for (int i = 0; i < 4; i++)
            {
                Foundation.Add(new PileLocation(new Pile(PileKind.Foundation, i), i + 3, 0, 0, 0));
            }

            Stock = new PileLocation(new Pile(PileKind.Stock, 0), 0, 0, 0, 0);
            Waste = new PileLocation(new Pile(PileKind.Waste, 0), 1, 0, 0, 0) { HorizontalOffset = 24 };
        }

        public List<PileLocation> Tableau { get; }

        public List<PileLocation> Foundation { get; }

        public PileLocation Stock { get; }

        public PileLocation Waste { get; }

        /// <summary>Minden rakás egységes listája a nézet számára.</summary>
        public IEnumerable<PileLocation> All
        {
            get
            {
                foreach (var p in Tableau) yield return p;
                foreach (var p in Foundation) yield return p;
                yield return Stock;
                yield return Waste;
            }
        }

        public PileLocation Get(PileRef reference) => reference.Kind switch
        {
            PileKind.Tableau => Tableau[reference.Index],
            PileKind.Foundation => Foundation[reference.Index],
            PileKind.Waste => Waste,
            _ => Stock
        };

        /// <summary>A gyűjtőhely, ahová az adott lap kerülhet (ha van ilyen).</summary>
        public PileLocation? FindFoundationFor(Card card) =>
            Foundation.FirstOrDefault(f => f.FoundationSuit == card.Suit);

        /// <summary>Összes kártya a táblán (ebben a sorrendben: oszlopok, gyűjtőhelyek, talon, pakli).</summary>
        public List<Card> AllCards()
        {
            var list = new List<Card>(52);
            foreach (var p in Tableau) list.AddRange(p.Pile.Cards);
            foreach (var p in Foundation) list.AddRange(p.Pile.Cards);
            list.AddRange(Waste.Pile.Cards);
            list.AddRange(Stock.Pile.Cards);
            return list;
        }

        /// <summary>Visszaállítja a táblát üres állapotba.</summary>
        public void Reset()
        {
            foreach (var p in All)
            {
                p.Pile.Clear();
                p.Pile.FaceDownCount = 0;
            }
        }
    }
}
