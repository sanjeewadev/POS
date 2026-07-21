using POS.BackOffice.UI.ViewModels;
using POS.Core.Services;
using System.Windows;
using System.Windows.Controls;

namespace POS.BackOffice.UI.Views.Pages.Admin
{
    public partial class UserManagementView : UserControl
    {
        public UserManagementView()
        {
            InitializeComponent();
        }

        private async void BtnSaveUser_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not UserManagementViewModel viewModel)
                return;

            string password = pwdBox.Password;

            try
            {
                await viewModel.ExecuteSaveAsync(password);
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Save User Management user",
                    ex);

                MessageBox.Show(
                    "The user could not be saved. BackOffice will remain open. " +
                    "Technical details were saved in the local POS Logs folder.",
                    "User Management",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                pwdBox.Clear();
            }
        }
    }
}
