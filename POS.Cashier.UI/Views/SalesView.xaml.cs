using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection; // Required to use the Factory
using POS.Cashier.UI.ViewModels;              // Access to Cashier's own LoginViewModel
using POS.Core.Enums;                         // Access to the shared Roles

namespace POS.Cashier.UI.Views
{
    public partial class SalesView : Window
    {
        public SalesView()
        {
            InitializeComponent();

            // Connect the Brain to the Screen
            this.DataContext = new POS.Cashier.UI.ViewModels.SalesViewModel();
        }

        // --- Contextual Panel Logic ---
        private void CartDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // If the cashier clicks a row, show the panel and grab keyboard focus instantly
            if (CartDataGrid.SelectedItem != null)
            {
                ContextualQtyPanel.Visibility = Visibility.Visible;

                // Set a default value (in a real app, this would pull the actual quantity)
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
        // ------------------------------------

        private void PayBtn_Click(object sender, RoutedEventArgs e)
        {
            POS.Cashier.UI.Dialogs.PayDialogView payWindow = new POS.Cashier.UI.Dialogs.PayDialogView();
            bool? result = payWindow.ShowDialog();

            if (result == true)
            {
                MessageBox.Show("Payment Successful! Ready for next customer.");
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
                // FIXED: We ask the Factory for the LoginViewModel instead of using 'new'
                var loginViewModel = App.Services?.GetService<LoginViewModel>();

                if (loginViewModel != null)
                {
                    // Re-attach the success logic so they can log back in
                    loginViewModel.LoginSuccessful += delegate (UserRole role)
                    {
                        SalesView newSalesWindow = new SalesView();
                        System.Windows.Application.Current.MainWindow = newSalesWindow;
                        newSalesWindow.Show();
                    };
                }

                POS.Cashier.UI.Views.LoginView loginWindow = new POS.Cashier.UI.Views.LoginView
                {
                    DataContext = loginViewModel
                };

                System.Windows.Application.Current.MainWindow = loginWindow;
                loginWindow.Show();

                // Close the current Sales screen
                this.Close();
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
                MessageBox.Show("Cart has been cleared.");
            }
        }
    }
}