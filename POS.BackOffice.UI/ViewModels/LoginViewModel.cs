using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Enums;
using POS.Core.Services;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class LoginViewModel :
        ObservableObject
    {
        private readonly AuthService _authService;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(
            nameof(LoginCommand))]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(
            nameof(LoginCommand))]
        private bool _isProcessing;

        public event Action<UserRole>?
            LoginSuccessful;

        public LoginViewModel(
            AuthService authService)
        {
            _authService = authService;
        }

        [RelayCommand(
            CanExecute = nameof(CanLogin))]
        private async Task LoginAsync(
            object parameter)
        {
            if (parameter is not
                PasswordBox passwordBox)
            {
                ErrorMessage =
                    "Password input is unavailable.";
                return;
            }

            IsProcessing = true;
            ErrorMessage = string.Empty;

            try
            {
                string safeUsername =
                    (Username ?? string.Empty).Trim();

                var (success, message) =
                    await _authService.LoginAsync(
                        safeUsername,
                        passwordBox.Password,
                        "BackOffice");

                if (!success ||
                    _authService.CurrentUser == null)
                {
                    ErrorMessage = message;
                    return;
                }

                LoginSuccessful?.Invoke(
                    _authService.CurrentUser.Role);
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Login",
                    ex);

                ErrorMessage =
                    "BackOffice could not complete the login. " +
                    "Technical details were saved in the local POS Logs folder.";
            }
            finally
            {
                passwordBox.Clear();
                IsProcessing = false;
            }
        }

        private bool CanLogin()
        {
            return !IsProcessing &&
                   !string.IsNullOrWhiteSpace(
                       Username);
        }
    }
}
