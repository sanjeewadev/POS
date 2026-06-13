using POS.BackOffice.UI.ViewModels;
using POS.Core.Enums;
using System.Windows;

namespace POS.BackOffice.UI.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow(LoginViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // Subscribe to the success event so the window knows when to close
            viewModel.LoginSuccessful += OnLoginSuccessful;
        }

        private void OnLoginSuccessful(UserRole role)
        {
            // Tell the Bootstrapper that authentication passed, then close this window
            this.DialogResult = true;
            this.Close();
        }
    }
}