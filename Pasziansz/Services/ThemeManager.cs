using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pasziansz.Services
{
    /// <summary>
    /// A kártyahátlap stílusának (zöld / kék / bordó) és az alkalmazás-szintű
    /// kártyahátlap ecsetnek a kezelése. A hátlap képe ResourceDictionary-ből
    /// (Resources/Cards.xaml) származik, így a csere csak egy erőforrás-kulcs cseréje.
    /// </summary>
    public static class ThemeManager
    {
        public const string CardBackBrushKey = "CardBackImage";

        public static CardBackStyle CurrentCardBack { get; private set; } = CardBackStyle.Green;

        /// <summary>Az aktuális hátlap képe.</summary>
        public static ImageSource CardBackImage { get; private set; } = CreateFallbackImage();

        public static event EventHandler? CardBackChanged;

        public static string KeyFor(CardBackStyle style) => style switch
        {
            CardBackStyle.Blue => "CardBack.blue",
            CardBackStyle.Red => "CardBack.red",
            _ => "CardBack.green"
        };

        /// <summary>Kártyahátlap cseréje a teljes alkalmazásra.</summary>
        public static void ApplyCardBack(CardBackStyle style)
        {
            CurrentCardBack = style;
            var key = KeyFor(style);
            ImageSource image = TryFindImage(key) ?? TryFindImage("CardBack.green") ?? CreateFallbackImage();
            CardBackImage = image;

            var brush = new ImageBrush(image)
            {
                Stretch = Stretch.UniformToFill,
                TileMode = TileMode.None
            };

            // Befagyasztás gyorsítótárazza a képet; ha az erőforrás nem fagyasztható,
            // a játék e nélkül is működik.
            if (brush.CanFreeze)
            {
                brush.Freeze();
            }

            if (Application.Current != null)
            {
                Application.Current.Resources[CardBackBrushKey] = brush;
            }

            CardBackChanged?.Invoke(null, EventArgs.Empty);
        }

        private static ImageSource? TryFindImage(string key)
        {
            try
            {
                if (Application.Current?.TryFindResource(key) is ImageSource image)
                {
                    return image;
                }
            }
            catch
            {
                // hiányzó erőforrás: a hívó kezeli
            }

            return null;
        }

        /// <summary>Ha valamiért nem lenne kép, egy egyszerű zöld hátlapot rajzolunk.</summary>
        private static ImageSource CreateFallbackImage()
        {
            const int w = 300, h = 436;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var rect = new Rect(0, 0, w, h);
                var brush = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#0B3A28"),
                    (Color)ColorConverter.ConvertFromString("#14603F"),
                    45);
                dc.DrawRoundedRectangle(brush, new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F9E6C")), 3),
                    new Rect(3, 3, w - 6, h - 6), 12, 12);
                dc.DrawRoundedRectangle(Brushes.Transparent,
                    new Pen(new SolidColorBrush(Color.FromArgb(90, 200, 235, 210)), 1.5),
                    new Rect(14, 14, w - 28, h - 28), 8, 8);
            }

            var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

    }
}
