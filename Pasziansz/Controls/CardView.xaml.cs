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

        public CardView()
        {
            InitializeComponent();
            Loaded += (_, _) => UpdateVisual();
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
                FallbackSuit.Foreground = Card.IsRed
                    ? new SolidColorBrush(Color.FromRgb(0xB3, 0x2A, 0x2A))
                    : new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B));
            }
        }

        private void UpdateChrome()
        {
            SelectionRing.Visibility = IsSelected || IsHinted || IsDropTarget
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (IsDropTarget)
            {
                SelectionRing.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2F, 0xAE, 0x72));
                SelectionRing.BorderThickness = new Thickness(3.5);
            }
            else if (IsHinted)
            {
                SelectionRing.BorderBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0xD8, 0x9C));
                SelectionRing.BorderThickness = new Thickness(3.5);
            }
            else
            {
                SelectionRing.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xBC, 0x78));
                SelectionRing.BorderThickness = new Thickness(2.5);
            }

            SelectionRing.Margin = IsSelected ? new Thickness(-1) : new Thickness(0);
            SelectionRing.Opacity = IsSelected ? 1.0 : 0.85;
        }

        private static ImageSource? TryFindCardImage(string key)
        {
            try
            {
                return Application.Current?.TryFindResource(key) as ImageSource;
            }
            catch
            {
                return null;
            }
        }
    }
}
