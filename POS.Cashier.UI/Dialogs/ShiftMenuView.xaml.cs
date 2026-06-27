using POS.Cashier.UI.Dialogs;
using POS.Cashier.UI.ViewModels;
using System.Windows;

namespace POS.Cashier.UI.Dialogs
{
    public partial class ShiftMenuView : Window
    {
        private readonly SalesViewModel _viewModel;

        public ShiftMenuView(SalesViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;

            RefreshStatusUI();
        }

        private void RefreshStatusUI()
        {
            // Populate the Read-Only Status Panel
            CashierNameTxt.Text = _viewModel.CashierName;
            ShiftIdTxt.Text = $"#{_viewModel.CurrentShiftId}";

            SecurityStatusTxt.Text = _viewModel.SecurityStatusMode;

            // Change colors and text based on current Manager Mode state
            if (_viewModel.IsManagerModeActive)
            {
                SecurityStatusTxt.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC3545")); // Red
                ToggleManagerBtn.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#28A745")); // Green
                ToggleManagerTxt.Text = "DROP TO CASHIER MODE";
            }
            else
            {
                SecurityStatusTxt.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#28A745")); // Green
                ToggleManagerBtn.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC3545")); // Red
                ToggleManagerTxt.Text = "ELEVATE TO MANAGER";
            }
        }

        private void LockTerminalBtn_Click(object sender, RoutedEventArgs e)
        {
            // 1. Close the menu
            //this.Close();

            //// 2. Open the full-screen lock we built in File 3
            //var lockScreen = new LockScreenView(_viewModel.CashierName);
            //lockScreen.ShowDialog();
        }

        private void ToggleManagerBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.IsManagerModeActive)
            {
                // Instantly drop privileges without needing a PIN
                _viewModel.SetManagerMode(false);
                RefreshStatusUI();
            }
            else
            {
                // Require PIN to elevate privileges
                //var authDialog = new ManagerAuthDialogView();
                //if (authDialog.ShowDialog() == true)
                //{
                //    _viewModel.SetManagerMode(true);
                //    RefreshStatusUI();

                //    // Optional: Auto-close the menu once elevated so they can get to work
                //    this.Close();
                //}
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void LogOffBtn_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to log off? The current shift will remain open.",
                "LOG OFF",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // 1. Drop any active manager privileges for safety
                _viewModel.SetManagerMode(false);

                // 2. Open the Login Window
                var loginViewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<POS.Cashier.UI.ViewModels.LoginViewModel>(App.Services);
                if (loginViewModel != null)
                {
                    loginViewModel.LoginSuccessful += delegate ()
                    {
                        var newSalesWindow = new POS.Cashier.UI.Views.SalesView();
                        Application.Current.MainWindow = newSalesWindow;
                        newSalesWindow.Show();
                    };
                }

                var loginWindow = new POS.Cashier.UI.Views.LoginView
                {
                    DataContext = loginViewModel
                };

                // 3. Swap the screens
                Application.Current.MainWindow = loginWindow;
                loginWindow.Show();

                // 4. Close the menu and the old Sales window
                this.Close();
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is POS.Cashier.UI.Views.SalesView)
                    {
                        window.Close();
                        break;
                    }
                }
            }
        }
    }
}