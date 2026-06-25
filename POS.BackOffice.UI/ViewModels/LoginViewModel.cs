using System;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Services;
using POS.Core.Enums;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly AuthService _authService;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoginCommand))] // <-- FIX: Wakes up the button when you type!
        private string _username = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
        private bool _isProcessing;

        // RESTORED: The event that MainViewModel and LoginWindow are looking for!
        public event Action<UserRole>? LoginSuccessful;

        public LoginViewModel(AuthService authService)
        {
            _authService = authService;
        }

        [RelayCommand(CanExecute = nameof(CanLogin))]
        private async Task LoginAsync(object parameter)
        {
            if (parameter is not PasswordBox passwordBox) return;

            IsProcessing = true;
            ErrorMessage = string.Empty;

            try
            {
                // REAL LOGIC: Call your actual database Auth Service!
                var (success, message) = await _authService.LoginAsync(Username, passwordBox.Password);

                if (success && _authService.CurrentUser != null)
                {
                    UserRole role = _authService.CurrentUser.Role;

                    // SHOUT TO THE UI: Triggers the window to close and MainViewModel to navigate
                    LoginSuccessful?.Invoke(role);
                }
                else
                {
                    ErrorMessage = message;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "A critical system error occurred during login.";
                MessageBox.Show(ex.Message, "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // SECURITY: Clear the password box UI so it doesn't linger in RAM
                passwordBox.Clear();
                IsProcessing = false;
            }
        }

        private bool CanLogin() => !IsProcessing && !string.IsNullOrWhiteSpace(Username);
    }
}