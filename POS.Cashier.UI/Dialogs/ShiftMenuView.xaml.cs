using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.Services;
using POS.Cashier.UI.ViewModels;
using POS.Cashier.UI.Views;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services.Documents;

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
            CashierNameTxt.Text = _viewModel.CashierName;
            ShiftIdTxt.Text = $"#{_viewModel.CurrentShiftId}";
            SecurityStatusTxt.Text = _viewModel.SecurityStatusMode;

            if (_viewModel.IsManagerModeActive)
            {
                SecurityStatusTxt.Foreground = Brush("#DC3545");
                ToggleManagerBtn.Background = Brush("#28A745");
                ToggleManagerTxt.Text = "DROP TO CASHIER MODE";
            }
            else
            {
                SecurityStatusTxt.Foreground = Brush("#28A745");
                ToggleManagerBtn.Background = Brush("#DC3545");
                ToggleManagerTxt.Text = "ELEVATE TO MANAGER";
            }
        }

        private static System.Windows.Media.SolidColorBrush Brush(string value) =>
            new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value));

        private async void XReportBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TillRepository till = App.Services!.GetRequiredService<TillRepository>();
                ShiftCashSummaryDto summary = await till.GetShiftCashSummaryAsync(_viewModel.CurrentShiftId, false)
                    ?? throw new InvalidOperationException("The active shift summary could not be loaded.");

                var dialog = new ShiftSummaryDialog(
                    summary,
                    App.Services.GetRequiredService<IReceiptPrintService>(),
                    App.Services.GetRequiredService<ShiftReportTextFormatter>(),
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
        }

        private async void CloseShiftBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.IsCheckoutInProgress)
            {
                MessageBox.Show("Wait for checkout to finish before closing the shift.", "Close Shift", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                TillRepository till = App.Services!.GetRequiredService<TillRepository>();
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
                    ManagerAuthViewModel authViewModel = App.Services.GetRequiredService<ManagerAuthViewModel>();
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

                    string text = App.Services.GetRequiredService<ShiftReportTextFormatter>()
                        .FormatZReport(closed, _viewModel.ReceiptPaperWidth);
                    await App.Services.GetRequiredService<IReceiptPrintService>()
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
                    Close();
                    await app.ReturnToLoginAsync(salesWindow);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Close Shift Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CustomerPaymentBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CustomerCreditRepository repository = App.Services!.GetRequiredService<CustomerCreditRepository>();
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

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

        private async void LogOffBtn_Click(object sender, RoutedEventArgs e)
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
                MessageBox.Show("The Cashier login route is unavailable.", "Log Off Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            bool returned = await app.ReturnToLoginAsync(salesWindow);
            if (returned)
                Close();
        }
    }
}
