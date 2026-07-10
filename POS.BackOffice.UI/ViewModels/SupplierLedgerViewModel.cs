using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class SupplierLedgerViewModel : ObservableObject
    {
        private readonly SupplierLedgerRepository _repository;
        private List<SupplierLedgerEntryDto> _allLedgerEntries = new();

        [ObservableProperty]
        private SupplierLedgerSupplierLookupDto? _selectedSupplier;

        [ObservableProperty]
        private SupplierLedgerEntryDto? _selectedLedgerEntry;

        [ObservableProperty]
        private decimal _totalBilled = 0m;

        [ObservableProperty]
        private decimal _totalCredits = 0m;

        [ObservableProperty]
        private decimal _totalPaid = 0m;

        [ObservableProperty]
        private decimal _netOutstanding = 0m;

        [ObservableProperty]
        private DateTime _paymentDate = DateTime.Now;

        [ObservableProperty]
        private decimal _paymentAmount = 0m;

        [ObservableProperty]
        private string _selectedPaymentMethod = "Cheque";

        [ObservableProperty]
        private string _bankName = string.Empty;

        [ObservableProperty]
        private string _referenceNumber = string.Empty;

        [ObservableProperty]
        private string _paymentRemarks = string.Empty;

        [ObservableProperty]
        private int _selectedTabIndex = 0;

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        public ObservableCollection<SupplierLedgerSupplierLookupDto> Suppliers { get; } = new();

        public ObservableCollection<string> PaymentMethods { get; } = new()
        {
            "Cash",
            "Cheque",
            "Credit / Debit Card",
            "Direct Bank Transfer"
        };

        public ObservableCollection<SupplierLedgerEntryDto> LedgerEntries { get; } = new();

        public SupplierLedgerViewModel(SupplierLedgerRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            IsBusy = true;

            try
            {
                Suppliers.Clear();

                var suppliers = await _repository.GetActiveSuppliersAsync();

                foreach (var supplier in suppliers)
                    Suppliers.Add(supplier);

                StatusMessage = $"Loaded {Suppliers.Count} active supplier(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load suppliers.";

                MessageBox.Show(
                    $"Failed to load suppliers:\n\n{ex.Message}",
                    "Supplier Ledger",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnSelectedSupplierChanged(SupplierLedgerSupplierLookupDto? value)
        {
            ClearLedgerOnly();

            if (value == null)
            {
                StatusMessage = "Select a supplier account.";
                return;
            }

            StatusMessage = "Supplier selected. Click LOAD LEDGER.";
        }

        partial void OnSelectedTabIndexChanged(int value)
        {
            ApplyTabFilter();
        }

        partial void OnPaymentAmountChanged(decimal value)
        {
            if (value < 0)
                PaymentAmount = 0m;
        }

        partial void OnSelectedPaymentMethodChanged(string value)
        {
            if (string.Equals(value, "Cash", StringComparison.OrdinalIgnoreCase))
            {
                BankName = string.Empty;
                ReferenceNumber = string.Empty;
            }
        }

        [RelayCommand]
        private async Task LoadLedgerAsync()
        {
            if (SelectedSupplier == null)
            {
                MessageBox.Show(
                    "Please select a supplier account first.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;

            try
            {
                _allLedgerEntries = await _repository.GetLedgerEntriesAsync(SelectedSupplier.Id);
                CalculateKpis();
                ApplyTabFilter();

                StatusMessage = _allLedgerEntries.Count == 0
                    ? "No ledger transactions found for selected supplier."
                    : $"Loaded {_allLedgerEntries.Count} ledger transaction(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load supplier ledger.";

                MessageBox.Show(
                    $"Failed to load supplier ledger:\n\n{ex.Message}",
                    "Supplier Ledger",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            if (SelectedSupplier == null)
            {
                StatusMessage = "Select a supplier account first.";
                return;
            }

            await LoadLedgerAsync();
        }

        private void CalculateKpis()
        {
            TotalBilled = Math.Round(
                _allLedgerEntries
                    .Where(e => e.EntryType == "GRN")
                    .Sum(e => e.ChargeAmount),
                2);

            TotalCredits = Math.Round(
                _allLedgerEntries
                    .Where(e => IsCreditType(e.EntryType))
                    .Sum(e => e.PaidAmount),
                2);

            TotalPaid = Math.Round(
                _allLedgerEntries
                    .Where(e => e.EntryType == "PAYMENT")
                    .Sum(e => e.PaidAmount),
                2);

            NetOutstanding = _allLedgerEntries.Any()
                ? Math.Round(_allLedgerEntries.First().RunningBalance, 2)
                : 0m;

            if (NetOutstanding < 0)
                NetOutstanding = 0m;
        }

        private void ApplyTabFilter()
        {
            LedgerEntries.Clear();

            IEnumerable<SupplierLedgerEntryDto> filtered = _allLedgerEntries;

            switch (SelectedTabIndex)
            {
                case 1:
                    filtered = _allLedgerEntries.Where(e => e.EntryType == "GRN");
                    break;

                case 2:
                    filtered = _allLedgerEntries.Where(e => IsCreditType(e.EntryType));
                    break;

                case 3:
                    filtered = _allLedgerEntries.Where(e => e.EntryType == "PAYMENT");
                    break;
            }

            foreach (var entry in filtered)
                LedgerEntries.Add(entry);
        }

        [RelayCommand]
        private void AutoFillHalf()
        {
            PaymentAmount = NetOutstanding > 0
                ? Math.Round(NetOutstanding / 2m, 2)
                : 0m;
        }

        [RelayCommand]
        private void AutoFillFull()
        {
            PaymentAmount = NetOutstanding > 0
                ? Math.Round(NetOutstanding, 2)
                : 0m;
        }

        [RelayCommand]
        private void ClearPayment()
        {
            PaymentDate = DateTime.Now;
            PaymentAmount = 0m;
            SelectedPaymentMethod = "Cheque";
            BankName = string.Empty;
            ReferenceNumber = string.Empty;
            PaymentRemarks = string.Empty;
            StatusMessage = "Payment form cleared.";
        }

        [RelayCommand]
        private async Task PostPaymentAsync()
        {
            if (SelectedSupplier == null)
            {
                MessageBox.Show(
                    "Please select and load a supplier account first.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!_allLedgerEntries.Any())
            {
                MessageBox.Show(
                    "Please load the supplier ledger before posting a payment.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!ValidatePaymentForm())
                return;

            var result = MessageBox.Show(
                $"Post supplier payment?\n\nSupplier: {SelectedSupplier.DisplayText}\nAmount: Rs. {PaymentAmount:N2}\nMethod: {SelectedPaymentMethod}",
                "Post Supplier Payment",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            IsBusy = true;

            try
            {
                var payment = new SupplierLedger
                {
                    SupplierId = SelectedSupplier.Id,
                    TransactionDate = PaymentDate.Date,
                    TransactionType = "PAYMENT",
                    ReferenceDocument = string.Empty,

                    ChargeAmount = 0m,
                    PaymentAmount = Math.Round(PaymentAmount, 2),

                    PaymentMethod = SelectedPaymentMethod.Trim(),
                    BankName = BankName.Trim(),
                    ReferenceNumber = ReferenceNumber.Trim(),

                    DueDate = PaymentDate.Date,
                    IsPaid = true,

                    CreatedBy = "Admin",
                    CreatedAt = DateTime.Now,

                    Remarks = string.IsNullOrWhiteSpace(PaymentRemarks)
                        ? "Supplier payment"
                        : PaymentRemarks.Trim()
                };

                await _repository.PostPaymentAsync(payment);

                MessageBox.Show(
                    "Supplier payment posted successfully.",
                    "Supplier Ledger",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                ClearPayment();
                await LoadLedgerAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = "Payment posting failed.";

                MessageBox.Show(
                    $"Payment posting failed:\n\n{ex.Message}",
                    "Supplier Ledger",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool ValidatePaymentForm()
        {
            if (NetOutstanding <= 0)
            {
                MessageBox.Show(
                    "This supplier has no outstanding balance to pay.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return false;
            }

            if (PaymentAmount <= 0)
            {
                MessageBox.Show(
                    "Payment amount must be greater than zero.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (PaymentAmount > NetOutstanding)
            {
                MessageBox.Show(
                    $"Payment amount cannot exceed outstanding balance.\n\nOutstanding balance: Rs. {NetOutstanding:N2}",
                    "Overpayment Blocked",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (PaymentDate.Date > DateTime.Now.Date.AddDays(1))
            {
                MessageBox.Show(
                    "Payment date cannot be in the far future.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (PaymentDate.Date < new DateTime(2000, 1, 1))
            {
                MessageBox.Show(
                    "Payment date is not valid.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(SelectedPaymentMethod))
            {
                MessageBox.Show(
                    "Payment method is required.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            string method = SelectedPaymentMethod.Trim();
            bool isCash = method.Equals("Cash", StringComparison.OrdinalIgnoreCase);
            bool isCheque = method.Contains("Cheque", StringComparison.OrdinalIgnoreCase);
            bool isBankTransfer = method.Contains("Bank", StringComparison.OrdinalIgnoreCase) ||
                                  method.Contains("Transfer", StringComparison.OrdinalIgnoreCase);
            bool isCard = method.Contains("Card", StringComparison.OrdinalIgnoreCase);

            if (!isCash && string.IsNullOrWhiteSpace(ReferenceNumber))
            {
                MessageBox.Show(
                    "Reference number / cheque number is required for this payment method.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if ((isCheque || isBankTransfer) && string.IsNullOrWhiteSpace(BankName))
            {
                MessageBox.Show(
                    "Bank name is required for cheque and bank transfer payments.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (isCard && string.IsNullOrWhiteSpace(ReferenceNumber))
            {
                MessageBox.Show(
                    "Card payment reference number is required.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if ((PaymentRemarks ?? string.Empty).Length > 250)
            {
                MessageBox.Show(
                    "Payment remarks cannot be longer than 250 characters.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        [RelayCommand]
        private void ClearAll()
        {
            SelectedSupplier = null;
            ClearLedgerOnly();
            ClearPayment();
            StatusMessage = "Ready.";
        }

        private void ClearLedgerOnly()
        {
            _allLedgerEntries.Clear();
            LedgerEntries.Clear();
            SelectedLedgerEntry = null;
            TotalBilled = 0m;
            TotalCredits = 0m;
            TotalPaid = 0m;
            NetOutstanding = 0m;
        }

        private static bool IsCreditType(string entryType)
        {
            return entryType == "DEBIT_NOTE" ||
                   entryType == "CREDIT_NOTE" ||
                   entryType == "SUPPLIER_RETURN";
        }
    }
}
