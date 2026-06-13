using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Services;
using POS.Core.Enums;

namespace POS.Cashier.UI.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly AuthService _authService;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isProcessing = false;

        // The "Shout" that App.xaml.cs listens to!
        public event Action<UserRole>? LoginSuccessful;

        public LoginViewModel(AuthService authService)
        {
            _authService = authService;
        }

        // CRITICAL FIX: The method now accepts the object parameter sent from XAML
        [RelayCommand]
        private async Task LoginAsync(object parameter)
        {
            // Safely cast the parameter back into the PasswordBox UI element
            if (parameter is not PasswordBox passwordBox || string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(passwordBox.Password))
            {
                ErrorMessage = "Please enter both a username and a password.";
                return;
            }

            IsProcessing = true;
            ErrorMessage = string.Empty;

            try
            {
                // 1. Call the secure Database & PBKDF2 Hashing Engine using the direct PasswordBox value
                var (success, message) = await _authService.LoginAsync(Username, passwordBox.Password);

                if (success && _authService.CurrentUser != null)
                {
                    // Wipe the memory
                    passwordBox.Clear();

                    // 2. Map the database string Role to your UserRole Enum (Using the new simplified roles)
                    UserRole role = MapRoleToEnum(_authService.CurrentUser.Role);

                    // 3. Shout to App.xaml.cs to open the register!
                    LoginSuccessful?.Invoke(role);
                }
                else
                {
                    // Access Denied
                    ErrorMessage = message;
                    passwordBox.Clear();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "A critical system error occurred during login.";
                MessageBox.Show(ex.Message, "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// Safely translates the new simplified database string roles into the Enum.
        /// </summary>
        private UserRole MapRoleToEnum(string dbRole)
        {
            return dbRole switch
            {
                "Super Admin" => UserRole.Admin, // Skeleton key
                "Admin" => UserRole.Admin,       // Standard Admin
                "Cashier" => UserRole.Cashier,   // Standard Cashier
                _ => UserRole.Cashier            // Default fallback
            };
        }
    }
}