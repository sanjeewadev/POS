using System;
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
    public partial class CustomerLedgerViewModel : ObservableObject
    {
        private readonly CustomerRepository _repository;

        // ==========================================
        // PROPERTIES: CUSTOMER SELECTION
        // ==========================================
        public ObservableCollection<CustomerMaster> AvailableCustomers { get; set; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomerSelected))]
        [NotifyPropertyChangedFor(nameof(HasPositiveCredit))]
        private CustomerMaster? _selectedCustomer;

        public bool IsCustomerSelected => SelectedCustomer != null;

        // This triggers the Red/Green color change in your XAML
        public bool HasPositiveCredit => SelectedCustomer != null && SelectedCustomer.CurrentBalance <= 0;

        // ==========================================
        // PROPERTIES: LEDGER & FILTERS
        // ==========================================
        public ObservableCollection<CustomerLedger> LedgerEntries { get; set; } = new();

        [ObservableProperty] private DateTime? _filterStartDate = DateTime.Today.AddDays(-30);
        [ObservableProperty] private DateTime? _filterEndDate = DateTime.Today;

        public CustomerLedgerViewModel(CustomerRepository repository)
        {
            _repository = repository;

            // Load the dropdown list immediately when the page opens
            _ = LoadCustomersAsync();
        }

        // ==========================================
        // TRIGGERS
        // ==========================================
        partial void OnSelectedCustomerChanged(CustomerMaster? value)
        {
            // Clear the ledger when a new customer is selected until they hit "LOAD"
            LedgerEntries.Clear();
        }

        // ==========================================
        // EXECUTION METHODS
        // ==========================================
        private async Task LoadCustomersAsync()
        {
            // Fetch all customers for the dropdown
            var data = await _repository.GetFilteredCustomersAsync("All Customers", "");
            AvailableCustomers.Clear();
            foreach (var item in data) AvailableCustomers.Add(item);
        }

        [RelayCommand]
        private async Task LoadLedgerAsync()
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

        [RelayCommand]
        private async Task FilterStatementAsync()
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

        [RelayCommand]
        private void PrintStatement()
        {
            if (SelectedCustomer == null || !LedgerEntries.Any())
            {
                MessageBox.Show("No statement data to print.", "Print Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // TODO: Hook up to your PDF reporting engine later
            MessageBox.Show($"Sending statement for {SelectedCustomer.FullName} to printer...", "Print Job Started", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void OpenReceivePaymentDialog()
        {
            if (SelectedCustomer == null)
            {
                MessageBox.Show("Please select an account first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // TODO: Open the "Receive Payment" popup dialog later
            MessageBox.Show($"Open Payment Dialog for {SelectedCustomer.FullName}.\nCurrent Balance: Rs. {SelectedCustomer.CurrentBalance:N2}",
                            "Payment Gateway", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}