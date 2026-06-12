using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace POS.Cashier.UI.Dialogs
{
    public partial class PayDialogView : Window
    {
        // We store the total amount the customer needs to pay here.
        // I put 800.00 as an example.
        private decimal totalDueAmount = 800.00m;

        public PayDialogView()
        {
            InitializeComponent();

            // When the window opens, show the starting balance
            CalculateBalance();
        }

        // ==========================================
        // 1. TAB SWITCHING LOGIC
        // ==========================================

        private void CashTabBtn_Click(object sender, RoutedEventArgs e)
        {
            // Show Cash, Hide Card
            CashPanel.Visibility = Visibility.Visible;
            CardPanel.Visibility = Visibility.Collapsed;
        }

        private void CardTabBtn_Click(object sender, RoutedEventArgs e)
        {
            // Show Card, Hide Cash
            CardPanel.Visibility = Visibility.Visible;
            CashPanel.Visibility = Visibility.Collapsed;
        }

        // ==========================================
        // 2. MATH & CALCULATIONS
        // ==========================================

        private void CalculateBalance()
        {
            // First, try to read the text typed in the Tendered box.
            // "TryParse" safely checks if it is a real number.
            if (decimal.TryParse(TenderedTxt.Text, out decimal tenderedAmount))
            {
                // Calculate the balance (Money given - Money due)
                decimal balance = tenderedAmount - totalDueAmount;

                // If the balance is less than zero, they haven't paid enough yet.
                // We show 0.00 so we don't show a negative number.
                if (balance < 0)
                {
                    BalanceTxt.Text = "0.00";
                }
                else
                {
                    // Update the screen with the correct change!
                    BalanceTxt.Text = balance.ToString("0.00");
                }
            }
            else
            {
                // If the box is empty or has a typing error, set balance to 0
                BalanceTxt.Text = "0.00";
            }
        }

        // ==========================================
        // 3. ACTION BUTTONS (Close the window)
        // ==========================================

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            // Closes the window and tells the main screen the payment was cancelled
            this.DialogResult = false;
            this.Close();
        }

        private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            // You can add code here later to print the receipt!

            // Closes the window and tells the main screen the payment was a success
            this.DialogResult = true;
            this.Close();
        }
    }
}