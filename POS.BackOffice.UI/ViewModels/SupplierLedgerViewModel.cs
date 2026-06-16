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

        // --- ACCOUNT SELECTION ---
        [ObservableProperty] private Supplier? _selectedSupplier;

        // --- KPI CARDS ---
        [ObservableProperty] private decimal _totalBilled;
        [ObservableProperty] private decimal _totalCredits;
        [ObservableProperty] private decimal _totalPaid;
        [ObservableProperty] private decimal _netOutstanding;

        // --- PAYMENT FORM ---
        [ObservableProperty] private DateTime _paymentDate = DateTime.Now;
        [ObservableProperty] private decimal _paymentAmount;
        [ObservableProperty] private string _selectedPaymentMethod = "Cheque";
        [ObservableProperty] private string _bankName = string.Empty;
        [ObservableProperty] private string _referenceNumber = string.Empty;
        [ObservableProperty] private string _paymentRemarks = string.Empty;

        // --- TAB FILTERING ---
        [ObservableProperty] private int _selectedTabIndex = 0;

        // --- COLLECTIONS ---
        public ObservableCollection<Supplier> Suppliers { get; set; } = new();
        public ObservableCollection<string> PaymentMethods { get; set; } = new(new[] { "Cash", "Cheque", "Credit / Debit Card", "Direct Bank Transfer" });

        private List<SupplierLedgerEntryDto> _allLedgerEntries = new();
        public ObservableCollection<SupplierLedgerEntryDto> LedgerEntries { get; set; } = new();

        public SupplierLedgerViewModel(SupplierLedgerRepository repository)
        {
            _repository = repository;
            _ = LoadSuppliersAsync();
        }

        private async Task LoadSuppliersAsync()
        {
            var suppliers = await _repository.GetActiveSuppliersAsync();
            foreach (var sup in suppliers) Suppliers.Add(sup);
        }

        // --- CORE FETCHING & MATH ENGINE ---
        [RelayCommand]
        private async Task LoadLedgerAsync()
        {
            if (SelectedSupplier == null) return;

            // Fetch the perfectly chronological statement from our new Repository
            _allLedgerEntries = await _repository.GetLedgerEntriesAsync(SelectedSupplier.Id);

            CalculateKPIs();
            ApplyTabFilter();
        }

        private void CalculateKPIs()
        {
            TotalBilled = _allLedgerEntries.Where(e => e.EntryType == "GRN").Sum(e => e.ChargeAmount);

            // Assuming Returns/Debit Notes are saved as "DEBIT_NOTE" in the future
            TotalCredits = _allLedgerEntries.Where(e => e.EntryType == "DEBIT_NOTE").Sum(e => e.PaidAmount);

            TotalPaid = _allLedgerEntries.Where(e => e.EntryType == "PAYMENT").Sum(e => e.PaidAmount);

            if (_allLedgerEntries.Any())
            {
                // Because the repository reverses the list, the very first item is the most recent balance!
                NetOutstanding = _allLedgerEntries.First().RunningBalance;
            }
            else
            {
                NetOutstanding = 0;
            }
        }

        // --- TAB FILTERING (The "Smart" Tabs) ---
        partial void OnSelectedTabIndexChanged(int value) => ApplyTabFilter();

        private void ApplyTabFilter()
        {
            LedgerEntries.Clear();
            IEnumerable<SupplierLedgerEntryDto> filtered = _allLedgerEntries;

            switch (SelectedTabIndex)
            {
                case 1: // GRN History
                    filtered = _allLedgerEntries.Where(e => e.EntryType == "GRN");
                    break;
                case 2: // Return History
                    filtered = _allLedgerEntries.Where(e => e.EntryType == "DEBIT_NOTE");
                    break;
                case 3: // Payment History
                    filtered = _allLedgerEntries.Where(e => e.EntryType == "PAYMENT");
                    break;
            }

            foreach (var entry in filtered) LedgerEntries.Add(entry);
        }

        // --- QUICK FILL BUTTONS ---
        [RelayCommand]
        private void AutoFillHalf() => PaymentAmount = Math.Round(NetOutstanding / 2, 2);

        [RelayCommand]
        private void AutoFillFull() => PaymentAmount = NetOutstanding;

        [RelayCommand]
        private void ClearPayment()
        {
            PaymentAmount = 0;
            SelectedPaymentMethod = "Cheque";
            BankName = string.Empty;
            ReferenceNumber = string.Empty;
            PaymentRemarks = string.Empty;
            PaymentDate = DateTime.Now;
        }

        // --- SECURE PAYMENT PROCESSING ---
        [RelayCommand]
        private async Task PostPaymentAsync()
        {
            if (SelectedSupplier == null)
            {
                MessageBox.Show("Please load a Supplier account first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (PaymentAmount <= 0)
            {
                MessageBox.Show("Payment Amount must be greater than zero.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedPaymentMethod != "Cash" && string.IsNullOrWhiteSpace(ReferenceNumber))
            {
                MessageBox.Show("A Reference Number or Cheque Number is strictly required for this payment method.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"Post secure payment of Rs. {PaymentAmount:N2} to {SelectedSupplier.CompanyName}?", "Confirm Payment", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var payment = new SupplierLedger
                    {
                        SupplierId = SelectedSupplier.Id,
                        TransactionDate = this.PaymentDate,
                        TransactionType = "PAYMENT",
                        ChargeAmount = 0m,
                        PaymentAmount = this.PaymentAmount,
                        PaymentMethod = this.SelectedPaymentMethod,
                        BankName = this.BankName.Trim(),
                        ReferenceNumber = this.ReferenceNumber.Trim(),
                        Remarks = this.PaymentRemarks.Trim()
                    };

                    await _repository.PostPaymentAsync(payment);
                    MessageBox.Show("Payment successfully processed and applied to the ledger!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    ClearPayment();

                    // Reload everything to instantly update the UI grid and KPI cards
                    await LoadLedgerAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Database Error: {ex.Message}", "System Protection", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}