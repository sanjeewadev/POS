using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.BackOffice.UI.Views.Dialogs;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Services.Documents;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class CustomerLedgerViewModel : ObservableObject
    {
        private readonly CustomerRepository _customerRepository;
        private readonly CustomerCreditRepository _creditRepository;
        private readonly AuthService _authService;
        private readonly CustomerStatementTextFormatter _statementFormatter;

        public ObservableCollection<CustomerMaster> AvailableCustomers { get; } = new();
        public ObservableCollection<CustomerLedgerStatementRowDto> LedgerEntries { get; } = new();
        public ObservableCollection<CustomerOpenInvoiceDto> OpenInvoices { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomerSelected))]
        private CustomerMaster? _selectedCustomer;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentBalance))]
        [NotifyPropertyChangedFor(nameof(AvailableCredit))]
        [NotifyPropertyChangedFor(nameof(OverdueAmount))]
        [NotifyPropertyChangedFor(nameof(HasPositiveCredit))]
        private CustomerAccountSummaryDto? _currentSummary;

        [ObservableProperty] private DateTime? _filterStartDate = DateTime.Today.AddDays(-30);
        [ObservableProperty] private DateTime? _filterEndDate = DateTime.Today;
        [ObservableProperty] private string _statusText = "Select a customer account.";

        public bool IsCustomerSelected => SelectedCustomer != null;
        public decimal CurrentBalance => CurrentSummary?.CurrentBalance ?? 0m;
        public decimal AvailableCredit => CurrentSummary?.AvailableCredit ?? 0m;
        public decimal OverdueAmount => CurrentSummary?.OverdueAmount ?? 0m;
        public bool HasPositiveCredit => CurrentBalance <= 0m;

        public CustomerLedgerViewModel(
            CustomerRepository customerRepository,
            CustomerCreditRepository creditRepository,
            AuthService authService,
            CustomerStatementTextFormatter statementFormatter)
        {
            _customerRepository = customerRepository;
            _creditRepository = creditRepository;
            _authService = authService;
            _statementFormatter = statementFormatter;
            _ = LoadCustomersAsync();
        }

        partial void OnSelectedCustomerChanged(CustomerMaster? value)
        {
            CurrentSummary = null;
            LedgerEntries.Clear();
            OpenInvoices.Clear();
            StatusText = value == null
                ? "Select a customer account."
                : "Press Load Ledger to view the account.";
        }

        private async Task LoadCustomersAsync()
        {
            var data = await _customerRepository.GetFilteredCustomersAsync("All Customers", string.Empty);
            AvailableCustomers.Clear();
            foreach (CustomerMaster item in data)
                AvailableCustomers.Add(item);
        }

        [RelayCommand]
        private async Task LoadLedgerAsync()
        {
            await LoadSummaryAsync(null, null);
        }

        [RelayCommand]
        private async Task FilterStatementAsync()
        {
            if (FilterStartDate == null || FilterEndDate == null)
            {
                MessageBox.Show("Select both a Start and End date.", "Customer Ledger", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (FilterStartDate.Value.Date > FilterEndDate.Value.Date)
            {
                MessageBox.Show("Start date cannot be after End date.", "Customer Ledger", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await LoadSummaryAsync(FilterStartDate.Value, FilterEndDate.Value);
        }

        private async Task LoadSummaryAsync(DateTime? startDate, DateTime? endDate)
        {
            if (SelectedCustomer == null)
            {
                MessageBox.Show("Select an account first.", "Customer Ledger", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                CurrentSummary = await _creditRepository.GetAccountSummaryAsync(
                    SelectedCustomer.Id,
                    startDate,
                    endDate);

                LedgerEntries.Clear();
                foreach (CustomerLedgerStatementRowDto row in CurrentSummary.StatementRows)
                    LedgerEntries.Add(row);

                OpenInvoices.Clear();
                foreach (CustomerOpenInvoiceDto invoice in CurrentSummary.OpenInvoices)
                    OpenInvoices.Add(invoice);

                StatusText = $"{LedgerEntries.Count} ledger entries. {OpenInvoices.Count} open invoice(s).";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load customer ledger: {ex.Message}", "Customer Ledger", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void PrintStatement()
        {
            if (CurrentSummary == null || !LedgerEntries.Any())
            {
                MessageBox.Show("No statement data to print.", "Customer Ledger", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string text = _statementFormatter.Format(CurrentSummary, FilterStartDate, FilterEndDate);
            var document = new FlowDocument
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                PagePadding = new Thickness(36),
                ColumnGap = 0,
                ColumnWidth = double.PositiveInfinity
            };
            document.Blocks.Add(new Paragraph(new Run(text)) { Margin = new Thickness(0) });

            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
                printDialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, $"Customer Statement - {CurrentSummary.CustomerCode}");
        }

        [RelayCommand]
        private async Task OpenReceivePaymentDialogAsync()
        {
            if (SelectedCustomer == null)
            {
                MessageBox.Show("Select an account first.", "Customer Ledger", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                CustomerAccountSummaryDto summary = await _creditRepository.GetAccountSummaryAsync(SelectedCustomer.Id);
                if (summary.CurrentBalance <= 0m)
                {
                    MessageBox.Show("This customer has no outstanding balance.", "Receive Payment", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new CustomerPaymentDialog(summary)
                {
                    Owner = Application.Current.MainWindow
                };

                if (dialog.ShowDialog() != true)
                    return;

                string userName = string.IsNullOrWhiteSpace(_authService.CurrentUser?.Username)
                    ? "BackOffice"
                    : _authService.CurrentUser.Username.Trim();

                CustomerPaymentResultDto result = await _creditRepository.ReceivePaymentAsync(
                    new CustomerPaymentRequest
                    {
                        ReceiptToken = Guid.NewGuid(),
                        CustomerId = SelectedCustomer.Id,
                        Amount = dialog.PaymentAmount,
                        PaymentMethod = dialog.PaymentMethod,
                        PaymentDate = DateTime.Now,
                        ReferenceNo = dialog.ReferenceNo,
                        BankOrCardType = dialog.BankOrCardType,
                        DestinationAccount = dialog.DestinationAccount,
                        ProcessedBy = userName,
                        Remarks = "Received from Customer Ledger"
                    });

                MessageBox.Show(
                    $"Payment saved.\nReceipt: {result.ReceiptNo}\nRemaining balance: Rs. {result.RemainingBalance:N2}",
                    "Receive Payment",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await RefreshSelectedCustomerAsync();
                await LoadSummaryAsync(FilterStartDate, FilterEndDate);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Receive Payment", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RefreshSelectedCustomerAsync()
        {
            if (SelectedCustomer == null)
                return;

            int selectedId = SelectedCustomer.Id;
            var customers = await _customerRepository.GetFilteredCustomersAsync("All Customers", string.Empty);
            AvailableCustomers.Clear();
            foreach (CustomerMaster customer in customers)
                AvailableCustomers.Add(customer);
            SelectedCustomer = AvailableCustomers.FirstOrDefault(row => row.Id == selectedId);
        }
    }
}
