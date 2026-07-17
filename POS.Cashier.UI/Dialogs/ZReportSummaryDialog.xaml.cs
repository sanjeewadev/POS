using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace POS.Cashier.UI.Dialogs
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

            string brushKey = variance switch
            {
                < 0m => "CashierDangerBrush",
                > 0m => "CashierWarningDarkBrush",
                _ => "CashierSuccessDarkBrush"
            };

            VarianceText.Foreground = (Brush)FindResource(brushKey);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ConfirmButton.Focus();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            DialogResult = false;
            e.Handled = true;
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
