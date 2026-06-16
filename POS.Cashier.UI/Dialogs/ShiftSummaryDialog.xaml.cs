using System.Windows;

namespace POS.Cashier.UI.Dialogs
{
    public partial class ShiftSummaryDialog : Window
    {
        public ShiftSummaryDialog() { InitializeComponent(); }
        private void CancelBtn_Click(object sender, RoutedEventArgs e) { this.Close(); }
    }
}