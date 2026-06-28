using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace POS.Cashier.UI.Views.Dialogs
{
    public partial class GiftVoucherPaymentWindow : Window
    {
        // Public properties to return checked voucher info back to home window cart
        public double VoucherAmount { get; private set; } = 0;
        public string VoucherNumber { get; private set; } = string.Empty;

        public GiftVoucherPaymentWindow()
        {
            InitializeComponent();
            txtVoucherInput.Focus();
        }

        // Catch scanner enter keys
        private void TxtVoucherInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ProcessVoucherLookup();
                e.Handled = true;
            }
        }

        private void ValidateVoucherBtn_Click(object sender, RoutedEventArgs e)
        {
            ProcessVoucherLookup();
        }

        private void ProcessVoucherLookup()
        {
            string inputCode = txtVoucherInput.Text.Trim();

            if (string.IsNullOrWhiteSpace(inputCode))
            {
                MessageBox.Show("Please scan or enter a valid voucher number.", "Validation Alert", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // TODO: Query Master Item/Voucher Inventory table to grab original assigned price parameters
            // Mock matching result:
            VoucherNumber = inputCode;
            VoucherAmount = 2500.00;

            // Update UI components
            lblVoucherCode.Text = VoucherNumber;
            lblVoucherValue.Text = VoucherAmount.ToString("N2");

            // Turn status card to success light-green alert tint
            brdVoucherStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4EDDA"));

            // Open the button pathway to commit the value back to the checkout sequence
            btnApplyToBill.IsEnabled = true;
            btnApplyToBill.Focus();
        }

        private void ApplyToBillBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}