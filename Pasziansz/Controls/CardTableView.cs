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
        private readonly Dictionary<Card, (PileRef Pile, int Index, bool FaceUp)> _meta = new();
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
            FullRelayout(animate: true);
        }

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

            var live = new HashSet<Card>();
            int z = 0;

            foreach (var pv in _piles.Values)
            {
                var location = pv.Location;
                var pile = location.Pile;

                PositionSlot(pv);

                for (int i = 0; i < pile.Count; i++)
                {
                    var card = pile.Cards[i];
                    live.Add(card);

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

                    view.Width = _cardW;
                    view.Height = _cardH;
                    view.Card = card;
                    view.IsFaceUp = IsFaceUp(pile, i);

                    var point = CardPosition(location, i);
                    _positions[card] = point;
                    _meta[card] = (new PileRef(location.Kind, location.Index), i, IsFaceUp(pile, i));

                    Panel.SetZIndex(view, ++z);

                    if (animate && view.IsLoaded)
                    {
                        AnimateTo(view, point.X, point.Y);
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

            // Kikerült lapok
            var stale = _views.Keys.Where(c => !live.Contains(c)).ToList();
            foreach (var card in stale)
            {
                var view = _views[card];
                _cardCanvas.Children.Remove(view);
                _views.Remove(card);
                _positions.Remove(card);
                _meta.Remove(card);
            }

            Panel.SetZIndex(_dropHighlight, 5000);
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
                int fromEnd = pile.Count - 1 - index;
                int slot = Math.Clamp(2 - fromEnd, 0, 2);
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
            double slotWidth = pile.Kind == PileKind.Waste ? _cardW + 2 * WasteFan : _cardW;

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

        private static void AnimateTo(UIElement element, double x, double y)
        {
            double currentX = Canvas.GetLeft(element);
            double currentY = Canvas.GetTop(element);
            if (double.IsNaN(currentX)) currentX = x;
            if (double.IsNaN(currentY)) currentY = y;
            if (Math.Abs(currentX - x) < 0.4 && Math.Abs(currentY - y) < 0.4)
            {
                return;
            }

            var duration = TimeSpan.FromMilliseconds(150);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            var leftAnim = new DoubleAnimation(currentX, x, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop };
            var topAnim = new DoubleAnimation(currentY, y, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop };

            leftAnim.Completed += (_, _) =>
            {
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
                var hitMeta = _meta[pair.Key];

                // Csak a lap látható sávjában adunk találatot: a felette fekvő lap
                // eltakarja az alatta lévő nagy részét, így a kattintás a takaró lapot éri.
                double visibleHeight = VisibleCardHeight(hitMeta);
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

                double width = pv.Location.Kind == PileKind.Waste ? _cardW + 2 * WasteFan : _cardW;
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
        private double VisibleCardHeight((PileRef Pile, int Index, bool FaceUp) meta)
        {
            if (_game == null)
            {
                return _cardH;
            }

            var pile = _game.Board.Get(meta.Pile).Pile;
            bool isTop = meta.Index == pile.Count - 1;

            if (meta.Pile.Kind == PileKind.Tableau)
            {
                // A lefordított lapok sávja kisebb, mint a felfordítottaké.
                double step = meta.FaceUp ? StepFor(pile) : FaceDownStep;
                return isTop ? _cardH : Math.Min(_cardH, step);
            }

            if (meta.Pile.Kind == PileKind.Waste)
            {
                // A talonnál a háromlapos rajongás: a jobb szélső (legfelső) lap a teljes.
                int fromEnd = pile.Count - 1 - meta.Index;
                return fromEnd == 0 ? _cardH : Math.Min(_cardH, WasteFan);
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
            }

            var point = e.GetPosition(this);
            _pointerDown = point;
            _dragging = false;
            _suppressClick = false;

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

            if (!_dragging && delta.Length > 5)
            {
                _dragging = true;
                Cursor = Cursors.SizeAll;
                ClearHint();
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
                if (TryHit(point, out var location, out _, out _) &&
                    !RefOf(location).Equals(_selectedPile.Value))
                {
                    TryMove(_selectedPile.Value, _selectedIndex, RefOf(location));
                }
                else
                {
                    ResetDragTransforms();
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

            FullRelayout(animate: false);
        }

        private void TryMove(PileRef from, int sourceIndex, PileRef to)
        {
            ResetDragTransforms();
            var result = _game!.Move(from, sourceIndex, to);
            if (result.Success)
            {
                SoundPlayer.PlayCardPlace();
                MoveCompleted?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(result.Message))
                {
                    SoundPlayer.PlayInvalid();
                    InvalidMove?.Invoke(this, result.Message);
                }

                ClearSelection(silent: false);
                FullRelayout(animate: false);
            }
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
