using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pasziansz.Models;
using Pasziansz.Services;

namespace Pasziansz.Controls
{
    /// <summary>
    /// Egy kártya megjelenítése. A kártya képét a ResourceDictionary-ből
    /// (Resources/Cards.xaml) veszi a <see cref="Card.ImageResourceKey"/> kulcs alapján;
    /// lefordított állapotban a téma kártyahátlapját rajzolja.
    /// </summary>
    public partial class CardView : UserControl
    {
        public static readonly DependencyProperty CardProperty =
            DependencyProperty.Register(nameof(Card), typeof(Card), typeof(CardView),
                new PropertyMetadata(null, OnVisualPropertyChanged));

        public static readonly DependencyProperty IsFaceUpProperty =
            DependencyProperty.Register(nameof(IsFaceUp), typeof(bool), typeof(CardView),
                new PropertyMetadata(true, OnVisualPropertyChanged));

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(CardView),
                new PropertyMetadata(false, OnChromePropertyChanged));

        public static readonly DependencyProperty IsHintedProperty =
            DependencyProperty.Register(nameof(IsHinted), typeof(bool), typeof(CardView),
                new PropertyMetadata(false, OnChromePropertyChanged));

        public static readonly DependencyProperty IsDropTargetProperty =
            DependencyProperty.Register(nameof(IsDropTarget), typeof(bool), typeof(CardView),
                new PropertyMetadata(false, OnChromePropertyChanged));

        private static readonly ImageBrush BackBrush = new ImageBrush
        {
            Stretch = Stretch.UniformToFill
        };

        // A keret (kijelölés / tipp / lerakási cél) ecsetjei. Korábban minden
        // frissítéskor új SolidColorBrush készült laponként és állapotváltáskor,
        // ami felesleges terhelés volt a rajzolási körben. Ezek változatlanok,
        // ezért elég egyszer létrehozni őket (Freeze -> gyorsabb kirajzolás).
        private static readonly Brush RingDropTarget = CreateFrozen(0x2F, 0xAE, 0x72);
        private static readonly Brush RingHint = CreateFrozen(0x5A, 0xD8, 0x9C);
        private static readonly Brush RingSelected = CreateFrozen(0xD8, 0xBC, 0x78);
        private static readonly Brush FallbackRed = CreateFrozen(0xB3, 0x2A, 0x2A);
        private static readonly Brush FallbackBlack = CreateFrozen(0x1B, 0x1B, 0x1B);

        private static Brush CreateFrozen(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        public CardView()
        {
            InitializeComponent();

            // A betöltéskori egyszeri frissítés után a tulajdonság-változások
            // gondoskodnak a megjelenésről.
            Loaded += OnFirstLoaded;
        }

        private void OnFirstLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnFirstLoaded;
            UpdateVisual();
        }

        public Card? Card
        {
            get => (Card?)GetValue(CardProperty);
            set => SetValue(CardProperty, value);
        }

        /// <summary>Képpel felfelé van-e a lap.</summary>
        public bool IsFaceUp
        {
            get => (bool)GetValue(IsFaceUpProperty);
            set => SetValue(IsFaceUpProperty, value);
        }

        /// <summary>Ki van-e jelölve (kiválasztott lap).</summary>
        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        /// <summary>Tipp kiemelése.</summary>
        public bool IsHinted
        {
            get => (bool)GetValue(IsHintedProperty);
            set => SetValue(IsHintedProperty, value);
        }

        /// <summary>Lerakási cél kiemelése.</summary>
        public bool IsDropTarget
        {
            get => (bool)GetValue(IsDropTargetProperty);
            set => SetValue(IsDropTargetProperty, value);
        }

        /// <summary>A kártyahátlap cseréje (téma-váltáskor hívja a főablak).</summary>
        public void RefreshBack()
        {
            if (!IsFaceUp)
            {
                UpdateVisual();
            }
        }

        private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CardView view)
            {
                // Teljesítmény: a teljes újrarajzolás (kép feloldása a
                // ResourceDictionary-ből) csak akkor kell, ha tényleg változott a
                // lap vagy a fordítottsága. A rács minden lépésnél beállítja a
                // tulajdonságokat, de az érték többnyire ugyanaz.
                if (Equals(e.OldValue, e.NewValue))
                {
                    return;
                }

                view.UpdateVisual();
            }
        }

        private static void OnChromePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CardView view)
            {
                view.UpdateChrome();
            }
        }

        private void UpdateVisual()
        {
            if (!IsFaceUp)
            {
                BackBrush.ImageSource = ThemeManager.CardBackImage;
                BackLayer.Background = BackBrush;
                BackLayer.Visibility = Visibility.Visible;
                FaceLayer.Visibility = Visibility.Collapsed;
                FallbackLayer.Visibility = Visibility.Collapsed;
                return;
            }

            BackLayer.Visibility = Visibility.Collapsed;
            FaceLayer.Visibility = Visibility.Visible;

            if (Card == null)
            {
                FaceImage.Source = null;
                FaceLayer.Visibility = Visibility.Collapsed;
                FallbackLayer.Visibility = Visibility.Collapsed;
                return;
            }

            var image = TryFindCardImage(Card.ImageResourceKey);
            if (image != null)
            {
                FaceImage.Source = image;
                FallbackLayer.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Ha a kép nem található, szöveges tartalékkal jelenítjük meg a lapot.
                FaceLayer.Visibility = Visibility.Collapsed;
                FallbackLayer.Visibility = Visibility.Visible;
                FallbackRank.Text = Models.Card.RankToCode(Card.Rank);
                FallbackSuit.Text = Card.SuitSymbol;
                FallbackSuit.Foreground = Card.IsRed ? FallbackRed : FallbackBlack;
            }
        }

        private void UpdateChrome()
        {
            SelectionRing.Visibility = IsSelected || IsHinted || IsDropTarget
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (IsDropTarget)
            {
                SelectionRing.BorderBrush = RingDropTarget;
                SelectionRing.BorderThickness = new Thickness(3.5);
            }
            else if (IsHinted)
            {
                SelectionRing.BorderBrush = RingHint;
                SelectionRing.BorderThickness = new Thickness(3.5);
            }
            else
            {
                SelectionRing.BorderBrush = RingSelected;
                SelectionRing.BorderThickness = new Thickness(2.5);
            }

            SelectionRing.Margin = IsSelected ? new Thickness(-1) : new Thickness(0);
            SelectionRing.Opacity = IsSelected ? 1.0 : 0.85;
        }

        /// <summary>
        /// A lap képének gyorsítótára (erőforrás-kulcs -> kép). A
        /// <see cref="Application.TryFindResource"/> több szótáron át keres, ezért
        /// laponként és újrarajzolásonként ismételni pazarló: összesen 52 kulcs van,
        /// ezeket elég egyszer feloldani.
        /// </summary>
        private static readonly Dictionary<string, ImageSource?> ImageCache = new();

        private static ImageSource? TryFindCardImage(string key)
        {
            lock (ImageCache)
            {
                if (ImageCache.TryGetValue(key, out var cached))
                {
                    return cached;
                }
            }

            ImageSource? image = null;
            try
            {
                image = Application.Current?.TryFindResource(key) as ImageSource;
            }
            catch
            {
                // Ha a kép nem található, marad a szöveges tartalék.
            }

            lock (ImageCache)
            {
                ImageCache[key] = image;
            }

            return image;
        }

        /// <summary>A gyorsítótár ürítése (téma-/erőforrás-váltáskor).</summary>
        public static void ClearImageCache()
        {
            lock (ImageCache)
            {
                ImageCache.Clear();
            }
        }
    }
}
