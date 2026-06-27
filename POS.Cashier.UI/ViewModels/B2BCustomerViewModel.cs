using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;

namespace POS.Cashier.UI.ViewModels
{
    public partial class B2BCustomerViewModel : ObservableObject
    {
        private readonly CustomerRepository _repository;

        // ==========================================
        // SEARCH ENGINE
        // ==========================================
        [ObservableProperty] private string _searchTerm = string.Empty;

        public ObservableCollection<CustomerSearchDto> Customers { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomerSelected))]
        private CustomerSearchDto? _selectedCustomer;

        public bool IsCustomerSelected => SelectedCustomer != null;

        // ==========================================
        // FINANCIAL & SECURITY DISPLAY
        // ==========================================
        [ObservableProperty] private string _displayCompanyName = "COMPANY NAME";
        [ObservableProperty] private decimal _creditLimit = 0m;
        [ObservableProperty] private decimal _currentDebt = 0m;
        [ObservableProperty] private decimal _availableCredit = 0m;

        [ObservableProperty] private bool _isAccountLocked = false;
        [ObservableProperty] private string _lockReasonText = string.Empty;

        // Controls whether the "Attach" button is physically enabled
        [ObservableProperty] private bool _canAttachToInvoice = false;

        // Lets the Window know when to close itself
        public event Action<bool>? ActionCompleted;

        public B2BCustomerViewModel(CustomerRepository repository)
        {
            _repository = repository;
            _ = SearchAsync(); // Auto-load the grid when the dialog opens
        }

        // ==========================================
        // TRIGGERS
        // ==========================================
        partial void OnSelectedCustomerChanged(CustomerSearchDto? value)
        {
            if (value != null)
            {
                UpdateFinancialDashboard(value);
            }
            else
            {
                ResetFinancialDashboard();
            }
        }

        // ==========================================
        // COMMANDS
        // ==========================================
        [RelayCommand]
        private async Task SearchAsync()
        {
            var results = await _repository.SearchB2BCustomersAsync(SearchTerm);
            Customers.Clear();
            foreach (var c in results) Customers.Add(c);

            SelectedCustomer = null;
        }

        [RelayCommand]
        private void Attach()
        {
            if (SelectedCustomer != null && CanAttachToInvoice)
            {
                ActionCompleted?.Invoke(true);
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            ActionCompleted?.Invoke(false);
        }

        // ==========================================
        // INTERNAL LOGIC
        // ==========================================
        private void UpdateFinancialDashboard(CustomerSearchDto customer)
        {
            DisplayCompanyName = string.IsNullOrWhiteSpace(customer.CompanyName) ? customer.FullName : customer.CompanyName;
            CreditLimit = customer.CreditLimit;
            CurrentDebt = customer.CurrentBalance;
            AvailableCredit = customer.RemainingCredit;

            // Strict Security Evaluation
            if (customer.IsCreditLocked)
            {
                IsAccountLocked = true;
                LockReasonText = "❌ ACCOUNT LOCKED BY MANAGEMENT";
                CanAttachToInvoice = false; // Block the sale
            }
            else if (AvailableCredit <= 0 && customer.CreditLimit > 0)
            {
                IsAccountLocked = true;
                LockReasonText = "❌ CREDIT LIMIT EXCEEDED";
                CanAttachToInvoice = false; // Block the sale
            }
            else
            {
                IsAccountLocked = false;
                LockReasonText = string.Empty;
                CanAttachToInvoice = true; // Safe to proceed
            }
        }

        private void ResetFinancialDashboard()
        {
            DisplayCompanyName = "COMPANY NAME";
            CreditLimit = 0m;
            CurrentDebt = 0m;
            AvailableCredit = 0m;
            IsAccountLocked = false;
            CanAttachToInvoice = false;
        }
    }
}