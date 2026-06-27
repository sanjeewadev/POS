using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Services;

namespace POS.Cashier.UI.ViewModels
{
    public partial class ManagerAuthViewModel : ObservableObject
    {
        private readonly AuthService _authService;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AuthenticateCommand))]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AuthenticateCommand))]
        private bool _isProcessing;

        // The View (Window) will listen to this event so it knows when to close
        public event Action<bool>? AuthenticationCompleted;

        public ManagerAuthViewModel(AuthService authService)
        {
            _authService = authService;
        }

        [RelayCommand(CanExecute = nameof(CanAuthenticate))]
        private async Task AuthenticateAsync(object parameter)
        {
            if (parameter is not PasswordBox passwordBox) return;

            IsProcessing = true;
            ErrorMessage = string.Empty;

            try
            {
                // 1. Send the credentials to your central Auth Service
                var (success, message) = await _authService.LoginAsync(Username, passwordBox.Password);

                if (success)
                {
                    // 2. THE VAULT DOOR: Check if the user is actually a Manager or Admin
                    if (_authService.IsManager)
                    {
                        // Success! Send the green light to close the popup
                        AuthenticationCompleted?.Invoke(true);
                    }
                    else
                    {
                        // Valid password, but insufficient rank!
                        ErrorMessage = "ACCESS DENIED: Manager privileges required.";
                    }
                }
                else
                {
                    // Wrong username or password
                    ErrorMessage = message;
                }
            }
            catch (Exception)
            {
                ErrorMessage = "A critical system error occurred during authentication.";
            }
            finally
            {
                // SECURITY: Always clear the password box from RAM immediately
                passwordBox.Clear();
                IsProcessing = false;
            }
        }

        private bool CanAuthenticate()
        {
            // UI Locking: Button is disabled if processing OR if the username is blank
            return !IsProcessing && !string.IsNullOrWhiteSpace(Username);
        }
    }
}