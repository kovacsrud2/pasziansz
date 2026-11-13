using System;
using System.Windows;
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
        protected override void OnStartup(StartupEventArgs e)
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

            var window = new MainWindow();
            window.ApplyInitialWindowBounds();
            MainWindow = window;
            window.Show();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                "Váratlan hiba történt a játék közben:\n\n" + e.Exception.Message,
                "Zöld Pasziánsz",
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
                    "Zöld Pasziánsz",
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
