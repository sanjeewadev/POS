using POS.BackOffice.UI.ViewModels;
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
            if (DataContext is UserManagementViewModel viewModel)
            {
                // Extract plain text securely right before passing it to the logic engine
                string password = pwdBox.Password;
                string pin = pinBox.Password;

                await viewModel.ExecuteSaveAsync(password, pin);

                // Wipe the UI boxes from memory after processing
                pwdBox.Clear();
                pinBox.Clear();
            }
        }
    }
}