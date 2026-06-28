using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace POS.Cashier.UI.Views
{
    public partial class SalesView : Window
    {
        private readonly DispatcherTimer _inactivityTimer;
        private const int INACTIVITY_TIMEOUT_MINUTES = 3;

        public SalesView()
        {
            InitializeComponent();

            DataContext = App.Services!.GetRequiredService<SalesViewModel>();

            _inactivityTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(INACTIVITY_TIMEOUT_MINUTES)
            };

            _inactivityTimer.Tick += InactivityTimer_Tick;
            _inactivityTimer.Start();

            MouseMove += ResetInactivityTimer;
            PreviewKeyDown += ResetInactivityTimer;
            PreviewMouseDown += ResetInactivityTimer;
            TouchDown += ResetInactivityTimer;
        }

        private SalesViewModel? ViewModel => DataContext as SalesViewModel;

        private void ResetInactivityTimer(object? sender, EventArgs e)
        {
            _inactivityTimer.Stop();
            _inactivityTimer.Start();
        }

        private void InactivityTimer_Tick(object? sender, EventArgs e)
        {
            _inactivityTimer.Stop();

            MessageBox.Show(
                "Terminal locked due to inactivity.",
                "Security Lockout",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            PerformLogOff();
        }

        // =========================================================
        // KEYBOARD / SCANNER INPUT
        // =========================================================

        private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (e.OriginalSource is TextBox textBox && !textBox.IsReadOnly)
                return;

            ViewModel?.AppendTerminalInput(e.Text);
            e.Handled = true;
        }

        private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.OriginalSource is TextBox textBox && !textBox.IsReadOnly)
                return;

            if (ViewModel == null)
                return;

            if (e.Key == Key.Enter)
            {
                await ViewModel.HandleTerminalEnterAsync();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Back)
            {
                ViewModel.BackspaceTerminalInput();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                if (!string.IsNullOrWhiteSpace(ViewModel.TerminalInput))
                {
                    ViewModel.ClearTerminalInput();
                    _ = ViewModel.ShowNotificationAsync("Input cleared.", "#F59E0B");
                }
                else
                {
                    CancelSaleBtn_Click(this, new RoutedEventArgs());
                }

                e.Handled = true;
                return;
            }

            if (e.Key == Key.F2)
            {
                SeekBtn_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F4)
            {
                ViewModel.ApplyTerminalInputAsQuantityToSelected();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F5)
            {
                ViewModel.ApplyTerminalInputAsDiscountPercentToSelected();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F12)
            {
                PayBtn_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Add || e.Key == Key.OemPlus)
            {
                ViewModel.IncreaseSelectedQuantity();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Subtract || e.Key == Key.OemMinus)
            {
                ViewModel.DecreaseSelectedQuantity();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up)
            {
                MoveCartSelection(-1);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down)
            {
                MoveCartSelection(1);
                e.Handled = true;
            }
        }

        private void MoveCartSelection(int direction)
        {
            if (CartDataGrid.Items.Count == 0)
                return;

            int currentIndex = CartDataGrid.SelectedIndex;

            if (currentIndex < 0)
                currentIndex = 0;
            else
                currentIndex += direction;

            if (currentIndex < 0)
                currentIndex = 0;

            if (currentIndex >= CartDataGrid.Items.Count)
                currentIndex = CartDataGrid.Items.Count - 1;

            CartDataGrid.SelectedIndex = currentIndex;
            CartDataGrid.ScrollIntoView(CartDataGrid.SelectedItem);
        }

        // =========================================================
        // TOUCH NUMPAD
        // =========================================================

        private void NumpadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content != null)
            {
                ViewModel?.AppendTerminalInput(btn.Content.ToString() ?? string.Empty);
            }
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ClearTerminalInput();
            if (ViewModel != null)
                _ = ViewModel.ShowNotificationAsync("Input cleared.", "#F59E0B");
        }

        private void BackspaceBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.BackspaceTerminalInput();
        }

        private async void EnterBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
                await ViewModel.HandleTerminalEnterAsync();
        }

        private void QtyBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ApplyTerminalInputAsQuantityToSelected();
        }

        private void DiscountBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ApplyTerminalInputAsDiscountPercentToSelected();
        }

        private void PlusBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.IncreaseSelectedQuantity();
        }

        private void MinusBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.DecreaseSelectedQuantity();
        }

        // =========================================================
        // GRID / CART ACTIONS
        // =========================================================

        private void CartDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel != null && CartDataGrid.SelectedItem is POS.Cashier.UI.Models.CartItem item)
            {
                ViewModel.SelectedCartItem = item;
            }
        }

        private void RemoveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (ViewModel.SelectedCartItem == null)
            {
                _ = ViewModel.ShowNotificationAsync("Please select an item to remove.", "#F59E0B");
                return;
            }

            ViewModel.RemoveSelectedItem();
        }

        private void CancelSaleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (ViewModel.Cart.Count == 0)
            {
                ViewModel.ClearTerminalInput();
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to clear this entire sale?",
                "Cancel Sale",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                ViewModel.ClearCartCommand.Execute(null);
            }
        }

        // =========================================================
        // SEEK / CUSTOMER / DIALOGS
        // =========================================================

        private void SeekBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var seekDialog = new POS.Cashier.UI.Dialogs.ProductSeekDialog
                {
                    Owner = this
                };

                seekDialog.ShowDialog();
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;
            }
        }

        private void CustomerBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var b2bDialog = new POS.Cashier.UI.Dialogs.B2BCustomerDialogView();
                bool? result = b2bDialog.ShowDialog();

                if (result == true && b2bDialog.ViewModel?.SelectedCustomer != null)
                {
                    ViewModel?.AttachB2BCustomer(b2bDialog.ViewModel.SelectedCustomer);
                }
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;
            }
        }

        private void PriceOverrideBtn_Click(object sender, RoutedEventArgs e)
        {
            new POS.Cashier.UI.Dialogs.PriceOverrideDialog().ShowDialog();
        }

        private void SuspendRecallBtn_Click(object sender, RoutedEventArgs e)
        {
            new POS.Cashier.UI.Dialogs.HoldRecallDialog().ShowDialog();
        }

        private void FloatCashBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.AddFloatCommand.Execute(null);
        }

        private void StockInquiryBtn_Click(object sender, RoutedEventArgs e)
        {
            new POS.Cashier.UI.Dialogs.StockInquiryDialog().ShowDialog();
        }

        private void ReturnBtn_Click(object sender, RoutedEventArgs e)
        {
            new POS.Cashier.UI.Dialogs.ReturnInvoiceDialog().ShowDialog();
        }

        private void ReportsBtn_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Reports dialog coming soon.");
        }

        // =========================================================
        // PAYMENT
        // =========================================================

        private void PayBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (ViewModel.Cart.Count == 0)
                return;

            _ = ViewModel.ShowNotificationAsync(
                "Checkout posting is blocked until FIFO batch sale flow is updated.",
                "#F59E0B");

            MessageBox.Show(
                "The cart table is ready, but final checkout should not be tested yet.\n\nNext step: update SalesRepository to allocate FIFO batches and post sales safely.",
                "Checkout Not Ready",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // =========================================================
        // CASH MOVEMENT
        // =========================================================

        private void PaidInBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (decimal.TryParse(ViewModel.TerminalInput, out decimal amount) && amount > 0)
            {
                ViewModel.ClearTerminalInput();
                OpenCashMovementDialog("Paid In", amount, ViewModel);
            }
            else
            {
                _ = ViewModel.ShowNotificationAsync("Amount required before Paid In.", "#F59E0B");
                ViewModel.ClearTerminalInput();
            }
        }

        private void PaidOutBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (decimal.TryParse(ViewModel.TerminalInput, out decimal amount) && amount > 0)
            {
                ViewModel.ClearTerminalInput();
                OpenCashMovementDialog("Paid Out", amount, ViewModel);
            }
            else
            {
                _ = ViewModel.ShowNotificationAsync("Amount required before Paid Out.", "#F59E0B");
                ViewModel.ClearTerminalInput();
            }
        }

        private void OpenCashMovementDialog(string type, decimal amount, SalesViewModel viewModel)
        {
            if (viewModel.CurrentShiftId == 0)
            {
                _ = viewModel.ShowNotificationAsync("Action blocked: No active shift found.", "#EF4444");
                return;
            }

            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var cashVM = App.Services!.GetRequiredService<POS.Cashier.UI.ViewModels.CashMovementViewModel>();
                cashVM.Initialize(type, amount, viewModel.CurrentShiftId, viewModel.CashierName);

                var cashDialog = new POS.Cashier.UI.Dialogs.CashMovementDialogView(cashVM)
                {
                    Owner = this
                };

                cashDialog.ShowDialog();
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;
            }
        }

        // =========================================================
        // SYSTEM / SHIFT / OTHER
        // =========================================================

        private void ShiftMenuBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                var shiftMenu = new POS.Cashier.UI.Dialogs.ShiftMenuView(ViewModel);
                shiftMenu.ShowDialog();
            }
            else
            {
                MessageBox.Show(
                    "System Error: Could not locate the Sales configuration.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void LoyaltyBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var loyaltyDialog = new POS.Cashier.UI.Dialogs.LoyaltyCustomerDialogView();
                bool? result = loyaltyDialog.ShowDialog();

                if (result == true && loyaltyDialog.SelectedCustomer != null)
                {
                    var customer = loyaltyDialog.SelectedCustomer;

                    MessageBox.Show(
                        $"Loyalty Customer Applied:\n{customer.FullName}\nDiscount: {customer.ActiveDiscountName}",
                        "Customer Linked",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;
            }
        }

        private void ExpressItemsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var expressDialog = new POS.Cashier.UI.Dialogs.ExpressItemDialogView
                {
                    Owner = this
                };

                expressDialog.ShowDialog();
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;
            }
        }

        private void FreeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (ViewModel.SelectedCartItem == null)
            {
                _ = ViewModel.ShowNotificationAsync("Please select an item to make it free.", "#F59E0B");
                return;
            }

            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var reasonVM = App.Services!.GetRequiredService<POS.Cashier.UI.ViewModels.FreeItemReasonModalViewModel>();
                reasonVM.Initialize(ViewModel.SelectedCartItem.Description, ViewModel.SelectedCartItem.UnitPrice);

                var dialog = new POS.Cashier.UI.Dialogs.FreeItemReasonModalWindow(reasonVM)
                {
                    Owner = this
                };

                bool? result = dialog.ShowDialog();

                if (result == true)
                {
                    ViewModel.ApplyFreeItemLogic(ViewModel.SelectedCartItem, reasonVM.SelectedReason);
                }
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;
            }
        }

        private void LogOffBtn_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to pause the register and log off?",
                "Log Off",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                PerformLogOff();
            }
        }

        private void PerformLogOff()
        {
            _inactivityTimer.Stop();

            var loginViewModel = App.Services?.GetService<POS.Cashier.UI.ViewModels.LoginViewModel>();

            if (loginViewModel != null)
            {
                loginViewModel.LoginSuccessful += delegate
                {
                    SalesView newSalesWindow = new SalesView();
                    Application.Current.MainWindow = newSalesWindow;
                    newSalesWindow.Show();
                };
            }

            POS.Cashier.UI.Views.LoginView loginWindow = new POS.Cashier.UI.Views.LoginView
            {
                DataContext = loginViewModel
            };

            Application.Current.MainWindow = loginWindow;
            loginWindow.Show();

            Close();
        }
    }
}