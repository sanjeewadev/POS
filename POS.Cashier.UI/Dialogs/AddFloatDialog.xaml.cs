using System;
using System.Windows;
using System.Windows.Controls;

namespace POS.Cashier.UI.Views.Dialogs
{
    public partial class AddFloatDialog : Window
    {
        // Public properties to pass final state out to parent application layers
        public decimal TotalCashCalculated { get; private set; }
        public string ReferenceNote { get; private set; } = string.Empty;
        public bool IsFloatIn { get; private set; } = true;

        public AddFloatDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Automatically recalculates itemized line totals and global total when any text box updates
        /// </summary>
        private void Qty_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (PnlDenominations == null || LblTotalCashCount == null) return;

            decimal totalSum = 0;

            totalSum += UpdateLineTotal(TxtQty5000, LblTotal5000);
            totalSum += UpdateLineTotal(TxtQty1000, LblTotal1000);
            totalSum += UpdateLineTotal(TxtQty500, LblTotal500);
            totalSum += UpdateLineTotal(TxtQty100, LblTotal100);
            totalSum += UpdateLineTotal(TxtQty50, LblTotal50);
            totalSum += UpdateLineTotal(TxtQty20, LblTotal20);
            totalSum += UpdateLineTotal(TxtQty10, LblTotal10);
            totalSum += UpdateLineTotal(TxtQty5, LblTotal5);

            TotalCashCalculated = totalSum;
            LblTotalCashCount.Text = string.Format("Rs. {0:N2}", totalSum);
        }

        private decimal UpdateLineTotal(TextBox txtQty, TextBlock lblLineTotal)
        {
            if (txtQty == null || lblLineTotal == null) return 0;

            int faceValue = Convert.ToInt32(txtQty.Tag);
            int.TryParse(txtQty.Text, out int qty);

            decimal lineTotal = faceValue * qty;
            lblLineTotal.Text = string.Format("{0:N2}", lineTotal);

            return lineTotal;
        }

        /// <summary>
        /// Handles final submission operations for both Float In and Float Out flows
        /// </summary>
        private void Action_Click(object sender, RoutedEventArgs e)
        {
            if (TotalCashCalculated <= 0)
            {
                MessageBox.Show("Please count and input currency items before saving.", "Empty Drop Balance", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (sender is Button clickedButton)
            {
                // Detect destination target logic path
                IsFloatIn = (clickedButton == BtnFloatIn);
                ReferenceNote = TxtNote.Text;

                this.DialogResult = true;
                this.Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}