using System.Windows;
using POS.Cashier.UI.ViewModels; // FIXED: Pointing to the Cashier's OWN Brain!

namespace POS.Cashier.UI.Views
{
    public partial class LoginView : Window
    {
        public LoginView()
        {
            InitializeComponent();
        }

        // This method securely passes the password to the ViewModel as the user types it
        private void txtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is LoginViewModel vm)
            {
                vm.Password = txtPassword.Password;
            }
        }
    }
}