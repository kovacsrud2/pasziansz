using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Pasziansz.Game;
using Pasziansz.Models;
using Pasziansz.Services;

namespace Pasziansz.Controls
{
    /// <summary>
    /// A kártyaasztal vezérlője.
    ///
    /// Minden kártya egyetlen <see cref="Canvas"/>-on helyezkedik el, globális
    /// koordinátákkal. A rakások (oszlopok, gyűjtőhelyek, pakli, talon) helyét a
    /// <see cref="Board"/> modell és a rácsszámítás adja.
    ///
    /// Kezelés:
    ///  - kattintás a húzópaklira: húzás (1 vagy 3 lap)
    ///  - kattintás egy lapra: kiválasztás, majd a célra kattintva áthelyezés
    ///  - húzás az egérrel: áthelyezés (drag &amp; drop)
    ///  - dupla kattintás: felpakolás a gyűjtőhelyre
    /// </summary>
    public sealed class CardTableView : FrameworkElement
    {
        public const double CardWidth = 108;
        public const double CardHeight = 157;
        private const double ColumnGap = 18;
        private const double RowGap = 26;
        private const double BoardMargin = 18;
        private const double WasteFanBase = 26;

        // A kártyavászonnak ÁTLÁTSZÓ (de nem null) háttere kell: a WPF csak akkor
        // találja el a vezérlőt az egérrel, ha a pont alatt van található vizuális elem.
        // A kártyák maguk IsHitTestVisible=false értékkel jönnek létre (a találatot
        // saját magunk számoljuk), így e nélkül semmilyen egéresemény nem érkezne meg.
        private readonly Canvas _cardCanvas = new()
        {
            ClipToBounds = false,
            Background = Brushes.Transparent
        };

        private readonly Canvas _hintCanvas = new()
        {
            ClipToBounds = false,
            IsHitTestVisible = false,
            Background = Brushes.Transparent
        };
        private readonly Dictionary<(PileKind Kind, int Index), PileVisual> _piles = new();
        private readonly Dictionary<Card, CardView> _views = new();
        private readonly Dictionary<Card, Point> _positions = new();
        /// <summary>
        /// Találati adatok laponként: melyik rakásban van, hányadik, felfordítva van-e,
        /// és mekkora a látható (nem takart) magassága. A látható magasságot az
        /// elrendezés már úgyis kiszámolja, ezért itt tároljuk – így kattintáskor nem
        /// kell újra bejárni a modellt.
        /// </summary>
        private readonly Dictionary<Card, (PileRef Pile, int Index, bool FaceUp, double VisibleHeight)> _meta = new();

        // Újrafelhasznált segédgyűjtemények az elrendezéshez: így nem készül
        // lépésenként új halmaz és lista (kevesebb szemétgyűjtési terhelés).
        private readonly HashSet<Card> _liveCards = new();
        private readonly List<Card> _staleCards = new();
        private readonly Dictionary<Pile, double> _tableauStep = new();

