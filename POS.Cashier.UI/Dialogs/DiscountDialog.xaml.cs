using System.Windows;

namespace POS.Cashier.UI.Dialogs
{
    public partial class DiscountDialog : Window
    {
        public DiscountDialog()
        {
            InitializeComponent();
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