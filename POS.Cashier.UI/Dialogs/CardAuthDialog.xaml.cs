using System;
using System.Windows;
using System.Windows.Controls;

namespace POS.Cashier.UI.Views.Dialogs
{
    public partial class CardPaymentDialog : Window
    {
        public string SelectedCardType { get; private set; } = string.Empty;
        public string MaskedCardNumber { get; private set; } = string.Empty;

        private bool _isFormatting = false; // To prevent infinite loops in TextChanged

        public CardPaymentDialog()
        {
            InitializeComponent();
            TxtCardNumber.Focus();
        }

        /// <summary>
        /// Automatically formats user input into XXXX-XXXX-XXXX-XXXX live as they type
        /// </summary>
        private void TxtCardNumber_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormatting) return;

            _isFormatting = true;

            // 1. Get raw numbers only
            string raw = TxtCardNumber.Text.Replace("-", "").Replace(" ", "");
            string formatted = "";

            // 2. Re-build string adding dashes every 4 characters
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && i % 4 == 0)
                {
                    formatted += " - ";
                }
                formatted += raw[i];
            }

            // 3. Update text field and maintain the cursor positioning at the end
            TxtCardNumber.Text = formatted;
            TxtCardNumber.CaretIndex = TxtCardNumber.Text.Length;

            _isFormatting = false;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            // Extract pure digits for length evaluation
            string rawInput = TxtCardNumber.Text.Replace("-", "").Replace(" ", "");

            // Verification boundary checks
            if (rawInput.Length < 15 || rawInput.Length > 16 || !long.TryParse(rawInput, out _))
            {
                MessageBox.Show("Please enter a valid 15 or 16-digit card number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtCardNumber.Focus();
                return;
            }

            if (CboCardType.SelectedItem is ComboBoxItem item)
            {
                SelectedCardType = item.Content.ToString();
            }

            // Constructing secure internal masked values for storage rules
            string first6 = rawInput.Substring(0, 6);
            string last4 = rawInput.Substring(rawInput.Length - 4);

            MaskedCardNumber = $"{first6}-XXXX-XXXX-{last4}";

            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}