using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Enums;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Utilities;
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
        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _selectedFilterRole = "All Roles";

        // --- FORM FIELDS ---
        [ObservableProperty]
        private string _firstName = string.Empty;

        [ObservableProperty]
        private string _lastName = string.Empty;

        [ObservableProperty]
        private string _employeeId = string.Empty;

        [ObservableProperty]
        private string _mobile = string.Empty;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private UserRole _selectedRole = UserRole.Cashier;

        [ObservableProperty]
        private string _statusText = "Active";

        public ObservableCollection<string> FilterRoles { get; } =
            new() { "All Roles", "Admin", "Manager", "Cashier" };

        public ObservableCollection<UserRole> AvailableRoles { get; } =
            new() { UserRole.Admin, UserRole.Manager, UserRole.Cashier };

        public ObservableCollection<string> AccountStatuses { get; } =
            new() { "Active", "Suspended" };

        public ObservableCollection<User> Users { get; } = new();

        [ObservableProperty]
        private User? _selectedUser;

        public UserManagementViewModel(
            UserRepository userRepository,
            AuthService authService)
        {
            _userRepository = userRepository;
            _authService = authService;

            _ = LoadUsersAsync();
        }

        partial void OnSearchTextChanged(string value)
        {
            _ = LoadUsersAsync();
        }

        partial void OnSelectedFilterRoleChanged(string value)
        {
            _ = LoadUsersAsync();
        }

        partial void OnSelectedUserChanged(User? value)
        {
            if (value == null)
            {
                return;
            }

            FirstName = value.FirstName;
            LastName = value.LastName;
            EmployeeId = value.EmployeeId;
            Mobile = value.Mobile;
            Username = value.Username;
            SelectedRole = value.Role;
            StatusText = value.IsActive ? "Active" : "Suspended";
        }

        private async Task LoadUsersAsync()
        {
            Users.Clear();

            var data = await _userRepository.GetAllAsync(
                SearchText,
                SelectedFilterRole);

            foreach (var user in data)
            {
                Users.Add(user);
            }
        }

        public async Task ExecuteSaveAsync(string plainTextPassword)
        {
            if (!_authService.IsAdmin)
            {
                MessageBox.Show(
                    "Only Administrators can create or modify system users.",
                    "Access Denied",
                    MessageBoxButton.OK,
                    MessageBoxImage.Hand);

                return;
            }

            string firstName = FirstName.Trim();
            string lastName = LastName.Trim();
            string username = Username.Trim();

            if (string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(lastName) ||
                string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show(
                    "First Name, Last Name, and Username are mandatory.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (firstName.Length > 50 ||
                lastName.Length > 50 ||
                username.Length > 50)
            {
                MessageBox.Show(
                    "First Name, Last Name, and Username cannot exceed 50 characters.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (username.Length < 4)
            {
                MessageBox.Show(
                    "Username must contain at least 4 characters.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (IsReservedUsername(username))
            {
                MessageBox.Show(
                    "That username is reserved. Choose another username.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            bool isNewUser = SelectedUser == null;

            if (isNewUser && string.IsNullOrWhiteSpace(plainTextPassword))
            {
                MessageBox.Show(
                    "A password is required when creating a new user.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (!string.IsNullOrWhiteSpace(plainTextPassword))
            {
                var passwordValidation =
                    PasswordPolicy.Validate(plainTextPassword, username);

                if (!passwordValidation.IsValid)
                {
                    MessageBox.Show(
                        passwordValidation.ErrorMessage,
                        "Password Validation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }
            }

            bool isUnique = await _userRepository.IsUsernameUniqueAsync(
                username,
                SelectedUser?.Id ?? 0);

            if (!isUnique)
            {
                MessageBox.Show(
                    "This username is already taken.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            try
            {
                var user = SelectedUser ?? new User();

                user.FirstName = firstName;
                user.LastName = lastName;
                user.EmployeeId = EmployeeId.Trim();
                user.Mobile = Mobile.Trim();
                user.Username = username;
                user.Role = SelectedRole;
                user.IsActive = StatusText == "Active";

                if (!string.IsNullOrWhiteSpace(plainTextPassword))
                {
                    user.PasswordHash = SecurityHelper.HashData(
                        plainTextPassword,
                        out string passwordSalt);

                    user.PasswordSalt = passwordSalt;
                }

                if (isNewUser)
                {
                    await _userRepository.AddAsync(user);
                }
                else
                {
                    await _userRepository.UpdateAsync(user);
                }

                MessageBox.Show(
                    $"User {user.Username} was successfully saved.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                ClearForm();
                await LoadUsersAsync();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Administrator Protection",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Database Error: {ex.Message}",
                    "System Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ClearForm()
        {
            SelectedUser = null;
            FirstName = string.Empty;
            LastName = string.Empty;
            EmployeeId = string.Empty;
            Mobile = string.Empty;
            Username = string.Empty;
            SelectedRole = UserRole.Cashier;
            StatusText = "Active";
        }

        private static bool IsReservedUsername(string username)
        {
            return username.Equals("sa", StringComparison.OrdinalIgnoreCase) ||
                   username.Equals("superadmin", StringComparison.OrdinalIgnoreCase) ||
                   username.Equals("system", StringComparison.OrdinalIgnoreCase);
        }
    }
}
