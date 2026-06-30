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

        // =========================================================
        // SEARCH MODE
        // =========================================================
        // All      = normal Customer button
        // Loyalty  = Loyalty button shortcut: Retail + IsDiscountEligible
        // Wholesale = Wholesale/B2B shortcut
        // Credit   = credit-capable customers
        [ObservableProperty]
        private string _lookupMode = "All";

        [ObservableProperty]
        private string _dialogTitle = "CUSTOMER LOOKUP";

        [ObservableProperty]
        private string _directoryTitle = "Customer Directory";

        [ObservableProperty]
        private string _searchLabel = "Search (Code/Name/Phone/NIC/BR/VAT):";

        [ObservableProperty]
        private string _attachButtonText = "ATTACH TO INVOICE";

        [ObservableProperty]
        private string _searchTerm = string.Empty;

        public ObservableCollection<CustomerSearchDto> Customers { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomerSelected))]
        private CustomerSearchDto? _selectedCustomer;

        public bool IsCustomerSelected => SelectedCustomer != null;

        // =========================================================
        // SELECTED CUSTOMER DISPLAY
        // =========================================================

        [ObservableProperty]
        private string _displayCompanyName = "CUSTOMER NAME";

        [ObservableProperty]
        private string _displayContactName = string.Empty;

        [ObservableProperty]
        private string _displayCustomerType = string.Empty;

        [ObservableProperty]
        private string _displayDiscountStatus = string.Empty;

        [ObservableProperty]
        private string _displayCreditStatus = string.Empty;

        [ObservableProperty]
        private decimal _creditLimit = 0m;

        [ObservableProperty]
        private decimal _currentDebt = 0m;

        [ObservableProperty]
        private decimal _availableCredit = 0m;

        [ObservableProperty]
        private bool _isAccountLocked = false;

        [ObservableProperty]
        private string _lockReasonText = string.Empty;

        [ObservableProperty]
        private bool _canAttachToInvoice = false;

        public event Action<bool>? ActionCompleted;

        public B2BCustomerViewModel(CustomerRepository repository)
        {
            _repository = repository;

            ConfigureForAllCustomers();
            _ = SearchAsync();
        }

        // =========================================================
        // MODE CONFIGURATION
        // =========================================================

        public void ConfigureForAllCustomers()
        {
            LookupMode = "All";
            DialogTitle = "CUSTOMER LOOKUP";
            DirectoryTitle = "Customer Directory";
            SearchLabel = "Search (Code/Name/Phone/NIC/BR/VAT):";
            AttachButtonText = "ATTACH CUSTOMER";
        }

        public void ConfigureForRetailLoyaltyCustomers()
        {
            LookupMode = "Loyalty";
            DialogTitle = "LOYALTY CUSTOMER LOOKUP";
            DirectoryTitle = "Retail Loyalty Customers";
            SearchLabel = "Search Loyalty Customer (Name/Phone/Code/NIC):";
            AttachButtonText = "ATTACH LOYALTY CUSTOMER";
        }

        public void ConfigureForWholesaleCustomers()
        {
            LookupMode = "Wholesale";
            DialogTitle = "WHOLESALE CUSTOMER LOOKUP";
            DirectoryTitle = "Wholesale Customer Directory";
            SearchLabel = "Search Wholesale Customer (Company/Phone/BR/VAT):";
            AttachButtonText = "ATTACH WHOLESALE CUSTOMER";
        }

        public void ConfigureForCreditCustomers()
        {
            LookupMode = "Credit";
            DialogTitle = "CREDIT CUSTOMER LOOKUP";
            DirectoryTitle = "Credit Customer Directory";
            SearchLabel = "Search Credit Customer (Name/Phone/Company/Code):";
            AttachButtonText = "ATTACH CREDIT CUSTOMER";
        }

        public async Task ReloadAsync()
        {
            await SearchAsync();
        }

        // =========================================================
        // TRIGGERS
        // =========================================================

        partial void OnSelectedCustomerChanged(CustomerSearchDto? value)
        {
            if (value != null)
                UpdateCustomerDashboard(value);
            else
                ResetCustomerDashboard();
        }

        partial void OnLookupModeChanged(string value)
        {
            ResetCustomerDashboard();
        }

        // =========================================================
        // COMMANDS
        // =========================================================

        [RelayCommand]
        private async Task SearchAsync()
        {
            try
            {
                var results = LookupMode switch
                {
                    "Loyalty" => await _repository.SearchRetailLoyaltyCustomersAsync(SearchTerm),
                    "Wholesale" => await _repository.SearchWholesaleCustomersAsync(SearchTerm),
                    "Credit" => await _repository.SearchB2BCustomersAsync(SearchTerm),
                    _ => await _repository.SearchActiveCustomersAsync(SearchTerm)
                };

                Customers.Clear();

                foreach (var customer in results)
                    Customers.Add(customer);

                SelectedCustomer = null;
            }
            catch
            {
                Customers.Clear();
                SelectedCustomer = null;

                DisplayCompanyName = "SEARCH FAILED";
                DisplayContactName = "Unable to load customers.";
                DisplayCustomerType = string.Empty;
                DisplayDiscountStatus = string.Empty;
                DisplayCreditStatus = string.Empty;
                CanAttachToInvoice = false;
                IsAccountLocked = true;
                LockReasonText = "Customer search failed. Check database/schema.";
            }
        }

        [RelayCommand]
        private void Attach()
        {
            if (SelectedCustomer == null)
                return;

            if (!CanAttachToInvoice)
                return;

            ActionCompleted?.Invoke(true);
        }

        [RelayCommand]
        private void Cancel()
        {
            ActionCompleted?.Invoke(false);
        }

        // =========================================================
        // INTERNAL LOGIC
        // =========================================================

        private void UpdateCustomerDashboard(CustomerSearchDto customer)
        {
            DisplayCompanyName = customer.DisplayName;
            DisplayContactName = customer.DisplaySubName;
            DisplayCustomerType = customer.DisplayCustomerType;
            DisplayDiscountStatus = customer.DiscountLabel;
            DisplayCreditStatus = customer.CreditStatusText;

            CreditLimit = customer.CreditLimit;
            CurrentDebt = customer.CurrentBalance;
            AvailableCredit = customer.RemainingCredit;

            // Attach rules:
            // - For normal customer attach, inactive customers blocked.
            // - Credit problems should not block normal cash/card sales.
            // - Credit problems should block only Credit mode.
            if (!customer.IsActive)
            {
                IsAccountLocked = true;
                LockReasonText = "CUSTOMER ACCOUNT IS INACTIVE";
                CanAttachToInvoice = false;
                return;
            }

            if (LookupMode == "Credit")
            {
                if (!customer.CanUseCredit)
                {
                    IsAccountLocked = true;
                    LockReasonText = customer.CreditWarningText;
                    CanAttachToInvoice = false;
                    return;
                }
            }

            IsAccountLocked = false;
            LockReasonText = string.Empty;
            CanAttachToInvoice = true;
        }

        private void ResetCustomerDashboard()
        {
            DisplayCompanyName = "CUSTOMER NAME";
            DisplayContactName = string.Empty;
            DisplayCustomerType = string.Empty;
            DisplayDiscountStatus = string.Empty;
            DisplayCreditStatus = string.Empty;

            CreditLimit = 0m;
            CurrentDebt = 0m;
            AvailableCredit = 0m;

            IsAccountLocked = false;
            LockReasonText = string.Empty;
            CanAttachToInvoice = false;
        }
    }
}