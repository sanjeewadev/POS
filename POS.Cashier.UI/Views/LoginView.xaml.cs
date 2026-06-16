using System.Windows;
using POS.Cashier.UI.ViewModels;

namespace POS.Cashier.UI.Views
{
    public partial class LoginView : Window
    {
        public LoginView()
        {
            InitializeComponent();
        }

        // ADD THIS METHOD: It manually pushes the password to the brain whenever you type
        private void txtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is LoginViewModel viewModel)
            {
                viewModel.Password = txtPassword.Password;
            }
        }
    }
}