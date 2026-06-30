using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.Dialogs;
using POS.Cashier.UI.ViewModels;
using System;
using System.Globalization;
using System.Linq;
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
                if (ViewModel.IsPaymentModeActive)
                {
                    ViewModel.CancelPaymentMode();
                    e.Handled = true;
                    return;
                }

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

            if (e.Key == Key.F7)
            {
                OpenCashTenderDialog();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F8)
            {
                OpenCardTenderDialog("VISA");
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F9)
            {
                OpenCardTenderDialog("MasterCard");
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F10)
            {
                OpenCardTenderDialog("AMEX");
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F11)
            {
                OpenChequeTenderDialog();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F12)
            {
                ViewModel.EnterPaymentMode();
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

        private void FixedDiscountBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (!TryReadTerminalDecimal(
                    out decimal amount,
                    "Enter rupee discount amount first.",
                    "Enter a valid rupee discount amount."))
            {
                return;
            }

            ViewModel.ClearTerminalInput();
            ViewModel.ApplyFixedDiscountToSelected(amount);
        }

        private void PercentDiscountBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (!TryReadTerminalDecimal(
                    out decimal percent,
                    "Enter discount percentage first.",
                    "Enter a valid discount percentage."))
            {
                return;
            }

            ViewModel.ClearTerminalInput();
            ViewModel.ApplyPercentDiscountToSelected(percent);
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

            if (ViewModel.IsPaymentModeActive)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Cancel payment mode before removing cart items.",
                    "#F59E0B");

                return;
            }

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

            if (ViewModel.IsPaymentModeActive)
            {
                ViewModel.CancelPaymentMode();
                return;
            }

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
        // EMBEDDED PAYMENT FLOW
        // =========================================================

        private void SubTotalBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.EnterPaymentMode();
        }

        private void PayBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            string buttonText = GetButtonText(sender);

            if (string.IsNullOrWhiteSpace(buttonText))
                return;

            if (buttonText.Equals("Cash", StringComparison.OrdinalIgnoreCase))
            {
                OpenCashTenderDialog();
                return;
            }

            if (buttonText.Equals("VISA", StringComparison.OrdinalIgnoreCase))
            {
                OpenCardTenderDialog("VISA");
                return;
            }

            if (buttonText.Equals("MasterCard", StringComparison.OrdinalIgnoreCase))
            {
                OpenCardTenderDialog("MasterCard");
                return;
            }

            if (buttonText.Equals("AMEX", StringComparison.OrdinalIgnoreCase))
            {
                OpenCardTenderDialog("AMEX");
                return;
            }

            if (buttonText.Equals("Cheque", StringComparison.OrdinalIgnoreCase))
            {
                OpenChequeTenderDialog();
                return;
            }

            if (buttonText.Equals("Gift Voucher", StringComparison.OrdinalIgnoreCase) ||
    buttonText.Equals("GiftVoucher", StringComparison.OrdinalIgnoreCase))
            {
                OpenGiftVoucherTenderDialog();
                return;
            }

            _ = ViewModel.ShowNotificationAsync($"{buttonText} payment is coming later.", "#F59E0B");
        }

        private void OpenCashTenderDialog()
        {
            if (ViewModel == null)
                return;

            EnsurePaymentModeStarted();

            if (!ViewModel.IsPaymentModeActive)
                return;

            if (ViewModel.BalanceDue <= 0m)
            {
                _ = ViewModel.ShowNotificationAsync("Invoice is already fully paid.", "#10B981");
                return;
            }

            decimal typedAmount = GetTerminalInputAmountOrZero();
            ViewModel.ClearTerminalInput();

            var tenderViewModel = new CashTenderDialogViewModel();
            tenderViewModel.Initialize(ViewModel.BalanceDue, typedAmount);

            var dialog = new POS.Cashier.UI.Dialogs.CashTenderDialog(tenderViewModel)
            {
                Owner = this
            };

            bool? result = ShowTenderDialogWithDim(dialog);

            if (result == true)
            {
                ViewModel.AddConfirmedCashPayment(
                    tenderViewModel.AppliedAmount,
                    tenderViewModel.TenderedAmount,
                    tenderViewModel.ChangeAmount);
            }
        }

        private void OpenCardTenderDialog(string cardType)
        {
            if (ViewModel == null)
                return;

            EnsurePaymentModeStarted();

            if (!ViewModel.IsPaymentModeActive)
                return;

            if (ViewModel.BalanceDue <= 0m)
            {
                _ = ViewModel.ShowNotificationAsync("Invoice is already fully paid.", "#10B981");
                return;
            }

            decimal typedAmount = GetTerminalInputAmountOrZero();
            ViewModel.ClearTerminalInput();

            var tenderViewModel = new CardTenderDialogViewModel();
            tenderViewModel.Initialize(cardType, ViewModel.BalanceDue, typedAmount);

            var dialog = new POS.Cashier.UI.Dialogs.CardTenderDialog(tenderViewModel)
            {
                Owner = this
            };

            bool? result = ShowTenderDialogWithDim(dialog);

            if (result == true)
            {
                ViewModel.AddConfirmedCardPayment(
                    tenderViewModel.CardType,
                    tenderViewModel.CardAmount,
                    tenderViewModel.LastSixDigits,
                    tenderViewModel.ReferenceNo);
            }
        }

        private void OpenChequeTenderDialog()
        {
            if (ViewModel == null)
                return;

            EnsurePaymentModeStarted();

            if (!ViewModel.IsPaymentModeActive)
                return;

            if (ViewModel.BalanceDue <= 0m)
            {
                _ = ViewModel.ShowNotificationAsync("Invoice is already fully paid.", "#10B981");
                return;
            }

            decimal typedAmount = GetTerminalInputAmountOrZero();
            ViewModel.ClearTerminalInput();

            var tenderViewModel = new ChequeTenderDialogViewModel();
            tenderViewModel.Initialize(ViewModel.BalanceDue, typedAmount);

            var dialog = new POS.Cashier.UI.Dialogs.ChequeTenderDialog(tenderViewModel)
            {
                Owner = this
            };

            bool? result = ShowTenderDialogWithDim(dialog);

            if (result == true)
            {
                ViewModel.AddConfirmedChequePayment(
                    tenderViewModel.ChequeAmount,
                    tenderViewModel.ChequeNo,
                    tenderViewModel.BankOrBranchText,
                    tenderViewModel.ChequeDate);
            }
        }

        private void OpenGiftVoucherTenderDialog()
        {
            if (ViewModel == null)
                return;

            EnsurePaymentModeStarted();

            if (!ViewModel.IsPaymentModeActive)
                return;

            if (ViewModel.BalanceDue <= 0m)
            {
                _ = ViewModel.ShowNotificationAsync("Invoice is already fully paid.", "#10B981");
                return;
            }

            if (ViewModel.Cart.Any(c => c.IsGiftVoucherSale))
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Gift voucher cannot be used to buy another gift voucher.",
                    "#EF4444");

                return;
            }

            ViewModel.ClearTerminalInput();

            var dialog = new GiftVoucherTenderDialog(ViewModel.BalanceDue)
            {
                Owner = this
            };

            bool? result = ShowTenderDialogWithDim(dialog);

            if (result == true)
            {
                ViewModel.AddConfirmedGiftVoucherPayment(
                    dialog.GiftVoucherId,
                    dialog.VoucherNo,
                    dialog.VoucherBarcode,
                    dialog.VoucherAmount,
                    dialog.AmountToApply,
                    dialog.ForfeitedAmount);
            }
        }

        private void EnsurePaymentModeStarted()
        {
            if (ViewModel == null)
                return;

            if (!ViewModel.IsPaymentModeActive)
                ViewModel.EnterPaymentMode();
        }

        private bool? ShowTenderDialogWithDim(Window dialog)
        {
            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                return dialog.ShowDialog();
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;
            }
        }

        private bool TryReadTerminalDecimal(
    out decimal value,
    string emptyMessage,
    string invalidMessage)
        {
            value = 0m;

            if (ViewModel == null)
                return false;

            string input = (ViewModel.TerminalInput ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                _ = ViewModel.ShowNotificationAsync(emptyMessage, "#F59E0B");
                return false;
            }

            input = input.Replace(",", string.Empty);

            if (decimal.TryParse(
                    input,
                    NumberStyles.Number,
                    CultureInfo.CurrentCulture,
                    out decimal currentCultureValue))
            {
                value = Math.Round(currentCultureValue, 2);
                return true;
            }

            if (decimal.TryParse(
                    input,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out decimal invariantValue))
            {
                value = Math.Round(invariantValue, 2);
                return true;
            }

            _ = ViewModel.ShowNotificationAsync(invalidMessage, "#EF4444");
            ViewModel.ClearTerminalInput();

            return false;
        }

        private decimal GetTerminalInputAmountOrZero()
        {
            if (ViewModel == null)
                return 0m;

            string input = (ViewModel.TerminalInput ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(input))
                return 0m;

            input = input.Replace(",", string.Empty);

            if (decimal.TryParse(
                    input,
                    NumberStyles.Number,
                    CultureInfo.CurrentCulture,
                    out decimal currentCultureValue))
            {
                return currentCultureValue > 0m
                    ? Math.Round(currentCultureValue, 2)
                    : 0m;
            }

            if (decimal.TryParse(
                    input,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out decimal invariantValue))
            {
                return invariantValue > 0m
                    ? Math.Round(invariantValue, 2)
                    : 0m;
            }

            return 0m;
        }

        private async void ConfirmSaleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            await ViewModel.ConfirmSaleFromPaymentModeAsync();
        }

        private void CancelPaymentBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.CancelPaymentMode();
        }

        private void RemovePaymentBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.RemoveSelectedPaymentLine();
        }

        private void UnsupportedPaymentBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            string buttonText = GetButtonText(sender);

            if (buttonText.Equals("Cheque", StringComparison.OrdinalIgnoreCase))
            {
                OpenChequeTenderDialog();
                return;
            }

            if (buttonText.Equals("Gift Voucher", StringComparison.OrdinalIgnoreCase) ||
    buttonText.Equals("GiftVoucher", StringComparison.OrdinalIgnoreCase))
            {
                OpenGiftVoucherTenderDialog();
                return;
            }

            _ = ViewModel.ShowNotificationAsync(
                $"{buttonText} will be added after Cash/Card/Cheque workflow is stable.",
                "#F59E0B");
        }

        private static string GetButtonText(object sender)
        {
            if (sender is not Button button || button.Content == null)
                return string.Empty;

            return button.Content
                .ToString()
                ?.Replace("\r", " ")
                .Replace("\n", " ")
                .Trim() ?? string.Empty;
        }

        // =========================================================
        // SEEK / CUSTOMER / DIALOGS
        // =========================================================

        private bool BlockDialogIfPaymentMode(string actionName)
        {
            if (ViewModel == null)
                return true;

            if (!ViewModel.IsPaymentModeActive)
                return false;

            _ = ViewModel.ShowNotificationAsync(
                $"Cancel payment mode before {actionName}.",
                "#F59E0B");

            return true;
        }

        private void SeekBtn_Click(object sender, RoutedEventArgs e)
        {
            if (BlockDialogIfPaymentMode("opening seek"))
                return;

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
            OpenCustomerLookupDialog("All");
        }

        private void NewPriceBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (!TryReadTerminalDecimal(
                    out decimal newPrice,
                    "Enter new price first.",
                    "Enter a valid new price."))
            {
                return;
            }

            ViewModel.ClearTerminalInput();
            ViewModel.ApplyNewPriceToSelected(newPrice);
        }

        private void DiscountRuleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (BlockDialogIfPaymentMode("applying discount rule"))
                return;

            if (ViewModel.SelectedCartItem == null)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Please select an item before applying discount rule.",
                    "#F59E0B");

                return;
            }

            if (ViewModel.SelectedCartItem.IsGiftVoucherSale)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Gift voucher sale line cannot use discount rule.",
                    "#EF4444");

                return;
            }

            if (ViewModel.SelectedCartItem.IsFreeItem)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Free item line cannot use discount rule.",
                    "#EF4444");

                return;
            }

            if (ViewModel.SelectedCartItem.IsPriceOverridden)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Discount rule cannot be applied after New Price.",
                    "#EF4444");

                return;
            }

            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                string customerType = GetDiscountCustomerType();

                string approvedBy = ViewModel.IsManagerModeActive
                    ? ViewModel.CashierName
                    : string.Empty;

                var dialog = new POS.Cashier.UI.Dialogs.DiscountRuleDialog(
                    ViewModel.SelectedCartItem,
                    customerType,
                    ViewModel.IsManagerModeActive,
                    approvedBy)
                {
                    Owner = this
                };

                bool? result = dialog.ShowDialog();

                if (result == true && dialog.Result != null)
                {
                    ViewModel.ApplyDiscountRuleToSelected(
                        ViewModel.SelectedCartItem,
                        dialog.Result);
                }
            }
            catch (Exception ex)
            {
                _ = ViewModel.ShowNotificationAsync(
                    $"Discount rule failed: {ex.Message}",
                    "#EF4444");
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;
            }
        }

        private string GetDiscountCustomerType()
        {
            if (ViewModel == null)
                return "Walk-In";

            if (ViewModel.ActiveB2BCustomer == null)
                return "Walk-In";

            if (ViewModel.ActiveB2BCustomer.IsWholesale)
                return "Wholesale";

            if (ViewModel.ActiveB2BCustomer.IsDiscountEligible)
                return "Loyalty";

            return "Retail";
        }

        private void SuspendRecallBtn_Click(object sender, RoutedEventArgs e)
        {
            if (BlockDialogIfPaymentMode("suspend/recall"))
                return;

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
            if (BlockDialogIfPaymentMode("processing return"))
                return;

            new POS.Cashier.UI.Dialogs.ReturnInvoiceDialog().ShowDialog();
        }

        private void ReportsBtn_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Reports dialog coming soon.");
        }

        // =========================================================
        // CUSTOMER LOOKUP
        // =========================================================

        private void OpenCustomerLookupDialog(string lookupMode = "All")
        {
            if (ViewModel == null)
                return;

            if (ViewModel.IsPaymentModeActive)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Cancel payment mode before changing customer.",
                    "#F59E0B");

                return;
            }

            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var dialog = new B2BCustomerDialogView(lookupMode)
                {
                    Owner = this
                };

                bool? result = dialog.ShowDialog();

                if (result == true && dialog.SelectedCustomer != null)
                {
                    ViewModel.AttachCustomer(dialog.SelectedCustomer);
                }
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;
            }
        }

        private void OpenLoyaltyCustomerLookupDialog()
        {
            if (ViewModel == null)
                return;

            if (ViewModel.IsPaymentModeActive)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Cancel payment mode before changing customer.",
                    "#F59E0B");

                return;
            }

            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var dialog = new LoyaltyCustomerDialogView
                {
                    Owner = this
                };

                bool? result = dialog.ShowDialog();

                if (result == true && dialog.SelectedCustomer != null)
                {
                    ViewModel.AttachCustomer(dialog.SelectedCustomer);
                }
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;
            }
        }

        private void DetachCustomerFromSale()
        {
            if (ViewModel == null)
                return;

            ViewModel.DetachCustomer();
        }

        // =========================================================
        // CASH MOVEMENT
        // =========================================================

        private void PaidInBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (ViewModel.IsPaymentModeActive)
            {
                _ = ViewModel.ShowNotificationAsync("Cancel payment mode before Paid In.", "#F59E0B");
                return;
            }

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

            if (ViewModel.IsPaymentModeActive)
            {
                _ = ViewModel.ShowNotificationAsync("Cancel payment mode before Paid Out.", "#F59E0B");
                return;
            }

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
            OpenLoyaltyCustomerLookupDialog();
        }

        private void CustomerRegisterBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (BlockDialogIfPaymentMode("registering customer"))
                return;

            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var dialog = new QuickCustomerCreateDialog
                {
                    Owner = this
                };

                bool? result = dialog.ShowDialog();

                if (result == true)
                {
                    _ = ViewModel.ShowNotificationAsync(
                        "Customer saved. Use Customer button to search and attach.",
                        "#10B981");
                }
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;
            }
        }

        private void SellVoucherBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (BlockDialogIfPaymentMode("selling gift voucher"))
                return;

            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var dialog = new SellGiftVoucherDialog
                {
                    Owner = this
                };

                bool? result = dialog.ShowDialog();

                if (result == true)
                {
                    ViewModel.AddGiftVoucherSaleLine(
                        dialog.GiftVoucherId,
                        dialog.VoucherNo,
                        dialog.VoucherBarcode,
                        dialog.VoucherAmount,
                        dialog.DisplayDescription);
                }
            }
            catch (Exception ex)
            {
                _ = ViewModel.ShowNotificationAsync(
                    $"Gift voucher sale failed: {ex.Message}",
                    "#EF4444");
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;
            }
        }

        private void ExpressItemsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (BlockDialogIfPaymentMode("opening express items"))
                return;

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

            if (ViewModel.IsPaymentModeActive)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Cancel payment mode before free item action.",
                    "#F59E0B");

                return;
            }

            if (ViewModel.SelectedCartItem == null)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Please select an item to make it free.",
                    "#F59E0B");

                return;
            }

            if (ViewModel.SelectedCartItem.IsGiftVoucherSale)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Gift voucher sale line cannot be made free.",
                    "#EF4444");

                return;
            }

            if (ViewModel.SelectedCartItem.IsFreeItem)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "This item is already marked as free.",
                    "#F59E0B");

                return;
            }

            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var dialog = new FreeItemReasonModalWindow(ViewModel.SelectedCartItem)
                {
                    Owner = this
                };

                bool? result = dialog.ShowDialog();

                if (result == true && dialog.Result != null)
                {
                    ViewModel.ApplyFreeItemLogic(
                        ViewModel.SelectedCartItem,
                        dialog.Result);
                }
            }
            catch (Exception ex)
            {
                _ = ViewModel.ShowNotificationAsync(
                    $"Free item failed: {ex.Message}",
                    "#EF4444");
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