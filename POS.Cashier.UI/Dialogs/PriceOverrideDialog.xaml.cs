using System.Windows;

namespace POS.Cashier.UI.Dialogs
{
    public partial class PriceOverrideDialog : Window
    {
        public PriceOverrideDialog() { InitializeComponent(); }
        private void CancelBtn_Click(object sender, RoutedEventArgs e) { this.DialogResult = false; this.Close(); }
        private void ConfirmBtn_Click(object sender, RoutedEventArgs e) { this.DialogResult = true; this.Close(); }
    }
}