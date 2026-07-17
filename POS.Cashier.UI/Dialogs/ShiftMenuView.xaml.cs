using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.Services;
using POS.Cashier.UI.ViewModels;
using POS.Cashier.UI.Views;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Services.Documents;

namespace POS.Cashier.UI.Dialogs
{
    public partial class ShiftMenuView : Window
    {
        private readonly SalesViewModel _viewModel;
        private bool _isOperationInProgress;
        private bool _allowClose;

        public ShiftMenuView(SalesViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            RefreshStatusUI();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            XReportBtn.Focus();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || _isOperationInProgress)
                return;

            Close();
            e.Handled = true;
        }

        private void RefreshStatusUI()
        {
            CashierNameTxt.Text = _viewModel.CashierName;
            ShiftIdTxt.Text = $"#{_viewModel.CurrentShiftId}";
            SecurityStatusTxt.Text = _viewModel.SecurityStatusMode;

            if (_viewModel.IsManagerModeActive)
            {
                SecurityStatusTxt.Foreground = ResourceBrush("CashierDangerBrush");
                ToggleManagerBtn.Background = ResourceBrush("CashierSuccessBrush");
                ToggleManagerBtn.BorderBrush = ResourceBrush("CashierSuccessDarkBrush");
                ToggleManagerTxt.Text = "DROP TO CASHIER MODE";
            }
            else
            {
                SecurityStatusTxt.Foreground = ResourceBrush("CashierSuccessBrush");
                ToggleManagerBtn.Background = ResourceBrush("CashierDangerBrush");
                ToggleManagerBtn.BorderBrush = ResourceBrush("CashierDangerDarkBrush");
                ToggleManagerTxt.Text = "ELEVATE TO MANAGER";
            }
        }

        private Brush ResourceBrush(string key)
        {
            return (Brush)FindResource(key);
        }

        private bool TryBeginOperation(string statusText)
        {
            if (_isOperationInProgress)
                return false;

            _isOperationInProgress = true;
            MenuActionsPanel.IsEnabled = false;
            CloseMenuButton.IsEnabled = false;
            OperationText.Text = statusText;
            OperationText.Visibility = Visibility.Visible;
            return true;
        }

        private void EndOperation()
        {
            _isOperationInProgress = false;
            MenuActionsPanel.IsEnabled = true;
            CloseMenuButton.IsEnabled = true;
            OperationText.Visibility = Visibility.Collapsed;
        }

        private static IServiceProvider GetServices()
        {
            return App.Services
                ?? throw new InvalidOperationException("Cashier services are not initialized.");
        }

