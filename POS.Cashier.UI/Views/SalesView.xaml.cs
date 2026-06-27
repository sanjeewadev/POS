using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.ViewModels;
using POS.Core.Enums;
using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace POS.Cashier.UI.Views
{
    public partial class SalesView : Window
    {
        // THIS IS THE INVISIBLE BUFFER!
        private StringBuilder _barcodeBuffer = new StringBuilder();

        private DispatcherTimer _inactivityTimer;
        private const int INACTIVITY_TIMEOUT_MINUTES = 3;

        public SalesView()
        {
            InitializeComponent();
            this.DataContext = App.Services!.GetRequiredService<SalesViewModel>();

            _inactivityTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(INACTIVITY_TIMEOUT_MINUTES)
            };
            _inactivityTimer.Tick += InactivityTimer_Tick;
            _inactivityTimer.Start();

            this.MouseMove += ResetInactivityTimer;
            this.PreviewKeyDown += ResetInactivityTimer;
            this.PreviewMouseDown += ResetInactivityTimer;
            this.TouchDown += ResetInactivityTimer;
        }

        private void ResetInactivityTimer(object? sender, EventArgs e)
        {
            _inactivityTimer.Stop();
            _inactivityTimer.Start();
        }

        private void InactivityTimer_Tick(object? sender, EventArgs e)
        {
            _inactivityTimer.Stop();
            MessageBox.Show("Terminal locked due to inactivity.", "Security Lockout", MessageBoxButton.OK, MessageBoxImage.Warning);
            PerformLogOff();
        }

        // ==========================================
        // INVISIBLE HYBRID INPUT ENGINE 
        // ==========================================
        private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Do not capture if the user is typing into a real text box
            if (e.OriginalSource is TextBox) return;

            // FIX: Removed the 50ms trap entirely! 
            // Manual typing is now perfectly safe and captured.
            _barcodeBuffer.Append(e.Text);
            e.Handled = true;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.OriginalSource is TextBox) return;

            // 1. EXECUTE BARCODE SCAN (Triggered by 'Enter')
            if (e.Key == Key.Enter)
            {
                if (_barcodeBuffer.Length > 0)
                {
                    string scannedCode = _barcodeBuffer.ToString().Trim();
                    _barcodeBuffer.Clear();

                    if (this.DataContext is SalesViewModel viewModel)
                    {
                        _ = viewModel.ProcessBarcodeAsync(scannedCode);
                    }
                }
                e.Handled = true;
                return;
            }
            // 2. HANDLE BACKSPACE (To correct manual typing mistakes invisibly)
            else if (e.Key == Key.Back)
            {
                if (_barcodeBuffer.Length > 0)
                {
                    _barcodeBuffer.Length--; // Remove the last character typed
                }
                e.Handled = true;
                return;
            }

            // 3. HARDWARE SHORTCUTS
            if (e.Key == Key.F12)
            {
                PayBtn_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                _barcodeBuffer.Clear(); // Added clear buffer to Escape key just in case!
                CancelSaleBtn_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        // ==========================================
        // ON-SCREEN NUMPAD ROUTING
        // ==========================================
        private void NumpadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content != null)
            {
                string value = btn.Content.ToString() ?? "";
                _barcodeBuffer.Append(value); // Route touch clicks to the same invisible buffer
            }
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            _barcodeBuffer.Clear();
            if (this.DataContext is SalesViewModel viewModel)
            {
                _ = viewModel.ShowNotificationAsync("Input cleared.", "#F59E0B"); // Orange alert
            }
        }

        // ==========================================
        // GRID AND CART ACTIONS
        // ==========================================
        private void CartDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Contextual Qty Removed as requested
        }

        private void SeekBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DimmingCurtain != null) DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                // Simply spawn the new Window. 
                // The WeakReferenceMessenger handles sending the item to the cart silently!
                var seekDialog = new POS.Cashier.UI.Dialogs.ProductSeekDialog();
                seekDialog.Owner = this;
                seekDialog.ShowDialog();
            }
            finally
            {
                if (DimmingCurtain != null) DimmingCurtain.Visibility = Visibility.Collapsed;
            }
        }

        private void CustomerBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DimmingCurtain != null) DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var b2bDialog = new POS.Cashier.UI.Dialogs.B2BCustomerDialogView();
                bool? result = b2bDialog.ShowDialog();

                // Check if the dialog closed with "true" AND if the ViewModel inside it actually selected a customer
                if (result == true && b2bDialog.ViewModel?.SelectedCustomer != null)
                {
                    // Pass the DTO safely over to the SalesViewModel!
                    if (this.DataContext is SalesViewModel salesVM)
                    {
                        salesVM.AttachB2BCustomer(b2bDialog.ViewModel.SelectedCustomer);
                    }
                }
            }
            finally
            {
                if (DimmingCurtain != null) DimmingCurtain.Visibility = Visibility.Collapsed;
            }
        }

        private void DiscountBtn_Click(object sender, RoutedEventArgs e)
            => new POS.Cashier.UI.Dialogs.PriceDiscountDialog().ShowDialog();

        private void PriceOverrideBtn_Click(object sender, RoutedEventArgs e)
            => new POS.Cashier.UI.Dialogs.PriceOverrideDialog().ShowDialog();

        private void SuspendRecallBtn_Click(object sender, RoutedEventArgs e)
            => new POS.Cashier.UI.Dialogs.HoldRecallDialog().ShowDialog();

        private void FloatCashBtn_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is POS.Cashier.UI.ViewModels.SalesViewModel viewModel)
            {
                viewModel.AddFloatCommand.Execute(null);
            }
        }

        private void StockInquiryBtn_Click(object sender, RoutedEventArgs e)
            => new POS.Cashier.UI.Dialogs.StockInquiryDialog().ShowDialog();

        private void ReturnBtn_Click(object sender, RoutedEventArgs e)
            => new POS.Cashier.UI.Dialogs.ReturnInvoiceDialog().ShowDialog();

        private void ReportsBtn_Click(object sender, RoutedEventArgs e)
            => MessageBox.Show("Reports Dialog coming soon.");

        private void PayBtn_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is SalesViewModel viewModel)
            {
                if (viewModel.Cart.Count == 0) return;

                string buttonText = (sender as Button)?.Content?.ToString() ?? "";
                Window dialog = null;

                switch (buttonText)
                {
                    case "Cash":
                        break;
                    case "VISA":
                    case "MasterCard":
                    case "AMEX":
                        dialog = new POS.Cashier.UI.Dialogs.CardPaymentDialog();
                        break;
                    case "Cheque":
                    //case "Credit Note":
                    //    dialog = new POS.Cashier.UI.Dialogs.ChequePaymentDialog();
                    //    break;
                    case "Gift Voucher":
                        var voucherVM = App.Services!.GetRequiredService<POS.Cashier.UI.ViewModels.RedeemVoucherModalViewModel>();
                        voucherVM.Initialize(viewModel.NetValue);
                        dialog = new POS.Cashier.UI.Dialogs.RedeemVoucherModalWindow(voucherVM);
                        break;
                    case "Cust Credit":
                    case "LOYALTY":
                        break;
                    case "Sub\nTotal":
                        MessageBox.Show("Unified Split Checkout View coming soon.");
                        return;
                    default:
                        return;
                }

                if (dialog != null)
                {
                    if (DimmingCurtain != null) DimmingCurtain.Visibility = Visibility.Visible;

                    try
                    {
                        bool? result = dialog.ShowDialog();

                        if (result == true)
                        {
                            _ = viewModel.FinalizeCheckoutAsync(buttonText, viewModel.NetValue);
                        }
                    }
                    finally
                    {
                        if (DimmingCurtain != null) DimmingCurtain.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private void RemoveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is SalesViewModel viewModel)
            {
                if (CartDataGrid.SelectedItem != null)
                {
                    viewModel.RemoveItemCommand.Execute(CartDataGrid.SelectedItem);
                }
                else
                {
                    _ = viewModel.ShowNotificationAsync("Please select an item to remove.", "#F59E0B");
                }
            }
        }

        private void CancelSaleBtn_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to clear this entire sale? This cannot be undone.",
                "CANCEL SALE",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                if (this.DataContext is SalesViewModel viewModel)
                {
                    viewModel.ClearCartCommand.Execute(null);
                }
            }
        }

        // ==========================================
        // PREFIX INPUT: PAID IN / PAID OUT ROUTING
        // ==========================================
        private void PaidInBtn_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is SalesViewModel viewModel)
            {
                // Reads the invisible buffer!
                if (decimal.TryParse(_barcodeBuffer.ToString(), out decimal amount) && amount > 0)
                {
                    _barcodeBuffer.Clear();
                    OpenCashMovementDialog("Paid In", amount, viewModel);
                }
                else
                {
                    _ = viewModel.ShowNotificationAsync("Amount required: Type an amount before clicking Paid In.", "#F59E0B");
                    _barcodeBuffer.Clear();
                }
            }
        }

        private void PaidOutBtn_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is SalesViewModel viewModel)
            {
                if (decimal.TryParse(_barcodeBuffer.ToString(), out decimal amount) && amount > 0)
                {
                    _barcodeBuffer.Clear();
                    OpenCashMovementDialog("Paid Out", amount, viewModel);
                }
                else
                {
                    _ = viewModel.ShowNotificationAsync("Amount required: Type an amount before clicking Paid Out.", "#F59E0B");
                    _barcodeBuffer.Clear();
                }
            }
        }

        private void OpenCashMovementDialog(string type, decimal amount, SalesViewModel viewModel)
        {
            if (viewModel.CurrentShiftId == 0)
            {
                _ = viewModel.ShowNotificationAsync("Action blocked: No active shift found.", "#EF4444");
                return;
            }

            if (DimmingCurtain != null) DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var cashVM = App.Services!.GetRequiredService<POS.Cashier.UI.ViewModels.CashMovementViewModel>();
                cashVM.Initialize(type, amount, viewModel.CurrentShiftId, viewModel.CashierName);

                var cashDialog = new POS.Cashier.UI.Dialogs.CashMovementDialogView(cashVM);
                cashDialog.Owner = this;
                cashDialog.ShowDialog();
            }
            finally
            {
                if (DimmingCurtain != null) DimmingCurtain.Visibility = Visibility.Collapsed;
            }
        }

        // ==========================================
        // SYSTEM LOGOFF & NAVIGATION
        // ==========================================
        private void LogOffBtn_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to pause the register and log off?",
                "LOG OFF",
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
                loginViewModel.LoginSuccessful += delegate ()
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
            this.Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
        }

        private void ShiftMenuBtn_Click(object sender, RoutedEventArgs e)
        {
            var currentViewModel = this.DataContext as POS.Cashier.UI.ViewModels.SalesViewModel;

            if (currentViewModel != null)
            {
                var shiftMenu = new POS.Cashier.UI.Dialogs.ShiftMenuView(currentViewModel);
                shiftMenu.ShowDialog();
            }
            else
            {
                MessageBox.Show("System Error: Could not locate the Sales configuration.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoyaltyBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DimmingCurtain != null) DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var loyaltyDialog = new POS.Cashier.UI.Dialogs.LoyaltyCustomerDialogView();
                bool? result = loyaltyDialog.ShowDialog();

                if (result == true && loyaltyDialog.SelectedCustomer != null)
                {
                    var customer = loyaltyDialog.SelectedCustomer;
                    MessageBox.Show($"Loyalty Customer Applied:\n{customer.FullName}\nDiscount: {customer.ActiveDiscountName}",
                                    "Customer Linked", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            finally
            {
                if (DimmingCurtain != null) DimmingCurtain.Visibility = Visibility.Collapsed;
            }
        }

        private void ExpressItemsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DimmingCurtain != null) DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var expressDialog = new POS.Cashier.UI.Dialogs.ExpressItemDialogView();
                expressDialog.Owner = this;
                expressDialog.ShowDialog();
            }
            finally
            {
                if (DimmingCurtain != null) DimmingCurtain.Visibility = Visibility.Collapsed;
            }
        }

        private void FreeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is SalesViewModel viewModel)
            {
                if (CartDataGrid.SelectedItem == null)
                {
                    _ = viewModel.ShowNotificationAsync("Please select an item from the cart to make it free.", "#F59E0B");
                    return;
                }

                dynamic selectedItem = CartDataGrid.SelectedItem;

                if (DimmingCurtain != null) DimmingCurtain.Visibility = Visibility.Visible;

                try
                {
                    var reasonVM = App.Services!.GetRequiredService<POS.Cashier.UI.ViewModels.FreeItemReasonModalViewModel>();

                    reasonVM.Initialize(selectedItem.Description, selectedItem.UnitPrice);

                    var dialog = new POS.Cashier.UI.Dialogs.FreeItemReasonModalWindow(reasonVM);
                    dialog.Owner = this;
                    bool? result = dialog.ShowDialog();

                    if (result == true)
                    {
                        viewModel.ApplyFreeItemLogic(selectedItem, reasonVM.SelectedReason);
                    }
                }
                finally
                {
                    if (DimmingCurtain != null) DimmingCurtain.Visibility = Visibility.Collapsed;
                }
            }
        }
    }
}