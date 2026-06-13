using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.ViewModels;
using POS.Core.Enums;

namespace POS.Cashier.UI.Views
{
    public partial class SalesView : Window
    {
        // ==========================================
        // GHOST SCANNER MEMORY
        // ==========================================
        private StringBuilder _barcodeBuffer = new StringBuilder();
        private DateTime _lastKeystroke = DateTime.Now;

        public SalesView()
        {
            InitializeComponent();

            // CRITICAL FIX: Ask the application factory to build the ViewModel 
            // so it automatically injects your ItemRepository!
            this.DataContext = App.Services.GetRequiredService<SalesViewModel>();
        }

        // ==========================================
        // 1. THE GHOST SCANNER (Character Catcher)
        // Uses TextCompositionEventArgs
        // ==========================================
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

        // ==========================================
        // 2. THE ACTION CATCHER (Enter Key & F-Keys)
        // Uses KeyEventArgs
        // ==========================================
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // A. Did the scanner just finish sending the barcode by hitting 'Enter'?
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

            // B. Global F-Key Shortcuts
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

        // ==========================================
        // 3. UI INTERACTIONS (Contextual Panels)
        // ==========================================
        private void CartDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // If the cashier clicks a row, show the panel and grab keyboard focus instantly
            if (CartDataGrid.SelectedItem != null)
            {
                ContextualQtyPanel.Visibility = Visibility.Visible;

                // Set a default value (in Phase 2, this will pull the actual line item quantity)
                txtQtyInput.Text = "1";

                // Focus and select the text so the cashier can just start typing without backspacing
                txtQtyInput.Focus();
                txtQtyInput.SelectAll();
            }
            else
            {
                // Hide it safely if nothing is selected
                ContextualQtyPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void HideQtyPanel_Click(object sender, RoutedEventArgs e)
        {
            ContextualQtyPanel.Visibility = Visibility.Collapsed;
            CartDataGrid.SelectedItem = null; // Deselect the item to reset the layout
        }

        // ==========================================
        // 4. TRANSACTION NAVIGATION & DIALOGS
        // ==========================================
        private void PayBtn_Click(object sender, RoutedEventArgs e)
        {
            POS.Cashier.UI.Dialogs.PayDialogView payWindow = new POS.Cashier.UI.Dialogs.PayDialogView();
            bool? result = payWindow.ShowDialog();

            if (result == true)
            {
                // We will hook this to the ViewModel later to finalize the DB save
                MessageBox.Show("Payment Successful! Ready for next customer.");
            }
        }

        private void CancelSaleBtn_Click(object sender, RoutedEventArgs e)
        {
            POS.Cashier.UI.Dialogs.ConfirmDialogView cancelWarning = new POS.Cashier.UI.Dialogs.ConfirmDialogView(
                "CANCEL SALE",
                "Are you sure you want to clear this entire sale? This cannot be undone."
            );

            bool? result = cancelWarning.ShowDialog();

            if (result == true)
            {
                // We will hook this to the ViewModel later to wipe the observable collection
                MessageBox.Show("Cart has been cleared.");
            }
        }

        private void PaidInBtn_Click(object sender, RoutedEventArgs e)
        {
            POS.Cashier.UI.Dialogs.CashMovementDialogView paidInWindow =
                new POS.Cashier.UI.Dialogs.CashMovementDialogView(POS.Cashier.UI.Dialogs.MovementType.PaidIn);
            paidInWindow.ShowDialog();
        }

        private void PaidOutBtn_Click(object sender, RoutedEventArgs e)
        {
            POS.Cashier.UI.Dialogs.CashMovementDialogView paidOutWindow =
                new POS.Cashier.UI.Dialogs.CashMovementDialogView(POS.Cashier.UI.Dialogs.MovementType.PaidOut);
            paidOutWindow.ShowDialog();
        }

        private void LogOffBtn_Click(object sender, RoutedEventArgs e)
        {
            POS.Cashier.UI.Dialogs.ConfirmDialogView logOffWarning = new POS.Cashier.UI.Dialogs.ConfirmDialogView(
                "LOG OFF",
                "Are you sure you want to close the register and log off?"
            );

            bool? result = logOffWarning.ShowDialog();

            if (result == true)
            {
                // Ask the DI Factory for the LoginViewModel so it keeps the same AuthService state
                var loginViewModel = App.Services?.GetService<LoginViewModel>();

                if (loginViewModel != null)
                {
                    // Re-attach the success logic so the next cashier can log in seamlessly
                    loginViewModel.LoginSuccessful += delegate (UserRole role)
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

                // Close the current Sales screen
                this.Close();
            }
        }
    }
}