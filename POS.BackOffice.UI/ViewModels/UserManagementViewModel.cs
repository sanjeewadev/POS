using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Utilities;
using POS.Core.Enums;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class UserManagementViewModel : ViewModelBase
    {
        private readonly UserRepository _userRepository;
        private readonly AuthService _authService;

        // --- DIRECTORY FILTERS ---
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedFilterRole = "All Roles";

        // --- FORM FIELDS ---
        [ObservableProperty] private string _firstName = string.Empty;
        [ObservableProperty] private string _lastName = string.Empty;
        [ObservableProperty] private string _employeeId = string.Empty;
        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private string _mobile = string.Empty;
        [ObservableProperty] private string _username = string.Empty;

        // Default role is now Cashier (Using Enum)
        [ObservableProperty] private UserRole _selectedRole = UserRole.Cashier;
        [ObservableProperty] private string _statusText = "Active";

        // --- COLLECTIONS ---
        // Includes the new "Manager" role
        public ObservableCollection<string> FilterRoles { get; set; } = new() { "All Roles", "Admin", "Manager", "Cashier" };
        public ObservableCollection<UserRole> AvailableRoles { get; set; } = new() { UserRole.Admin, UserRole.Manager, UserRole.Cashier };

        public ObservableCollection<string> AccountStatuses { get; set; } = new() { "Active", "Suspended" };
        public ObservableCollection<User> Users { get; set; } = new();

        [ObservableProperty] private User? _selectedUser;

        public UserManagementViewModel(UserRepository userRepository, AuthService authService)
        {
            _userRepository = userRepository;
            _authService = authService;
            _ = LoadUsersAsync();
        }

        // --- AUTO-TRIGGERS ---

        partial void OnSearchTextChanged(string value) => _ = LoadUsersAsync();
        partial void OnSelectedFilterRoleChanged(string value) => _ = LoadUsersAsync();

        partial void OnSelectedUserChanged(User? value)
        {
            if (value != null)
            {
                FirstName = value.FirstName;
                LastName = value.LastName;
                EmployeeId = value.EmployeeId;
                Email = value.Email;
                Mobile = value.Mobile;
                Username = value.Username;
                SelectedRole = value.Role;
                StatusText = value.IsActive ? "Active" : "Suspended";
            }
        }

        // --- ACTIONS ---

        private async Task LoadUsersAsync()
        {
            Users.Clear();
            var data = await _userRepository.GetAllAsync(SearchText, SelectedFilterRole);
            foreach (var user in data)
            {
                Users.Add(user);
            }
        }

        public async Task ExecuteSaveAsync(string plainTextPassword, string plainTextPin)
        {
            // 1. RBAC Security Check (Checks for Admin OR the Skeleton Key Super Admin)
            if (!_authService.IsAdmin)
            {
                MessageBox.Show("Only Administrators can create or modify system users.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Hand);
                return;
            }

            // 2. Validation
            if (string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(Username))
            {
                MessageBox.Show("First Name and Username are mandatory.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool isNewUser = SelectedUser == null;

            if (isNewUser && string.IsNullOrWhiteSpace(plainTextPassword))
            {
                MessageBox.Show("A password is required when creating a new user.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check Username uniqueness
            bool isUnique = await _userRepository.IsUsernameUniqueAsync(Username, SelectedUser?.Id ?? 0);
            if (!isUnique)
            {
                MessageBox.Show("This username is already taken.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var user = SelectedUser ?? new User();

                user.FirstName = FirstName.Trim();
                user.LastName = LastName.Trim();
                user.EmployeeId = EmployeeId.Trim();
                user.Email = Email.Trim();
                user.Mobile = Mobile.Trim();
                user.Username = Username.Trim();
                user.Role = SelectedRole;
                user.IsActive = StatusText == "Active";

                // 3. Cryptographic Hashing
                if (!string.IsNullOrWhiteSpace(plainTextPassword))
                {
                    user.PasswordHash = SecurityHelper.HashData(plainTextPassword, out string salt);
                    user.PasswordSalt = salt;
                }

                if (!string.IsNullOrWhiteSpace(plainTextPin))
                {
                    user.PosPinHash = SecurityHelper.HashData(plainTextPin, out string pinSalt);
                    user.PosPinSalt = pinSalt;
                }

                // 4. Save to Database
                if (isNewUser)
                {
                    await _userRepository.AddAsync(user);
                }
                else
                {
                    await _userRepository.UpdateAsync(user);
                }

                MessageBox.Show($"User {user.Username} successfully saved.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                await LoadUsersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Error: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ClearForm()
        {
            SelectedUser = null;
            FirstName = string.Empty;
            LastName = string.Empty;
            EmployeeId = string.Empty;
            Email = string.Empty;
            Mobile = string.Empty;
            Username = string.Empty;
            SelectedRole = UserRole.Cashier;
            StatusText = "Active";
        }
    }
}