        private readonly Border _dropHighlight = new()
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(2.5),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2F, 0xAE, 0x72)),
            Background = new SolidColorBrush(Color.FromArgb(0x1E, 0x2F, 0xAE, 0x72)),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };

        private KlondikeGame? _game;
        private double _cardW = CardWidth;
        private double _cardH = CardHeight;

        // Kiválasztás / húzás
        private PileRef? _selectedPile;
        private int _selectedIndex = -1;
        private Point _pointerDown;
        private bool _dragging;
        private bool _suppressClick;

        /// <summary>
        /// A húzás jelenlegi elmozdulása (egér-koordinátában). A lerakás ennek
        /// alapján számolja ki, hol is volt a kártya a képernyőn – a nézet
        /// <see cref="Canvas"/>-pozíciója ugyanis egy futó animáció miatt
        /// átmeneti értéket is mutathat, az elmozdulás viszont pontos.
        /// </summary>
        private Vector _dragDelta;

        /// <summary>
        /// A húzással épp lerakott lapok képernyő-pozíciója (a húzás pillanatában).
        /// A lerakás után ebből a pontból animálunk az új helyre, hogy a kártya ne
        /// ugorjon vissza a forrásra, majd onnan repüljön át.
        /// </summary>
        private Dictionary<Card, Point>? _droppedPositions;

        public CardTableView()
        {
            ClipToBounds = true;
            Focusable = true;
            SnapsToDevicePixels = true;


            AddVisualChild(_cardCanvas);
            AddVisualChild(_hintCanvas);
        }

        /// <summary>
        /// Átlátszó találati réteg: enélkül a FrameworkElement nem található el az
        /// egérrel (a kártyák IsHitTestVisible=false, a vásznak pedig üresek lennének).
        /// </summary>
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        }

        protected override int VisualChildrenCount => 2;

        protected override Visual GetVisualChild(int index) => index == 0 ? _cardCanvas : _hintCanvas;

        // ====================================================================
        //  Események a főablak felé
        // ====================================================================

        /// <summary>Szabálytalan lépés vagy hibaüzenet.</summary>
        public event EventHandler<string>? InvalidMove;

        /// <summary>Sikeres lépés (a főablak frissíti az állapotsort).</summary>
        public event EventHandler? MoveCompleted;

        /// <summary>A húzópaklira kattintottak.</summary>
        public event EventHandler? StockClicked;

        /// <summary>Kártya kiválasztva (a főablak tipp-panelhez).</summary>
        public event EventHandler? SelectionChanged;

        public PileRef? SelectedPile => _selectedPile;

        public int SelectedIndex => _selectedIndex;

        // ====================================================================
        //  Modell
        // ====================================================================

        public void AttachGame(KlondikeGame game)
        {
            if (_game != null)
            {
                _game.MovePerformed -= OnMovePerformed;
            }

            // Újracsatolásnál (pl. mentett parti betöltése) a korábbi vizuális elemek
            // nem maradhatnak a vásznon, különben duplán jelenne meg a tábla.
            DetachVisuals();

            _game = game;
            _game.MovePerformed += OnMovePerformed;

            foreach (var pile in _game.Board.All)
            {
                _piles[(pile.Kind, pile.Index)] = new PileVisual(pile);
            }

            foreach (var pv in _piles.Values)
            {
                _cardCanvas.Children.Add(pv.Slot);
                _cardCanvas.Children.Add(pv.Label);
            }

            _cardCanvas.Children.Add(_dropHighlight);

            RecalculateMetrics();
            FullRelayout(animate: false);
        }

        /// <summary>A korábbi játékhoz tartozó vizuális elemek eltávolítása.</summary>
        private void DetachVisuals()
        {
            foreach (var pv in _piles.Values)
            {
                _cardCanvas.Children.Remove(pv.Slot);
                _cardCanvas.Children.Remove(pv.Label);
            }

            _cardCanvas.Children.Remove(_dropHighlight);

            foreach (var view in _views.Values)
            {
                _cardCanvas.Children.Remove(view);
            }

            // A kijelölést még a gyűjtemények ürítése előtt töröljük (a ClearSelection
            // a nézeteket állítja vissza).
            ClearSelection(silent: true);

            _piles.Clear();
            _views.Clear();
            _positions.Clear();
            _meta.Clear();
            _tableauStep.Clear();

            _dropHighlight.Visibility = Visibility.Collapsed;
        }

        /// <summary>Téma-váltás: a lefordított lapok hátlapjának cseréje.</summary>
        public void RefreshCardBacks()
        {
            foreach (var view in _views.Values)
            {
                view.RefreshBack();
            }
        }

        private void OnMovePerformed(object? sender, MoveRecord e)
        {
            ClearSelection(silent: true);
            ClearHint();
            _dropHighlight.Visibility = Visibility.Collapsed;

            // Húzással történt lerakásnál a lap a húzás alatti helyéről simul át;
            // egyébként a szokásos animáció fut (pl. kattintással való áthelyezés).
            FullRelayout(animate: true);
        }

        /// <summary>
        /// A kattintás-felismeréshez tartozó adatok felépítése a jelenlegi tábláról.
        /// Egyszer fut le elrendezésenként, a lapok számával arányos.
        /// </summary>
        private void RebuildHitMetadata()
        {
            _meta.Clear();
            if (_game == null)
            {
                return;
            }

            foreach (var pileLocation in _game.Board.All)
            {
                var pile = pileLocation.Pile;
                var reference = new PileRef(pileLocation.Kind, pileLocation.Index);

                for (int i = 0; i < pile.Count; i++)
                {
                    // Csak a nézettel rendelkező lapokra van szükség: a találati
                    // ciklus a _positions kulcsain megy végig, és onnan indexel ide.
                    var card = pile.Cards[i];
                    if (!_positions.ContainsKey(card))
                    {
                        continue;
                    }

                    bool faceUp = IsFaceUp(pile, i);
                    _meta[card] = (reference, i, faceUp, VisibleCardHeight(pileLocation, i, faceUp));
                }
            }
        }

        /// <summary>Egyezik-e a nézet mérete a kért értékkel (a NaN-t is kezelve).</summary>
        private static bool IsSameSize(FrameworkElement view, double width, double height) =>
            !double.IsNaN(view.Width) && !double.IsNaN(view.Height)
            && Math.Abs(view.Width - width) < 0.01
            && Math.Abs(view.Height - height) < 0.01;

        /// <summary>Teljes újrarajzolás (pl. új osztás után).</summary>
        public void RefreshFromModel() => FullRelayout(animate: false);

        // ====================================================================
        //  Méretek
        // ====================================================================

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            RecalculateMetrics();
            FullRelayout(animate: false);
        }

        private void RecalculateMetrics()
        {
            double available = Math.Max(360, ActualWidth - 2 * BoardMargin);
            double byWidth = (available - 6 * ColumnGap) / 7.0;

            // Négy sor (gyűjtőhelyek, pakli/talon, két oszlopsor-magasság) + átfedések
            double byHeight = (ActualHeight - 2 * BoardMargin) / 4.6;

            double w = Math.Min(CardWidth, Math.Min(byWidth, byHeight * CardWidth / CardHeight));
            _cardW = Math.Max(58, w);
            _cardH = _cardW * CardHeight / CardWidth;
        }

        private double Scale => _cardW / CardWidth;

        /// <summary>Alapértelmezett oszlop-átfedés felfordított lapoknál.</summary>
        private double FaceUpStep => 33 * Scale;

        private double FaceDownStep => 12 * Scale;

        /// <summary>
        /// A hosszú oszlopok ne lógjanak ki az asztalról: a rendelkezésre álló
        /// magassághoz igazítjuk a felfordított lapok átfedését (a lapok sűrűbben
        /// csúsznak egymásra, ha kell).
        /// </summary>
        private void ComputeTableauSteps()
        {
            _tableauStep.Clear();
            if (_game == null)
            {
                return;
            }

            double tableauTop = RowY(2);
            double bottomReserve = 74;                       // eszköztár helye
            double available = Math.Max(_cardH, ActualHeight - tableauTop - bottomReserve);

            foreach (var column in _game.Board.Tableau)
            {
                var pile = column.Pile;
                double down = pile.FaceDownCount * FaceDownStep;
                int up = Math.Max(1, pile.Count - pile.FaceDownCount);
                double usable = Math.Max(_cardH * 0.4, available - _cardH - down);
                double step = up <= 1 ? FaceUpStep : Math.Min(FaceUpStep, usable / (up - 1));
                _tableauStep[pile] = Math.Max(7 * Scale, step);
            }
        }

        private double StepFor(Pile pile) =>
            _tableauStep.TryGetValue(pile, out double step) ? step : FaceUpStep;

        private double WasteFan => WasteFanBase * Scale;

        /// <summary>
        /// Hány lap látszik a talonon egymás mellett. 3 lapos húzásnál a szabály
        /// szerint három lap játszható, 1 laposnál csak a legfelső – különben a
        /// korábban húzott lapok kilógnának, és egyre több lap látszana.
        /// </summary>
        private int WasteVisibleCount => _game?.DrawCount == 3 ? 3 : 1;

        private double ColumnX(int column) => BoardMargin + column * (_cardW + ColumnGap);

        private double RowY(int row) => BoardMargin + row * (_cardH + RowGap);

        // ====================================================================
        //  Elrendezés
        // ====================================================================

        private void FullRelayout(bool animate)
        {
            if (_game == null)
            {
                return;
            }

            ComputeTableauSteps();

            _liveCards.Clear();
            int z = 0;

            foreach (var pv in _piles.Values)
            {
                var location = pv.Location;
                var pile = location.Pile;

                PositionSlot(pv);

                for (int i = 0; i < pile.Count; i++)
                {
                    var card = pile.Cards[i];
                    _liveCards.Add(card);

                    if (!_views.TryGetValue(card, out var view))
                    {
                        view = new CardView
                        {
                            Focusable = false,
                            IsHitTestVisible = false
                        };
                        _views[card] = view;
                        _cardCanvas.Children.Add(view);
                    }

                    // Teljesítmény: a méret és a lap/fordítottság beállítása csak
                    // akkor, ha tényleg változott. A rács minden lépésnél végigfut a
                    // lapokon, de ezek az értékek többnyire ugyanazok maradnak – a
                    // felesleges beállítás újrarajzolást és WPF-értesítéseket vált ki.
                    if (!IsSameSize(view, _cardW, _cardH))
                    {
                        view.Width = _cardW;
                        view.Height = _cardH;
                    }

                    if (!Equals(view.Card, card))
                    {
                        view.Card = card;
                    }

                    bool faceUp = IsFaceUp(pile, i);
                    if (view.IsFaceUp != faceUp)
                    {
                        view.IsFaceUp = faceUp;
                    }

                    var point = CardPosition(location, i);
                    _positions[card] = point;

                    Panel.SetZIndex(view, ++z);

                    bool fromDrop = animate && _droppedPositions != null &&
                                    _droppedPositions.TryGetValue(card, out var dropStart);

                    if (animate && view.IsLoaded)
                    {
                        if (fromDrop)
                        {
                            // A lerakott kártya a húzás alatti képernyő-pozíciójából
                            // induljon, ne a forrásrakás régi helyéről.
                            AnimateToFrom(view, dropStart, point);
                        }
                        else
                        {
                            AnimateTo(view, point.X, point.Y);
                        }
                    }
                    else
                    {
                        view.BeginAnimation(Canvas.LeftProperty, null);
                        view.BeginAnimation(Canvas.TopProperty, null);
                        Canvas.SetLeft(view, point.X);
                        Canvas.SetTop(view, point.Y);
                    }
                }
            }

            // A találati információk (melyik rakásban, hányadik lap, felfordítva-e)
            // a végén, egyetlen bejárással készülnek el. Korábban a fenti ciklus
            // minden lapra írt a szótárba, ami lépésenként 52 felesleges írás volt.
            RebuildHitMetadata();

            // Kikerült lapok: a _views kulcsait bejárva, újrafelhasznált listába
            // gyűjtjük (a törlést csak utána végezzük, hogy a bejárás biztonságos
            // legyen). Így nincs LINQ-lista és külön halmaz minden lépésnél.
            _staleCards.Clear();
            foreach (var pair in _views)
            {
                if (!_liveCards.Contains(pair.Key))
                {
                    _staleCards.Add(pair.Key);
                }
            }

            foreach (var card in _staleCards)
            {
                var view = _views[card];
                _cardCanvas.Children.Remove(view);
                _views.Remove(card);
                _positions.Remove(card);
                _meta.Remove(card);
            }

            Panel.SetZIndex(_dropHighlight, 5000);

            // A lerakás-indulópontok csak az ezt követő elrendezésre érvényesek.
            _droppedPositions = null;
        }

        private bool IsFaceUp(Pile pile, int index) =>
            pile.Kind != PileKind.Stock && pile.IsFaceUp(index);

        private Point CardPosition(PileLocation location, int index)
        {
            var pile = location.Pile;
            double x = ColumnX(location.Column);
            double y = RowY(location.Row);

            if (pile.Kind == PileKind.Waste)
            {
                int visible = WasteVisibleCount;
                int fromEnd = pile.Count - 1 - index;

                // A látható sávon belül jobbra tolódnak, a régebbi lapok a legfelső alá
                // kerülnek (1 lapos módban tehát csak egy lap látszik).
                int slot = Math.Max(0, visible - 1 - fromEnd);
                x += slot * WasteFan;
            }
            else if (pile.Kind == PileKind.Tableau)
            {
                int faceDown = pile.FaceDownCount;
                double step = StepFor(pile);
                y += index < faceDown
                    ? index * FaceDownStep
                    : faceDown * FaceDownStep + (index - faceDown) * step;
            }

            return new Point(x, y);
        }

        private void PositionSlot(PileVisual pv)
        {
            var location = pv.Location;
            var pile = location.Pile;
            double x = ColumnX(location.Column);
            double y = RowY(location.Row);
            double slotWidth = pile.Kind == PileKind.Waste
            ? _cardW + (WasteVisibleCount - 1) * WasteFan
            : _cardW;

            pv.Slot.Width = slotWidth;
            pv.Slot.Height = _cardH;
            pv.Slot.CornerRadius = new CornerRadius(Math.Max(5, 9 * Scale));
            pv.Slot.Visibility = pile.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            pv.Slot.Child = pile.Count == 0 ? BuildSlotContent(location) : null;

            Canvas.SetLeft(pv.Slot, x);
            Canvas.SetTop(pv.Slot, y);
            Panel.SetZIndex(pv.Slot, 0);

            // Felirat (oszlopszám / gyűjtőhely szimbólum)
            pv.Label.Text = location.ShortLabel;
            pv.Label.FontSize = Math.Max(10, (location.Kind == PileKind.Foundation ? 24 : 11) * Scale);
            pv.Label.Foreground = location.IsRedFoundation
                ? new SolidColorBrush(Color.FromArgb(0x99, 0xE8, 0xA0, 0x90))
                : new SolidColorBrush(Color.FromArgb(0x8C, 0x9F, 0xD8, 0xB6));
            pv.Label.Visibility = pile.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            pv.Label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            double labelX = location.Kind == PileKind.Foundation
                ? x + (_cardW - pv.Label.DesiredSize.Width) / 2
                : x + 4;
            double labelY = location.Kind == PileKind.Tableau
                ? y + _cardH + 2
                : y + (_cardH - pv.Label.DesiredSize.Height) / 2;

            Canvas.SetLeft(pv.Label, labelX);
            Canvas.SetTop(pv.Label, labelY);
            Panel.SetZIndex(pv.Label, 1);
        }

        private UIElement BuildSlotContent(PileLocation location)
        {
            var panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };

            switch (location.Kind)
            {
                case PileKind.Stock:
                    panel.Children.Add(new TextBlock
                    {
                        Text = "↻",
                        FontSize = Math.Max(16, 26 * Scale),
                        Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xA8, 0xE0, 0xBE)),
                        HorizontalAlignment = HorizontalAlignment.Center
                    });
                    panel.Children.Add(new TextBlock
                    {
                        Text = "újrakeverés",
                        FontSize = Math.Max(8, 9.5 * Scale),
                        Foreground = new SolidColorBrush(Color.FromArgb(0x77, 0xA8, 0xE0, 0xBE)),
                        HorizontalAlignment = HorizontalAlignment.Center
                    });
                    break;

                case PileKind.Waste:
                    panel.Children.Add(new TextBlock
                    {
                        Text = "talon",
                        FontSize = Math.Max(8, 9.5 * Scale),
                        Foreground = new SolidColorBrush(Color.FromArgb(0x66, 0xA8, 0xE0, 0xBE)),
                        HorizontalAlignment = HorizontalAlignment.Center
                    });
                    break;
            }

            return panel;
        }

        /// <summary>
        /// A húzással lerakott kártya animálása: a húzás alatti képernyő-pozícióból
        /// (abszolút Canvas-koordináta) csúszik az új helyére. Így nincs ugrás a
        /// lerakás pillanatában, és nincs visszaugrás a forrásrakásra sem.
        /// </summary>
        private static void AnimateToFrom(UIElement element, Point start, Point target)
        {
            // A húzás közbeni transzformáció megszűnik; a kártyát a valódi
            // (húzás alatti) pozíciójára állítjuk, és onnan animálunk.
            element.RenderTransform = Transform.Identity;
            element.BeginAnimation(Canvas.LeftProperty, null);
            element.BeginAnimation(Canvas.TopProperty, null);
            Canvas.SetLeft(element, start.X);
            Canvas.SetTop(element, start.Y);

            AnimateTo(element, target.X, target.Y);
        }

        private static void AnimateTo(UIElement element, double x, double y)
        {
            double currentX = Canvas.GetLeft(element);
            double currentY = Canvas.GetTop(element);
            if (double.IsNaN(currentX)) currentX = x;
            if (double.IsNaN(currentY)) currentY = y;
            if (Math.Abs(currentX - x) < 0.4 && Math.Abs(currentY - y) < 0.4)
            {
                element.BeginAnimation(Canvas.LeftProperty, null);
                element.BeginAnimation(Canvas.TopProperty, null);
                Canvas.SetLeft(element, x);
                Canvas.SetTop(element, y);
                return;
            }

            var duration = TimeSpan.FromMilliseconds(150);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            var leftAnim = new DoubleAnimation(currentX, x, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop };
            var topAnim = new DoubleAnimation(currentY, y, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop };

            // A folyamatban lévő animációt lecseréljük; a végén a végleges
            // Canvas-pozíciót állítjuk be (az animációtól függetlenül).
            leftAnim.Completed += (_, _) =>
            {
                element.BeginAnimation(Canvas.LeftProperty, null);
                element.BeginAnimation(Canvas.TopProperty, null);
                Canvas.SetLeft(element, x);
                Canvas.SetTop(element, y);
            };

            element.BeginAnimation(Canvas.LeftProperty, leftAnim);
            element.BeginAnimation(Canvas.TopProperty, topAnim);
        }

        // ====================================================================
        //  Találat
        // ====================================================================

        protected override Size MeasureOverride(Size availableSize)
        {
            // Mindig kitöltjük a rendelkezésre álló helyet (a szülő Grid adja).
            return new Size(
                double.IsInfinity(availableSize.Width) ? 900 : availableSize.Width,
                double.IsInfinity(availableSize.Height) ? 640 : availableSize.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            // A kártyák pozíciói globális koordináták (a CardTableView tetejéhez képest),
            // ezért a vásznaknak a teljes területet odaadjuk.
            _cardCanvas.Measure(finalSize);
            _cardCanvas.Arrange(new Rect(new Point(0, 0), finalSize));
            _hintCanvas.Measure(finalSize);
            _hintCanvas.Arrange(new Rect(new Point(0, 0), finalSize));
            _cardCanvas.Width = finalSize.Width;
            _cardCanvas.Height = finalSize.Height;
            _hintCanvas.Width = finalSize.Width;
            _hintCanvas.Height = finalSize.Height;
            return finalSize;
        }

        private bool TryHit(Point point, out PileLocation location, out int index, out Card? card)
        {
            location = null!;
            index = -1;
            card = null;

            if (_game == null)
            {
                return false;
            }

            int bestZ = -1;

            // 1) Kártyák (a legmagasabb Z-indexű találat nyer)
            foreach (var pair in _positions)
            {
                if (!_meta.TryGetValue(pair.Key, out var hitMeta))
                {
                    // Nincs találati adat (pl. épp eltűnő lap): nem kattintható.
                    continue;
                }

                // Csak a lap látható sávjában adunk találatot: a felette fekvő lap
                // eltakarja az alatta lévő nagy részét, így a kattintás a takaró lapot éri.
                double visibleHeight = hitMeta.VisibleHeight;
                if (visibleHeight <= 0)
                {
                    // Teljesen takart lap (pl. az 1 lapos húzásnál a korábbi lapok).
                    continue;
                }

                var rect = new Rect(pair.Value, new Size(_cardW, visibleHeight));
                if (!rect.Contains(point))
                {
                    continue;
                }

                if (!_views.TryGetValue(pair.Key, out var view))
                {
                    continue;
                }

                int z = Panel.GetZIndex(view);
                if (z <= bestZ)
                {
                    continue;
                }

                bestZ = z;
                location = _game.Board.Get(hitMeta.Pile);
                index = hitMeta.Index;
                card = pair.Key;
            }

            if (card != null)
            {
                return true;
            }

            // 2) Üres rakások
            foreach (var pv in _piles.Values)
            {
                if (pv.Location.Pile.Count != 0)
                {
                    continue;
                }

                double width = pv.Location.Kind == PileKind.Waste && pv.Location.Pile.Count == 0
                    ? _cardW + (WasteVisibleCount - 1) * WasteFan
                    : _cardW;
                var rect = new Rect(ColumnX(pv.Location.Column), RowY(pv.Location.Row), width, _cardH);
                if (rect.Contains(point))
                {
                    location = pv.Location;
                    index = -1;
                    card = null;
                    return true;
                }
            }

            return false;
        }

        private static PileRef RefOf(PileLocation location) => new PileRef(location.Kind, location.Index);

        /// <summary>
        /// A kártya látható (nem takart) magassága. A rakás tetején lévő lap teljes
        /// magasságban látszik, a többiek csak a rájuk csúsztatott sávban.
        /// </summary>
        private double VisibleCardHeight(PileLocation location, int index, bool faceUp)
        {
            var pile = location.Pile;
            bool isTop = index == pile.Count - 1;

            if (location.Kind == PileKind.Tableau)
            {
                // A lefordított lapok sávja kisebb, mint a felfordítottaké.
                double step = faceUp ? StepFor(pile) : FaceDownStep;
                return isTop ? _cardH : Math.Min(_cardH, step);
            }

            if (location.Kind == PileKind.Waste)
            {
                // A talonnál a jobb szélső (legfelső) lap látszik teljesen; a régebbiek
                // csak annyit, hogy épp kilátszanak a legfelső alól. 1 lapos húzásnál
                // a korábbi lapok teljesen a legfelső alatt vannak.
                int fromEnd = pile.Count - 1 - index;
                if (fromEnd == 0)
                {
                    return _cardH;
                }

                return fromEnd < WasteVisibleCount ? Math.Min(_cardH, WasteFan) : 0;
            }

            return _cardH;
        }

        // ====================================================================
        //  Kiválasztás
        // ====================================================================

        private void Select(PileRef reference, int index)
        {
            ClearSelection(silent: true);
            _selectedPile = reference;
            _selectedIndex = index;

            var pile = _game!.Board.Get(reference).Pile;
            for (int i = index; i < pile.Count; i++)
            {
                if (_views.TryGetValue(pile.Cards[i], out var view))
                {
                    view.IsSelected = true;
                }
            }

            UpdateSlotHighlights(reference);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ClearSelection(bool silent)
        {
            _selectedPile = null;
            _selectedIndex = -1;

            foreach (var view in _views.Values)
            {
                view.IsSelected = false;
                view.RenderTransform = Transform.Identity;
            }

            if (_game != null)
            {
                UpdateSlotHighlights(null);
            }

            if (!silent)
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void UpdateSlotHighlights(PileRef? source)
        {
            foreach (var pv in _piles.Values)
            {
                pv.Slot.BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0xA8, 0xE0, 0xBE));
            }

            if (source == null || _game == null)
            {
                return;
            }

            var sourcePile = _game.Board.Get(source.Value).Pile;
            if (sourcePile.Count == 0 || _selectedIndex < 0 || _selectedIndex >= sourcePile.Count)
            {
                return;
            }

            var lead = sourcePile.Cards[_selectedIndex];

            foreach (var pv in _piles.Values)
            {
                if (ReferenceEquals(pv.Location.Pile, sourcePile))
                {
                    continue;
                }

                bool valid = pv.Location.Kind switch
                {
                    PileKind.Foundation => Rules.CanPlaceOnFoundation(lead, pv.Location.Pile.TopCard,
                        pv.Location.FoundationSuit!.Value),
                    PileKind.Tableau => Rules.CanPlaceOnTableau(lead, pv.Location.Pile.TopCard),
                    _ => false
                };

                if (valid)
                {
                    pv.Slot.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2F, 0xAE, 0x72));
                }
            }
        }

        // ====================================================================
        //  Egér
        // ====================================================================

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();

            if (_game == null)
            {
                return;
            }

            // Ha az előző húzás valamiért nem fejeződött be rendesen (pl. az egérgombot
            // az ablakon kívül engedték fel), itt takarítunk.
            if (!_dragging && IsMouseCaptured)
            {
                ReleaseMouseCapture();
                ResetDragTransforms();
                _selectedPile = null;
                _selectedIndex = -1;
                FullRelayout(animate: false);
            }

            var point = e.GetPosition(this);
            _pointerDown = point;
            _dragging = false;
            _suppressClick = false;
            _dragDelta = default;

            if (!TryHit(point, out var location, out int index, out var card))
            {
                bool hadSelection = _selectedPile != null;
                ClearSelection(silent: false);
                if (hadSelection)
                {
                    FullRelayout(animate: true);
                }

                return;
            }

            var reference = RefOf(location);
            var pile = location.Pile;

            // Dupla kattintás: felpakolás a gyűjtőhelyre
            if (e.ClickCount == 2 && card != null && index == pile.Count - 1 &&
                (location.Kind == PileKind.Tableau || location.Kind == PileKind.Waste))
            {
                _suppressClick = true;
                SendToFoundation(reference, index);
                return;
            }

            if (location.Kind == PileKind.Stock)
            {
                StockClicked?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (index < 0)
            {
                // Üres rakásra kattintás: kiválasztott lap odalép
                if (_selectedPile != null)
                {
                    TryMove(_selectedPile.Value, _selectedIndex, reference);
                }

                return;
            }

            if (!pile.IsFaceUp(index))
            {
                // Leferdített lapra kattintás
                if (_selectedPile != null && !_selectedPile.Value.Equals(reference))
                {
                    TryMove(_selectedPile.Value, _selectedIndex, reference);
                }
                else if (CanFlipAt(pile, index))
                {
                    var result = _game.FlipTopCard(reference);
                    if (result.Success)
                    {
                        SoundPlayer.PlayCardDraw();
                        MoveCompleted?.Invoke(this, EventArgs.Empty);
                    }
                }

                return;
            }

            // Már kiválasztott lap + kattintás egy másik rakásra: áthelyezés
            if (_selectedPile != null && !_selectedPile.Value.Equals(reference))
            {
                TryMove(_selectedPile.Value, _selectedIndex, reference);
                return;
            }

            if (_selectedPile != null && _selectedPile.Value.Equals(reference) && _selectedIndex == index)
            {
                ClearSelection(silent: false);
                return;
            }

            if (Rules.CanPickUp(pile, index, out _))
            {
                Select(reference, index);
                CaptureMouse();
            }
            else
            {
                // Fedett lap az oszlop belsejében: csak akkor fordítható, ha már semmi
                // nincs felette; egyébként jelezzük, hogy előbb fel kell szabadítani.
                if (CanFlipAt(pile, index))
                {
                    _game.FlipTopCard(reference);
                }
                else
                {
                    InvalidMove?.Invoke(this, "Ez a lap le van fordítva, és még lapok vannak felette. " +
                                              "Előbb azokat kell elmozgatni.");
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!IsMouseCaptured || _selectedPile == null || e.LeftButton != MouseButtonState.Pressed)
            {
                // Csak a kurzor visszajelzése: kártya fölött kéz, egyébként nyíl.
                if (!IsMouseCaptured && _game != null)
                {
                    Cursor = TryHit(e.GetPosition(this), out _, out _, out _)
                        ? Cursors.Hand
                        : Cursors.Arrow;
                }

                return;
            }

            // Húzás közben mindig a "mozgatás" kurzor látszódjon.
            if (Cursor != Cursors.SizeAll)
            {
                Cursor = Cursors.SizeAll;
            }

            var current = e.GetPosition(this);
            var delta = current - _pointerDown;
            _dragDelta = delta;

            if (!_dragging && delta.Length > 5)
            {
                _dragging = true;
                Cursor = Cursors.SizeAll;
                ClearHint();

                // Az előző lépés animációja még futhat: zárjuk le, különben a
                // felvett kártya az egér alatt tovább csúszkálna.
                SettleAnimations();
            }

            if (!_dragging)
            {
                return;
            }

            var pile = _game!.Board.Get(_selectedPile.Value).Pile;
            for (int i = _selectedIndex; i < pile.Count; i++)
            {
                if (_views.TryGetValue(pile.Cards[i], out var view))
                {
                    view.RenderTransform = new TranslateTransform(delta.X, delta.Y);
                    Panel.SetZIndex(view, 6000 + i);
                }
            }

            UpdateDropHighlight(current);
        }

        private void UpdateDropHighlight(Point point)
        {
            if (!TryHit(point, out var location, out _, out _) || _selectedPile == null)
            {
                _dropHighlight.Visibility = Visibility.Collapsed;
                return;
            }

            var sourcePile = _game!.Board.Get(_selectedPile.Value).Pile;
            if (ReferenceEquals(sourcePile, location.Pile) || sourcePile.Count == 0)
            {
                _dropHighlight.Visibility = Visibility.Collapsed;
                return;
            }

            var lead = sourcePile.Cards[_selectedIndex];
            bool valid = location.Kind switch
            {
                PileKind.Foundation => Rules.CanPlaceOnFoundation(lead, location.Pile.TopCard,
                    location.FoundationSuit!.Value),
                PileKind.Tableau => Rules.CanPlaceOnTableau(lead, location.Pile.TopCard),
                _ => false
            };

            if (!valid)
            {
                _dropHighlight.Visibility = Visibility.Collapsed;
                return;
            }

            _dropHighlight.Width = _cardW;
            _dropHighlight.Height = _cardH;
            Canvas.SetLeft(_dropHighlight, ColumnX(location.Column));
            Canvas.SetTop(_dropHighlight, RowY(location.Row));
            _dropHighlight.Visibility = Visibility.Visible;
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            _dropHighlight.Visibility = Visibility.Collapsed;
            Cursor = Cursors.Arrow;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }

            Cursor = Cursors.Arrow;
            _dropHighlight.Visibility = Visibility.Collapsed;

            if (_game == null || _selectedPile == null)
            {
                _dragging = false;
                return;
            }

            if (_dragging)
            {
                var point = e.GetPosition(this);

                // A lerakás pillanatában mért pontos elmozdulás.
                _dragDelta = point - _pointerDown;
                if (TryHit(point, out var location, out _, out _) &&
                    !RefOf(location).Equals(_selectedPile.Value))
                {
                    TryMove(_selectedPile.Value, _selectedIndex, RefOf(location));
                }
                else
                {
                    // Nem rakható le: a lap visszakerül a helyére.
                    ResetDragTransforms();
                    FullRelayout(animate: true);
                }

                _dragging = false;
            }

            if (_suppressClick)
            {
                _suppressClick = false;
            }
        }

        /// <summary>Felfordítható-e a lap: a legfelső lap, és lefordított állapotban van.</summary>
        private static bool CanFlipAt(Pile pile, int index) =>
            pile.Kind == PileKind.Tableau
            && index == pile.Count - 1
            && pile.FaceDownCount > 0
            && !pile.IsFaceUp(index);

        /// <summary>
        /// A húzás közbeni vizuális eltolás törlése. Szándékosan NEM rendezzük újra
        /// a táblát: a hívó (sikeres lerakás vagy visszaállítás) úgyis elrendezést
        /// indít, és egy azonnali újrarendezés megszakítaná az épp induló animációt.
        /// </summary>
        private void ResetDragTransforms()
        {
            if (_selectedPile == null || _game == null)
            {
                return;
            }

            var pile = _game.Board.Get(_selectedPile.Value).Pile;
            for (int i = _selectedIndex; i < pile.Count; i++)
            {
                if (_views.TryGetValue(pile.Cards[i], out var view))
                {
                    view.RenderTransform = Transform.Identity;
                }
            }
        }

        /// <summary>
        /// A folyamatban lévő pozíció-animációk lezárása: minden kártya azonnal a
        /// kiszámított helyére kerül. Húzás indításakor hívjuk, hogy a felvett lap
        /// pontosan az egér alatt legyen (ne az animáció átmeneti helyén).
        /// </summary>
        private void SettleAnimations()
        {
            foreach (var pair in _views)
            {
                if (!_positions.TryGetValue(pair.Key, out var target))
                {
                    continue;
                }

                var view = pair.Value;
                view.BeginAnimation(Canvas.LeftProperty, null);
                view.BeginAnimation(Canvas.TopProperty, null);
                Canvas.SetLeft(view, target.X);
                Canvas.SetTop(view, target.Y);
            }
        }

        private void TryMove(PileRef from, int sourceIndex, PileRef to)
        {
            // Húzással történő lerakásnál a lapok a képernyőn az egérnél vannak.
            // Ezt a pozíciót átadjuk a lerakás animációjának, hogy a kártya a
            // jelenlegi helyéről simuljon az új helyére (ne ugorjon vissza).
            var startPositions = CaptureDragPositions(from, sourceIndex);

            ResetDragTransforms();

            // FIGYELEM: a Move() már kibocsátja a MovePerformed eseményt, amely
            // azonnal elrendezi a táblát (FullRelayout). Ezért a kiindulópontokat
            // a Move() ELŐTT kell beállítani, különben az animáció nem használná.
            _droppedPositions = startPositions;

            var result = _game!.Move(from, sourceIndex, to);
            if (result.Success)
            {
                SoundPlayer.PlayCardPlace();
                MoveCompleted?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _droppedPositions = null;

                if (!string.IsNullOrWhiteSpace(result.Message))
                {
                    SoundPlayer.PlayInvalid();
                    InvalidMove?.Invoke(this, result.Message);
                }

                ClearSelection(silent: false);
                FullRelayout(animate: false);
            }
        }

        /// <summary>
        /// A húzással mozgatott lapok tényleges képernyő-pozíciójának rögzítése a
        /// lerakás animációjához.
        ///
        /// A húzás alatt a lap <see cref="Canvas"/>-pozíciója még a forrásrakás
        /// helye, a húzás alatti eltolást a <see cref="TranslateTransform"/> adja –
        /// a látszó hely tehát a kettő összege. Ebből kell indítani az animációt,
        /// különben a kártya a lerakás pillanatában ugrik egyet.
        /// </summary>
        private Dictionary<Card, Point>? CaptureDragPositions(PileRef from, int sourceIndex)
        {
            if (_game == null)
            {
                return null;
            }

            var pile = _game.Board.Get(from).Pile;
            if (sourceIndex < 0 || sourceIndex >= pile.Count)
            {
                return null;
            }

            var positions = new Dictionary<Card, Point>();
            for (int i = sourceIndex; i < pile.Count; i++)
            {
                var card = pile.Cards[i];
                if (!_views.TryGetValue(card, out var view))
                {
                    continue;
                }

                if (Math.Abs(_dragDelta.X) < 0.5 && Math.Abs(_dragDelta.Y) < 0.5)
                {
                    // Nem húzott lap (pl. kattintásos áthelyezés): marad a szokásos animáció.
                    continue;
                }

                // A lap tényleges képernyő-pozíciója: a Canvas-pozíció + a húzás
                // közbeni eltolás (a TranslatePoint a transzformációt is figyelembe
                // veszi). Ha ez nem értelmezhető, a Canvas-pozícióból számolunk.
                Point visual;
                try
                {
                    visual = view.TranslatePoint(new Point(0, 0), this);
                }
                catch (InvalidOperationException)
                {
                    visual = new Point(double.NaN, double.NaN);
                }

                if (double.IsNaN(visual.X) || double.IsNaN(visual.Y))
                {
                    double left = Canvas.GetLeft(view);
                    double top = Canvas.GetTop(view);
                    if (double.IsNaN(left) || double.IsNaN(top))
                    {
                        continue;
                    }

                    visual = new Point(left + _dragDelta.X, top + _dragDelta.Y);
                }

                positions[card] = visual;
            }

            return positions.Count > 0 ? positions : null;
        }

        private void SendToFoundation(PileRef source, int index)
        {
            var card = _game!.Board.Get(source).Pile.Cards[index];
            var foundation = _game.Board.FindFoundationFor(card);
            if (foundation == null)
            {
                return;
            }

            var result = _game.Move(source, index, PileRef.Foundation(foundation.Index));
            if (result.Success)
            {
                SoundPlayer.PlayFoundation();
                MoveCompleted?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                SoundPlayer.PlayInvalid();
                InvalidMove?.Invoke(this, result.Message);
            }
        }

        /// <summary>A kiválasztott lap felpakolása a gyűjtőhelyre (szóköz billentyűre).</summary>
        public bool SendSelectionToFoundation()
        {
            if (_selectedPile == null || _game == null)
            {
                return false;
            }

            var pile = _game.Board.Get(_selectedPile.Value).Pile;
            if (_selectedIndex < 0 || _selectedIndex != pile.Count - 1)
            {
                return false;
            }

            // Csak akkor jelezzük sikeresnek, ha a lap tényleg a gyűjtőhelyére kerülhet.
            var card = pile.Cards[_selectedIndex];
            var foundation = _game.Board.FindFoundationFor(card);
            if (foundation == null ||
                !Rules.CanPlaceOnFoundation(card, foundation.Pile.TopCard, card.Suit))
            {
                return false;
            }

            SendToFoundation(_selectedPile.Value, _selectedIndex);
            return true;
        }

        /// <summary>A kiválasztott lap(ok) áthelyezése a megadott oszlopra (1–7).</summary>
        public bool SendSelectionToTableau(int columnIndex)
        {
            if (_selectedPile == null || _game == null || columnIndex < 0 || columnIndex > 6)
            {
                return false;
            }

            TryMove(_selectedPile.Value, _selectedIndex, PileRef.Tableau(columnIndex));
            return true;
        }

        // ====================================================================
        //  Tipp
        // ====================================================================

        public void ShowHint(KlondikeGame.Hint hint)
        {
            ClearHint();

            if (_game == null)
            {
                return;
            }

            if (hint.SourceIndex >= 0)
            {
                var sourcePile = _game.Board.Get(hint.From).Pile;
                if (hint.SourceIndex < sourcePile.Count &&
                    _views.TryGetValue(sourcePile.Cards[hint.SourceIndex], out var view) &&
                    _positions.TryGetValue(sourcePile.Cards[hint.SourceIndex], out var point))
                {
                    AddPulseRing(point.X - 2, point.Y - 2, _cardW + 4, _cardH + 4,
                        Color.FromRgb(0x5A, 0xD8, 0x9C), 3);
                }
            }

            var target = _game.Board.Get(hint.To);
            AddPulseRing(ColumnX(target.Column) - 4, RowY(target.Row) - 4,
                _cardW + 8, _cardH + 8, Color.FromRgb(0xD8, 0xBC, 0x78), 3);
        }

        private void AddPulseRing(double x, double y, double width, double height, Color color, double thickness)
        {
            var ring = new Border
            {
                Width = width,
                Height = height,
                CornerRadius = new CornerRadius(11),
                BorderThickness = new Thickness(thickness),
                BorderBrush = new SolidColorBrush(color),
                Background = new SolidColorBrush(Color.FromArgb(0x14, color.R, color.G, color.B)),
                IsHitTestVisible = false
            };

            Canvas.SetLeft(ring, x);
            Canvas.SetTop(ring, y);
            _hintCanvas.Children.Add(ring);

            var pulse = new DoubleAnimation(0.35, 1.0, TimeSpan.FromMilliseconds(680))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            ring.BeginAnimation(OpacityProperty, pulse);
        }

        private void ClearHint() => _hintCanvas.Children.Clear();

        // ====================================================================
        //  Segédosztályok
        // ====================================================================

        private sealed class PileVisual
        {
            public PileVisual(PileLocation location)
            {
                Location = location;
                Slot = new Border
                {
                    CornerRadius = new CornerRadius(9),
                    Background = new SolidColorBrush(Color.FromArgb(0x22, 0x6F, 0xBF, 0x95)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0xA8, 0xE0, 0xBE)),
                    BorderThickness = new Thickness(1.2),
                    IsHitTestVisible = false
                };
                Label = new TextBlock
                {
                    FontWeight = FontWeights.SemiBold,
                    IsHitTestVisible = false
                };
            }

            public PileLocation Location { get; }

            public Border Slot { get; }

            public TextBlock Label { get; }
        }
    }
}
