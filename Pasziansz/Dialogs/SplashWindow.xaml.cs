using System.Windows;

namespace Pasziansz
{
    /// <summary>
    /// A program betöltő képernyője: indításkor rövid ideig az ikonképet mutatja,
    /// amíg a főablak elkészül. A képet a <c>Assets\Splash\nemeskep_2.jpg</c>
    /// beépített erőforrás adja.
    /// </summary>
    public partial class SplashWindow : Window
    {
        /// <summary>A betöltő képernyő alapértelmezett láthatósági ideje.</summary>
        public const int DefaultDurationMilliseconds = 1000;

        public SplashWindow()
        {
            InitializeComponent();
        }
    }
}
