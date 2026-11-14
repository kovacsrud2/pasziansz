using System;
using System.Collections.Generic;
using System.Linq;
using Pasziansz.Models;

namespace Pasziansz.Game
{
    /// <summary>A lépés eredménye – a nézet ebből tudja, mit kell animálni/hangosítani.</summary>
    public sealed class MoveResult
    {
        private MoveResult(bool success, string message, MoveRecord? record)
        {
            Success = success;
            Message = message;
            Record = record;
        }

        public bool Success { get; }

        public string Message { get; }

        public MoveRecord? Record { get; }

        public static MoveResult Ok(MoveRecord? record, string message = "") => new MoveResult(true, message, record);

        public static MoveResult Fail(string message) => new MoveResult(false, message, null);

        public static MoveResult Nothing() => new MoveResult(false, string.Empty, null);
    }

    /// <summary>
    /// A Klondike pasziánsz teljes játéklogikája: osztás, húzás, áthelyezés,
    /// automatikus felpakolás, visszavonás, tippadás és a győzelem figyelése.
    /// A modell nem ismeri a felületet, csak a szabályokat alkalmazza.
    /// </summary>
    public sealed class KlondikeGame
    {
        /// <summary>A visszavonható lépések felső korlátja (a legrégebbiek kiesnek).</summary>
        public const int MaxUndoDepth = 500;

        private readonly List<MoveRecord> _undoStack = new();
        private readonly Random _random;
        private List<Card>? _lastDeal;

        /// <summary>A lépés felvétele a visszavonás-verembe, a mérethatár betartásával.</summary>
        private void PushUndo(MoveRecord record)
        {
            _undoStack.Add(record);
            if (_undoStack.Count > MaxUndoDepth)
            {
                _undoStack.RemoveAt(0);
            }
        }

        public KlondikeGame(Random? random = null)
        {
            _random = random ?? Random.Shared;
            Board = new Board();
        }

        public Board Board { get; }

        /// <summary>Egyszerre 1 vagy 3 lapot húzunk.</summary>
        public int DrawCount { get; private set; } = 1;

        public int Score { get; private set; }

        public int MoveCount { get; private set; }

        public int RecycleCount { get; private set; }

        /// <summary>A gyűjtőhelyeken lévő lapok száma (0..52).</summary>
        public int CardsOnFoundation => Board.Foundation.Sum(f => f.Pile.Count);

        public bool IsWon => CardsOnFoundation == Deck.FullDeckSize;

        public bool CanUndo => _undoStack.Count > 0;

        public int UndoDepth => _undoStack.Count;

        public DateTime GameStartedAt { get; private set; } = DateTime.Now;

        public TimeSpan Elapsed => DateTime.Now - GameStartedAt;

        /// <summary>Az utolsó lépés szöveges leírása (állapotsor).</summary>
        public string LastAction { get; private set; } = string.Empty;

        /// <summary>Az utolsó osztás lapjai (a húzópakli aljától a tetejéig), az újraosztáshoz.</summary>
        public bool CanRestart => _lastDeal is { Count: var count } && count == Deck.FullDeckSize;

        public event EventHandler<MoveRecord>? MovePerformed;

        public event EventHandler? GameChanged;

        public event EventHandler? GameWon;

