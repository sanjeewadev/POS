using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using POS.Cashier.UI.Services;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services;

namespace POS.Cashier.UI.Dialogs
{
    public partial class CustomerAccountPaymentDialog : Window
    {
        private readonly CustomerCreditRepository _repository;
        private readonly int _shiftSessionId;
        private readonly string _terminalNo;
        private readonly string _cashierName;
        private readonly Guid _receiptToken = Guid.NewGuid();
        private CustomerAccountSummaryDto? _summary;
        private bool _isSaving;
        private int _selectionLoadVersion;

        public CustomerAccountPaymentDialog(
            CustomerCreditRepository repository,
            int shiftSessionId,
            string terminalNo,
            string cashierName)
        {
            InitializeComponent();
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _shiftSessionId = shiftSessionId;
            _terminalNo = terminalNo ?? string.Empty;
            _cashierName = cashierName ?? string.Empty;
            Loaded += CustomerAccountPaymentDialog_Loaded;
        }

        private async void CustomerAccountPaymentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            CustomerBox.IsEnabled = false;

            try
            {
                var customers = await _repository.GetCreditCustomersAsync();
                CustomerBox.ItemsSource = customers
                    .Where(row => row.CurrentBalance > 0m)
                    .ToList();
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Load customer accounts for payment",
                    ex);

                MessageBox.Show(
                    $"Customer accounts could not be loaded.\n\n{ex.Message}",
                    "Customer Payment",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                CustomerBox.IsEnabled = true;
                CustomerBox.Focus();
            }
        }

        private async void CustomerBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int loadVersion = ++_selectionLoadVersion;
            _summary = null;
            AmountBox.IsEnabled = false;
            ResetSummaryDisplay();

            if (CustomerBox.SelectedItem is not CustomerMaster customer)
                return;

            try
            {
                CustomerAccountSummaryDto summary =
                    await _repository.GetAccountSummaryAsync(customer.Id);

                if (loadVersion != _selectionLoadVersion ||
                    CustomerBox.SelectedItem is not CustomerMaster selectedCustomer ||
                    selectedCustomer.Id != customer.Id)
                {
                    return;
                }

                _summary = summary;
                OutstandingText.Text = $"Rs. {summary.CurrentBalance:N2}";
                OverdueText.Text = $"Rs. {summary.OverdueAmount:N2}";
                InvoiceCountText.Text = summary.OpenInvoices.Count
                    .ToString(CultureInfo.InvariantCulture);
                AmountBox.Text = summary.CurrentBalance
                    .ToString("0.00", CultureInfo.InvariantCulture);
                AmountBox.IsEnabled = true;
                AmountBox.Focus();
                AmountBox.SelectAll();
            }
            catch (Exception ex)
            {
                if (loadVersion != _selectionLoadVersion)
                    return;

                LocalLogService.WriteException(
                    "Cashier",
                    "Load selected customer account summary",
                    ex);

                MessageBox.Show(
                    $"The selected account could not be loaded.\n\n{ex.Message}",
                    "Customer Payment",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_isSaving)
                return;

            if (CustomerBox.SelectedItem is not CustomerMaster customer || _summary == null)
            {
                MessageBox.Show(
                    "Select a customer account.",
                    "Customer Payment",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                CustomerBox.Focus();
                return;
            }

            if (!decimal.TryParse(
                    AmountBox.Text,
                    NumberStyles.Number,
                    CultureInfo.CurrentCulture,
                    out decimal amount) &&
                !decimal.TryParse(
                    AmountBox.Text,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out amount))
            {
                MessageBox.Show(
                    "Enter a valid payment amount.",
                    "Customer Payment",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                AmountBox.Focus();
                AmountBox.SelectAll();
                return;
            }

            string method =
                (MethodBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
                ?? "Cash";
            string reference = (ReferenceBox.Text ?? string.Empty).Trim();

            SetSavingState(true);

            try
            {
                CustomerPaymentResultDto result =
                    await _repository.ReceivePaymentAsync(
                        new CustomerPaymentRequest
                        {
                            ReceiptToken = _receiptToken,
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
                    $"Payment saved.\nReceipt: {result.ReceiptNo}\n" +
                    $"Remaining balance: Rs. {result.RemainingBalance:N2}",
                    "Customer Payment",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                _isSaving = false;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Save customer account payment",
                    ex);

                MessageBox.Show(
                    $"The payment could not be saved.\n\n{ex.Message}",
                    "Customer Payment",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                SetSavingState(false);
            }
        }

        private void SetSavingState(bool isSaving)
        {
            _isSaving = isSaving;
            SaveButton.IsEnabled = !isSaving;
            CancelButton.IsEnabled = !isSaving;
            CustomerBox.IsEnabled = !isSaving;
            AmountBox.IsEnabled = !isSaving && _summary != null;
            MethodBox.IsEnabled = !isSaving;
            ReferenceBox.IsEnabled = !isSaving;
            BusyText.Visibility = isSaving ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ResetSummaryDisplay()
        {
            OutstandingText.Text = "Rs. 0.00";
            OverdueText.Text = "Rs. 0.00";
            InvoiceCountText.Text = "0";
            AmountBox.Text = string.Empty;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || _isSaving)
                return;

            DialogResult = false;
            e.Handled = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (!_isSaving)
                DialogResult = false;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_isSaving)
                e.Cancel = true;

            base.OnClosing(e);
        }
    }
}
