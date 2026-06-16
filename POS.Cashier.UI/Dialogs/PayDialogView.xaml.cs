using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace POS.Cashier.UI.Dialogs
{
    public partial class PayDialogView : Window
    {
        // Public property so the main SalesView can pass the actual Cart Total in
        public decimal TotalDueAmount { get; set; } = 0.00m;

        // Upgraded Constructor: Now accepts the actual bill total when opening
        public PayDialogView(decimal netValue)
        {
            InitializeComponent();

            TotalDueAmount = netValue;

            if (TotalDueTxt != null)
            {
                TotalDueTxt.Text = TotalDueAmount.ToString("N2");
            }

            // When the window opens, default to 0.00 and calculate
            if (TenderedTxt != null)
            {
                TenderedTxt.Text = "0.00";
            }

            CalculateBalance();
        }

        // Fallback default constructor
        public PayDialogView()
        {
            InitializeComponent();
            CalculateBalance();
        }

        // ==========================================
        // 1. TAB SWITCHING LOGIC (5 Tabs)
        // ==========================================

        private void ResetTabs()
        {
            // Helper method to reset colors and hide all panels
            CashTabBtn.Background = CardTabBtn.Background = ChequeTabBtn.Background = CreditTabBtn.Background = VoucherTabBtn.Background = (SolidColorBrush)FindResource("HeaderBgBrush");
            CashTabBtn.Foreground = CardTabBtn.Foreground = ChequeTabBtn.Foreground = CreditTabBtn.Foreground = VoucherTabBtn.Foreground = (SolidColorBrush)FindResource("TextInactiveBrush");

            CashPanel.Visibility = CardPanel.Visibility = ChequePanel.Visibility = CreditPanel.Visibility = VoucherPanel.Visibility = Visibility.Collapsed;
        }

        private void CashTabBtn_Click(object sender, RoutedEventArgs e)
        {
            ResetTabs();
            CashTabBtn.Background = (SolidColorBrush)FindResource("ActionBtnBrush");
            CashTabBtn.Foreground = (SolidColorBrush)FindResource("TextLightBrush");
            CashPanel.Visibility = Visibility.Visible;
        }

        private void CardTabBtn_Click(object sender, RoutedEventArgs e)
        {
            ResetTabs();
            CardTabBtn.Background = (SolidColorBrush)FindResource("ActionBtnBrush");
            CardTabBtn.Foreground = (SolidColorBrush)FindResource("TextLightBrush");
            CardPanel.Visibility = Visibility.Visible;
        }

        private void ChequeTabBtn_Click(object sender, RoutedEventArgs e)
        {
            ResetTabs();
            ChequeTabBtn.Background = (SolidColorBrush)FindResource("ActionBtnBrush");
            ChequeTabBtn.Foreground = (SolidColorBrush)FindResource("TextLightBrush");
            ChequePanel.Visibility = Visibility.Visible;
        }

        private void CreditTabBtn_Click(object sender, RoutedEventArgs e)
        {
            ResetTabs();
            CreditTabBtn.Background = (SolidColorBrush)FindResource("ActionBtnBrush");
            CreditTabBtn.Foreground = (SolidColorBrush)FindResource("TextLightBrush");
            CreditPanel.Visibility = Visibility.Visible;
        }

        private void VoucherTabBtn_Click(object sender, RoutedEventArgs e)
        {
            ResetTabs();
            VoucherTabBtn.Background = (SolidColorBrush)FindResource("ActionBtnBrush");
            VoucherTabBtn.Foreground = (SolidColorBrush)FindResource("TextLightBrush");
            VoucherPanel.Visibility = Visibility.Visible;
        }

        // ==========================================
        // 2. MATH & CALCULATIONS
        // ==========================================

        private void CalculateBalance()
        {
            if (TenderedTxt == null || BalanceTxt == null) return;

            if (decimal.TryParse(TenderedTxt.Text, out decimal tenderedAmount))
            {
                decimal balance = tenderedAmount - TotalDueAmount;

                if (balance < 0)
                {
                    BalanceTxt.Text = "0.00";
                }
                else
                {
                    BalanceTxt.Text = balance.ToString("0.00");
                }
            }
            else
            {
                BalanceTxt.Text = "0.00";
            }
        }

        // Optional: Wire this to your TenderedTxt "TextChanged" event in XAML 
        // so it calculates instantly when typing with a physical keyboard
        private void TenderedTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            CalculateBalance();
        }

        // ==========================================
        // 3. ACTION BUTTONS
        // ==========================================

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            // Payment Validation: If paying by cash, ensure they handed over enough money
            if (CashPanel.Visibility == Visibility.Visible)
            {
                if (decimal.TryParse(TenderedTxt.Text, out decimal tenderedAmount))
                {
                    if (tenderedAmount < TotalDueAmount)
                    {
                        MessageBox.Show("Insufficient amount tendered. Please collect the full amount before confirming.", "Payment Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    MessageBox.Show("Invalid tender amount.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // Success - Close dialog and return True to the main Sales screen
            this.DialogResult = true;
            this.Close();
        }
    }
}