using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.Dialogs;
using POS.Cashier.UI.ViewModels;

namespace POS.Cashier.UI.Views
{
    public partial class LoginView : Window
    {
        public LoginView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is LoginViewModel oldViewModel)
            {
                oldViewModel.ManagerOverrideRequested -= OnManagerOverrideRequested;
            }
            if (e.NewValue is LoginViewModel newViewModel)
            {
                newViewModel.ManagerOverrideRequested += OnManagerOverrideRequested;
            }
        }

        private async System.Threading.Tasks.Task OnManagerOverrideRequested()
        {
            if (App.Services == null) return;

            var authViewModel = App.Services.GetRequiredService<ManagerAuthViewModel>();
            var dialog = new ManagerAuthDialogView(authViewModel)
            {
                Owner = this
            };

            bool? result = dialog.ShowDialog();
            if (result == true && !string.IsNullOrEmpty(authViewModel.AuthorizedUsername))
            {
                if (DataContext is LoginViewModel viewModel)
                {
                    await viewModel.ProcessManagerOverrideAsync(authViewModel.AuthorizedUsername);
                }
            }
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