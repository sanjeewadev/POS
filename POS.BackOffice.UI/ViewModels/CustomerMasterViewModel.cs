using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class CustomerMasterViewModel : ObservableObject
    {
        private readonly CustomerRepository _repository;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        // =========================================================
        // TOP BAR / GRID
        // =========================================================

        public ObservableCollection<CustomerMaster> CustomersList { get; } = new();

        public ObservableCollection<string> TypeFilters { get; } = new()
        {
            "All Customers",
            "Retail",
            "Wholesale",
            "Discount Eligible",
            "Credit Enabled",
            "Inactive"
        };

        public ObservableCollection<string> CustomerTypes { get; } = new()
        {
            "Retail",
            "Wholesale"
        };

        public ObservableCollection<string> CreditStatuses { get; } = new()
        {
            "None",
            "PendingApproval",
            "Active",
            "Hold"
        };

        [ObservableProperty]
        private string _selectedTypeFilter = "All Customers";

        [ObservableProperty]
        private string _searchKeyword = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomerSelected))]
        [NotifyPropertyChangedFor(nameof(SelectedCurrentBalance))]
        [NotifyPropertyChangedFor(nameof(SelectedAvailableCredit))]
        private CustomerMaster? _selectedCustomer;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomerSelected))]
        private bool _isNewCustomerMode = false;

        // Existing XAML uses IsCustomerSelected to enable the form.
        // For new customer mode, the form also needs to be enabled.
        public bool IsCustomerSelected => SelectedCustomer != null || IsNewCustomerMode;

        public decimal SelectedCurrentBalance => SelectedCustomer?.CurrentBalance ?? 0m;

        public decimal SelectedAvailableCredit
        {
            get
            {
                if (SelectedCustomer == null)
                    return Math.Max(0m, InputCreditLimit);

                return Math.Max(0m, InputCreditLimit - SelectedCustomer.CurrentBalance);
            }
        }

        // =========================================================
        // FORM INPUTS
        // =========================================================

        [ObservableProperty]
        private int _currentCustomerId = 0;

        [ObservableProperty]
        private string _inputCustomerCode = "[ AUTO ]";

        [ObservableProperty]
        private string _inputFullName = string.Empty;

        [ObservableProperty]
        private string _inputPhone = string.Empty;

        [ObservableProperty]
        private string _inputEmail = string.Empty;

        [ObservableProperty]
        private string _inputAddress = string.Empty;

        [ObservableProperty]
        private DateTime? _inputBirthday;

        [ObservableProperty]
        private string _inputNicNumber = string.Empty;

        [ObservableProperty]
        private string _inputCompanyName = string.Empty;

        [ObservableProperty]
        private string _inputBusinessRegistrationNumber = string.Empty;

        [ObservableProperty]
        private string _inputVatNumber = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DiscountEligibilityLabel))]
        [NotifyPropertyChangedFor(nameof(CustomerTypeHelpText))]
        private string _inputCustomerType = "Retail";

        [ObservableProperty]
        private bool _inputIsDiscountEligible = false;

        [ObservableProperty]
        private bool _inputIsActive = true;

        [ObservableProperty]
        private bool _inputIsCreditEnabled = false;

        [ObservableProperty]
        private string _inputCreditStatus = "None";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedAvailableCredit))]
        private decimal _inputCreditLimit = 0m;

        [ObservableProperty]
        private int _inputCreditDays = 0;

        [ObservableProperty]
        private bool _inputIsCreditLocked = false;

        public string DiscountEligibilityLabel
        {
            get
            {
                return InputCustomerType.Equals("Wholesale", StringComparison.OrdinalIgnoreCase)
                    ? "Discount Enabled"
                    : "Loyalty Customer";
            }
        }

        public string CustomerTypeHelpText
        {
            get
            {
                return InputCustomerType.Equals("Wholesale", StringComparison.OrdinalIgnoreCase)
                    ? "Wholesale customer uses wholesale pricing. Discount rules are assigned from BackOffice."
                    : "Retail customer uses retail pricing. Loyalty/manual discounts are assigned from BackOffice.";
            }
        }

        // =========================================================
        // TEMPORARY LEGACY COMPATIBILITY
        // =========================================================
        // Keep until the old LoyaltyDiscountProfile page is replaced.
        public ObservableCollection<LoyaltyDiscountProfile> AvailableLoyaltyGroups { get; } = new();

        [ObservableProperty]
        private LoyaltyDiscountProfile? _inputLoyaltyGroup;

        public CustomerMasterViewModel(
            CustomerRepository repository,
            IDbContextFactory<AppDbContext> contextFactory)
        {
            _repository = repository;
            _contextFactory = contextFactory;

            _ = InitializeAsync();
        }

        // =========================================================
        // INITIAL LOAD
        // =========================================================

        private async Task InitializeAsync()
        {
            await LoadLegacyDiscountProfilesAsync();
            await RefreshDataAsync();
            AddNewCustomer();
        }

        private async Task LoadLegacyDiscountProfilesAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var profiles = await context.LoyaltyDiscountProfiles
                    .AsNoTracking()
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.ProfileName)
                    .ToListAsync();

                AvailableLoyaltyGroups.Clear();

                foreach (var profile in profiles)
                    AvailableLoyaltyGroups.Add(profile);
            }
            catch
            {
                // Ignore during transition.
                // The new discount-rule engine will replace this later.
            }
        }

        // =========================================================
        // PROPERTY TRIGGERS
        // =========================================================

        partial void OnSelectedCustomerChanged(CustomerMaster? value)
        {
            if (value == null)
                return;

            IsNewCustomerMode = false;
            PopulateFormFromSelected(value);
        }

        partial void OnSelectedTypeFilterChanged(string value)
        {
            _ = RefreshDataAsync();
        }

        partial void OnInputCustomerTypeChanged(string value)
        {
            OnPropertyChanged(nameof(DiscountEligibilityLabel));
            OnPropertyChanged(nameof(CustomerTypeHelpText));
        }

        partial void OnInputIsCreditEnabledChanged(bool value)
        {
            if (!value)
            {
                InputCreditStatus = "None";
                InputCreditLimit = 0m;
                InputCreditDays = 0;
                InputIsCreditLocked = false;
            }
            else
            {
                if (InputCreditStatus == "None")
                    InputCreditStatus = "Active";
            }
        }

        // =========================================================
        // COMMANDS
        // =========================================================

        [RelayCommand]
        private async Task RefreshDataAsync()
        {
            try
            {
                var data = await _repository.GetFilteredCustomersAsync(
                    SelectedTypeFilter,
                    SearchKeyword);

                CustomersList.Clear();

                foreach (var customer in data)
                    CustomersList.Add(customer);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load customers: {ex.Message}",
                    "Customer Master",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void AddNewCustomer()
        {
            SelectedCustomer = null;
            IsNewCustomerMode = true;

            CurrentCustomerId = 0;
            InputCustomerCode = "[ AUTO ]";

            InputFullName = string.Empty;
            InputPhone = string.Empty;
            InputEmail = string.Empty;
            InputAddress = string.Empty;
            InputBirthday = null;
            InputNicNumber = string.Empty;

            InputCompanyName = string.Empty;
            InputBusinessRegistrationNumber = string.Empty;
            InputVatNumber = string.Empty;

            InputCustomerType = "Retail";
            InputIsDiscountEligible = false;
            InputIsActive = true;

            InputIsCreditEnabled = false;
            InputCreditStatus = "None";
            InputCreditLimit = 0m;
            InputCreditDays = 0;
            InputIsCreditLocked = false;

            InputLoyaltyGroup = null;

            OnPropertyChanged(nameof(IsCustomerSelected));
            OnPropertyChanged(nameof(SelectedCurrentBalance));
            OnPropertyChanged(nameof(SelectedAvailableCredit));
            OnPropertyChanged(nameof(DiscountEligibilityLabel));
            OnPropertyChanged(nameof(CustomerTypeHelpText));
        }

        [RelayCommand]
        private async Task SaveCustomerAsync()
        {
            if (string.IsNullOrWhiteSpace(InputFullName))
            {
                MessageBox.Show(
                    "Customer name is required.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(InputPhone))
            {
                MessageBox.Show(
                    "Phone number is required.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!InputCustomerType.Equals("Retail", StringComparison.OrdinalIgnoreCase) &&
                !InputCustomerType.Equals("Wholesale", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "Customer type must be Retail or Wholesale.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (InputCreditLimit < 0m)
            {
                MessageBox.Show(
                    "Credit limit cannot be negative.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (InputCreditDays < 0)
            {
                MessageBox.Show(
                    "Credit days cannot be negative.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                var customerToSave = BuildCustomerFromInputs();

                var saved = await _repository.SaveCustomerProfileAsync(customerToSave);

                MessageBox.Show(
                    "Customer profile saved successfully.",
                    "Customer Master",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await RefreshDataAsync();

                var selectedAgain = CustomersList.FirstOrDefault(c => c.Id == saved.Id);

                if (selectedAgain != null)
                    SelectedCustomer = selectedAgain;
                else
                    AddNewCustomer();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to save customer: {ex.Message}",
                    "Customer Master",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ClearForm()
        {
            AddNewCustomer();
        }

        // =========================================================
        // INTERNAL LOGIC
        // =========================================================

        private void PopulateFormFromSelected(CustomerMaster c)
        {
            CurrentCustomerId = c.Id;
            InputCustomerCode = c.CustomerCode;

            InputFullName = c.FullName;
            InputPhone = c.Phone;
            InputEmail = c.Email ?? string.Empty;
            InputAddress = c.Address ?? string.Empty;
            InputBirthday = c.Birthday;
            InputNicNumber = c.NicNumber ?? string.Empty;

            InputCompanyName = c.CompanyName ?? string.Empty;
            InputBusinessRegistrationNumber = c.BusinessRegistrationNumber ?? string.Empty;
            InputVatNumber = c.VatRegistrationNumber ?? string.Empty;

            InputCustomerType = string.IsNullOrWhiteSpace(c.CustomerType)
                ? "Retail"
                : c.CustomerType;

            InputIsDiscountEligible = c.IsDiscountEligible;
            InputIsActive = c.IsActive;

            InputIsCreditEnabled = c.IsCreditEnabled;
            InputCreditStatus = string.IsNullOrWhiteSpace(c.CreditStatus)
                ? "None"
                : c.CreditStatus;

            InputCreditLimit = c.CreditLimit;
            InputCreditDays = c.CreditDays;
            InputIsCreditLocked = c.IsCreditLocked;

            if (c.LoyaltyDiscountProfileId.HasValue)
            {
                InputLoyaltyGroup = AvailableLoyaltyGroups
                    .FirstOrDefault(p => p.Id == c.LoyaltyDiscountProfileId.Value);
            }
            else
            {
                InputLoyaltyGroup = null;
            }

            OnPropertyChanged(nameof(IsCustomerSelected));
            OnPropertyChanged(nameof(SelectedCurrentBalance));
            OnPropertyChanged(nameof(SelectedAvailableCredit));
            OnPropertyChanged(nameof(DiscountEligibilityLabel));
            OnPropertyChanged(nameof(CustomerTypeHelpText));
        }

        private CustomerMaster BuildCustomerFromInputs()
        {
            bool creditEnabled = InputIsCreditEnabled;

            string creditStatus = creditEnabled
                ? NormalizeCreditStatus(InputCreditStatus)
                : "None";

            if (creditEnabled && creditStatus == "None")
                creditStatus = "Active";

            return new CustomerMaster
            {
                Id = CurrentCustomerId,

                CustomerCode = CurrentCustomerId == 0
                    ? string.Empty
                    : InputCustomerCode,

                FullName = InputFullName.Trim(),
                Phone = NormalizePhone(InputPhone),
                Email = (InputEmail ?? string.Empty).Trim(),
                Address = (InputAddress ?? string.Empty).Trim(),
                Birthday = InputBirthday,
                NicNumber = (InputNicNumber ?? string.Empty).Trim(),

                CompanyName = (InputCompanyName ?? string.Empty).Trim(),
                BusinessRegistrationNumber = (InputBusinessRegistrationNumber ?? string.Empty).Trim(),
                VatRegistrationNumber = (InputVatNumber ?? string.Empty).Trim(),

                CustomerType = InputCustomerType.Equals("Wholesale", StringComparison.OrdinalIgnoreCase)
                    ? "Wholesale"
                    : "Retail",

                IsDiscountEligible = InputIsDiscountEligible,
                IsActive = InputIsActive,

                IsCreditEnabled = creditEnabled,
                CreditStatus = creditStatus,
                CreditLimit = creditEnabled ? Math.Max(0m, InputCreditLimit) : 0m,
                CreditDays = creditEnabled ? Math.Max(0, InputCreditDays) : 0,
                IsCreditLocked = creditEnabled && InputIsCreditLocked,

                UpdatedAt = DateTime.Now,

                // Temporary old profile bridge.
                LoyaltyDiscountProfileId = InputLoyaltyGroup?.Id
            };
        }

        private static string NormalizeCreditStatus(string? value)
        {
            string status = (value ?? "None").Trim();

            if (status.Equals("PendingApproval", StringComparison.OrdinalIgnoreCase))
                return "PendingApproval";

            if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                return "Active";

            if (status.Equals("Hold", StringComparison.OrdinalIgnoreCase))
                return "Hold";

            return "None";
        }

        private static string NormalizePhone(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim()
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty);
        }
    }
}