        /// <summary>Új osztás. A <paramref name="drawCount"/> 1 vagy 3.</summary>
        public void NewGame(int drawCount, IEnumerable<Card>? forcedDeck = null)
        {
            DrawCount = drawCount == 3 ? 3 : 1;
            Score = 0;
            MoveCount = 0;
            RecycleCount = 0;
            _undoStack.Clear();
            Board.Reset();

            var deck = forcedDeck?.ToList() ?? Deck.CreateShuffled(_random);
            if (deck.Count != Deck.FullDeckSize)
            {
                deck = Deck.CreateShuffled(_random);
            }

            _lastDeal = deck.ToList();

            int cursor = 0;

            // Klondike osztás: az i. oszlop i+1 lapot kap, ebből i lap lefordítva,
            // a legfelső felfordítva.
            for (int i = 0; i < 7; i++)
            {
                var pile = Board.Tableau[i].Pile;
                for (int j = 0; j <= i; j++)
                {
                    pile.Add(deck[cursor++]);
                }

                pile.FaceDownCount = i;
            }

            var stock = Board.Stock.Pile;
            for (; cursor < deck.Count; cursor++)
            {
                stock.Add(deck[cursor]);
            }

            stock.FaceDownCount = stock.Count;
            Board.Waste.Pile.FaceDownCount = 0;
            GameStartedAt = DateTime.Now;
            LastAction = "Új osztás";

            GameChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Az utolsó osztás megismétlése ugyanazokkal a lapokkal, tiszta pontszámmal.</summary>
        public MoveResult RestartCurrentDeal()
        {
            if (!CanRestart)
            {
                return MoveResult.Fail("Nincs újraindítható osztás.");
            }

            var deal = _lastDeal!;
            var drawCount = DrawCount;
            NewGame(drawCount, deal);
            LastAction = "Az osztás megismételve";
            GameChanged?.Invoke(this, EventArgs.Empty);
            return MoveResult.Ok(null, LastAction);
        }

        private PileLocation Get(PileRef reference) => Board.Get(reference);

        /// <summary>Húzás a húzópakliból a talonba (1 vagy 3 lap). Ha a pakli üres, újrakever.</summary>
        public MoveResult DrawFromStock()
        {
            var stock = Board.Stock.Pile;
            if (stock.Count == 0)
            {
                return RecycleWaste();
            }

            var waste = Board.Waste.Pile;
            int toDraw = Math.Min(DrawCount, stock.Count);
            var drawn = new List<Card>(toDraw);
            for (int i = 0; i < toDraw; i++)
            {
                var card = stock.RemoveAt(stock.Count - 1);
                waste.Add(card);
                drawn.Add(card);
            }

            MoveCount++;
            LastAction = toDraw == 1 ? $"Húztál: {drawn[^1].ShortCode}" : $"{toDraw} lapot húztál";

            var record = new MoveRecord(MoveKind.DrawFromStock)
            {
                From = PileRef.Stock(),
                To = PileRef.Waste(),
                Cards = drawn,
                ScoreBefore = Score,
                MovesBefore = MoveCount - 1
            };

            PushUndo(record);
            MovePerformed?.Invoke(this, record);
            GameChanged?.Invoke(this, EventArgs.Empty);
            return MoveResult.Ok(record, LastAction);
        }

        /// <summary>A talon lapjainak visszafordítása a húzópakliba (a sorrend megfordul).</summary>
        public MoveResult RecycleWaste()
        {
            var stock = Board.Stock.Pile;
            var waste = Board.Waste.Pile;

            if (waste.Count == 0)
            {
                return MoveResult.Fail("Nincs több lap a pakliban – nincs mit újrakeverni.");
            }

            int scoreBefore = Score;
            int movesBefore = MoveCount;
            var cards = waste.Snapshot().ToList();

            waste.Clear();
            // A talon tetejéről indulva kerülnek a pakli aljára, így a sorrend megfordul.
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                stock.Add(cards[i]);
            }

            stock.FaceDownCount = stock.Count;
            RecycleCount++;
            MoveCount++;
            Score = Scoring.Clamp(Score + Scoring.RecycleStock);
            LastAction = "A talon újrakeverve";

            var record = new MoveRecord(MoveKind.RecycleStock)
            {
                From = PileRef.Waste(),
                To = PileRef.Stock(),
                Cards = cards,
                ScoreBefore = scoreBefore,
                MovesBefore = movesBefore
            };

            PushUndo(record);
            MovePerformed?.Invoke(this, record);
            GameChanged?.Invoke(this, EventArgs.Empty);
            return MoveResult.Ok(record, LastAction);
        }

