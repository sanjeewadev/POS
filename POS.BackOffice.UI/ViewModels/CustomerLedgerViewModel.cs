using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using POS.Core.Models;
using POS.Core.Repositories; // Ensure you have a repository that can fetch Ledger entries

namespace POS.BackOffice.UI.ViewModels
{
    public class CustomerLedgerViewModel : ViewModelBase
    {
        private readonly CustomerAdminRepository _repository; // Reusing your admin repo, or a dedicated LedgerRepo

        // ==========================================
        // PROPERTIES: CUSTOMER SELECTION
        // ==========================================
        public ObservableCollection<CustomerMaster> AvailableCustomers { get; set; } = new();

        private CustomerMaster? _selectedCustomer;
        public CustomerMaster? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                _selectedCustomer = value;
                OnPropertyChanged(nameof(SelectedCustomer));
                OnPropertyChanged(nameof(IsCustomerSelected));
                OnPropertyChanged(nameof(HasPositiveCredit));

                // Clear the ledger when a new customer is selected until they hit "LOAD"
                LedgerEntries.Clear();
            }
        }

        public bool IsCustomerSelected => SelectedCustomer != null;

        // This triggers the Red/Green color change in your XAML
        public bool HasPositiveCredit => SelectedCustomer != null && SelectedCustomer.CurrentBalance <= 0;

        // ==========================================
        // PROPERTIES: LEDGER & FILTERS
        // ==========================================
        public ObservableCollection<CustomerLedger> LedgerEntries { get; set; } = new();

        private DateTime? _filterStartDate = DateTime.Today.AddDays(-30); // Default to last 30 days
        public DateTime? FilterStartDate
        {
            get => _filterStartDate;
            set { _filterStartDate = value; OnPropertyChanged(nameof(FilterStartDate)); }
        }

        private DateTime? _filterEndDate = DateTime.Today;
        public DateTime? FilterEndDate
        {
            get => _filterEndDate;
            set { _filterEndDate = value; OnPropertyChanged(nameof(FilterEndDate)); }
        }

        // ==========================================
        // COMMANDS
        // ==========================================
        public ICommand LoadLedgerCommand { get; }
        public ICommand FilterStatementCommand { get; }
        public ICommand PrintStatementCommand { get; }
        public ICommand OpenReceivePaymentDialogCommand { get; }

        public CustomerLedgerViewModel(CustomerAdminRepository repository)
        {
            _repository = repository;

            LoadLedgerCommand = new RelayCommand(async (o) => await ExecuteLoadLedger());
            FilterStatementCommand = new RelayCommand(async (o) => await ExecuteFilterStatement());
            PrintStatementCommand = new RelayCommand((o) => ExecutePrintStatement());
            OpenReceivePaymentDialogCommand = new RelayCommand((o) => ExecuteReceivePayment());

            // Load the dropdown list immediately when the page opens
            _ = LoadCustomersAsync();
        }

        // ==========================================
        // EXECUTION METHODS
        // ==========================================
        private async Task LoadCustomersAsync()
        {
            // Fetching all customers (or specifically Wholesale/Credit customers)
            var data = await _repository.GetFilteredCustomersAsync("All Customers", "");
            AvailableCustomers.Clear();
            foreach (var item in data) AvailableCustomers.Add(item);
        }

        private async Task ExecuteLoadLedger()
        {
            if (SelectedCustomer == null)
            {
                MessageBox.Show("Please select an account first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Fetch the full ledger (no date filters)
                var entries = await _repository.GetCustomerLedgerAsync(SelectedCustomer.Id);

                LedgerEntries.Clear();
                foreach (var entry in entries) LedgerEntries.Add(entry);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load ledger: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteFilterStatement()
        {
            if (SelectedCustomer == null) return;
            if (FilterStartDate == null || FilterEndDate == null)
            {
                MessageBox.Show("Please select both a Start and End date.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Fetch the ledger using the selected date range
                var entries = await _repository.GetCustomerLedgerAsync(SelectedCustomer.Id, FilterStartDate.Value, FilterEndDate.Value);

                LedgerEntries.Clear();
                foreach (var entry in entries) LedgerEntries.Add(entry);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to filter statement: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecutePrintStatement()
        {
            if (SelectedCustomer == null || !LedgerEntries.Any())
            {
                MessageBox.Show("No statement data to print.", "Print Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // TODO: Hook up to your PDF reporting engine (e.g., Crystal Reports, iTextSharp, or standard PrintDialog)
            MessageBox.Show($"Sending statement for {SelectedCustomer.FullName} to printer...", "Print Job Started", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExecuteReceivePayment()
        {
            if (SelectedCustomer == null)
            {
                MessageBox.Show("Please select an account first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // TODO: Phase 3 - Open the "Receive Payment" popup dialog so the manager can enter Cheque/Cash details
            MessageBox.Show($"Open Payment Dialog for {SelectedCustomer.FullName}.\nCurrent Balance: Rs. {SelectedCustomer.CurrentBalance:N2}",
                            "Payment Gateway", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}