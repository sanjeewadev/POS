using System;
using System.Windows;
using System.Windows.Controls;

namespace POS.Cashier.UI.Views.Dialogs
{
    public partial class DocumentTenderDialog : Window
    {
        // Public properties to access inputs from the parent window
        public string SelectedBank => (cmbBank.SelectedItem as ComboBoxItem)?.Content.ToString();
        public string ChequeNumber => txtChequeNumber.Text.Trim();

        public DocumentTenderDialog()
        {
            InitializeComponent();

            // Focus on Cheque Number field by default
            txtChequeNumber.Focus();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            // Simple validation to ensure cheque number is provided
            if (string.IsNullOrWhiteSpace(ChequeNumber))
            {
                MessageBox.Show("Please enter the cheque number.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtChequeNumber.Focus();
                return;
            }

            this.DialogResult = true;
            this.Close();
        }
    }
}