using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.ViewModels;
using POS.Cashier.UI.ViewModels.Dialogs;
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
        private StringBuilder _barcodeBuffer = new StringBuilder();
        private DateTime _lastKeystroke = DateTime.Now;

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

        private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (e.OriginalSource is TextBox) return;

            TimeSpan elapsed = DateTime.Now - _lastKeystroke;
            if (elapsed.TotalMilliseconds > 50)
            {
                _barcodeBuffer.Clear();
            }

            _barcodeBuffer.Append(e.Text);
            _lastKeystroke = DateTime.Now;
            e.Handled = true;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _barcodeBuffer.Length > 0)
            {
                string scannedCode = _barcodeBuffer.ToString().Trim();
                _barcodeBuffer.Clear();

                if (this.DataContext is SalesViewModel viewModel)
                {
                    _ = viewModel.ProcessBarcodeAsync(scannedCode);
                }

                e.Handled = true;
                return;
            }

            if (e.OriginalSource is not TextBox)
            {
                if (e.Key == Key.F12)
                {
                    PayBtn_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    CancelSaleBtn_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
            }
        }

        private void CartDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CartDataGrid.SelectedItem != null)
            {
                ContextualQtyPanel.Visibility = Visibility.Visible;
                txtQtyInput.Text = "1";
                txtQtyInput.Focus();
                txtQtyInput.SelectAll();
            }
            else
            {
                ContextualQtyPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void HideQtyPanel_Click(object sender, RoutedEventArgs e)
        {
            ContextualQtyPanel.Visibility = Visibility.Collapsed;
            CartDataGrid.SelectedItem = null;
        }

        private void NumpadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content != null)
            {
                string value = btn.Content.ToString() ?? "";

                if (ContextualQtyPanel.Visibility == Visibility.Visible)
                {
                    txtQtyInput.Text += value;
                }
            }
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ContextualQtyPanel.Visibility == Visibility.Visible)
            {
                txtQtyInput.Text = "";
            }
        }

        private void SeekBtn_Click(object sender, RoutedEventArgs e)
        {
            var currentViewModel = this.DataContext as POS.Cashier.UI.ViewModels.SalesViewModel;
            if (currentViewModel == null) return;

            var itemRepo = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<POS.Core.Repositories.ItemMasterRepository>(App.Services!);

            var seekDialog = new POS.Cashier.UI.Views.Dialogs.ProductSeekDialog(currentViewModel, itemRepo);
            seekDialog.ShowDialog();
        }

        private void QtyPlusBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ContextualQtyPanel.Visibility == Visibility.Visible && int.TryParse(txtQtyInput.Text, out int qty))
            {
                txtQtyInput.Text = (qty + 1).ToString();
            }
        }

        private void QtyMinusBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ContextualQtyPanel.Visibility == Visibility.Visible && int.TryParse(txtQtyInput.Text, out int qty) && qty > 1)
            {
                txtQtyInput.Text = (qty - 1).ToString();
            }
        }

        private void CustomerBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DimmingCurtain != null) DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var b2bDialog = new POS.Cashier.UI.Views.Dialogs.B2BCustomerDialogView();
                bool? result = b2bDialog.ShowDialog();

                if (result == true && b2bDialog.SelectedCustomer != null)
                {
                    var customer = b2bDialog.SelectedCustomer;
                    MessageBox.Show($"B2B Account Attached:\n{customer.CompanyName}\nAvailable Credit: Rs. {customer.RemainingCredit:N2}",
                                    "Wholesale Link Active", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            finally
            {
                if (DimmingCurtain != null) DimmingCurtain.Visibility = Visibility.Collapsed;
            }
        }

        private void DiscountBtn_Click(object sender, RoutedEventArgs e)
            => new POS.Cashier.UI.Views.Dialogs.PriceDiscountDialog().ShowDialog();

        private void PriceOverrideBtn_Click(object sender, RoutedEventArgs e)
            => new POS.Cashier.UI.Views.Dialogs.PriceDiscountDialog().ShowDialog();

        private void SuspendRecallBtn_Click(object sender, RoutedEventArgs e)
            => new POS.Cashier.UI.Views.Dialogs.SuspendedCartsDialog().ShowDialog();

        private void AddFloatBtn_Click(object sender, RoutedEventArgs e)
            => new POS.Cashier.UI.Views.Dialogs.AddFloatDialog().ShowDialog();

        private void StockInquiryBtn_Click(object sender, RoutedEventArgs e)
            => MessageBox.Show("Stock Inquiry Dialog coming soon.");

        private void ReturnBtn_Click(object sender, RoutedEventArgs e)
            => MessageBox.Show("Return Dialog coming soon.");

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
                        dialog = new POS.Cashier.UI.Views.Dialogs.CashTenderDialog();
                        break;
                    case "VISA":
                    case "MasterCard":
                    case "AMEX":
                        dialog = new POS.Cashier.UI.Views.Dialogs.CardAuthDialog();
                        break;
                    case "Cheque":
                    case "Credit Note":
                        dialog = new POS.Cashier.UI.Views.Dialogs.DocumentTenderDialog();
                        break;
                    case "Gift Voucher":
                        // SPINS UP THE NEW DIALOG!
                        var voucherVM = App.Services!.GetRequiredService<RedeemVoucherModalViewModel>();
                        voucherVM.Initialize(viewModel.NetValue);
                        dialog = new POS.Cashier.UI.Dialogs.RedeemVoucherModalWindow(voucherVM);
                        break;
                    case "Cust Credit":
                    case "LOYALTY":
                        dialog = new POS.Cashier.UI.Views.Dialogs.CustomerAccountDialog();
                        break;
                    case "Sub\nTotal":
                        MessageBox.Show("Unified Split Checkout View coming soon.");
                        return;
                    default:
                        return;
                }

                if (dialog != null)
                {
                    // Darken screen if the dialog isn't managing it natively yet
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
            if (CartDataGrid.SelectedItem != null && this.DataContext is SalesViewModel viewModel)
            {
                viewModel.RemoveItemCommand.Execute(CartDataGrid.SelectedItem);
            }
            else
            {
                MessageBox.Show("Please select an item to remove.", "Action Required", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private void PaidInBtn_Click(object sender, RoutedEventArgs e)
        {
            DimmingCurtain.Visibility = Visibility.Visible;
            try
            {
                var cashDialog = new POS.Cashier.UI.Dialogs.CashMovementDialogView(POS.Cashier.UI.Dialogs.MovementType.PaidIn);
                cashDialog.ShowDialog();
            }
            finally
            {
                DimmingCurtain.Visibility = Visibility.Collapsed;
            }
        }

        private void PaidOutBtn_Click(object sender, RoutedEventArgs e)
            => new POS.Cashier.UI.Dialogs.CashMovementDialogView(POS.Cashier.UI.Dialogs.MovementType.PaidOut).ShowDialog();

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

            var loginViewModel = App.Services?.GetService<LoginViewModel>();
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
                var shiftMenu = new POS.Cashier.UI.Views.Dialogs.ShiftMenuView(currentViewModel);
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
                var loyaltyDialog = new POS.Cashier.UI.Views.Dialogs.LoyaltyCustomerDialogView();
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
                var expressDialog = new POS.Cashier.UI.Views.Dialogs.ExpressItemDialogView();
                bool? result = expressDialog.ShowDialog();

                if (result == true && !string.IsNullOrWhiteSpace(expressDialog.SelectedSkuCode))
                {
                    System.Media.SystemSounds.Beep.Play();
                    if (this.DataContext is SalesViewModel viewModel)
                    {
                        _ = viewModel.ProcessBarcodeAsync(expressDialog.SelectedSkuCode);
                    }
                }
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
                // 1. Check if they actually selected an item in the grid!
                if (CartDataGrid.SelectedItem == null)
                {
                    MessageBox.Show("Please select an item from the cart to make it free.", "Select Item", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // We use 'dynamic' here temporarily, but you should cast this to your actual CartItemDto class (e.g., as CartItemDto)
                dynamic selectedItem = CartDataGrid.SelectedItem;

                // 2. Darken Screen
                if (DimmingCurtain != null) DimmingCurtain.Visibility = Visibility.Visible;

                try
                {
                    // 3. Spin up the ViewModel and hand it the item details
                    var reasonVM = App.Services!.GetRequiredService<FreeItemReasonModalViewModel>();

                    // Pass the description and price to the popup
                    reasonVM.Initialize(selectedItem.Description, selectedItem.UnitPrice);

                    // 4. Show Window
                    var dialog = new POS.Cashier.UI.Dialogs.FreeItemReasonModalWindow(reasonVM);
                    dialog.Owner = this;
                    bool? result = dialog.ShowDialog();

                    // 5. If they tapped a reason, apply the math!
                    if (result == true)
                    {
                        // Trigger the logic in your SalesViewModel to zero out the price
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