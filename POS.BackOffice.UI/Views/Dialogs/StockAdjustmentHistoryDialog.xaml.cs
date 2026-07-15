using System.Windows;

namespace POS.BackOffice.UI.Views.Dialogs
{
    public partial class StockAdjustmentHistoryDialog : Window
    {
        public StockAdjustmentHistoryDialog()
        {
            InitializeComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
