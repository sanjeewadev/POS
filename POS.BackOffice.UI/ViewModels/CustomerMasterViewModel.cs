using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class CustomerMasterViewModel : ObservableObject
    {
        private readonly CustomerRepository _repository;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        // ==========================================
        // TOP BAR CONTROLS
        // ==========================================
        public ObservableCollection<CustomerMaster> CustomersList { get; set; } = new();

        [ObservableProperty] private string _selectedTypeFilter = "All Customers";
        [ObservableProperty] private string _searchKeyword = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomerSelected))]
        private CustomerMaster? _selectedCustomer;

        public bool IsCustomerSelected => SelectedCustomer != null;

        // ==========================================
        // FORM INPUT PROPERTIES
        // ==========================================
        [ObservableProperty] private int _currentCustomerId = 0;
        [ObservableProperty] private string _inputCustomerCode = "[ AUTO ]";
        [ObservableProperty] private string _inputFullName = string.Empty;
        [ObservableProperty] private string _inputPhone = string.Empty;
        [ObservableProperty] private string _inputEmail = string.Empty;
        [ObservableProperty] private string _inputAddress = string.Empty;
        [ObservableProperty] private string _inputCompanyName = string.Empty;
        [ObservableProperty] private string _inputVatNumber = string.Empty;
        [ObservableProperty] private string _inputCustomerType = "Retail";
        [ObservableProperty] private bool _inputIsActive = true;
        [ObservableProperty] private decimal _inputCreditLimit = 0m;
        [ObservableProperty] private int _inputCreditDays = 0;
        [ObservableProperty] private bool _inputIsCreditLocked = false;

        // --- NEW: THE DISCOUNT & LOYALTY BRIDGE ---
        public ObservableCollection<LoyaltyDiscountProfile> AvailableLoyaltyGroups { get; set; } = new();
        [ObservableProperty] private LoyaltyDiscountProfile? _inputLoyaltyGroup;

        public CustomerMasterViewModel(CustomerRepository repository, IDbContextFactory<AppDbContext> contextFactory)
        {
            _repository = repository;
            _contextFactory = contextFactory;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            // 1. Load the available Discount/Loyalty Profiles for the dropdown
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var profiles = await context.LoyaltyDiscountProfiles.AsNoTracking().ToListAsync();
                foreach (var p in profiles) AvailableLoyaltyGroups.Add(p);
            }
            catch
            {
                // Silently bypass if the LoyaltyDiscountProfiles table is empty or missing during initial setup
            }

            // 2. Load the initial customer grid
            await LoadDataAsync();
        }

        // ==========================================
        // SMART TRIGGERS
        // ==========================================
        partial void OnSelectedCustomerChanged(CustomerMaster? value)
        {
            if (value != null) PopulateFormFromSelected(value);
        }

        // ==========================================
        // LOGIC METHODS
        // ==========================================
        [RelayCommand]
        private async Task LoadDataAsync()
        {
            var data = await _repository.GetFilteredCustomersAsync(SelectedTypeFilter, SearchKeyword);
            CustomersList.Clear();
            foreach (var item in data) CustomersList.Add(item);
        }

        [RelayCommand]
        private void PrepareNewCustomer()
        {
            SelectedCustomer = null;
            CurrentCustomerId = 0;
            InputCustomerCode = "[ AUTO ]";
            InputFullName = string.Empty;
            InputPhone = string.Empty;
            InputEmail = string.Empty;
            InputAddress = string.Empty;
            InputCompanyName = string.Empty;
            InputVatNumber = string.Empty;
            InputCustomerType = "Retail";
            InputIsActive = true;
            InputCreditLimit = 0m;
            InputCreditDays = 0;
            InputIsCreditLocked = false;
            InputLoyaltyGroup = null; // Clear the dropdown

            // Force the UI to unlock the form grids
            OnPropertyChanged(nameof(IsCustomerSelected));
        }

        private void PopulateFormFromSelected(CustomerMaster c)
        {
            CurrentCustomerId = c.Id;
            InputCustomerCode = c.CustomerCode;
            InputFullName = c.FullName;
            InputPhone = c.Phone;
            InputEmail = c.Email ?? string.Empty;
            InputAddress = c.Address ?? string.Empty;
            InputCompanyName = c.CompanyName ?? string.Empty;
            InputVatNumber = c.VatRegistrationNumber ?? string.Empty;
            InputCustomerType = c.CustomerType;
            InputIsActive = c.IsActive;
            InputCreditLimit = c.CreditLimit;
            InputCreditDays = c.CreditDays;
            InputIsCreditLocked = c.IsCreditLocked;

            // Re-select the correct Discount Profile in the dropdown
            if (c.LoyaltyDiscountProfileId.HasValue)
            {
                InputLoyaltyGroup = AvailableLoyaltyGroups.FirstOrDefault(p => p.Id == c.LoyaltyDiscountProfileId.Value);
            }
            else
            {
                InputLoyaltyGroup = null;
            }
        }

        [RelayCommand]
        private async Task SaveCustomerAsync()
        {
            if (string.IsNullOrWhiteSpace(InputFullName) || string.IsNullOrWhiteSpace(InputPhone))
            {
                MessageBox.Show("Full Name and Phone Number are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var customerToSave = new CustomerMaster
                {
                    Id = CurrentCustomerId,
                    CustomerCode = CurrentCustomerId == 0 ? string.Empty : InputCustomerCode,
                    FullName = InputFullName.Trim(),
                    Phone = InputPhone.Trim(),
                    Email = InputEmail.Trim(),
                    Address = InputAddress.Trim(),
                    CompanyName = InputCompanyName.Trim(),
                    VatRegistrationNumber = InputVatNumber.Trim(),
                    CustomerType = InputCustomerType,
                    IsActive = InputIsActive,
                    CreditLimit = InputCreditLimit,
                    CreditDays = InputCreditDays,
                    IsCreditLocked = InputIsCreditLocked,

                    // NEW: Pass the selected ID to the repository!
                    LoyaltyDiscountProfileId = InputLoyaltyGroup?.Id
                };

                await _repository.SaveCustomerProfileAsync(customerToSave);

                MessageBox.Show("Customer profile saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save customer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}