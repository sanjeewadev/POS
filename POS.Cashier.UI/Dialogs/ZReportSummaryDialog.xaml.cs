using System.Windows;
using System.Windows.Media;

namespace POS.Cashier.UI.Views.Dialogs
{
    public partial class ZReportSummaryDialog : Window
    {
        public ZReportSummaryDialog(decimal expectedCash, decimal actualCash)
        {
            InitializeComponent();

            ExpectedText.Text = $"Rs. {expectedCash:N2}";
            CountedText.Text = $"Rs. {actualCash:N2}";

            decimal variance = actualCash - expectedCash;
            VarianceText.Text = $"Rs. {variance:N2}";

            // Visual feedback for shortages vs overages
            if (variance < 0)
            {
                VarianceText.Foreground = new SolidColorBrush(Colors.DarkRed); // Shortage
            }
            else if (variance > 0)
            {
                VarianceText.Foreground = new SolidColorBrush(Colors.DarkOrange); // Overage
            }
            else
            {
                VarianceText.Foreground = new SolidColorBrush(Colors.DarkGreen); // Perfect match
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}