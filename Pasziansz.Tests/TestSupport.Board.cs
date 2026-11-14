using System;
using System.Collections.Generic;
using System.Linq;
using Pasziansz.Game;
using Pasziansz.Models;

namespace Pasziansz.Tests;

/// <summary>Teljes tábla-állapot szöveges lenyomata (a visszavonás ellenőrzéséhez).</summary>
internal static class Snap
{
    /// <summary>Minden rakás: típus, sorszám, lefordított lapok száma és a lapok sorrendje.</summary>
    public static string Of(KlondikeGame game) => string.Join(
        "|",
        game.Board.All.Select(location =>
            $"{location.Kind}#{location.Index}[{location.Pile.FaceDownCount}]" +
            string.Join(",", location.Pile.Cards.Select(c => c.ShortCode))));

    /// <summary>A táblán lévő összes lap (52 lapos parti esetén 52 elem).</summary>
    public static List<Card> AllCards(KlondikeGame game) => game.Board.AllCards();
}

/// <summary>
/// Kézzel felépített tábla-állapotok a teszteléshez (a rakások tartalmának
/// közvetlen beállítása a nyilvános <see cref="Board"/> API-n keresztül).
/// </summary>
internal static class BoardSetup
{
    /// <summary>Új, kiosztott, majd teljesen kiürített játék (0 pont, 0 lépés, üres tábla).</summary>
    public static KlondikeGame EmptyGame(int drawCount = 1, int seed = 1)
    {
        var game = new KlondikeGame(new Random(seed));
        game.NewGame(drawCount);
        ClearAll(game);
        return game;
    }

    /// <summary>Minden rakás kiürítése.</summary>
    public static void ClearAll(KlondikeGame game)
    {
        foreach (var location in game.Board.All)
        {
            location.Pile.Clear();
            location.Pile.FaceDownCount = 0;
        }
    }

    /// <summary>Egy oszlop beállítása (alulról felfelé), a megadott számú lefordított lappal.</summary>
    public static Pile SetTableau(KlondikeGame game, int column, int faceDown, params string[] codes)
    {
        var pile = game.Board.Tableau[column].Pile;
        pile.Clear();
        pile.FaceDownCount = 0;
        foreach (var code in codes)
        {
            pile.Add(Cards.C(code));
        }

        pile.FaceDownCount = faceDown;
        return pile;
    }

    /// <summary>Egy gyűjtőhely beállítása (ász .. a megadott lap).</summary>
    public static Pile SetFoundation(KlondikeGame game, int index, params string[] codes)
    {
        var pile = game.Board.Foundation[index].Pile;
        pile.Clear();
        foreach (var code in codes)
        {
            pile.Add(Cards.C(code));
        }

        pile.FaceDownCount = 0;
        return pile;
    }

    /// <summary>A húzópakli beállítása (a lista eleje a pakli alja, a vége a teteje).</summary>
    public static Pile SetStock(KlondikeGame game, params string[] codes)
    {
        var pile = game.Board.Stock.Pile;
        pile.Clear();
        foreach (var code in codes)
        {
            pile.Add(Cards.C(code));
        }

        pile.FaceDownCount = pile.Count;
        return pile;
    }

    /// <summary>A talon beállítása (a lista eleje a talon alja).</summary>
    public static Pile SetWaste(KlondikeGame game, params string[] codes)
    {
        var pile = game.Board.Waste.Pile;
        pile.Clear();
        foreach (var code in codes)
        {
            pile.Add(Cards.C(code));
        }

        pile.FaceDownCount = 0;
        return pile;
    }

    /// <summary>
    /// A gyűjtőhelyek feltöltése minden szín ászával..dámájával (12-12 lap, összesen 48),
    /// így már csak a négy király hiányzik a győzelemhez.
    /// </summary>
    public static void FillFoundationExceptKings(KlondikeGame game)
    {
        SetFoundation(game, 0, FaceCodes("C"));
        SetFoundation(game, 1, FaceCodes("D"));
        SetFoundation(game, 2, FaceCodes("H"));
        SetFoundation(game, 3, FaceCodes("S"));
    }

    /// <summary>Egy szín ásztól dámáig tartó lapjai (12 lap).</summary>
    public static string[] FaceCodes(string suitCode) =>
        new[] { "A", "2", "3", "4", "5", "6", "7", "8", "9", "T", "J", "Q" }
            .Select(rank => rank + suitCode)
            .ToArray();

    /// <summary>Egy szín 13 lapjának kódja (ász..király).</summary>
    public static string[] ValueCodes(string suitCode) =>
        new[] { "A", "2", "3", "4", "5", "6", "7", "8", "9", "T", "J", "Q", "K" }
            .Select(rank => rank + suitCode)
            .ToArray();
}

/// <summary>
/// Egyszerű „játékos” a tesztekhez: a szabályok alapján összegyűjti a most
/// érvényes lépéseket. Csak a nyilvános <see cref="Rules"/> API-t használja.
/// </summary>
internal static class LegalMoves
{
    public readonly record struct Move(PileRef From, int Index, PileRef To);

    public static List<Move> For(KlondikeGame game)
    {
        var moves = new List<Move>();
        var waste = game.Board.Waste.Pile;

        if (waste.Count > 0)
        {
            var card = waste.TopCard!;
            int index = waste.Count - 1;
            foreach (var foundation in game.Board.Foundation)
            {
                if (Rules.CanPlaceOnFoundation(card, foundation.Pile.TopCard, foundation.FoundationSuit!.Value))
                {
                    moves.Add(new Move(PileRef.Waste(), index, PileRef.Foundation(foundation.Index)));
                }
            }

            foreach (var column in game.Board.Tableau)
            {
                if (Rules.CanPlaceOnTableau(card, column.Pile.TopCard))
                {
                    moves.Add(new Move(PileRef.Waste(), index, PileRef.Tableau(column.Index)));
                }
            }
        }

        foreach (var column in game.Board.Tableau)
        {
            var pile = column.Pile;
            for (int index = 0; index < pile.Count; index++)
            {
                if (!Rules.CanPickUp(pile, index, out _))
                {
                    continue;
                }

                var moving = pile.Cards.Skip(index).ToList();
                if (moving.Count == 1)
                {
                    foreach (var foundation in game.Board.Foundation)
                    {
                        if (Rules.CanPlaceOnFoundation(moving[0], foundation.Pile.TopCard,
                                foundation.FoundationSuit!.Value))
                        {
                            moves.Add(new Move(PileRef.Tableau(column.Index), index,
                                PileRef.Foundation(foundation.Index)));
                        }
                    }
                }

                foreach (var target in game.Board.Tableau)
                {
                    if (target.Index == column.Index)
                    {
                        continue;
                    }

                    if (Rules.CanPlaceOnTableau(moving[0], target.Pile.TopCard))
                    {
                        moves.Add(new Move(PileRef.Tableau(column.Index), index, PileRef.Tableau(target.Index)));
                    }
                }
            }
        }

        return moves;
    }
}
