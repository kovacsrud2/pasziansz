using System;
using System.Collections.Generic;
using System.Linq;
using Pasziansz.Models;

namespace Pasziansz.Tests;

/// <summary>
/// Kártya-kódok olvasása és determinisztikus paklik összeállítása a tesztekhez.
/// A kód két részből áll: érték + szín, pl. <c>AS</c> = ász pikk, <c>TH</c> = tízes kőr,
/// <c>QD</c> = dáma káró, <c>7C</c> = hetes treff.
/// <para>
/// A tízesre itt a <c>T</c> jelölést használjuk (a paklik olvashatóbbak így), a játék
/// viszont a felületen <c>10♥</c> formában írja ki – lásd <see cref="Card.RankToCode"/>.
/// </para>
/// </summary>
internal static class Cards
{
    /// <summary>Egyetlen kártya a kódja alapján.</summary>
    public static Card C(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Üres kártyakód.", nameof(code));
        }

        code = code.Trim().ToUpperInvariant();
        string rankPart = code[..^1];
        Suit suit = code[^1] switch
        {
            'C' => Suit.Clubs,
            'D' => Suit.Diamonds,
            'H' => Suit.Hearts,
            'S' => Suit.Spades,
            _ => throw new ArgumentException($"Ismeretlen szín a kódban: {code}", nameof(code))
        };

        int rank = rankPart switch
        {
            "A" => 1,
            "J" => 11,
            "Q" => 12,
            "K" => 13,
            "T" => 10,
            _ => int.TryParse(rankPart, out int parsed)
                ? parsed
                : throw new ArgumentException($"Ismeretlen érték a kódban: {code}", nameof(code))
        };

        return new Card(suit, rank);
    }

    /// <summary>Több kártya kódokból, a megadott sorrendben.</summary>
    public static List<Card> Many(params string[] codes) => codes.Select(C).ToList();
}

/// <summary>
/// Determinisztikus pakli összeállítása a Klondike osztási sorrendjében.
/// Az osztás a pakli <b>első</b> 28 lapját használja (az i. oszlop i+1 lapot kap),
/// a maradék 24 lap kerül a húzópakliba, a 28. indextől a teteje felé.
/// Ezért a <c>stockDrawOrder</c> a húzások sorrendjében adja meg a lapokat
/// (az első elem lesz az elsőként húzott lap).
/// </summary>
internal static class DeckBuilder
{
    /// <summary>Rendezett 52 lapos pakli a <see cref="Deck.CreateOrdered"/> sorrendjében.</summary>
    public static List<Card> OrderedDeck() => Deck.CreateOrdered();

    /// <summary>Az összes lap kódja a rendezett pakli sorrendjében (treff ász..király, káró, kőr, pikk).</summary>
    public static string[] AllCodes() => Deck.CreateOrdered().Select(CodeOf).ToArray();

    /// <summary>Kártya kódja (pl. <c>AS</c>, <c>TH</c>).</summary>
    public static string CodeOf(Card card) => Card.RankToCode(card.Rank) + SuitCode(card.Suit);

    /// <summary>A szín kódjele (C, D, H, S).</summary>
    public static string SuitCode(Suit suit) => suit switch
    {
        Suit.Clubs => "C",
        Suit.Diamonds => "D",
        Suit.Hearts => "H",
        _ => "S"
    };

    /// <summary>
    /// Pakli építése: hét oszlop (alulról felfelé) + a húzópakli húzási sorrendje.
    /// Ellenőrzi, hogy 7 oszlop van, az i. oszlopban i+1 lap, és hogy mind az 52 lap
    /// pontosan egyszer szerepel.
    /// </summary>
    public static List<Card> Build(IReadOnlyList<string[]> columns, IReadOnlyList<string> stockDrawOrder)
    {
        if (columns.Count != 7)
        {
            throw new ArgumentException($"Hét oszlop kell, de {columns.Count} van.", nameof(columns));
        }

        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].Length != i + 1)
            {
                throw new ArgumentException(
                    $"A(z) {i + 1}. oszlopban {i + 1} lapnak kell lennie, de {columns[i].Length} van.",
                    nameof(columns));
            }
        }

        if (stockDrawOrder.Count != 24)
        {
            throw new ArgumentException(
                $"A húzópakliba 24 lap kell, de {stockDrawOrder.Count} van.", nameof(stockDrawOrder));
        }

        var deal = columns.SelectMany(column => column.Select(Cards.C)).ToList();
        var stock = Enumerable.Reverse(stockDrawOrder).Select(Cards.C).ToList();
        var deck = deal.Concat(stock).ToList();

        if (!Deck.IsCompleteDeck(deck))
        {
            var duplicates = deck.GroupBy(c => c).Where(g => g.Count() > 1).Select(g => g.Key.ShortCode);
            throw new ArgumentException(
                "A pakli nem 52 különböző lap. Ismétlődő lapok: " + string.Join(", ", duplicates));
        }

        return deck;
    }
}
