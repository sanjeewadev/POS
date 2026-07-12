using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;

namespace POS.Cashier.UI.Dialogs
{
    public partial class CustomerAccountPaymentDialog : Window
    {
        private readonly CustomerCreditRepository _repository;
        private readonly int _shiftSessionId;
        private readonly string _terminalNo;
        private readonly string _cashierName;
        private CustomerAccountSummaryDto? _summary;

        public CustomerAccountPaymentDialog(
            CustomerCreditRepository repository,
            int shiftSessionId,
            string terminalNo,
            string cashierName)
        {
            InitializeComponent();
            _repository = repository;
            _shiftSessionId = shiftSessionId;
            _terminalNo = terminalNo;
            _cashierName = cashierName;
            Loaded += CustomerAccountPaymentDialog_Loaded;
        }

        private async void CustomerAccountPaymentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var customers = await _repository.GetCreditCustomersAsync();
                CustomerBox.ItemsSource = customers.Where(row => row.CurrentBalance > 0m).ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Customer Payment", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CustomerBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CustomerBox.SelectedItem is not CustomerMaster customer)
                return;

            try
            {
                _summary = await _repository.GetAccountSummaryAsync(customer.Id);
                OutstandingText.Text = $"Rs. {_summary.CurrentBalance:N2}";
                OverdueText.Text = $"Rs. {_summary.OverdueAmount:N2}";
                InvoiceCountText.Text = _summary.OpenInvoices.Count.ToString(CultureInfo.InvariantCulture);
                AmountBox.Text = _summary.CurrentBalance.ToString("0.00", CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Customer Payment", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (CustomerBox.SelectedItem is not CustomerMaster customer || _summary == null)
            {
                MessageBox.Show("Select a customer account.", "Customer Payment", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal amount) &&
                !decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
            {
                MessageBox.Show("Enter a valid payment amount.", "Customer Payment", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string method = (MethodBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cash";
            string reference = (ReferenceBox.Text ?? string.Empty).Trim();

            try
            {
                CustomerPaymentResultDto result = await _repository.ReceivePaymentAsync(
                    new CustomerPaymentRequest
                    {
                        ReceiptToken = Guid.NewGuid(),
                        CustomerId = customer.Id,
                        Amount = amount,
                        PaymentMethod = method,
                        PaymentDate = DateTime.Now,
                        ReferenceNo = reference,
                        BankOrCardType = method,
                        DestinationAccount = "Cashier Terminal",
                        ProcessedBy = _cashierName,
                        TerminalNo = _terminalNo,
                        ShiftSessionId = _shiftSessionId,
                        Remarks = "Customer account payment received at Cashier"
                    });

                MessageBox.Show(
                    $"Payment saved.\nReceipt: {result.ReceiptNo}\nRemaining balance: Rs. {result.RemainingBalance:N2}",
                    "Customer Payment",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Customer Payment", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
