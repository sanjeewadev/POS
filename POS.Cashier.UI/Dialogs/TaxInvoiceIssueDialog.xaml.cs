using System.Windows;
using System.Windows.Input;
using POS.Core.Models;

namespace POS.Cashier.UI.Dialogs
{
    public partial class TaxInvoiceIssueDialog : Window
    {
        public string CustomerNameValue =>
            CustomerNameTextBox.Text.Trim();

        public string CustomerTinValue =>
            CustomerTinTextBox.Text.Trim();

        public string CustomerVatNoValue =>
            CustomerVatTextBox.Text.Trim();

        public string CustomerAddressValue =>
            CustomerAddressTextBox.Text.Trim();

        public TaxInvoiceIssueDialog(SalesHeader sale)
        {
            InitializeComponent();

            CustomerNameTextBox.Text =
                sale?.CustomerName ?? string.Empty;
            CustomerTinTextBox.Text =
                sale?.CustomerTinSnapshot ?? string.Empty;
            CustomerVatTextBox.Text =
                sale?.CustomerVatNoSnapshot ?? string.Empty;
            CustomerAddressTextBox.Text =
                sale?.CustomerAddressSnapshot ?? string.Empty;
        }

        private void Continue_Click(
            object sender,
            RoutedEventArgs e)
        {
            ValidationText.Text = string.Empty;

            if (string.IsNullOrWhiteSpace(CustomerNameValue) ||
                CustomerNameValue.Equals(
                    "Walk-In",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                ValidationText.Text =
                    "Enter the Tax Invoice customer name.";
                CustomerNameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(CustomerAddressValue))
            {
                ValidationText.Text =
                    "Enter the customer address.";
                CustomerAddressTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(CustomerTinValue) &&
                string.IsNullOrWhiteSpace(CustomerVatNoValue))
            {
                ValidationText.Text =
                    "Enter the customer TIN or VAT registration number.";
                CustomerTinTextBox.Focus();
                return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(
            object sender,
            RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            DialogResult = false;
            Close();
            e.Handled = true;
        }
    }
}
