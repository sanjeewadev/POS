using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Services;
using POS.Core.Enums;

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

        [ObservableProperty]
        private string _authorizedUsername = string.Empty;

        [ObservableProperty]
        private int? _authorizedUserId;

        [ObservableProperty]
        private string _authorizedRole = string.Empty;

        [ObservableProperty]
        private bool _requireAdministrator;

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
                ManagerAuthorizationResult result =
                    await _authService.ValidateManagerCredentialsAsync(
                        Username,
                        passwordBox.Password);

                if (result.Success)
                {
                    if (RequireAdministrator && result.Role != UserRole.Admin)
                    {
                        AuthorizedUsername = string.Empty;
                        AuthorizedUserId = null;
                        AuthorizedRole = string.Empty;
                        ErrorMessage = "Administrator credentials are required for this action.";
                        return;
                    }

                    AuthorizedUsername = result.Username;
                    AuthorizedUserId = result.UserId;
                    AuthorizedRole = result.Role?.ToString() ?? string.Empty;
                    AuthenticationCompleted?.Invoke(true);
                }
                else
                {
                    AuthorizedUsername = string.Empty;
                    AuthorizedUserId = null;
                    AuthorizedRole = string.Empty;
                    ErrorMessage = result.Message;
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
