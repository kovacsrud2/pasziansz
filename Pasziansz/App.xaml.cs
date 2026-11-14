using System;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Threading;
using Pasziansz.Services;

namespace Pasziansz
{
    /// <summary>
    /// Az alkalmazás osztálya: gondoskodik a beállítások betöltéséről és a nem kezelt
    /// kivételek elfogásáról (hogy a játék ne omoljon össze egy-egy váratlan hibától).
    /// </summary>
    public partial class App : Application
    {
        /// <summary>A betöltő képernyő, amíg a főablak elkészül.</summary>
        private SplashWindow? _splash;

        /// <summary>
        /// Indítás: előbb a betöltő képernyő jelenik meg, és csak a megadott idő
        /// elteltével váltunk a főablakra. A várakozás aszinkron, hogy a felület
        /// közben ne fagyjon le (a kép kirajzolása is lefuthat).
        /// </summary>
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            var settings = SettingsStore.Load();
            try
            {
                ThemeManager.ApplyCardBack(settings.CardBackStyle);
            }
            catch
            {
                // Ha a hátlap betöltése nem sikerül, a kártyák alapértelmezett hátlapot kapnak.
            }

            // A betöltő képernyő bezárása ne állítsa le a programot: a főablak
            // ilyenkor még nem épült fel, és az "utolsó ablak" bezárására a WPF
            // leállna. A leállást a főablak zárása váltja ki.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                _splash = new SplashWindow();
                _splash.Show();

                // A betöltő képernyő láthatósági ideje. Aszinkron várakozás: a
                // Dispatcher közben kiszolgálja a festést, így a kép tényleg
                // megjelenik (blokkoló várakozásnál üres ablak látszana).
                await Task.Delay(SplashWindow.DefaultDurationMilliseconds);

                var window = new MainWindow();
                window.ApplyInitialWindowBounds();
                MainWindow = window;

                // Mostantól a főablak zárása állítja le a programot.
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                window.Show();
            }
            catch
            {
                // Ha a főablak nem építhető fel, ne maradjunk örökre futó, ablak
                // nélküli folyamatban.
                Shutdown();
                throw;
            }
            finally
            {
                _splash?.Close();
                _splash = null;
            }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                "Váratlan hiba történt a játék közben:\n\n" + e.Exception.Message,
                "Gészi pasziánsz",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                try
                {
                    SettingsStore.Save(Settings.Current);
                }
                catch
                {
                    // ha a mentés sem sikerül, már nem tudunk mit tenni
                }

                MessageBox.Show(
                    "Végzetes hiba történt:\n\n" + ex.Message,
                    "Gészi pasziánsz",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SettingsStore.Save(Settings.Current);
            base.OnExit(e);
        }
    }
}
