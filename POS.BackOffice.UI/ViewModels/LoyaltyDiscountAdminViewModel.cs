using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public class LoyaltyDiscountAdminViewModel : ViewModelBase // Inherits from the base in your screenshot
    {
        private readonly LoyaltyDiscountRepository _repository;

        // ==========================================
        // PROPERTIES: LEFT PANE (PROFILES)
        // ==========================================
        public ObservableCollection<LoyaltyDiscountProfile> Profiles { get; set; } = new();

        private LoyaltyDiscountProfile _currentProfile = new LoyaltyDiscountProfile();
        public LoyaltyDiscountProfile CurrentProfile
        {
            get => _currentProfile;
            set { _currentProfile = value; OnPropertyChanged(nameof(CurrentProfile)); }
        }

        private LoyaltyDiscountProfile? _selectedProfile;
        public LoyaltyDiscountProfile? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                _selectedProfile = value;
                OnPropertyChanged(nameof(SelectedProfile));
                if (value != null)
                {
                    // Copy to CurrentProfile so editing doesn't instantly affect the grid until saved
                    CurrentProfile = new LoyaltyDiscountProfile
                    {
                        Id = value.Id,
                        ProfileName = value.ProfileName,
                        DiscountType = value.DiscountType,
                        DiscountValue = value.DiscountValue,
                        Scope = value.Scope,
                        TargetCode = value.TargetCode,
                        IsActive = value.IsActive
                    };
                }
            }
        }

        // ==========================================
        // PROPERTIES: RIGHT PANE (CUSTOMERS)
        // ==========================================
        public ObservableCollection<CustomerMaster> Customers { get; set; } = new();

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(nameof(SearchText)); }
        }

        private CustomerMaster? _selectedCustomer;
        public CustomerMaster? SelectedCustomer
        {
            get => _selectedCustomer;
            set { _selectedCustomer = value; OnPropertyChanged(nameof(SelectedCustomer)); }
        }

        private LoyaltyDiscountProfile? _assignProfile;
        public LoyaltyDiscountProfile? AssignProfile
        {
            get => _assignProfile;
            set { _assignProfile = value; OnPropertyChanged(nameof(AssignProfile)); }
        }

        private DateTime? _assignExpiryDate;
        public DateTime? AssignExpiryDate
        {
            get => _assignExpiryDate;
            set { _assignExpiryDate = value; OnPropertyChanged(nameof(AssignExpiryDate)); }
        }

        // ==========================================
        // COMMANDS
        // ==========================================
        public ICommand SaveProfileCommand { get; }
        public ICommand ClearProfileCommand { get; }
        public ICommand DeleteProfileCommand { get; }
        public ICommand SearchCustomersCommand { get; }
        public ICommand AssignDiscountCommand { get; }

        public LoyaltyDiscountAdminViewModel(LoyaltyDiscountRepository repository)
        {
            _repository = repository;

            // Command Initialization (Assuming you have a standard RelayCommand in your project)
            // If your project uses a different command name like 'DelegateCommand', change it here.
            SaveProfileCommand = new RelayCommand(async (o) => await ExecuteSaveProfile());
            ClearProfileCommand = new RelayCommand((o) => ExecuteClearProfile());
            DeleteProfileCommand = new RelayCommand(async (o) => await ExecuteDeleteProfile());
            SearchCustomersCommand = new RelayCommand(async (o) => await ExecuteSearchCustomers());
            AssignDiscountCommand = new RelayCommand(async (o) => await ExecuteAssignDiscount());

            // Load Initial Data
            _ = LoadProfilesAsync();
        }

        // ==========================================
        // EXECUTION METHODS
        // ==========================================
        private async Task LoadProfilesAsync()
        {
            var data = await _repository.GetAllActiveProfilesAsync();
            Profiles.Clear();
            foreach (var item in data) Profiles.Add(item);
        }

        private async Task ExecuteSaveProfile()
        {
            if (string.IsNullOrWhiteSpace(CurrentProfile.ProfileName))
            {
                MessageBox.Show("Profile Name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _repository.SaveProfileAsync(CurrentProfile);
                await LoadProfilesAsync();
                ExecuteClearProfile();
                MessageBox.Show("Discount Profile Saved Successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteClearProfile()
        {
            CurrentProfile = new LoyaltyDiscountProfile();
            SelectedProfile = null;
        }

        private async Task ExecuteDeleteProfile()
        {
            if (SelectedProfile == null || SelectedProfile.Id == 0) return;

            var result = MessageBox.Show($"Are you sure you want to delete '{SelectedProfile.ProfileName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                await _repository.DeleteProfileAsync(SelectedProfile.Id);
                await LoadProfilesAsync();
                ExecuteClearProfile();
            }
        }

        private async Task ExecuteSearchCustomers()
        {
            var data = await _repository.SearchRetailCustomersAsync(SearchText);
            Customers.Clear();
            foreach (var item in data) Customers.Add(item);
        }

        private async Task ExecuteAssignDiscount()
        {
            if (SelectedCustomer == null)
            {
                MessageBox.Show("Please select a customer first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int? profileId = AssignProfile?.Id; // Null is valid (removes the discount)

                await _repository.AssignDiscountToCustomerAsync(SelectedCustomer.Id, profileId, AssignExpiryDate);

                MessageBox.Show($"Discount logic updated for {SelectedCustomer.FullName}.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // Refresh the customer grid to show the new discount
                await ExecuteSearchCustomers();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error assigning discount: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}