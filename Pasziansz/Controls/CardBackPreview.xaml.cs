using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pasziansz.Services;

namespace Pasziansz.Controls
{
    /// <summary>
    /// Kicsi kártyahátlap-előnézet a beállításokhoz. A háttér a téma aktuális
    /// hátlapját mutatja; a stílus előnézete a <see cref="Style"/> tulajdonsággal
    /// állítható be anélkül, hogy a globális témát megváltoztatnánk.
    /// </summary>
    public partial class CardBackPreview : UserControl
    {
        public static readonly DependencyProperty BackStyleProperty =
            DependencyProperty.Register(nameof(BackStyle), typeof(CardBackStyle), typeof(CardBackPreview),
                new PropertyMetadata(CardBackStyle.Green, OnStyleChanged));

        private static readonly ImageBrush Brush = new ImageBrush { Stretch = Stretch.UniformToFill };

        public CardBackPreview()
        {
            InitializeComponent();
            Loaded += (_, _) => ApplyImage();
        }

        /// <summary>A háttér előnézetének stílusa.</summary>
        public CardBackStyle BackStyle
        {
            get => (CardBackStyle)GetValue(BackStyleProperty);
            set => SetValue(BackStyleProperty, value);
        }

        private static void OnStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CardBackPreview preview)
            {
                preview.ApplyImage();
            }
        }

        private void ApplyImage()
        {
            var key = ThemeManager.KeyFor(BackStyle);
            var image = Application.Current?.TryFindResource(key) as ImageSource
                        ?? ThemeManager.CardBackImage;
            Brush.ImageSource = image;
            Chrome.Background = Brush;
        }
    }
}