        /// <summary>Egy kártya felfordítása az oszlopban (kézzel történő felfedés).</summary>
        public MoveResult FlipTopCard(PileRef reference)
        {
            var pile = Get(reference).Pile;
            if (reference.Kind != PileKind.Tableau || pile.Count == 0)
            {
                return MoveResult.Fail("Itt nincs felfordítható lap.");
            }

            if (pile.FaceDownCount == 0 || pile.FaceDownCount != pile.Count)
            {
                return MoveResult.Fail("Ebben az oszlopban nincs lefordított lap.");
            }

            int scoreBefore = Score;
            int movesBefore = MoveCount;
            pile.FaceDownCount--;
            var card = pile.TopCard;

            var record = new MoveRecord(MoveKind.Flip)
            {
                From = reference,
                FlippedCard = card,
                Cards = card != null ? new[] { card } : Array.Empty<Card>(),
                ScoreBefore = scoreBefore,
                MovesBefore = movesBefore
            };

            PushUndo(record);
            MoveCount++;
            LastAction = $"{card?.ShortCode} felfordítva";
            MovePerformed?.Invoke(this, record);
            GameChanged?.Invoke(this, EventArgs.Empty);
            return MoveResult.Ok(record, LastAction);
        }

        /// <summary>
        /// Lap(ok) áthelyezése. A <paramref name="sourceIndex"/> a forrásrakásban lévő
        /// kártya indexe (innen a tetejéig mozognak a lapok).
        /// </summary>
        public MoveResult Move(PileRef from, int sourceIndex, PileRef to, bool validateRun = true)
        {
            var source = Get(from);
            var target = Get(to);

            if (ReferenceEquals(source, target))
            {
                return MoveResult.Nothing();
            }

            var sourcePile = source.Pile;
            var targetPile = target.Pile;

            if (sourceIndex < 0 || sourceIndex >= sourcePile.Count)
            {
                return MoveResult.Fail("Érvénytelen lap.");
            }

            if (validateRun && !Rules.CanPickUp(sourcePile, sourceIndex, out string reason))
            {
                return MoveResult.Fail(reason);
            }

            var moving = sourcePile.TakeFrom(sourceIndex);
            var leadCard = moving[0];

            if (target.Kind == PileKind.Foundation)
            {
                if (moving.Count != 1)
                {
                    return MoveResult.Fail("A gyűjtőhelyre csak egy lap tehető.");
                }

                var suit = target.FoundationSuit!.Value;
                if (!Rules.CanPlaceOnFoundation(leadCard, targetPile.TopCard, suit))
                {
                    string needed = targetPile.TopCard is null
                        ? $"{suit.ToHungarianName()} ász"
                        : $"{Card.RankToHungarian(targetPile.TopCard.Rank + 1)} {suit.ToHungarianName()}";
                    return MoveResult.Fail($"Ide csak {needed} kerülhet.");
                }
            }
            else if (target.Kind == PileKind.Tableau)
            {
                if (!Rules.CanPlaceOnTableau(leadCard, targetPile.TopCard))
                {
                    return targetPile.IsEmpty
                        ? MoveResult.Fail("Üres oszlopra csak király kerülhet.")
                        : MoveResult.Fail(
                            $"Erre a lapra csak {Card.RankToHungarian(targetPile.TopCard!.Rank - 1)} " +
                            $"{(targetPile.TopCard.IsRed ? "fekete" : "piros")} színnel lehet pakolni.");
                }
            }
            else
            {
                return MoveResult.Fail("Erre a rakásra nem lehet lapot tenni.");
            }

            int scoreBefore = Score;
            int movesBefore = MoveCount;

            // Eltávolítás a forrásból: mindig ugyanazt az indexet vesszük, mert a
            // felette lévő lapok minden törlésnél eggyel lejjebb csúsznak.
            for (int i = 0; i < moving.Count; i++)
            {
                sourcePile.RemoveAt(sourceIndex);
            }

            foreach (var card in moving)
            {
                targetPile.Add(card);
            }

            // A forrás legfelső lapjának felfordítása
            Card? flipped = null;
            if (source.Kind == PileKind.Tableau
                && sourcePile.Count > 0
                && sourcePile.FaceDownCount == sourcePile.Count)
            {
                sourcePile.FaceDownCount--;
                flipped = sourcePile.TopCard;
            }

            Score = Scoring.Clamp(Score + Scoring.ForTransfer(from, to, flipped != null,
                target.Kind == PileKind.Foundation));
            MoveCount++;

            var parts = string.Join(" ", moving.Select(c => c.ShortCode));
            LastAction = $"{parts} → {to}";

            var record = MoveRecord.Transfer(from, to, moving, flipped, scoreBefore, movesBefore);
            PushUndo(record);

            MovePerformed?.Invoke(this, record);
            GameChanged?.Invoke(this, EventArgs.Empty);

            if (IsWon)
            {
                GameWon?.Invoke(this, EventArgs.Empty);
            }

            return MoveResult.Ok(record, LastAction);
        }

