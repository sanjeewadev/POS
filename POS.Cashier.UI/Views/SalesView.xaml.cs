using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.Dialogs;
using POS.Cashier.UI.Models;
using POS.Cashier.UI.Services;
using POS.Cashier.UI.ViewModels;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Configuration;
using POS.Core.Services;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace POS.Cashier.UI.Views
{
    public partial class SalesView : Window
    {
        private readonly CashierLockService _lockService;
        private readonly int _autoLockTimeoutMinutes;
        private bool _isReturningToLogin;

        private TerminalActionMode _terminalActionMode = TerminalActionMode.Normal;
        private bool _isDialogOpen;

        private enum TerminalActionMode
        {
            Normal,
            Quantity,
            FixedDiscount,
            PercentDiscount,
            NewPrice
        }

        public SalesView()
            : this(
                App.Services!
                    .GetRequiredService<SalesViewModel>(),
                App.Services!
                    .GetRequiredService<CashierLockService>(),
                10)
        {
        }

        public SalesView(
            SalesViewModel viewModel,
            CashierLockService lockService,
            int autoLockTimeoutMinutes)
        {
            InitializeComponent();

            Focusable = true;

            DataContext = viewModel
                ?? throw new ArgumentNullException(
                    nameof(viewModel));

            _lockService = lockService
                ?? throw new ArgumentNullException(
                    nameof(lockService));

            _autoLockTimeoutMinutes =
                Math.Clamp(
                    autoLockTimeoutMinutes,
                    0,
                    120);

            Loaded += SalesView_Loaded;
            Closed += SalesView_Closed;
        }

        private SalesViewModel? ViewModel => DataContext as SalesViewModel;

        private async void SalesView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            _lockService.Start(
                this,
                _autoLockTimeoutMinutes);

            await HandleActiveCartRecoveryAsync();
            ReturnFocusToTerminalInput();
        }

        private async Task HandleActiveCartRecoveryAsync()
        {
            if (ViewModel == null)
                return;

            try
            {
                CashierCartSessionDto? active =
                    await ViewModel.GetActiveCartForRecoveryAsync();

                if (active == null)
                    return;

                MessageBoxResult choice = MessageBox.Show(
                    $"An unfinished cart was recovered.\n\n" +
                    $"Reference: {active.ReferenceNo}\n" +
                    $"Items: {active.ItemCount}\n" +
                    $"Value: Rs. {active.NetTotal:N2}\n\n" +
                    "Yes = Resume Cart\nNo = Cancel Cart\nCancel = Log Off",
                    "Recover Cashier Cart",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (choice == MessageBoxResult.Yes)
                {
                    await ViewModel.RestoreActiveCartAsync(active);
                    await ViewModel.ShowNotificationAsync(
                        $"Recovered {active.ReferenceNo}. Payment must be entered again.",
                        "#D97706");
                    return;
                }

                if (choice == MessageBoxResult.No)
                {
                    var reasonDialog = new CartCancellationReasonDialog
                    {
                        Owner = this
                    };

                    if (reasonDialog.ShowDialog() == true)
                    {
                        await ViewModel.CancelRecoveredActiveCartAsync(
                            active,
                            reasonDialog.ReasonCode,
                            reasonDialog.ReasonText);

                        await ViewModel.ShowNotificationAsync(
                            $"Cancelled recovered cart {active.ReferenceNo}.",
                            "#F59E0B");
                    }
                    else
                    {
                        await ViewModel.RestoreActiveCartAsync(active);
                    }

                    return;
                }

                PerformLogOff();
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Recover active cart",
                    ex);

                MessageBox.Show(
                    $"The saved cart could not be recovered.\n\n{ex.Message}",
                    "Cart Recovery Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SalesView_Closed(
            object? sender,
            EventArgs e)
        {
            _lockService.Stop();
        }

        private void ReturnFocusToTerminalInput()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsVisible)
                    return;

                Focus();
                Keyboard.Focus(this);
            }), DispatcherPriority.ApplicationIdle);
        }

        private static bool IsEditableTextInputSource(object source)
        {
            return source is TextBox textBox && !textBox.IsReadOnly;
        }

        // =========================================================
        // TERMINAL MODE
        // =========================================================

        private void SetTerminalActionMode(
            TerminalActionMode mode,
            string displayMode,
            string message)
        {
            if (ViewModel == null)
                return;

            if (ViewModel.IsPaymentModeActive)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Cancel payment mode before editing cart lines.",
                    "#F59E0B");

                ReturnFocusToTerminalInput();
                return;
            }

            _terminalActionMode = mode;
            ViewModel.TerminalInputMode = displayMode;

            if (!string.IsNullOrWhiteSpace(message))
                _ = ViewModel.ShowNotificationAsync(message, "#2563EB");

            ReturnFocusToTerminalInput();
        }

        private void ResetTerminalActionMode()
        {
            _terminalActionMode = TerminalActionMode.Normal;

            if (ViewModel != null && !ViewModel.IsPaymentModeActive)
                ViewModel.TerminalInputMode = "SCAN / QTY";
        }

        private bool HasTerminalInput()
        {
            return ViewModel != null &&
                   !string.IsNullOrWhiteSpace(ViewModel.TerminalInput);
        }

        private bool ExecutePendingTerminalAction()
        {
            if (ViewModel == null)
                return false;

            if (_terminalActionMode == TerminalActionMode.Normal)
                return false;

            if (string.IsNullOrWhiteSpace(ViewModel.TerminalInput))
            {
                _ = ViewModel.ShowNotificationAsync("Enter a value first.", "#F59E0B");
                ReturnFocusToTerminalInput();
                return true;
            }

            switch (_terminalActionMode)
            {
                case TerminalActionMode.Quantity:
                    ViewModel.ApplyTerminalInputAsQuantityToSelected();
                    break;

                case TerminalActionMode.FixedDiscount:
                    ApplyFixedDiscountFromTerminalInput();
                    break;

                case TerminalActionMode.PercentDiscount:
                    ApplyPercentDiscountFromTerminalInput();
                    break;

                case TerminalActionMode.NewPrice:
                    ApplyNewPriceFromTerminalInput();
                    break;
            }

            ResetTerminalActionMode();
            ReturnFocusToTerminalInput();

            return true;
        }

        // =========================================================
        // KEYBOARD / SCANNER INPUT
        // =========================================================

        private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (_isDialogOpen)
                return;

            if (IsEditableTextInputSource(e.OriginalSource))
                return;

            ViewModel?.AppendTerminalInput(e.Text);
            e.Handled = true;
        }

        private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_isDialogOpen)
                return;

            if (IsEditableTextInputSource(e.OriginalSource))
                return;

            if (ViewModel == null)
            {
                e.Handled = true;
                return;
            }

            // Ctrl shortcuts
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Key == Key.P)
                {
                    PrintBtn_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    ReturnFocusToTerminalInput();
                    return;
                }

                if (e.Key == Key.Q)
                {
                    PrintQuotationBtn_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    ReturnFocusToTerminalInput();
                    return;
                }

                if (e.Key == Key.L)
                {
                    PrintLastBillBtn_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    ReturnFocusToTerminalInput();
                    return;
                }
            }

            if (e.Key == Key.Enter)
            {
                e.Handled = true;

                if (ExecutePendingTerminalAction())
                    return;

                // Stop empty Enter from reaching DataGrid/WPF and ringing.
                if (!ViewModel.IsPaymentModeActive &&
                    string.IsNullOrWhiteSpace(ViewModel.TerminalInput))
                {
                    ResetTerminalActionMode();
                    ReturnFocusToTerminalInput();
                    return;
                }

                await ViewModel.HandleTerminalEnterAsync();

                ResetTerminalActionMode();
                ReturnFocusToTerminalInput();
                return;
            }

            if (e.Key == Key.Back)
            {
                ViewModel.BackspaceTerminalInput();
                e.Handled = true;
                ReturnFocusToTerminalInput();
                return;
            }

            if (e.Key == Key.Escape)
            {
                e.Handled = true;

                if (ViewModel.IsPaymentModeActive)
                {
                    ViewModel.CancelPaymentMode();
                    ResetTerminalActionMode();
                    ReturnFocusToTerminalInput();
                    return;
                }

                if (!string.IsNullOrWhiteSpace(ViewModel.TerminalInput))
                {
                    ViewModel.ClearTerminalInput();
                    ResetTerminalActionMode();
                    _ = ViewModel.ShowNotificationAsync("Input cleared.", "#F59E0B");
                    ReturnFocusToTerminalInput();
                    return;
                }

                ResetTerminalActionMode();
                _ = ViewModel.ShowNotificationAsync("Ready.", "#64748B");
                ReturnFocusToTerminalInput();
                return;
            }

            if (e.Key == Key.Delete)
            {
                ConfirmAndRemoveSelectedCartLine();
                e.Handled = true;
                ReturnFocusToTerminalInput();
                return;
            }

            if (e.Key == Key.F1)
            {
                SeekBtn_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F2)
            {
                QuantityShortcut();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F3)
            {
                FixedDiscountShortcut();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F4)
            {
                PercentDiscountShortcut();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F5)
            {
                NewPriceShortcut();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F6)
            {
                DiscountRuleBtn_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F7)
            {
                CustomerBtn_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F8)
            {
                SuspendRecallBtn_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F9)
            {
                ViewModel.EnterPaymentMode();
                ResetTerminalActionMode();
                e.Handled = true;
                ReturnFocusToTerminalInput();
                return;
            }

            if (e.Key == Key.F10)
            {
                PrintBtn_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F11)
            {
                ShiftMenuBtn_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F12)
            {
                LockTerminal(
                    "Locked manually with F12.");

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Add || e.Key == Key.OemPlus)
            {
                ViewModel.IncreaseSelectedQuantity();
                e.Handled = true;
                ReturnFocusToTerminalInput();
                return;
            }

            if (e.Key == Key.Subtract || e.Key == Key.OemMinus)
            {
                ViewModel.DecreaseSelectedQuantity();
                e.Handled = true;
                ReturnFocusToTerminalInput();
                return;
            }

            if (e.Key == Key.Up)
            {
                MoveCartSelection(-1);
                e.Handled = true;
                ReturnFocusToTerminalInput();
                return;
            }

            if (e.Key == Key.Down)
            {
                MoveCartSelection(1);
                e.Handled = true;
                ReturnFocusToTerminalInput();
                return;
            }

            if (e.Key == Key.Home)
            {
                MoveCartSelectionToStart();
                e.Handled = true;
                ReturnFocusToTerminalInput();
                return;
            }

            if (e.Key == Key.End)
            {
                MoveCartSelectionToEnd();
                e.Handled = true;
                ReturnFocusToTerminalInput();
            }
        }

        private void QuantityShortcut()
        {
            if (ViewModel == null)
                return;

            if (HasTerminalInput())
            {
                ViewModel.ApplyTerminalInputAsQuantityToSelected();
                ResetTerminalActionMode();
                ReturnFocusToTerminalInput();
                return;
            }

            SetTerminalActionMode(
                TerminalActionMode.Quantity,
                "QTY",
                "Quantity mode. Type quantity and press Enter.");
        }

        private void FixedDiscountShortcut()
        {
            if (ViewModel == null)
                return;

            if (HasTerminalInput())
            {
                ApplyFixedDiscountFromTerminalInput();
                ResetTerminalActionMode();
                ReturnFocusToTerminalInput();
                return;
            }

            SetTerminalActionMode(
                TerminalActionMode.FixedDiscount,
                "RS DISC",
                "Rs Discount mode. Type amount and press Enter.");
        }

        private void PercentDiscountShortcut()
        {
            if (ViewModel == null)
                return;

            if (HasTerminalInput())
            {
                ApplyPercentDiscountFromTerminalInput();
                ResetTerminalActionMode();
                ReturnFocusToTerminalInput();
                return;
            }

            SetTerminalActionMode(
                TerminalActionMode.PercentDiscount,
                "% DISC",
                "% Discount mode. Type percentage and press Enter.");
        }

        private void NewPriceShortcut()
        {
            if (ViewModel == null)
                return;

            if (HasTerminalInput())
            {
                ApplyNewPriceFromTerminalInput();
                ResetTerminalActionMode();
                ReturnFocusToTerminalInput();
                return;
            }

            SetTerminalActionMode(
                TerminalActionMode.NewPrice,
                "NEW PRICE",
                "New Price mode. Type final price and press Enter.");
        }

        private void ApplyFixedDiscountFromTerminalInput()
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

        private void ApplyPercentDiscountFromTerminalInput()
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

        private void ApplyNewPriceFromTerminalInput()
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

            if (ViewModel != null && CartDataGrid.SelectedItem is CartItem item)
                ViewModel.SelectedCartItem = item;
        }

        private void MoveCartSelectionToStart()
        {
            if (CartDataGrid.Items.Count == 0)
                return;

            CartDataGrid.SelectedIndex = 0;
            CartDataGrid.ScrollIntoView(CartDataGrid.SelectedItem);

            if (ViewModel != null && CartDataGrid.SelectedItem is CartItem item)
                ViewModel.SelectedCartItem = item;
        }

        private void MoveCartSelectionToEnd()
        {
            if (CartDataGrid.Items.Count == 0)
                return;

            CartDataGrid.SelectedIndex = CartDataGrid.Items.Count - 1;
            CartDataGrid.ScrollIntoView(CartDataGrid.SelectedItem);

            if (ViewModel != null && CartDataGrid.SelectedItem is CartItem item)
                ViewModel.SelectedCartItem = item;
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

            ReturnFocusToTerminalInput();
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ClearTerminalInput();
            ResetTerminalActionMode();

            if (ViewModel != null)
                _ = ViewModel.ShowNotificationAsync("Input cleared.", "#F59E0B");

            ReturnFocusToTerminalInput();
        }

        private void BackspaceBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.BackspaceTerminalInput();
            ReturnFocusToTerminalInput();
        }

        private async void EnterBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                if (ExecutePendingTerminalAction())
                {
                    ReturnFocusToTerminalInput();
                    return;
                }

                if (!ViewModel.IsPaymentModeActive &&
                    string.IsNullOrWhiteSpace(ViewModel.TerminalInput))
                {
                    ResetTerminalActionMode();
                    ReturnFocusToTerminalInput();
                    return;
                }

                await ViewModel.HandleTerminalEnterAsync();
                ResetTerminalActionMode();
            }

            ReturnFocusToTerminalInput();
        }

        private void QtyBtn_Click(object sender, RoutedEventArgs e)
        {
            QuantityShortcut();
        }

        private void FixedDiscountBtn_Click(object sender, RoutedEventArgs e)
        {
            FixedDiscountShortcut();
        }

        private void InvoiceDiscountBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            ViewModel?.ApplyTerminalInputAsInvoiceDiscount();
            ReturnFocusToTerminalInput();
        }

        private void PercentDiscountBtn_Click(object sender, RoutedEventArgs e)
        {
            PercentDiscountShortcut();
        }

        private void PlusBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.IncreaseSelectedQuantity();
            ReturnFocusToTerminalInput();
        }

        private void MinusBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.DecreaseSelectedQuantity();
            ReturnFocusToTerminalInput();
        }

        // =========================================================
        // GRID / CART ACTIONS
        // =========================================================

        private void CartDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel != null && CartDataGrid.SelectedItem is CartItem item)
                ViewModel.SelectedCartItem = item;

            ReturnFocusToTerminalInput();
        }

        private void RemoveBtn_Click(object sender, RoutedEventArgs e)
        {
            ConfirmAndRemoveSelectedCartLine();
            ReturnFocusToTerminalInput();
        }

        private void ConfirmAndRemoveSelectedCartLine()
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

            string itemName = string.IsNullOrWhiteSpace(ViewModel.SelectedCartItem.Description)
                ? "selected item"
                : ViewModel.SelectedCartItem.Description;

            MessageBoxResult result = MessageBox.Show(
                $"Remove '{itemName}' from this sale?",
                "Remove Item",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            ViewModel.RemoveSelectedItem();
            ResetTerminalActionMode();
        }

        private async void CancelSaleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (ViewModel.IsPaymentModeActive)
            {
                ViewModel.CancelPaymentMode();
                ResetTerminalActionMode();
                ReturnFocusToTerminalInput();
                return;
            }

            if (ViewModel.Cart.Count == 0)
            {
                ViewModel.ClearTerminalInput();
                ResetTerminalActionMode();
                ReturnFocusToTerminalInput();
                return;
            }

            var dialog = new CartCancellationReasonDialog
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
            {
                ReturnFocusToTerminalInput();
                return;
            }

            try
            {
                await ViewModel.CancelCurrentCartAsync(
                    dialog.ReasonCode,
                    dialog.ReasonText);

                ResetTerminalActionMode();
                await ViewModel.ShowNotificationAsync(
                    "Cart cancelled and recorded for audit.",
                    "#F59E0B");
            }
            catch (Exception ex)
            {
                await ViewModel.ShowNotificationAsync(
                    $"Cart cancellation failed: {ex.Message}",
                    "#EF4444");
            }

            ReturnFocusToTerminalInput();
        }

        // =========================================================
        // PAYMENT FLOW
        // =========================================================

        private void SubTotalBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.EnterPaymentMode();
            ResetTerminalActionMode();
            ReturnFocusToTerminalInput();
        }

        private void PayBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            string buttonText = GetButtonText(sender);

            if (string.IsNullOrWhiteSpace(buttonText))
                return;

            if (buttonText.Equals("Cust Credit", StringComparison.OrdinalIgnoreCase) ||
                buttonText.Equals("Customer Credit", StringComparison.OrdinalIgnoreCase))
            {
                EnsurePaymentModeStarted();
                decimal amount = GetTerminalInputAmountOrZero();
                if (amount <= 0m)
                    amount = ViewModel.BalanceDue;
                ViewModel.AddConfirmedCustomerCreditPayment(amount);
                ReturnFocusToTerminalInput();
                return;
            }

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
            ReturnFocusToTerminalInput();
        }

        private void OpenCashTenderDialog()
        {
            if (ViewModel == null)
                return;

            decimal typedAmount = GetTerminalInputAmountOrZero();

            EnsurePaymentModeStarted();

            if (!ViewModel.IsPaymentModeActive)
                return;

            if (ViewModel.BalanceDue <= 0m)
            {
                _ = ViewModel.ShowNotificationAsync("Invoice is already fully paid.", "#10B981");
                ReturnFocusToTerminalInput();
                return;
            }

            ViewModel.ClearTerminalInput();

            var tenderViewModel = new CashTenderDialogViewModel();
            tenderViewModel.Initialize(ViewModel.BalanceDue, typedAmount);

            var dialog = new CashTenderDialog(tenderViewModel)
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

            ResetTerminalActionMode();
            ReturnFocusToTerminalInput();
        }

        private void OpenCardTenderDialog(string cardType)
        {
            if (ViewModel == null)
                return;

            decimal typedAmount = GetTerminalInputAmountOrZero();

            EnsurePaymentModeStarted();

            if (!ViewModel.IsPaymentModeActive)
                return;

            if (ViewModel.BalanceDue <= 0m)
            {
                _ = ViewModel.ShowNotificationAsync("Invoice is already fully paid.", "#10B981");
                ReturnFocusToTerminalInput();
                return;
            }

            ViewModel.ClearTerminalInput();

            var tenderViewModel = new CardTenderDialogViewModel();
            tenderViewModel.Initialize(cardType, ViewModel.BalanceDue, typedAmount);

            var dialog = new CardTenderDialog(tenderViewModel)
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

            ResetTerminalActionMode();
            ReturnFocusToTerminalInput();
        }

        private void OpenChequeTenderDialog()
        {
            if (ViewModel == null)
                return;

            decimal typedAmount = GetTerminalInputAmountOrZero();

            EnsurePaymentModeStarted();

            if (!ViewModel.IsPaymentModeActive)
                return;

            if (ViewModel.BalanceDue <= 0m)
            {
                _ = ViewModel.ShowNotificationAsync("Invoice is already fully paid.", "#10B981");
                ReturnFocusToTerminalInput();
                return;
            }

            ViewModel.ClearTerminalInput();

            var tenderViewModel = new ChequeTenderDialogViewModel();
            tenderViewModel.Initialize(ViewModel.BalanceDue, typedAmount);

            var dialog = new ChequeTenderDialog(tenderViewModel)
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

            ResetTerminalActionMode();
            ReturnFocusToTerminalInput();
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
                ReturnFocusToTerminalInput();
                return;
            }

            if (ViewModel.Cart.Any(c => c.IsGiftVoucherSale))
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Gift voucher cannot be used to buy another gift voucher.",
                    "#EF4444");

                ReturnFocusToTerminalInput();
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
                    dialog.ForfeitedAmount,
                    dialog.AuthorizedBy);
            }

            ResetTerminalActionMode();
            ReturnFocusToTerminalInput();
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
            _isDialogOpen = true;

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

                _isDialogOpen = false;
                ReturnFocusToTerminalInput();
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
            ReturnFocusToTerminalInput();
        }

        private void CancelPaymentBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.CancelPaymentMode();
            ResetTerminalActionMode();
            ReturnFocusToTerminalInput();
        }

        private void RemovePaymentBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.RemoveSelectedPaymentLine();
            ReturnFocusToTerminalInput();
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

            ReturnFocusToTerminalInput();
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

            ReturnFocusToTerminalInput();
            return true;
        }

        private void SeekBtn_Click(object sender, RoutedEventArgs e)
        {
            if (BlockDialogIfPaymentMode("opening product search"))
                return;

            if (_isDialogOpen)
                return;

            _isDialogOpen = true;

            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var seekDialog = new ProductSeekDialog
                {
                    Owner = this
                };

                seekDialog.ShowDialog();
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;

                _isDialogOpen = false;
                ReturnFocusToTerminalInput();
            }
        }

        private void CustomerBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenCustomerLookupDialog("All");
        }

        private void NewPriceBtn_Click(object sender, RoutedEventArgs e)
        {
            NewPriceShortcut();
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

                ReturnFocusToTerminalInput();
                return;
            }

            if (ViewModel.SelectedCartItem.IsGiftVoucherSale)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Gift voucher sale line cannot use discount rule.",
                    "#EF4444");

                ReturnFocusToTerminalInput();
                return;
            }

            if (ViewModel.SelectedCartItem.IsFreeItem)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Free item line cannot use discount rule.",
                    "#EF4444");

                ReturnFocusToTerminalInput();
                return;
            }

            if (ViewModel.SelectedCartItem.IsPriceOverridden)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Discount rule cannot be applied after New Price.",
                    "#EF4444");

                ReturnFocusToTerminalInput();
                return;
            }

            if (_isDialogOpen)
                return;

            _isDialogOpen = true;

            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                string customerType = GetDiscountCustomerType();

                string approvedBy = ViewModel.IsManagerModeActive
                    ? ViewModel.CashierName
                    : string.Empty;

                var dialog = new DiscountRuleDialog(
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

                ResetTerminalActionMode();
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

                _isDialogOpen = false;
                ReturnFocusToTerminalInput();
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

        private async void SuspendRecallBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (BlockDialogIfPaymentMode("suspend/recall"))
                return;

            if (_isDialogOpen)
                return;

            string action = GetButtonText(sender);

            if (action.Equals("Suspend", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    CashierCartSessionDto held =
                        await ViewModel.SuspendCurrentCartAsync();

                    await ViewModel.ShowNotificationAsync(
                        $"Cart suspended as {held.ReferenceNo}.",
                        "#10B981");
                }
                catch (Exception ex)
                {
                    await ViewModel.ShowNotificationAsync(
                        $"Suspend failed: {ex.Message}",
                        "#EF4444");
                }

                ReturnFocusToTerminalInput();
                return;
            }

            _isDialogOpen = true;

            try
            {
                var heldCarts = await ViewModel.GetHeldCartsAsync();

                if (heldCarts.Count == 0)
                {
                    await ViewModel.ShowNotificationAsync(
                        "There are no held carts for this cashier and shift.",
                        "#F59E0B");
                    return;
                }

                var dialog = new HoldRecallDialog(heldCarts)
                {
                    Owner = this
                };

                if (dialog.ShowDialog() != true || dialog.SelectedCart == null)
                    return;

                if (dialog.RequestedAction == HoldRecallAction.Recall)
                {
                    CashierCartSessionDto recalled =
                        await ViewModel.RecallHeldCartAsync(dialog.SelectedCart.Id);

                    await ViewModel.ShowNotificationAsync(
                        $"Recalled {recalled.ReferenceNo}. Payment must be entered again.",
                        "#10B981");
                    return;
                }

                if (dialog.RequestedAction == HoldRecallAction.Cancel)
                {
                    var reasonDialog = new CartCancellationReasonDialog
                    {
                        Owner = this
                    };

                    if (reasonDialog.ShowDialog() == true)
                    {
                        await ViewModel.CancelHeldCartAsync(
                            dialog.SelectedCart.Id,
                            reasonDialog.ReasonCode,
                            reasonDialog.ReasonText);

                        await ViewModel.ShowNotificationAsync(
                            $"Cancelled held cart {dialog.SelectedCart.ReferenceNo}.",
                            "#F59E0B");
                    }
                }
            }
            catch (Exception ex)
            {
                await ViewModel.ShowNotificationAsync(
                    $"Recall failed: {ex.Message}",
                    "#EF4444");
            }
            finally
            {
                _isDialogOpen = false;
                ReturnFocusToTerminalInput();
            }
        }

        private void FloatCashBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.AddFloatCommand.Execute(null);
            ReturnFocusToTerminalInput();
        }

        private void StockInquiryBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isDialogOpen)
                return;

            _isDialogOpen = true;

            try
            {
                new StockInquiryDialog
                {
                    Owner = this
                }.ShowDialog();
            }
            finally
            {
                _isDialogOpen = false;
                ReturnFocusToTerminalInput();
            }
        }

        private void ReturnBtn_Click(object sender, RoutedEventArgs e)
        {
            if (BlockDialogIfPaymentMode("processing return"))
                return;

            if (_isDialogOpen || ViewModel == null || App.Services == null)
                return;

            _isDialogOpen = true;

            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                CustomerReturnViewModel returnViewModel =
                    App.Services.GetRequiredService<CustomerReturnViewModel>();

                returnViewModel.InitializeContext(
                    ViewModel.CurrentShiftId,
                    ViewModel.TerminalNo,
                    ViewModel.CashierName,
                    ViewModel.ReceiptPrinterName,
                    ViewModel.ReceiptPaperWidth);

                new ReturnInvoiceDialog(returnViewModel)
                {
                    Owner = this
                }.ShowDialog();
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Customer return workflow",
                    ex);

                _ = ViewModel.ShowNotificationAsync(
                    $"Return failed: {ex.Message}",
                    "#EF4444");
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;

                _isDialogOpen = false;
                ReturnFocusToTerminalInput();
            }
        }

        private void ReportsBtn_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Reports dialog coming soon.");
            ReturnFocusToTerminalInput();
        }

        // =========================================================
        // CUSTOMER
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

                ReturnFocusToTerminalInput();
                return;
            }

            if (_isDialogOpen)
                return;

            _isDialogOpen = true;

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
                    ViewModel.AttachCustomer(dialog.SelectedCustomer);

                ResetTerminalActionMode();
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;

                _isDialogOpen = false;
                ReturnFocusToTerminalInput();
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

                ReturnFocusToTerminalInput();
                return;
            }

            if (_isDialogOpen)
                return;

            _isDialogOpen = true;

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
                    ViewModel.AttachCustomer(dialog.SelectedCustomer);

                ResetTerminalActionMode();
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;

                _isDialogOpen = false;
                ReturnFocusToTerminalInput();
            }
        }

        private void DetachCustomerFromSale()
        {
            ViewModel?.DetachCustomer();
            ReturnFocusToTerminalInput();
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
                ReturnFocusToTerminalInput();
                return;
            }

            if (TryReadTerminalDecimal(
                    out decimal amount,
                    "Amount required before Paid In.",
                    "Enter a valid Paid In amount.") &&
                amount > 0m)
            {
                ViewModel.ClearTerminalInput();
                OpenCashMovementDialog("Paid In", amount, ViewModel);
            }

            ResetTerminalActionMode();
            ReturnFocusToTerminalInput();
        }

        private void PaidOutBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (ViewModel.IsPaymentModeActive)
            {
                _ = ViewModel.ShowNotificationAsync("Cancel payment mode before Paid Out.", "#F59E0B");
                ReturnFocusToTerminalInput();
                return;
            }

            if (TryReadTerminalDecimal(
                    out decimal amount,
                    "Amount required before Paid Out.",
                    "Enter a valid Paid Out amount.") &&
                amount > 0m)
            {
                ViewModel.ClearTerminalInput();
                OpenCashMovementDialog("Paid Out", amount, ViewModel);
            }

            ResetTerminalActionMode();
            ReturnFocusToTerminalInput();
        }

        private void OpenCashMovementDialog(string type, decimal amount, SalesViewModel viewModel)
        {
            if (viewModel.CurrentShiftId == 0)
            {
                _ = viewModel.ShowNotificationAsync("Action blocked: No active shift found.", "#EF4444");
                ReturnFocusToTerminalInput();
                return;
            }

            if (_isDialogOpen)
                return;

            _isDialogOpen = true;

            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var cashVM = App.Services!.GetRequiredService<CashMovementViewModel>();
                cashVM.Initialize(type, amount, viewModel.CurrentShiftId, viewModel.CashierName);

                var cashDialog = new CashMovementDialogView(cashVM)
                {
                    Owner = this
                };

                cashDialog.ShowDialog();
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;

                _isDialogOpen = false;
                ReturnFocusToTerminalInput();
            }
        }

        private async void DrawerBtn_Click(object sender, RoutedEventArgs e)
        {
            await OpenManualDrawerAsync(
                CashDrawerEventTypeCodes.ManualOpen,
                "MANUAL DRAWER OPEN",
                new[] { "Cash Count", "Drawer Check", "Hardware Test", "Other" },
                requireEmptyCart: false);
        }

        private async void NoSaleBtn_Click(object sender, RoutedEventArgs e)
        {
            await OpenManualDrawerAsync(
                CashDrawerEventTypeCodes.NoSale,
                "NO SALE DRAWER OPEN",
                new[] { "Make Change", "Cash Count", "Customer Request", "Other" },
                requireEmptyCart: true);
        }

        private async Task OpenManualDrawerAsync(
            string eventType,
            string title,
            string[] reasons,
            bool requireEmptyCart)
        {
            if (ViewModel == null || _isDialogOpen)
                return;

            if (ViewModel.IsPaymentModeActive)
            {
                _ = ViewModel.ShowNotificationAsync("Cancel payment mode before opening the drawer.", "#F59E0B");
                return;
            }

            if (requireEmptyCart && ViewModel.Cart.Any())
            {
                _ = ViewModel.ShowNotificationAsync("Cancel or suspend the current cart before No Sale.", "#F59E0B");
                return;
            }

            _isDialogOpen = true;
            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var reasonDialog = new DrawerReasonDialog(title, reasons) { Owner = this };
                if (reasonDialog.ShowDialog() != true)
                    return;

                ManagerAuthViewModel authViewModel = App.Services!.GetRequiredService<ManagerAuthViewModel>();
                var authDialog = new ManagerAuthDialogView(authViewModel) { Owner = this };
                if (authDialog.ShowDialog() != true)
                    return;

                await ViewModel.OpenAuditedDrawerAsync(
                    eventType,
                    reasonDialog.SelectedReason,
                    reasonDialog.Note,
                    authViewModel.AuthorizedUsername);

                _ = ViewModel.ShowNotificationAsync("Cash drawer opened and audited.", "#10B981");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Cash Drawer", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;
                _isDialogOpen = false;
                ReturnFocusToTerminalInput();
            }
        }

        // =========================================================
        // SYSTEM / SHIFT / OTHER
        // =========================================================

        public void LockTerminal(
            string reason = "Locked manually.")
        {
            _lockService.LockTerminal(reason);
            ReturnFocusToTerminalInput();
        }

        private void LockTerminalBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            LockTerminal(
                "Locked manually from the Sales screen.");
        }

        private void ShiftMenuBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isDialogOpen)
                return;

            _isDialogOpen = true;

            try
            {
                if (ViewModel != null)
                {
                    var shiftMenu = new ShiftMenuView(ViewModel)
                    {
                        Owner = this
                    };

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
            finally
            {
                _isDialogOpen = false;
                ReturnFocusToTerminalInput();
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

            if (_isDialogOpen)
                return;

            _isDialogOpen = true;

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

                _isDialogOpen = false;
                ReturnFocusToTerminalInput();
            }
        }

        private void SellVoucherBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (BlockDialogIfPaymentMode("selling gift voucher"))
                return;

            if (_isDialogOpen)
                return;

            _isDialogOpen = true;

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

                ResetTerminalActionMode();
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

                _isDialogOpen = false;
                ReturnFocusToTerminalInput();
            }
        }

        private void ExpressItemsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (BlockDialogIfPaymentMode("opening express items"))
                return;

            if (_isDialogOpen)
                return;

            _isDialogOpen = true;

            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var expressDialog = new ExpressItemDialogView
                {
                    Owner = this
                };

                expressDialog.ShowDialog();
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;

                _isDialogOpen = false;
                ReturnFocusToTerminalInput();
            }
        }

        private async void FreeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (ViewModel.IsPaymentModeActive)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Cancel payment mode before free item action.",
                    "#F59E0B");

                ReturnFocusToTerminalInput();
                return;
            }

            if (ViewModel.SelectedCartItem == null)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Please select an item to make it free.",
                    "#F59E0B");

                ReturnFocusToTerminalInput();
                return;
            }

            if (ViewModel.SelectedCartItem.IsGiftVoucherSale)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Gift voucher sale line cannot be made free.",
                    "#EF4444");

                ReturnFocusToTerminalInput();
                return;
            }

            if (ViewModel.SelectedCartItem.IsFreeItem)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "This item is already marked as free.",
                    "#F59E0B");

                ReturnFocusToTerminalInput();
                return;
            }

            if (_isDialogOpen)
                return;

            _isDialogOpen = true;

            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                CartItem selectedItem = ViewModel.SelectedCartItem;
                var dialog = new FreeItemReasonModalWindow(
                    selectedItem,
                    ViewModel.Cart.ToList(),
                    ViewModel.CashierName)
                {
                    Owner = this
                };

                bool? result = dialog.ShowDialog();

                if (result == true && dialog.Result != null)
                {
                    FreeItemApplyResult applyResult = dialog.Result;

                    if (applyResult.RequiresManagerApproval || applyResult.RequiresAdminApproval)
                    {
                        ManagerAuthViewModel authViewModel =
                            App.Services!.GetRequiredService<ManagerAuthViewModel>();
                        authViewModel.RequireAdministrator = applyResult.RequiresAdminApproval;

                        var authDialog = new ManagerAuthDialogView(authViewModel)
                        {
                            Owner = this,
                            Title = applyResult.RequiresAdminApproval
                                ? "Administrator Approval — Free Issue"
                                : "Manager Approval — Free Issue"
                        };

                        bool? authenticated = authDialog.ShowDialog();
                        if (authenticated != true || string.IsNullOrWhiteSpace(authViewModel.AuthorizedUsername))
                        {
                            await ViewModel.ShowNotificationAsync(
                                "Free Issue was not applied because approval was cancelled.",
                                "#F59E0B");
                            ResetTerminalActionMode();
                            return;
                        }

                        applyResult.ApprovedBy = authViewModel.AuthorizedUsername;
                        applyResult.ApprovedByUserId = authViewModel.AuthorizedUserId;
                        applyResult.ApprovedRole = authViewModel.AuthorizedRole;
                        applyResult.ApprovedAt = DateTime.Now;
                    }

                    ViewModel.ApplyFreeItemLogic(selectedItem, applyResult);
                }

                ResetTerminalActionMode();
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

                _isDialogOpen = false;
                ReturnFocusToTerminalInput();
            }
        }

        private void LogOffBtn_Click(object sender, RoutedEventArgs e)
        {
            string message = ViewModel?.Cart.Any() == true
                ? "The active cart will be saved for recovery. Log off now?"
                : "Are you sure you want to pause the register and log off?";

            MessageBoxResult result = MessageBox.Show(
                message,
                "Log Off",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
                PerformLogOff();
            else
                ReturnFocusToTerminalInput();
        }

        private async void PerformLogOff()
        {
            if (_isReturningToLogin)
                return;

            _isReturningToLogin = true;

            if (Application.Current is App app)
            {
                bool returned = await app.ReturnToLoginAsync(this);
                if (returned)
                {
                    _lockService.Stop();
                }
                else
                {
                    _isReturningToLogin = false;
                    ReturnFocusToTerminalInput();
                }

                return;
            }

            MessageBox.Show(
                "The Cashier login route is unavailable. " +
                "Close and reopen Cashier.",
                "Log Off Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            _isReturningToLogin = false;
        }

        private void MoreBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenMoreMenu();
        }

        private void OpenMoreMenu()
        {
            if (MoreBtn.ContextMenu == null)
                return;

            MoreBtn.ContextMenu.PlacementTarget = MoreBtn;
            MoreBtn.ContextMenu.Placement = PlacementMode.Bottom;
            MoreBtn.ContextMenu.IsOpen = true;

            ReturnFocusToTerminalInput();
        }

        // =========================================================
        // PRINT
        // =========================================================

        private async void PrintBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isDialogOpen)
                return;

            _isDialogOpen = true;

            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var dialog = new PrintOptionsDialog
                {
                    Owner = this
                };

                bool? result = dialog.ShowDialog();

                if (result != true)
                    return;

                if (dialog.SelectedPrintOption == "LastBill")
                {
                    await ShowLastReceiptWorkflowAsync();
                    return;
                }

                if (dialog.SelectedPrintOption == "TaxInvoice")
                {
                    await ShowTaxInvoiceWorkflowAsync();
                    return;
                }

                if (dialog.SelectedPrintOption == "Quotation")
                {
                    if (ViewModel != null)
                        await ViewModel.PrintCurrentCartQuotationAsync();
                }
            }
            catch (Exception ex)
            {
                HandlePrintWorkflowException(ex);
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;

                _isDialogOpen = false;
                ReturnFocusToTerminalInput();
            }
        }

        private async void PrintLastBillBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            await RunStandalonePrintWorkflowAsync(
                ShowLastReceiptWorkflowAsync);
        }

        private async void PrintQuotationBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            await RunStandalonePrintWorkflowAsync(
                async () =>
                {
                    if (ViewModel != null)
                        await ViewModel.PrintCurrentCartQuotationAsync();
                });
        }

        private async Task RunStandalonePrintWorkflowAsync(
            Func<Task> workflow)
        {
            if (_isDialogOpen)
                return;

            _isDialogOpen = true;

            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                await workflow();
            }
            catch (Exception ex)
            {
                HandlePrintWorkflowException(ex);
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;

                _isDialogOpen = false;
                ReturnFocusToTerminalInput();
            }
        }


        private void HandlePrintWorkflowException(Exception ex)
        {
            LocalLogService.WriteException(
                "Cashier",
                "Sales document preview or print workflow",
                ex);

            if (ViewModel != null)
            {
                _ = ViewModel.ShowNotificationAsync(
                    $"Document workflow failed: {ex.Message}",
                    "#EF4444");
                return;
            }

            MessageBox.Show(
                ex.Message,
                "Document Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private async Task ShowLastReceiptWorkflowAsync()
        {
            if (ViewModel == null)
                return;

            PreparedSalesDocument? document =
                await ViewModel.PrepareLastReceiptAsync();

            if (document == null)
                return;

            string preview =
                await ViewModel.BuildDocumentPreviewAsync(document);

            var dialog = new SalesDocumentPreviewDialog(
                "Sales Receipt Preview",
                preview)
            {
                Owner = this
            };

            bool? result = dialog.ShowDialog();

            if (result == true && dialog.PrintRequested)
                await ViewModel.PrintPreparedDocumentAsync(document);
        }

        private async Task ShowTaxInvoiceWorkflowAsync()
        {
            if (ViewModel == null)
                return;

            SalesHeader? sale =
                await ViewModel.GetLastCompletedSaleAsync();

            if (sale == null)
                return;

            var request = new TaxInvoiceIssueRequest
            {
                SalesHeaderId = sale.Id,
                CustomerName = sale.CustomerName,
                CustomerTin = sale.CustomerTinSnapshot,
                CustomerVatNo = sale.CustomerVatNoSnapshot,
                CustomerAddress = sale.CustomerAddressSnapshot,
                PerformedBy = ViewModel.CashierName,
                TerminalNo = ViewModel.TerminalNo
            };

            if (string.IsNullOrWhiteSpace(sale.TaxInvoiceNo))
            {
                var issueDialog = new TaxInvoiceIssueDialog(sale)
                {
                    Owner = this
                };

                if (issueDialog.ShowDialog() != true)
                    return;

                request.CustomerName =
                    issueDialog.CustomerNameValue;
                request.CustomerTin =
                    issueDialog.CustomerTinValue;
                request.CustomerVatNo =
                    issueDialog.CustomerVatNoValue;
                request.CustomerAddress =
                    issueDialog.CustomerAddressValue;
            }

            PreparedSalesDocument? document =
                await ViewModel.IssueOrPrepareTaxInvoiceAsync(request);

            if (document == null)
                return;

            string preview =
                await ViewModel.BuildDocumentPreviewAsync(document);

            var previewDialog = new SalesDocumentPreviewDialog(
                "Tax Invoice Preview",
                preview)
            {
                Owner = this
            };

            bool? result = previewDialog.ShowDialog();

            if (result == true && previewDialog.PrintRequested)
                await ViewModel.PrintPreparedDocumentAsync(document);
        }
    }
}
