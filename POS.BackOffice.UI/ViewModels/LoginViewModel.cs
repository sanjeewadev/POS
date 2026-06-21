using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Services;
using POS.Core.Enums;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class LoginViewModel : ViewModelBase
    {
        private readonly AuthService _authService;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isProcessing = false;

        public event Action<UserRole>? LoginSuccessful;

        public LoginViewModel(AuthService authService)
        {
            _authService = authService;
        }

        [RelayCommand]
        private async Task LoginAsync(object parameter)
        {
            if (parameter is not PasswordBox passwordBox || string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(passwordBox.Password))
            {
                ErrorMessage = "Please enter both username and password.";
                return;
            }

            IsProcessing = true;
            ErrorMessage = string.Empty;

            try
            {
                var (success, message) = await _authService.LoginAsync(Username, passwordBox.Password);

                if (success && _authService.CurrentUser != null)
                {
                    passwordBox.Clear();

                    // FIXED: We don't need a map anymore. It is natively an Enum!
                    UserRole role = _authService.CurrentUser.Role;

                    // Shout to MainViewModel to switch the screen
                    LoginSuccessful?.Invoke(role);
                }
                else
                {
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
    }
}