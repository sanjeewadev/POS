using System;
using System.Windows;

namespace POS.Cashier.UI.Dialogs
{
    public partial class DiscountDialog : Window
    {
        // Public properties to access input selections easily after dialog closes
        public bool IsPercentage => radPercentage.IsChecked ?? false;
        public string DiscountValue => txtDiscountValue.Text.Trim();
        public string ManagerPin => txtPin.Password;

        public DiscountDialog()
        {
            InitializeComponent();

            // Set initial focus directly to the value entry box
            txtDiscountValue.Focus();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            // Simple validation fallback check
            if (string.IsNullOrWhiteSpace(DiscountValue))
            {
                MessageBox.Show("Please enter a valid discount value.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtDiscountValue.Focus();
                return;
            }

            this.DialogResult = true;
            this.Close();
        }
    }
}