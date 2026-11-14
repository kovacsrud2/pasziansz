using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Pasziansz.Models
{
    /// <summary>A kártyarakás típusa.</summary>
    public enum PileKind
    {
        /// <summary>Oszlop: váltakozó színnel, csökkenő értékkel épül.</summary>
        Tableau,

        /// <summary>Gyűjtőhely: azonos színnel, növekvő értékkel, ásztól királyig.</summary>
        Foundation,

        /// <summary>Talon: a húzott lapok helye.</summary>
        Waste,

        /// <summary>Húzópakli (a maradék lapok, lefordítva).</summary>
        Stock
    }

    /// <summary>
    /// Egy kártyarakás. A lapokat a <see cref="Cards"/> gyűjtemény tárolja alulról
    /// felfelé (0. elem van legalul). A kollekció módosításait esemény jelzi,
    /// így a nézet lépésről lépésre követheti a változásokat.
    /// </summary>
    public sealed class Pile : INotifyPropertyChanged
    {
        private readonly ObservableCollection<Card> _cards = new ObservableCollection<Card>();
        private int _faceDownCount;

        public Pile(PileKind kind, int index)
        {
            Kind = kind;
            Index = index;
            _cards.CollectionChanged += OnCardsChanged;
        }

        public PileKind Kind { get; }

        /// <summary>A rakás sorszáma a saját típusán belül (0-tól).</summary>
        public int Index { get; }

        public ObservableCollection<Card> Cards => _cards;

        public int Count => _cards.Count;

        public bool IsEmpty => _cards.Count == 0;

        /// <summary>Hány lap van lefordítva (képpel lefelé) a rakás alján.</summary>
        public int FaceDownCount
        {
            get => _faceDownCount;
            set
            {
                int clamped = Math.Clamp(value, 0, _cards.Count);
                if (_faceDownCount != clamped)
                {
                    _faceDownCount = clamped;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TopCard));
                    OnPropertyChanged(nameof(FaceUpCount));
                    OnPropertyChanged(nameof(FirstFaceUpIndex));
                }
            }
        }

        /// <summary>A legfelső (látható) lap.</summary>
        public Card? TopCard => _cards.Count > 0 ? _cards[^1] : null;

        /// <summary>Az alsó, lefordított lap (ha van).</summary>
        public Card? BottomCard => _cards.Count > 0 ? _cards[0] : null;

        /// <summary>Látható (képpel felfelé lévő) lapok száma.</summary>
        public int FaceUpCount => Math.Max(0, _cards.Count - _faceDownCount);

        /// <summary>Az első képpel felfelé lévő lap indexe.</summary>
        public int FirstFaceUpIndex => _faceDownCount;

        /// <summary>Teljesen üres-e a rakás (sem lap, sem fordítás).</summary>
        public bool IsCompletelyEmpty => _cards.Count == 0;

        public bool Contains(Card card) => _cards.Contains(card);

        public int IndexOf(Card card) => _cards.IndexOf(card);

        public bool IsFaceUp(int index) => index >= _faceDownCount && index >= 0 && index < _cards.Count;

        public void Add(Card card)
        {
            _cards.Add(card);
        }

        public void AddRange(IEnumerable<Card> cards)
        {
            foreach (var card in cards)
            {
                _cards.Add(card);
            }
        }

        public void Insert(int index, Card card) => _cards.Insert(index, card);

        public Card RemoveAt(int index)
        {
            var card = _cards[index];
            _cards.RemoveAt(index);
            if (_faceDownCount > _cards.Count)
            {
                _faceDownCount = _cards.Count;
            }

            return card;
        }

        public bool Remove(Card card)
        {
            bool removed = _cards.Remove(card);
            if (removed && _faceDownCount > _cards.Count)
            {
                _faceDownCount = _cards.Count;
            }

            return removed;
        }

        public void Clear()
        {
            _faceDownCount = 0;
            _cards.Clear();
            OnPropertyChanged(nameof(FaceDownCount));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(TopCard));
            OnPropertyChanged(nameof(BottomCard));
            OnPropertyChanged(nameof(FaceUpCount));
            OnPropertyChanged(nameof(FirstFaceUpIndex));
        }

        /// <summary>A rakásban lévő lapok listája (alulról felfelé).</summary>
        public IReadOnlyList<Card> Snapshot() => new List<Card>(_cards);

        /// <summary>A megadott indextől a tetejéig tartó látható lapok.</summary>
        public List<Card> TakeFrom(int index)
        {
            var taken = new List<Card>();
            for (int i = index; i < _cards.Count; i++)
            {
                taken.Add(_cards[i]);
            }

            return taken;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnCardsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_faceDownCount > _cards.Count)
            {
                _faceDownCount = _cards.Count;
            }

            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(TopCard));
            OnPropertyChanged(nameof(FaceUpCount));
            OnPropertyChanged(nameof(BottomCard));
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public override string ToString() => $"{Kind}#{Index} ({_cards.Count} lap)";
    }
}