        /// <summary>Az utolsó lépés visszavonása.</summary>
        public MoveResult Undo()
        {
            if (_undoStack.Count == 0)
            {
                return MoveResult.Fail("Nincs visszavonható lépés.");
            }

            var record = _undoStack[^1];
            _undoStack.RemoveAt(_undoStack.Count - 1);

            switch (record.Kind)
            {
                case MoveKind.Transfer:
                {
                    var source = Get(record.From);
                    var target = Get(record.To);

                    // A lépéskor felfordított lap visszafordítása
                    if (record.FlippedCard != null)
                    {
                        source.Pile.FaceDownCount = Math.Min(source.Pile.Count, source.Pile.FaceDownCount + 1);
                    }

                    // A lapok visszahelyezése a forrásba (fordított sorrendben a tetejéről)
                    for (int i = record.Cards.Count - 1; i >= 0; i--)
                    {
                        target.Pile.Remove(record.Cards[i]);
                    }

                    foreach (var card in record.Cards)
                    {
                        source.Pile.Add(card);
                    }

                    break;
                }

                case MoveKind.RecycleStock:
                {
                    var stock = Board.Stock.Pile;
                    var waste = Board.Waste.Pile;
                    stock.Clear();
                    waste.Clear();
                    foreach (var card in record.Cards)
                    {
                        waste.Add(card);
                    }

                    stock.FaceDownCount = stock.Count;
                    RecycleCount = Math.Max(0, RecycleCount - 1);
                    break;
                }

                case MoveKind.DrawFromStock:
                {
                    var stock = Board.Stock.Pile;
                    var waste = Board.Waste.Pile;
                    for (int i = record.Cards.Count - 1; i >= 0; i--)
                    {
                        waste.Remove(record.Cards[i]);
                        stock.Add(record.Cards[i]);
                    }

                    stock.FaceDownCount = stock.Count;
                    break;
                }

                case MoveKind.Flip:
                {
                    var pile = Get(record.From).Pile;
                    pile.FaceDownCount = Math.Min(pile.Count, pile.FaceDownCount + 1);
                    break;
                }
            }

            Score = Scoring.Clamp(record.ScoreBefore);
            MoveCount = record.MovesBefore;
            LastAction = "Lépés visszavonva";
            GameChanged?.Invoke(this, EventArgs.Empty);
            return MoveResult.Ok(record, LastAction);
        }

        /// <summary>
        /// A felvehető lapok automatikus felpakolása a gyűjtőhelyre.
        /// Visszaadja a felpakolt lapok számát.
        /// </summary>
        public int AutoPlayToFoundation(int maxCards = 52)
        {
            int placed = 0;
            bool progress = true;

            while (progress && placed < maxCards)
            {
                progress = false;

                var waste = Board.Waste.Pile;
                if (waste.Count > 0)
                {
                    var card = waste.TopCard!;
                    var foundation = Board.FindFoundationFor(card);
                    if (foundation != null &&
                        Rules.CanPlaceOnFoundation(card, foundation.Pile.TopCard, card.Suit))
                    {
                        var result = Move(PileRef.Waste(), waste.Count - 1, PileRef.Foundation(foundation.Index));
                        if (result.Success)
                        {
                            placed++;
                            progress = true;
                            continue;
                        }
                    }
                }

                foreach (var column in Board.Tableau)
                {
                    var pile = column.Pile;
                    if (pile.Count == 0)
                    {
                        continue;
                    }

                    var card = pile.TopCard!;
                    var foundation = Board.FindFoundationFor(card);
                    if (foundation != null &&
                        Rules.CanPlaceOnFoundation(card, foundation.Pile.TopCard, card.Suit))
                    {
                        var result = Move(PileRef.Tableau(column.Index), pile.Count - 1,
                            PileRef.Foundation(foundation.Index));
                        if (result.Success)
                        {
                            placed++;
                            progress = true;
                            break;
                        }
                    }
                }
            }

            return placed;
        }

