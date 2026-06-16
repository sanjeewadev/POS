using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using POS.Core.Interfaces;

namespace POS.Cashier.UI.Dialogs
{
    public enum MovementType
    {
        PaidIn,
        PaidOut
    }

    public partial class CashMovementDialogView : Window
    {
        private MovementType _currentMode;
        private readonly ICashMovementService _cashService;
        private readonly IReceiptPrinterService _printerService; // ADDED: Hardware Service

        // In a real app, you'd get these from your current logged-in session state
        private int _currentShiftSessionId = 1;
        private string _currentCashierName = "Cashier Pos";

        public CashMovementDialogView(MovementType mode)
        {
            InitializeComponent();
            _currentMode = mode;

            // Resolve both the Database Service and the Hardware Service
            _cashService = App.Services!.GetRequiredService<ICashMovementService>();
            _printerService = App.Services!.GetRequiredService<IReceiptPrinterService>();

            SetupWindowUI();
        }

        private void SetupWindowUI()
        {
            AmountTxt.Text = "0";

            if (_currentMode == MovementType.PaidIn)
            {
                HeaderTxt.Text = "PAID IN (RECEIVE CASH)";
                AmountTxt.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#059669"));

                ReasonCombo.Items.Add("Customer Account Payment");
                ReasonCombo.Items.Add("Owner Injection");
                ReasonCombo.Items.Add("Change / Coin Exchange");
            }
            else if (_currentMode == MovementType.PaidOut)
            {
                HeaderTxt.Text = "PAID OUT (REMOVE CASH)";
                AmountTxt.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC3545"));

                ManagerPinPanel.Visibility = Visibility.Visible;

                ReasonCombo.Items.Add("Supplier Payout");
                ReasonCombo.Items.Add("Safe Drop / Bank Deposit");
                ReasonCombo.Items.Add("Store Expenses (Cleaning/Water)");
                ReasonCombo.Items.Add("Owner Draw");
            }

            ReasonCombo.SelectedIndex = 0;
        }

        private void NumpadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content != null)
            {
                string value = btn.Content.ToString()!;

                if (value == "C")
                {
                    AmountTxt.Text = "0";
                    return;
                }

                if (value == ".")
                {
                    if (!AmountTxt.Text.Contains("."))
                        AmountTxt.Text += ".";
                    return;
                }

                if (AmountTxt.Text == "0")
                {
                    AmountTxt.Text = value;
                }
                else
                {
                    if (AmountTxt.Text.Contains("."))
                    {
                        string[] parts = AmountTxt.Text.Split('.');
                        if (parts[1].Length >= 2) return;
                    }

                    AmountTxt.Text += value;
                }
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private async void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(AmountTxt.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("Please enter a valid amount greater than zero.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ReasonCombo.SelectedItem == null)
            {
                MessageBox.Show("Please select a Reason Category.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string category = ReasonCombo.SelectedItem.ToString()!;
            string remarks = RemarksTxt.Text.Trim();
            string authorizedBy = _currentCashierName;

            ConfirmBtn.IsEnabled = false;

            try
            {
                if (_currentMode == MovementType.PaidOut)
                {
                    string pin = ManagerPinBox.Password;
                    if (string.IsNullOrWhiteSpace(pin))
                    {
                        MessageBox.Show("Manager PIN is required to remove cash from the drawer.", "Security Check", MessageBoxButton.OK, MessageBoxImage.Error);
                        ConfirmBtn.IsEnabled = true;
                        return;
                    }

                    bool isAuthorized = await _cashService.VerifyManagerPinAsync(pin);
                    if (!isAuthorized)
                    {
                        MessageBox.Show("Invalid Manager PIN.", "Security Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                        ManagerPinBox.Clear();
                        ConfirmBtn.IsEnabled = true;
                        return;
                    }

                    authorizedBy = "Manager";
                }

                string typeString = _currentMode == MovementType.PaidIn ? "Paid In" : "Paid Out";

                // 1. Save to Database
                var movementRecord = await _cashService.ProcessMovementAsync(
                    _currentShiftSessionId,
                    typeString,
                    amount,
                    category,
                    remarks,
                    _currentCashierName,
                    authorizedBy);

                // 2. Trigger Hardware (Kick Drawer & Print Slip)
                _printerService.OpenCashDrawer();
                _printerService.PrintCashVoucher(movementRecord);

                this.DialogResult = true;
                this.Close();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Overdraft Prevented", MessageBoxButton.OK, MessageBoxImage.Stop);
                ConfirmBtn.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"A system error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ConfirmBtn.IsEnabled = true;
            }
        }
    }
}