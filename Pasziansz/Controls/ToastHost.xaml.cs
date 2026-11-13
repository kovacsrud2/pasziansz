using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Pasziansz.Controls
{
    /// <summary>
    /// Rövid, lebegő üzenet a játék alján (pl. „Ide csak piros hatos kerülhet.").
    /// A hibaüzenetek és tippek ezen keresztül jelennek meg, hogy ne szakítsák meg
    /// a játékot felugró ablakokkal.
    /// </summary>
    public partial class ToastHost : UserControl
    {
        private Storyboard? _storyboard;

        public ToastHost()
        {
            InitializeComponent();
        }

        /// <summary>Üzenet megjelenítése megadott ideig (másodperc).</summary>
        public void Show(string message, double seconds = 2.2, string icon = "♣")
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            MessageText.Text = message;
            IconText.Text = icon;
            Chrome.Visibility = Visibility.Visible;
            Chrome.Opacity = 1;

            _storyboard?.Stop(this);
            var animation = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromSeconds(seconds)
            };
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(0.75)));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1)));

            var storyboard = new Storyboard();
            Storyboard.SetTarget(animation, Chrome);
            Storyboard.SetTargetProperty(animation, new PropertyPath(OpacityProperty));
            storyboard.Children.Add(animation);
            storyboard.Completed += (_, _) =>
            {
                Chrome.Visibility = Visibility.Collapsed;
            };

            _storyboard = storyboard;
            storyboard.Begin(this);
        }

        public void HideToast()
        {
            _storyboard?.Stop(this);
            Chrome.Visibility = Visibility.Collapsed;
        }
    }
}