        /// <summary>Létezik-e most olyan lap, amit automatikusan fel lehet pakolni.</summary>
        public bool HasAutoPlayMove()
        {
            var waste = Board.Waste.Pile;
            if (waste.Count > 0)
            {
                var card = waste.TopCard!;
                var f = Board.FindFoundationFor(card);
                if (f != null && Rules.CanPlaceOnFoundation(card, f.Pile.TopCard, card.Suit))
                {
                    return true;
                }
            }

            foreach (var column in Board.Tableau)
            {
                if (column.Pile.Count == 0)
                {
                    continue;
                }

                var card = column.Pile.TopCard!;
                var f = Board.FindFoundationFor(card);
                if (f != null && Rules.CanPlaceOnFoundation(card, f.Pile.TopCard, card.Suit))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Tipp: egy hasznos lépés keresése, vagy null, ha nincs.</summary>
        public Hint? FindHint()
        {
            var waste = Board.Waste.Pile;

            // 1) A gyűjtőhelyre tehető lap a legjobb lépés
            if (waste.Count > 0)
            {
                var card = waste.TopCard!;
                var f = Board.FindFoundationFor(card);
                if (f != null && Rules.CanPlaceOnFoundation(card, f.Pile.TopCard, card.Suit))
                {
                    return new Hint(PileRef.Waste(), waste.Count - 1, PileRef.Foundation(f.Index),
                        $"Tedd a {card.ShortCode} lapot a(z) {card.Suit.ToHungarianName()} gyűjtőhelyre.");
                }
            }

            foreach (var column in Board.Tableau)
            {
                if (column.Pile.Count == 0)
                {
                    continue;
                }

                var card = column.Pile.TopCard!;
                var f = Board.FindFoundationFor(card);
                if (f != null && Rules.CanPlaceOnFoundation(card, f.Pile.TopCard, card.Suit))
                {
                    return new Hint(PileRef.Tableau(column.Index), column.Pile.Count - 1,
                        PileRef.Foundation(f.Index),
                        $"Tedd a {card.ShortCode} lapot a(z) {card.Suit.ToHungarianName()} gyűjtőhelyre.");
                }
            }

            // 2) Oszlop tetejének átmozgatása, ha ezzel felfordul egy lap
            for (int i = 0; i < Board.Tableau.Count; i++)
            {
                var pile = Board.Tableau[i].Pile;
                if (pile.Count == 0 || !pile.IsFaceUp(pile.Count - 1))
                {
                    continue;
                }

                int firstUp = pile.FirstFaceUpIndex;
                if (firstUp >= pile.Count - 1 || !Rules.IsMovableRun(pile, firstUp))
                {
                    continue;
                }

                var card = pile.Cards[firstUp];
                for (int j = 0; j < Board.Tableau.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    var target = Board.Tableau[j].Pile;
                    if (Rules.CanPlaceOnTableau(card, target.TopCard))
                    {
                        return new Hint(PileRef.Tableau(i), firstUp, PileRef.Tableau(j),
                            $"Mozgasd a(z) {card.ShortCode} alatti sort a(z) {j + 1}. oszlopra, " +
                            "hogy felforduljon a fedett lap.");
                    }
                }
            }

            // 3) Bármilyen érvényes áthelyezés a talonból
            for (int i = 0; i < Board.Tableau.Count; i++)
            {
                var pile = Board.Tableau[i].Pile;
                if (waste.Count > 0 && Rules.CanPlaceOnTableau(waste.TopCard!, pile.TopCard))
                {
                    return new Hint(PileRef.Waste(), waste.Count - 1, PileRef.Tableau(i),
                        $"Tedd a {waste.TopCard!.ShortCode} lapot a(z) {i + 1}. oszlopra.");
                }
            }

            // 4) Húzás / újrakeverés
            if (Board.Stock.Pile.Count > 0)
            {
                return new Hint(PileRef.Stock(), -1, PileRef.Waste(), "Húzz a húzópakliból.");
            }

            if (waste.Count > 0)
            {
                return new Hint(PileRef.Waste(), -1, PileRef.Stock(), "Keverd újra a talont, hogy újra húzhass.");
            }

            return null;
        }

        /// <summary>Egy tipp leírása.</summary>
        public sealed record Hint(PileRef From, int SourceIndex, PileRef To, string Description);
    }
}
