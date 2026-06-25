using System;
using System.Security;
using System.Threading.Tasks;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Enums;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
        private bool _isProcessing;

        [RelayCommand(CanExecute = nameof(CanLogin))]
        private async Task LoginAsync(object parameter)
        {
            if (parameter is not PasswordBox passwordBox) return;

            IsProcessing = true;
            ErrorMessage = string.Empty;

            try
            {
                // Capture the secure string
                SecureString securePassword = passwordBox.SecurePassword;

                // Simulate high-security database authentication
                await Task.Delay(2000);

                // Perform auth logic here...

                // On Success: Navigate to main application
            }
            catch (Exception ex)
            {
                ErrorMessage = "Invalid credentials or system error.";
            }
            finally
            {
                // SECURITY: Clear the password box UI and the secure string object
                passwordBox.Clear();
                IsProcessing = false;
            }
        }

        private bool CanLogin() => !IsProcessing && !string.IsNullOrWhiteSpace(Username);
    }
}