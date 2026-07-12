using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using POS.Core.Models.DTOs;

namespace POS.BackOffice.UI.Views.Dialogs
{
    public partial class CustomerPaymentDialog : Window
    {
        private readonly CustomerAccountSummaryDto _summary;

        public decimal PaymentAmount { get; private set; }
        public string PaymentMethod { get; private set; } = "Cash";
        public string ReferenceNo { get; private set; } = string.Empty;
        public string BankOrCardType { get; private set; } = string.Empty;
        public string DestinationAccount { get; private set; } = string.Empty;

        public CustomerPaymentDialog(CustomerAccountSummaryDto summary)
        {
            InitializeComponent();
            _summary = summary ?? throw new ArgumentNullException(nameof(summary));
            DataContext = summary;

            CustomerText.Text = $"{summary.CustomerCode} — {summary.CustomerName}";
            OutstandingText.Text = $"Rs. {summary.CurrentBalance:N2}";
            CreditLimitText.Text = $"Rs. {summary.CreditLimit:N2}";
            AvailableText.Text = $"Rs. {summary.AvailableCredit:N2}";
            AmountBox.Text = summary.CurrentBalance.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private void Receive_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal amount) &&
                !decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
            {
                MessageBox.Show("Enter a valid payment amount.", "Receive Payment", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
            if (amount <= 0m || amount > _summary.CurrentBalance)
            {
                MessageBox.Show($"Payment must be between Rs. 0.01 and Rs. {_summary.CurrentBalance:N2}.", "Receive Payment", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string method = (MethodBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cash";
            string reference = (ReferenceBox.Text ?? string.Empty).Trim();
            if ((method == "Card" || method == "Cheque" || method == "Bank Transfer") && string.IsNullOrWhiteSpace(reference))
            {
                MessageBox.Show($"{method} reference is required.", "Receive Payment", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string destination = (DestinationBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(destination))
            {
                MessageBox.Show("Enter the receiving counter or account.", "Receive Payment", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PaymentAmount = amount;
            PaymentMethod = method;
            ReferenceNo = reference;
            BankOrCardType = (BankBox.Text ?? string.Empty).Trim();
            DestinationAccount = destination;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
