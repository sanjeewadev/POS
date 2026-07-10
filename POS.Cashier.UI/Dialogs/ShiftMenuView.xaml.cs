using POS.Cashier.UI.ViewModels;
using POS.Cashier.UI.Views;
using System.Windows;

namespace POS.Cashier.UI.Dialogs
{
    public partial class ShiftMenuView : Window
    {
        private readonly SalesViewModel _viewModel;

        public ShiftMenuView(
            SalesViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;

            RefreshStatusUI();
        }

        private void RefreshStatusUI()
        {
            CashierNameTxt.Text =
                _viewModel.CashierName;

            ShiftIdTxt.Text =
                $"#{_viewModel.CurrentShiftId}";

            SecurityStatusTxt.Text =
                _viewModel.SecurityStatusMode;

            if (_viewModel.IsManagerModeActive)
            {
                SecurityStatusTxt.Foreground =
                    new System.Windows.Media
                        .SolidColorBrush(
                            (System.Windows.Media.Color)
                            System.Windows.Media
                                .ColorConverter
                                .ConvertFromString("#DC3545"));

                ToggleManagerBtn.Background =
                    new System.Windows.Media
                        .SolidColorBrush(
                            (System.Windows.Media.Color)
                            System.Windows.Media
                                .ColorConverter
                                .ConvertFromString("#28A745"));

                ToggleManagerTxt.Text =
                    "DROP TO CASHIER MODE";
            }
            else
            {
                SecurityStatusTxt.Foreground =
                    new System.Windows.Media
                        .SolidColorBrush(
                            (System.Windows.Media.Color)
                            System.Windows.Media
                                .ColorConverter
                                .ConvertFromString("#28A745"));

                ToggleManagerBtn.Background =
                    new System.Windows.Media
                        .SolidColorBrush(
                            (System.Windows.Media.Color)
                            System.Windows.Media
                                .ColorConverter
                                .ConvertFromString("#DC3545"));

                ToggleManagerTxt.Text =
                    "ELEVATE TO MANAGER";
            }
        }

        private void LockTerminalBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageBox.Show(
                "The password-based terminal lock will be connected " +
                "in the next controlled Cashier lock patch.",
                "Terminal Lock",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ToggleManagerBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_viewModel.IsManagerModeActive)
            {
                _viewModel.SetManagerMode(false);
                RefreshStatusUI();
                return;
            }

            MessageBox.Show(
                "Manager elevation is not enabled in this step.",
                "Manager Mode",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void CloseBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }

        private async void LogOffBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageBoxResult result =
                MessageBox.Show(
                    "Log off this user?\n\n" +
                    "The current shift will remain open.",
                    "Log Off",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            _viewModel.SetManagerMode(false);

            if (Application.Current is not App app ||
                Owner is not SalesView salesWindow)
            {
                MessageBox.Show(
                    "The Cashier login route is unavailable.",
                    "Log Off Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            Close();
            await app.ReturnToLoginAsync(salesWindow);
        }
    }
}
