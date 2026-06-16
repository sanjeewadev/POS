using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.ViewModels;
using POS.Core.Enums;

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

            // Pass the Brain AND the Repo into the new Live-Add Dialog
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

        // --- NEW DIALOG ROUTING METHODS ---

        private void CustomerBtn_Click(object sender, RoutedEventArgs e)
            => new POS.Cashier.UI.Views.Dialogs.CustomerManagementDialog().ShowDialog();

        private void DiscountBtn_Click(object sender, RoutedEventArgs e)
            => new POS.Cashier.UI.Views.Dialogs.PriceDiscountDialog().ShowDialog();

        private void PriceOverrideBtn_Click(object sender, RoutedEventArgs e)
            => new POS.Cashier.UI.Views.Dialogs.PriceDiscountDialog().ShowDialog();

        private void SuspendRecallBtn_Click(object sender, RoutedEventArgs e)
            => new POS.Cashier.UI.Views.Dialogs.SuspendedCartsDialog().ShowDialog();

        // Ensure you update your XAML to use Click="AddFloatBtn_Click" if you are not using standard Command bindings for it
        private void AddFloatBtn_Click(object sender, RoutedEventArgs e)
            => new POS.Cashier.UI.Views.Dialogs.AddFloatDialog().ShowDialog();

        // Stubs for future windows
        private void StockInquiryBtn_Click(object sender, RoutedEventArgs e)
            => MessageBox.Show("Stock Inquiry Dialog coming soon.");

        private void ReturnBtn_Click(object sender, RoutedEventArgs e)
            => MessageBox.Show("Return Dialog coming soon.");

        private void ReportsBtn_Click(object sender, RoutedEventArgs e)
            => MessageBox.Show("Reports Dialog coming soon.");

        // --- PAYMENT TRAFFIC COP ---
        private void PayBtn_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is SalesViewModel viewModel)
            {
                if (viewModel.Cart.Count == 0) return;

                string buttonText = (sender as Button)?.Content?.ToString() ?? "";
                Window dialog = null;

                // Route to the correct micro-dialog based on the button clicked
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
                    case "Gift Voucher":
                    case "Credit Note":
                        dialog = new POS.Cashier.UI.Views.Dialogs.DocumentTenderDialog();
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
                    bool? result = dialog.ShowDialog();

                    if (result == true)
                    {
                        // Fire the finalize checkout logic in your ViewModel once the dialog succeeds
                        _ = viewModel.FinalizeCheckoutAsync(buttonText, viewModel.NetValue);
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
            // Update this to use a custom ConfirmDialog later
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
            // 1. Turn on the dark overlay (Screen goes dark instantly)
            DimmingCurtain.Visibility = Visibility.Visible;

            try
            {
                // 2. Open the Cash Movement Dialog (Set to Paid In mode)
                var cashDialog = new POS.Cashier.UI.Dialogs.CashMovementDialogView(POS.Cashier.UI.Dialogs.MovementType.PaidIn);

                // Use ShowDialog() so the code pauses here and waits for the cashier to finish
                cashDialog.ShowDialog();
            }
            finally
            {
                // 3. Turn off the dark overlay (Screen returns to normal)
                // Putting this in a 'finally' block ensures the screen ALWAYS un-dims, 
                // even if the dialog crashes or throws an error!
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
            // 1. Grab the "Brain" (ViewModel) that is currently running this Sales Window
            var currentViewModel = this.DataContext as POS.Cashier.UI.ViewModels.SalesViewModel;

            if (currentViewModel != null)
            {
                // 2. Open the Shift Menu Hub, handing it the Brain so it can read the terminal status
                var shiftMenu = new POS.Cashier.UI.Views.Dialogs.ShiftMenuView(currentViewModel);
                shiftMenu.ShowDialog();
            }
            else
            {
                MessageBox.Show("System Error: Could not locate the Sales configuration.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


    }
}