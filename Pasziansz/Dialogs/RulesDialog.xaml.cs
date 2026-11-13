using System.Windows;

namespace Pasziansz
{
    /// <summary>A játékszabály leírását mutató párbeszédablak.</summary>
    public partial class RulesDialog : Window
    {
        public RulesDialog()
        {
            InitializeComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
