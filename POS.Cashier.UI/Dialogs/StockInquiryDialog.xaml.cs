using System.Windows;

namespace POS.Cashier.UI.Dialogs
{
    public partial class StockInquiryDialog : Window
    {
        public StockInquiryDialog() { InitializeComponent(); }
        private void CancelBtn_Click(object sender, RoutedEventArgs e) { this.Close(); }
    }
}