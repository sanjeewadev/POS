using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Core.Enums;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class LoginViewModel : ViewModelBase
    {
        // [NotifyCanExecuteChangedFor] tells the UI to re-check the Login button's 
        // enable/disable state every time the user types a letter.
        // Use a string "LoginCommand" instead of nameof() to prevent compiler panic!
        [ObservableProperty]
        [NotifyCanExecuteChangedFor("LoginCommand")]
        private string _username = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor("LoginCommand")]
        private string _password = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        // This is the "Shout"! It tells the main window that login was successful.
        public event Action<UserRole>? LoginSuccessful;

        public LoginViewModel()
        {
            // No manual command wiring needed! The toolkit handles it.
        }

        // The validation rule for the button
        private bool CanLogin()
        {
            return !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
        }

        // [RelayCommand] automatically generates a public 'LoginCommand'
        // and links it to the 'CanLogin' rule above.
        [RelayCommand(CanExecute = nameof(CanLogin))]
        private void Login()
        {
            // Clear any old error messages
            ErrorMessage = string.Empty;

            // Our temporary hardcoded security check
            if (Username == "admin" && Password == "123")
            {
                LoginSuccessful?.Invoke(UserRole.Admin);
            }
            else if (Username == "cashier" && Password == "123")
            {
                LoginSuccessful?.Invoke(UserRole.Cashier);
            }
            else
            {
                // If they type anything else, show an error!
                ErrorMessage = "Invalid username or password!";
            }
        }
    }
}