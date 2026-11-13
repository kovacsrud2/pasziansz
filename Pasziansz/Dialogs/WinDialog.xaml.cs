using System;
using System.Windows;

namespace Pasziansz
{
    /// <summary>
    /// Győzelmi párbeszédablak: a játszma eredménye és az újrakezdés lehetősége.
    /// </summary>
    public partial class WinDialog : Window
    {
        public WinDialog(int score, int seconds, int moves, int drawCount, bool newRecord)
        {
            InitializeComponent();

            ScoreValue.Text = score.ToString("N0");
            TimeValue.Text = TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"mm\:ss");
            MovesValue.Text = moves.ToString();
            VariantValue.Text = drawCount == 3 ? "3 lapos" : "1 lapos";
            RecordText.Visibility = newRecord ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PlayAgain_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
