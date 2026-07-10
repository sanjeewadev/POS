using POS.BackOffice.UI.ViewModels;
using POS.Core.Services;
using System;
using System.Windows;

namespace POS.BackOffice.UI.Views
{
    public partial class FirstRunAdminWindow :
        Window
    {
        private readonly FirstRunAdminViewModel
            _viewModel;

        public FirstRunAdminWindow(
            FirstRunAdminViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = viewModel;

            Loaded += (_, _) =>
            {
                FirstNameInput.Focus();
                FirstNameInput.SelectAll();
            };
        }

        private async void CreateButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            CreateButton.IsEnabled = false;
            _viewModel.ErrorMessage = string.Empty;

            try
            {
                var result =
                    await _viewModel
                        .CreateAdministratorAsync(
                            PasswordInput.Password,
                            ConfirmPasswordInput.Password);

                PasswordInput.Clear();
                ConfirmPasswordInput.Clear();

                if (!result.Success)
                {
                    _viewModel.ErrorMessage =
                        result.Message;
                    return;
                }

                MessageBox.Show(
                    "The first Administrator account was created. " +
                    "Sign in using the new username and password.",
                    "Administrator Created",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "First-run setup window",
                    ex);

                _viewModel.ErrorMessage =
                    "The Administrator account could not be created. " +
                    "Technical details were saved in the local POS Logs folder.";
            }
            finally
            {
                CreateButton.IsEnabled = true;
            }
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