        private async void XReportBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!TryBeginOperation("Loading shift summary..."))
                return;

            try
            {
                IServiceProvider services = GetServices();
                TillRepository till = services.GetRequiredService<TillRepository>();
                ShiftCashSummaryDto summary = await till.GetShiftCashSummaryAsync(_viewModel.CurrentShiftId, false)
                    ?? throw new InvalidOperationException("The active shift summary could not be loaded.");

                var dialog = new ShiftSummaryDialog(
                    summary,
                    services.GetRequiredService<IReceiptPrintService>(),
                    services.GetRequiredService<ShiftReportTextFormatter>(),
                    _viewModel.ReceiptPrinterName,
                    _viewModel.ReceiptPaperWidth)
                {
                    Owner = this
                };
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "X Report", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                EndOperation();
            }
        }

        private async void CloseShiftBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.IsCheckoutInProgress)
            {
                MessageBox.Show("Wait for checkout to finish before closing the shift.", "Close Shift", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryBeginOperation("Preparing shift close..."))
                return;

            try
            {
                IServiceProvider services = GetServices();
                TillRepository till = services.GetRequiredService<TillRepository>();
                if (await till.HasOpenCartsAsync(_viewModel.CurrentShiftId))
                {
                    MessageBox.Show(
                        "Complete or cancel every active and suspended cart before closing the shift.",
                        "Open Carts Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var countDialog = new ShiftCloseDialog { Owner = this };
                if (countDialog.ShowDialog() != true)
                    return;

                ShiftCashSummaryDto preview = await till.GetShiftCashSummaryAsync(_viewModel.CurrentShiftId, false)
                    ?? throw new InvalidOperationException("The close preview could not be calculated.");

                var confirmation = new ZReportSummaryDialog(preview.ExpectedCash, countDialog.CountedCash)
                {
                    Owner = this
                };
                if (confirmation.ShowDialog() != true)
                    return;

                decimal variance = decimal.Round(countDialog.CountedCash - preview.ExpectedCash, 2, MidpointRounding.AwayFromZero);
                string authorizedBy = string.Empty;
                if (variance != 0m)
                {
                    ManagerAuthViewModel authViewModel = services.GetRequiredService<ManagerAuthViewModel>();
                    var authDialog = new ManagerAuthDialogView(authViewModel) { Owner = this };
                    if (authDialog.ShowDialog() != true)
                        return;
                    authorizedBy = authViewModel.AuthorizedUsername;
                }

                ShiftCashSummaryDto closed = await till.CloseShiftSafelyAsync(
                    new ShiftCloseRequest
                    {
                        ShiftSessionId = _viewModel.CurrentShiftId,
                        TerminalNo = _viewModel.TerminalNo,
                        CashierName = _viewModel.CashierName,
                        CountedCash = countDialog.CountedCash,
                        ClosedBy = _viewModel.CashierName,
                        AuthorizedBy = authorizedBy,
                        VarianceNote = countDialog.VarianceNote,
                        CloseToken = Guid.NewGuid()
                    });

                try
                {
                    if (string.IsNullOrWhiteSpace(_viewModel.ReceiptPrinterName))
                        throw new InvalidOperationException("No receipt printer is configured.");

                    string text = services.GetRequiredService<ShiftReportTextFormatter>()
                        .FormatZReport(closed, _viewModel.ReceiptPaperWidth);
                    await services.GetRequiredService<IReceiptPrintService>()
                        .PrintTextAsync(text, _viewModel.ReceiptPrinterName, "POS Z Report");
                }
                catch (Exception printEx)
                {
                    MessageBox.Show(
                        $"The shift closed correctly, but the Z Report did not print: {printEx.Message}\n\nIt remains available in BackOffice.",
                        "Z Report Print Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                MessageBox.Show(
                    $"Shift closed.\n\nZ Report: {closed.ZReportNo}\nExpected: Rs. {closed.ExpectedCash:N2}\nCounted: Rs. {closed.CountedCash:N2}\nVariance: Rs. {closed.Variance:N2}",
                    "Shift Closed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                if (Application.Current is App app && Owner is SalesView salesWindow)
                {
                    _allowClose = true;
                    Close();
                    await app.ReturnToLoginAsync(salesWindow);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Close Shift Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (IsVisible)
                    EndOperation();
            }
        }

        private void CustomerPaymentBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!TryBeginOperation("Opening customer payment..."))
                return;

            try
            {
                IServiceProvider services = GetServices();
                CustomerCreditRepository repository = services.GetRequiredService<CustomerCreditRepository>();
                var dialog = new CustomerAccountPaymentDialog(
                    repository,
                    _viewModel.CurrentShiftId,
                    _viewModel.TerminalNo,
                    _viewModel.CashierName)
                {
                    Owner = this
                };
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Customer Payment", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                EndOperation();
            }
        }

        private void LockTerminalBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is not SalesView salesWindow)
            {
                MessageBox.Show("The Cashier lock route is unavailable.", "Terminal Lock", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Close();
            salesWindow.LockTerminal("Locked manually from Shift & Security.");
        }

        private void ToggleManagerBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.IsManagerModeActive)
            {
                _viewModel.SetManagerMode(false);
                RefreshStatusUI();
                return;
            }

            MessageBox.Show("Manager elevation is not enabled as a persistent session mode.", "Manager Mode", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!_isOperationInProgress)
                Close();
        }

        private async void LogOffBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!TryBeginOperation("Preparing log off..."))
                return;

            try
            {
                MessageBoxResult result = MessageBox.Show(
                    "Log off this user?\n\nThe current shift will remain open.",
                    "Log Off",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                _viewModel.SetManagerMode(false);

                if (Application.Current is not App app || Owner is not SalesView salesWindow)
                {
                    MessageBox.Show(
                        "The Cashier login route is unavailable.",
                        "Log Off Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                bool returned = await app.ReturnToLoginAsync(salesWindow);
                if (returned)
                {
                    _allowClose = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Shift menu log off",
                    ex);

                MessageBox.Show(
                    $"The Cashier could not return to login.\n\n{ex.Message}",
                    "Log Off Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                if (IsVisible)
                    EndOperation();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_isOperationInProgress && !_allowClose)
                e.Cancel = true;

            base.OnClosing(e);
        }
    }
}
