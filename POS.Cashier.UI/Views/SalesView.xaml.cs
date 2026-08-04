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
using System.ComponentModel;
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

        private DispatcherTimer? _clockTimer;

        private enum TerminalActionMode
        {
            Normal,
            Quantity,
            FixedDiscount,
            InvoiceDiscount,
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

            viewModel.PropertyChanged += ViewModel_PropertyChanged;
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

            UpdateNumLockStatus();
            await HandleActiveCartRecoveryAsync();
            ReturnFocusToTerminalInput();

            // start the live clock
            StartClock();
        }

        private void SalesView_Activated(object? sender, EventArgs e)
        {
            UpdateNumLockStatus();
        }

        private void UpdateNumLockStatus()
        {
            if (NumLockWarningBorder == null)
                return;

            NumLockWarningBorder.Visibility = Keyboard.IsKeyToggled(Key.NumLock)
                ? Visibility.Collapsed
                : Visibility.Visible;
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

                var recoveryDialog = new ActiveCartRecoveryDialog(
                    active.ReferenceNo,
                    active.ItemCount,
                    active.NetTotal)
                {
                    Owner = this
                };

                _isDialogOpen = true;
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Visible;
                try
                {
                    recoveryDialog.ShowDialog();
                }
                finally
                {
                    if (DimmingCurtain != null)
                        DimmingCurtain.Visibility = Visibility.Collapsed;
                    _isDialogOpen = false;
                }

                if (recoveryDialog.SelectedAction == ActiveCartRecoveryAction.Resume)
                {
                    await ViewModel.RestoreActiveCartAsync(active);
                    await ViewModel.ShowNotificationAsync(
                        $"Recovered {active.ReferenceNo}. Payment must be entered again.",
                        "#D97706");
                    return;
                }

                if (recoveryDialog.SelectedAction == ActiveCartRecoveryAction.CancelCart)
                {
                    var reasonDialog = new CartCancellationReasonDialog
                    {
                        Owner = this
                    };

                    bool? reasonResult;
                    _isDialogOpen = true;
                    if (DimmingCurtain != null)
                        DimmingCurtain.Visibility = Visibility.Visible;
                    try
                    {
                        reasonResult = reasonDialog.ShowDialog();
                    }
                    finally
                    {
                        if (DimmingCurtain != null)
                            DimmingCurtain.Visibility = Visibility.Collapsed;
                        _isDialogOpen = false;
                    }

                    if (reasonResult == true)
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
            if (ViewModel != null)
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;

            _lockService.Stop();

            _clockTimer?.Stop();
            _clockTimer = null;
        }

        private void ViewModel_PropertyChanged(
            object? sender,
            PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SalesViewModel.SelectedCartItem))
                ScrollSelectedCartLineIntoView();
        }

        private void ScrollSelectedCartLineIntoView()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsVisible || ViewModel?.SelectedCartItem == null)
                    return;

                CartDataGrid.SelectedItem = ViewModel.SelectedCartItem;
                CartDataGrid.ScrollIntoView(ViewModel.SelectedCartItem);
            }), DispatcherPriority.Background);
        }

        private void ReturnFocusToTerminalInput()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsVisible ||
                    _isDialogOpen ||
                    ViewModel?.IsCheckoutInProgress == true)
                {
                    return;
                }

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
                ViewModel.TerminalInputMode = "READY TO SCAN";
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

                case TerminalActionMode.InvoiceDiscount:
                    ViewModel.ApplyTerminalInputAsInvoiceDiscount();
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

            if (ViewModel?.IsCheckoutInProgress == true)
            {
                e.Handled = true;
                return;
            }

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

            if (ViewModel.IsCheckoutInProgress)
            {
                e.Handled = true;
                return;
            }

            if (e.Key == Key.NumLock)
            {
                _ = Dispatcher.BeginInvoke(
                    new Action(UpdateNumLockStatus),
                    DispatcherPriority.Input);
                return;
            }

            bool controlPressed =
                (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool shiftPressed =
                (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            // Ctrl shortcuts retained for existing document functions.
            if (controlPressed)
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

            if (shiftPressed && e.Key == Key.Delete)
            {
                CancelSaleBtn_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                e.Handled = true;

                // Ignore key-repeat so a held Enter cannot submit twice.
                if (e.IsRepeat)
                    return;

                if (ExecutePendingTerminalAction())
                    return;

                // Empty Enter in Scan mode is deliberately a no-op because
                // barcode scanners commonly append Enter automatically.
                if (!ViewModel.IsPaymentModeActive &&
                    string.IsNullOrWhiteSpace(ViewModel.TerminalInput))
                {
                    ResetTerminalActionMode();
                    ReturnFocusToTerminalInput();
                    return;
                }

                if (ShouldCancelForZeroValueSale())
                {
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
                _ = ViewModel.ShowNotificationAsync("Ready to scan.", "#64748B");
                ReturnFocusToTerminalInput();
                return;
            }

            if (e.Key == Key.Delete)
            {
                if (ViewModel.IsPaymentModeActive)
                    ViewModel.RemoveSelectedPaymentLine();
                else
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
                if (shiftPressed)
                    PercentDiscountShortcut();
                else
                    FixedDiscountShortcut();

                e.Handled = true;
                return;
            }

            if (e.Key == Key.F4)
            {
                InvoiceDiscountShortcut();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F5)
            {
                CustomerBtn_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F6)
            {
                SuspendBtn_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F7)
            {
                RecallBtn_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F8)
            {
                await OpenCashTenderDialogAsync();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F9)
            {
                await OpenCardTenderDialogAsync("Card");
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F10)
            {
                SubTotalBtn_Click(this, new RoutedEventArgs());
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
                LockTerminal("Locked manually with F12.");
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Multiply)
            {
                QuantityShortcut();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Add)
            {
                await OpenCashTenderDialogAsync();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Subtract)
            {
                if (ViewModel.IsPaymentModeActive)
                    ViewModel.RemoveSelectedPaymentLine();
                else
                    ConfirmAndRemoveSelectedCartLine();

                e.Handled = true;
                ReturnFocusToTerminalInput();
                return;
            }

            if (e.Key == Key.Divide)
            {
                // Intentionally unassigned in the first keyboard-flow release.
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up || e.Key == Key.Down)
            {
                e.Handled = true;

                if (HasTerminalInput())
                    return;

                if (ViewModel.IsPaymentModeActive)
                    MovePaymentSelection(e.Key == Key.Up ? -1 : 1);
                else
                    MoveCartSelection(e.Key == Key.Up ? -1 : 1);

                ReturnFocusToTerminalInput();
                return;
            }

            if (e.Key == Key.Left || e.Key == Key.Right)
            {
                // Reserved for choices inside dialogs; no destructive action
                // is attached to Left or Right on the main sale screen.
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Home || e.Key == Key.End)
            {
                e.Handled = true;

                if (HasTerminalInput())
                    return;

                if (ViewModel.IsPaymentModeActive)
                {
                    MovePaymentSelectionToBoundary(toStart: e.Key == Key.Home);
                }
                else if (e.Key == Key.Home)
                {
                    MoveCartSelectionToStart();
                }
                else
                {
                    MoveCartSelectionToEnd();
                }

                ReturnFocusToTerminalInput();
            }
        }

        private bool CanStartTerminalAction(
            string actionName,
            bool requireSelectedLine)
        {
            if (ViewModel == null || ViewModel.IsCheckoutInProgress)
                return false;

            if (ViewModel.IsBarcodeProcessing)
            {
                _ = ViewModel.ShowNotificationAsync(
                    $"Wait for the pending scan before {actionName}.",
                    "#F59E0B");

                ReturnFocusToTerminalInput();
                return false;
            }

            if (ViewModel.IsPaymentModeActive)
            {
                _ = ViewModel.ShowNotificationAsync(
                    $"Cancel payment mode before {actionName}.",
                    "#F59E0B");

                ReturnFocusToTerminalInput();
                return false;
            }

            if (requireSelectedLine && ViewModel.SelectedCartItem == null)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Select a sale line first.",
                    "#F59E0B");

                ReturnFocusToTerminalInput();
                return false;
            }

            if (HasTerminalInput())
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Press Enter to scan the current input, or Esc to clear it first.",
                    "#F59E0B");

                ReturnFocusToTerminalInput();
                return false;
            }

            return true;
        }

        private void QuantityShortcut()
        {
            if (!CanStartTerminalAction("changing quantity", requireSelectedLine: true))
                return;

            SetTerminalActionMode(
                TerminalActionMode.Quantity,
                "LINE EDIT - QTY",
                "Quantity mode. Type quantity and press Enter.");
        }

        private void FixedDiscountShortcut()
        {
            if (!CanStartTerminalAction("adding a line discount", requireSelectedLine: true))
                return;

            SetTerminalActionMode(
                TerminalActionMode.FixedDiscount,
                "LINE EDIT - RS DISC",
                "Line discount mode. Type rupee amount and press Enter.");
        }

        private void InvoiceDiscountShortcut()
        {
            if (!CanStartTerminalAction("adding an invoice discount", requireSelectedLine: false))
                return;

            SetTerminalActionMode(
                TerminalActionMode.InvoiceDiscount,
                "INVOICE DISC",
                "Invoice discount mode. Type rupee amount and press Enter.");
        }

        private void PercentDiscountShortcut()
        {
            if (!CanStartTerminalAction("adding a percentage discount", requireSelectedLine: true))
                return;

            SetTerminalActionMode(
                TerminalActionMode.PercentDiscount,
                "LINE EDIT - % DISC",
                "Percentage discount mode. Type percentage and press Enter.");
        }

        private void NewPriceShortcut()
        {
            if (!CanStartTerminalAction("changing price", requireSelectedLine: true))
                return;

            SetTerminalActionMode(
                TerminalActionMode.NewPrice,
                "LINE EDIT - NEW PRICE",
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

            string approvedBy = string.Empty;
            CartItem? selectedItem = ViewModel.SelectedCartItem;
            if (selectedItem != null &&
                (newPrice <= 0m ||
                (selectedItem.MinimumPrice > 0m && newPrice < selectedItem.MinimumPrice)))
            {
                ManagerAuthViewModel authViewModel = App.Services!
                    .GetRequiredService<ManagerAuthViewModel>();
                var authDialog = new ManagerAuthDialogView(authViewModel)
                {
                    Owner = this
                };

                if (newPrice <= 0m)
                {
                    authDialog.Title = "Manager Approval — Zero Price";
                }
                else
                {
                    authDialog.Title = "Manager Approval — Below Minimum Price";
                }

                _isDialogOpen = true;
                try
                {
                    if (authDialog.ShowDialog() != true)
                        return;
                }
                finally
                {
                    _isDialogOpen = false;
                }

                approvedBy = authViewModel.AuthorizedUsername;
            }

            ViewModel.ApplyNewPriceToSelected(newPrice, approvedBy);
        }

        private void MovePaymentSelection(int direction)
        {
            if (ViewModel == null || ViewModel.PaymentLines.Count == 0)
                return;

            int currentIndex = ViewModel.SelectedPaymentLine == null
                ? -1
                : ViewModel.PaymentLines.IndexOf(ViewModel.SelectedPaymentLine);

            currentIndex = Math.Clamp(
                currentIndex < 0 ? 0 : currentIndex + direction,
                0,
                ViewModel.PaymentLines.Count - 1);

            ViewModel.SelectedPaymentLine = ViewModel.PaymentLines[currentIndex];

            // UI grid was removed — selection/scrolling is no longer performed here.
        }

        private void MovePaymentSelectionToBoundary(bool toStart)
        {
            if (ViewModel == null || ViewModel.PaymentLines.Count == 0)
                return;

            int index = toStart ? 0 : ViewModel.PaymentLines.Count - 1;
            ViewModel.SelectedPaymentLine = ViewModel.PaymentLines[index];

            // UI grid removed — no SelectedItem/ScrollIntoView calls.
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
            InvoiceDiscountShortcut();
        }

        private void PercentDiscountBtn_Click(object sender, RoutedEventArgs e)
        {
            PercentDiscountShortcut();
        }

        private async void PlusBtn_Click(object sender, RoutedEventArgs e)
        {
            await OpenCashTenderDialogAsync();
        }

        private void MinusBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.IsPaymentModeActive == true)
                ViewModel.RemoveSelectedPaymentLine();
            else
                ConfirmAndRemoveSelectedCartLine();

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
            if (ViewModel == null || _isDialogOpen)
                return;

            if (ViewModel.IsPaymentModeActive)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Cancel payment mode before removing cart items.",
                    "#F59E0B");

                return;
            }

            CartItem? selectedItem = ViewModel.SelectedCartItem;
            if (selectedItem == null)
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Please select an item to remove.",
                    "#F59E0B");
                return;
            }

            string itemName = string.IsNullOrWhiteSpace(selectedItem.Description)
                ? "Selected item"
                : selectedItem.Description.Trim();
            string detail =
                $"{itemName}\n" +
                $"Quantity: {selectedItem.QuantityUomDisplay}\n" +
                $"Amount: Rs. {selectedItem.FinalLineAmount:N2}";

            _isDialogOpen = true;
            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var dialog = new CashierConfirmationDialog(
                    "Remove Sale Line",
                    "Remove Selected Line",
                    "Remove this line from the current sale?",
                    detail,
                    "Remove Line",
                    "Keep Line")
                {
                    Owner = this
                };

                if (dialog.ShowDialog() != true)
                    return;

                ViewModel.RemoveSelectedItem();
                ResetTerminalActionMode();
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;

                _isDialogOpen = false;
            }
        }

        private async void CancelSaleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null || _isDialogOpen)
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

            _isDialogOpen = true;
            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var dialog = new CartCancellationReasonDialog
                {
                    Owner = this
                };

                if (dialog.ShowDialog() != true)
                    return;

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
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;

                _isDialogOpen = false;
                ReturnFocusToTerminalInput();
            }
        }

        // =========================================================
        // PAYMENT FLOW
        // =========================================================

        private bool CanStartPaymentAction(string actionName)
        {
            if (ViewModel == null || ViewModel.IsCheckoutInProgress)
                return false;

            if (ViewModel.IsBarcodeProcessing)
            {
                _ = ViewModel.ShowNotificationAsync(
                    $"Wait for the pending scan before {actionName}.",
                    "#F59E0B");
                return false;
            }

            if (_terminalActionMode != TerminalActionMode.Normal)
            {
                _ = ViewModel.ShowNotificationAsync(
                    $"Complete or cancel the current line edit before {actionName}.",
                    "#F59E0B");
                return false;
            }

            if (!ViewModel.IsPaymentModeActive && HasTerminalInput())
            {
                _ = ViewModel.ShowNotificationAsync(
                    "Press Enter to scan the current input, or Esc to clear it before payment.",
                    "#F59E0B");
                return false;
            }

            return true;
        }

        private void SubTotalBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!CanStartPaymentAction("opening payment mode"))
            {
                ReturnFocusToTerminalInput();
                return;
            }

            ViewModel?.EnterPaymentMode();
            ResetTerminalActionMode();
            ReturnFocusToTerminalInput();
        }

        private async void PayBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            string buttonText = GetButtonText(sender);

            if (string.IsNullOrWhiteSpace(buttonText))
                return;

            if (buttonText.Equals("Cust Credit", StringComparison.OrdinalIgnoreCase) ||
                buttonText.Equals("Customer Credit", StringComparison.OrdinalIgnoreCase))
            {
                if (!CanStartPaymentAction("adding customer credit"))
                {
                    ReturnFocusToTerminalInput();
                    return;
                }

                EnsurePaymentModeStarted();
                decimal amount = GetTerminalInputAmountOrZero();
                if (amount <= 0m)
                    amount = ViewModel.BalanceDue;

                if (ViewModel.ActiveB2BCustomer == null)
                {
                    await ViewModel.ShowNotificationAsync("Customer Credit requires a selected customer.", "#EF4444");
                    ReturnFocusToTerminalInput();
                    return;
                }

                if (amount <= 0m || amount > ViewModel.BalanceDue)
                {
                    await ViewModel.ShowNotificationAsync("Invalid Customer Credit amount.", "#EF4444");
                    ReturnFocusToTerminalInput();
                    return;
                }

                if (!ViewModel.ActiveB2BCustomer.CanUseCredit)
                {
                    await ViewModel.ShowNotificationAsync(ViewModel.ActiveB2BCustomer.CreditWarningText, "#EF4444");
                    ReturnFocusToTerminalInput();
                    return;
                }

                _isDialogOpen = true;
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Visible;

                try
                {
                    var dialog = new CashierConfirmationDialog(
                        "Confirm Customer Credit",
                        "Apply Customer Credit",
                        $"Apply Rs. {amount:N2} credit for {ViewModel.ActiveB2BCustomer.DisplayName}?",
                        "This action will be recorded and cannot be undone.",
                        "Confirm",
                        "Cancel")
                    {
                        Owner = this
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        ViewModel.AddConfirmedCustomerCreditPayment(amount);
                        await FinalizePaymentIfCompleteAsync();
                    }
                }
                finally
                {
                    if (DimmingCurtain != null)
                        DimmingCurtain.Visibility = Visibility.Collapsed;

                    _isDialogOpen = false;
                }

                ReturnFocusToTerminalInput();
                return;
            }

            if (buttonText.StartsWith("Cash", StringComparison.OrdinalIgnoreCase))
            {
                await OpenCashTenderDialogAsync();
                return;
            }

            if (buttonText.Equals("VISA", StringComparison.OrdinalIgnoreCase))
            {
                await OpenCardTenderDialogAsync("VISA");
                return;
            }

            if (buttonText.Equals("MasterCard", StringComparison.OrdinalIgnoreCase))
            {
                await OpenCardTenderDialogAsync("MasterCard");
                return;
            }

            if (buttonText.Equals("AMEX", StringComparison.OrdinalIgnoreCase))
            {
                await OpenCardTenderDialogAsync("AMEX");
                return;
            }

            if (buttonText.Equals("Cheque", StringComparison.OrdinalIgnoreCase))
            {
                await OpenChequeTenderDialogAsync();
                return;
            }

            if (buttonText.Equals("Gift Voucher", StringComparison.OrdinalIgnoreCase) ||
                buttonText.Equals("GiftVoucher", StringComparison.OrdinalIgnoreCase))
            {
                await OpenGiftVoucherTenderDialogAsync();
                return;
            }

            _ = ViewModel.ShowNotificationAsync($"{buttonText} payment is coming later.", "#F59E0B");
            ReturnFocusToTerminalInput();
        }

        private async Task OpenCashTenderDialogAsync()
        {
            if (ViewModel == null || !CanStartPaymentAction("opening Cash payment"))
            {
                ReturnFocusToTerminalInput();
                return;
            }

            decimal typedAmount = GetTerminalInputAmountOrZero();
            EnsurePaymentModeStarted();

            if (!ViewModel.IsPaymentModeActive)
                return;

            if (ViewModel.BalanceDue <= 0m)
            {
                if (ShouldCancelForZeroValueSale())
                {
                    ReturnFocusToTerminalInput();
                    return;
                }

                await FinalizePaymentIfCompleteAsync();
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

                await FinalizePaymentIfCompleteAsync();
            }

            ResetTerminalActionMode();
            ReturnFocusToTerminalInput();
        }

        private async Task OpenCardTenderDialogAsync(string cardType)
        {
            if (ViewModel == null || !CanStartPaymentAction("opening Card payment"))
            {
                ReturnFocusToTerminalInput();
                return;
            }

            decimal typedAmount = GetTerminalInputAmountOrZero();
            EnsurePaymentModeStarted();

            if (!ViewModel.IsPaymentModeActive)
                return;

            if (ViewModel.BalanceDue <= 0m)
            {
                await FinalizePaymentIfCompleteAsync();
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

                await FinalizePaymentIfCompleteAsync();
            }

            ResetTerminalActionMode();
            ReturnFocusToTerminalInput();
        }

        private async Task OpenChequeTenderDialogAsync()
        {
            if (ViewModel == null || !CanStartPaymentAction("opening Cheque payment"))
            {
                ReturnFocusToTerminalInput();
                return;
            }

            decimal typedAmount = GetTerminalInputAmountOrZero();
            EnsurePaymentModeStarted();

            if (!ViewModel.IsPaymentModeActive)
                return;

            if (ViewModel.BalanceDue <= 0m)
            {
                await FinalizePaymentIfCompleteAsync();
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

                await FinalizePaymentIfCompleteAsync();
            }

            ResetTerminalActionMode();
            ReturnFocusToTerminalInput();
        }

        private async Task OpenGiftVoucherTenderDialogAsync()
        {
            if (ViewModel == null || !CanStartPaymentAction("opening Gift Voucher payment"))
            {
                ReturnFocusToTerminalInput();
                return;
            }

            EnsurePaymentModeStarted();

            if (!ViewModel.IsPaymentModeActive)
                return;

            if (ViewModel.BalanceDue <= 0m)
            {
                await FinalizePaymentIfCompleteAsync();
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

                await FinalizePaymentIfCompleteAsync();
            }

            ResetTerminalActionMode();
            ReturnFocusToTerminalInput();
        }

        private async Task FinalizePaymentIfCompleteAsync()
        {
            if (ViewModel == null ||
                !ViewModel.IsPaymentModeActive ||
                ViewModel.BalanceDue > 0m ||
                !ViewModel.CanConfirmPaymentSale)
            {
                return;
            }

            ResetTerminalActionMode();
            await ViewModel.FinalizeCheckoutAsync();
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

        private async void AlternatePaymentBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            string buttonText = GetButtonText(sender);

            if (buttonText.Equals("Cheque", StringComparison.OrdinalIgnoreCase))
            {
                await OpenChequeTenderDialogAsync();
                return;
            }

            if (buttonText.Equals("Gift Voucher", StringComparison.OrdinalIgnoreCase) ||
                buttonText.Equals("GiftVoucher", StringComparison.OrdinalIgnoreCase))
            {
                await OpenGiftVoucherTenderDialogAsync();
                return;
            }

            _ = ViewModel.ShowNotificationAsync(
                $"{buttonText} is not available for this transaction.",
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
            if (!CanStartTerminalAction("opening product search", requireSelectedLine: false))
                return;

            if (_isDialogOpen || ViewModel == null || App.Services == null)
                return;

            _isDialogOpen = true;

            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var seekViewModel = App.Services.GetRequiredService<PluSearchViewModel>();
                seekViewModel.ConfigurePricingMode(ViewModel.IsWholesaleMode);

                // CONTINUOUS SCANNING MAGIC: Listen for the item and throw it in the cart in the background!
                seekViewModel.ItemSelected += async (result) =>
                {
                    if (result != null)
                    {
                        await ViewModel.AddItemFromSeekAsync(result);
                    }
                };

                var seekDialog = new ProductSeekDialog(seekViewModel)
                {
                    Owner = this
                };

                seekDialog.ShowDialog();

                ResetTerminalActionMode();
            }
            catch (Exception ex)
            {
                _ = ViewModel.ShowNotificationAsync($"Search error: {ex.Message}", "#EF4444");
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

        private async void SuspendBtn_Click(object sender, RoutedEventArgs e)
        {
            await SuspendCurrentCartAsync();
        }

        private async Task SuspendCurrentCartAsync()
        {
            if (ViewModel == null)
                return;

            if (!CanStartTerminalAction(
                    "suspending the sale",
                    requireSelectedLine: false) ||
                _isDialogOpen)
            {
                return;
            }

            try
            {
                CashierCartSessionDto held =
                    await ViewModel.SuspendCurrentCartAsync();

                ResetTerminalActionMode();
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
            finally
            {
                ReturnFocusToTerminalInput();
            }
        }

        private async void RecallBtn_Click(object sender, RoutedEventArgs e)
        {
            await OpenRecallDialogAsync();
        }

        private async Task OpenRecallDialogAsync()
        {
            if (ViewModel == null)
                return;

            if (!CanStartTerminalAction(
                    "recalling a sale",
                    requireSelectedLine: false) ||
                _isDialogOpen)
            {
                return;
            }

            try
            {
                var heldCarts = await ViewModel.GetHeldCartsAsync();

                if (heldCarts.Count == 0)
                {
                    await ViewModel.ShowNotificationAsync(
                        "There are no held carts for this cashier and shift.",
                        "#F59E0B");
            ReturnFocusToTerminalInput();
                    return;
                }

        _isDialogOpen = true;
        if (DimmingCurtain != null)
            DimmingCurtain.Visibility = Visibility.Visible;

        try
                {
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

                ResetTerminalActionMode();
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
        finally
        {
            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Collapsed;

            _isDialogOpen = false;
            ReturnFocusToTerminalInput();
        }
            }
            catch (Exception ex)
            {
                await ViewModel.ShowNotificationAsync(
                    $"Recall failed: {ex.Message}",
                    "#EF4444");
            }
        }

        private void FloatCashBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.AddFloatCommand.Execute(null);
            ReturnFocusToTerminalInput();
        }

        private async void ReloadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null || _isDialogOpen)
                return;

            await ViewModel.ReloadCashierAsync();
            ReturnFocusToTerminalInput();
        }

        private void PricingModeBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.TogglePricingMode();
            ReturnFocusToTerminalInput();
        }

        private void StockInquiryBtn_Click(object sender, RoutedEventArgs e)
        {
            IServiceProvider? services = App.Services;

            if (_isDialogOpen || services == null)
                return;

            _isDialogOpen = true;

            try
            {
                StockInquiryViewModel stockInquiryViewModel =
                    services.GetRequiredService<StockInquiryViewModel>();

                new StockInquiryDialog(stockInquiryViewModel)
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

        // =========================================================
        // CUSTOMER
        // =========================================================

        private void OpenCustomerLookupDialog(string lookupMode = "All")
        {
            if (ViewModel == null)
                return;

            if (!CanStartTerminalAction(
                    "changing customer",
                    requireSelectedLine: false))
            {
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

            if (!CanStartTerminalAction(
                    "changing customer",
                    requireSelectedLine: false))
            {
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

        private bool ShouldCancelForZeroValueSale()
        {
            if (ViewModel == null)
                return false; // Don't cancel if VM is missing

            // This check is for an "instant zero-value sale" that would otherwise complete without any payment lines.
            bool requiresApproval =
                ViewModel.IsPaymentModeActive &&
                ViewModel.NetValue <= 0m &&
                !ViewModel.PaymentLines.Any();

            if (!requiresApproval)
            {
                return false; // Approval not needed, don't cancel.
            }

            _isDialogOpen = true;
            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;
            try
            {
                ManagerAuthViewModel authViewModel = App.Services!.GetRequiredService<ManagerAuthViewModel>();
                var authDialog = new ManagerAuthDialogView(authViewModel) { Owner = this, Title = "Manager Approval — Zero Value Sale" };
                if (authDialog.ShowDialog() != true)
                {
                    // Manager approval was cancelled
                    _ = ViewModel.ShowNotificationAsync("Zero value sale cancelled: Manager approval required.", "#F59E0B");
                    return true; // Yes, cancel the sale.
                }
                // If approved, we can proceed.
                return false; // Don't cancel.
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;
                _isDialogOpen = false;
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
            if (_isDialogOpen || _isReturningToLogin)
                return;

            bool hasActiveCart = ViewModel?.Cart.Any() == true;
            string message = hasActiveCart
                ? "Pause this register and return to the Cashier login screen?"
                : "Return to the Cashier login screen?";
            string detail = hasActiveCart
                ? "The active cart will be saved and offered for recovery at the next login."
                : "The current cashier session will end. The open shift remains unchanged.";

            _isDialogOpen = true;
            if (DimmingCurtain != null)
                DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                var dialog = new CashierConfirmationDialog(
                    "Cashier Log Off",
                    "Log Off Cashier",
                    message,
                    detail,
                    "Log Off",
                    "Stay Signed In",
                    isDangerAction: false)
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                    PerformLogOff();
            }
            finally
            {
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Collapsed;

                _isDialogOpen = false;
                ReturnFocusToTerminalInput();
            }
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
            var btn = (Button)sender;
            var menu = (ContextMenu)FindResource("MoreContextMenu");
            menu.PlacementTarget = btn;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void LockTerminalMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Defer actual navigation/action until after the menu closes
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Reuse your existing handler or perform navigation here
                LockTerminalBtn_Click(this, new RoutedEventArgs());
            }), DispatcherPriority.Background);
        }

        private void ShiftMenuMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ShiftMenuBtn_Click(this, new RoutedEventArgs());
            }), DispatcherPriority.Background);
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
                    if (ViewModel != null)
                    {
                        PreparedSalesDocument? document = await ViewModel.PrepareLastReceiptAsync();
                        if (document != null)
                        {
                            await ShowPreparedDocumentDialogAsync(document);
                        }
                    }
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
            if (_isDialogOpen || ViewModel == null)
                return;

            try
            {
                PreparedSalesDocument? document = await ViewModel.PrepareLastReceiptAsync();

                if (document == null)
                {
                    ReturnFocusToTerminalInput();
                    return;
                }

                _isDialogOpen = true;
                if (DimmingCurtain != null)
                    DimmingCurtain.Visibility = Visibility.Visible;

                try
                {
                    await ShowPreparedDocumentDialogAsync(document);
                }
                finally
                {
                    if (DimmingCurtain != null)
                        DimmingCurtain.Visibility = Visibility.Collapsed;
                    _isDialogOpen = false;
                    ReturnFocusToTerminalInput();
                }
            }
            catch (Exception ex)
            {
                HandlePrintWorkflowException(ex);
            }
        }

        private async void PrintQuotationBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isDialogOpen || ViewModel == null)
                return;

            if (await CanPrintQuotationAsync() == false)
                return;

            await RunWorkflowWithDimmingAsync(ViewModel.PrintCurrentCartQuotationAsync);
        }

        private async Task<bool> CanPrintQuotationAsync()
        {
            if (ViewModel == null) return false;

            if (ViewModel.IsPaymentModeActive)
            {
                await ViewModel.ShowNotificationAsync("Cancel payment mode before printing quotation.", "#F59E0B");
                ReturnFocusToTerminalInput();
                return false;
            }

            if (!ViewModel.Cart.Any())
            {
                await ViewModel.ShowNotificationAsync("Cannot print quotation. Cart is empty.", "#F59E0B");
                ReturnFocusToTerminalInput();
                return false;
            }

            return true;
        }

        private async Task RunWorkflowWithDimmingAsync(Func<Task> workflow)
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
                "Tax Invoice",
                preview,
                document.DocumentNumber)
            {
                Owner = this
            };

            bool? result = previewDialog.ShowDialog();

            if (result == true && previewDialog.PrintRequested)
                await ViewModel.PrintPreparedDocumentAsync(document);
        }

        private async Task ShowPreparedDocumentDialogAsync(PreparedSalesDocument document)
        {
            if (ViewModel == null)
                return;

            string preview =
                await ViewModel.BuildDocumentPreviewAsync(document);

            var dialog = new SalesDocumentPreviewDialog(
                document.DocumentType == SalesDocumentTypes.TaxInvoice ? "Tax Invoice" : "Sales Receipt",
                preview,
                document.DocumentNumber)
            {
                Owner = this
            };

            bool? result = dialog.ShowDialog();

            if (result == true && dialog.PrintRequested)
            {
                await ViewModel.PrintPreparedDocumentAsync(document);
            }
        }

        private void StartClock()
{
    // stop and detach any existing timer to avoid duplicate handlers
    if (_clockTimer != null)
    {
        _clockTimer.Stop();
        _clockTimer.Tick -= ClockTimer_Tick;
    }

    _clockTimer = new DispatcherTimer
    {
        Interval = TimeSpan.FromSeconds(1)
    };
    _clockTimer.Tick += ClockTimer_Tick;
    _clockTimer.Start();
}

private void ClockTimer_Tick(object? sender, EventArgs e)
{
    if (ViewModel != null)
        ViewModel.CurrentDate = DateTime.Now;
}
    }
}
