using System.Windows;

namespace POS.Cashier.UI.Dialogs
{
    public partial class CustomerSelectionDialog : Window
    {
        public CustomerSelectionDialog()
        {
            InitializeComponent();
            txtSearch.Focus();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}