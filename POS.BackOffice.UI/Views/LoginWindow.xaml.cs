using POS.BackOffice.UI.ViewModels;
using POS.Core.Enums;
using System.Windows;

namespace POS.BackOffice.UI.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow(
            LoginViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.LoginSuccessful +=
                OnLoginSuccessful;

            Loaded += (_, _) =>
            {
                UsernameInput.Focus();
                UsernameInput.SelectAll();
            };
        }

        private void OnLoginSuccessful(
            UserRole role)
        {
            DialogResult = true;
            Close();
        }

        private void ExitButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
