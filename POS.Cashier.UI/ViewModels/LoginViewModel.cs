using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Enums; // Access to UserRole

namespace POS.Cashier.UI.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _username = string.Empty;

        // We don't use ObservableProperty for passwords for security reasons in WPF
        public string Password { get; set; } = string.Empty;

        // The "Shout" that App.xaml.cs listens to!
        public event Action<UserRole>? LoginSuccessful;

        [RelayCommand]
        private void Login()
        {
            // Simple validation
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                MessageBox.Show("Please enter a username and password.", "Login Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // TEMPORARY HARDCODED BYPASS: (Replace this with database check later)
            if (Username.ToLower() == "admin" && Password == "123")
            {
                // Shout to App.xaml.cs to open the register!
                LoginSuccessful?.Invoke(UserRole.Admin);
            }
            else if (Username.ToLower() == "cashier" && Password == "123")
            {
                LoginSuccessful?.Invoke(UserRole.Cashier);
            }
            else
            {
                MessageBox.Show("Invalid username or password.", "Login Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}