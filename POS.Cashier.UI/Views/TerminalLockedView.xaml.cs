using System;
using System.Windows;

namespace POS.Cashier.UI.Views
{
    public partial class TerminalLockedView : Window
    {
        // Property to tell the caller if the manager approved the override
        public bool IsOverrideApproved { get; private set; } = false;

        public TerminalLockedView(string lockedCashierName)
        {
            InitializeComponent();
            LockedCashierTxt.Text = lockedCashierName.ToUpper();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            IsOverrideApproved = false;
            this.DialogResult = false;
            this.Close();
        }

        private void OverrideBtn_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to perform a Manager Override? This will allow you to access the locked shift. All actions will be audited.",
                "Confirm Override",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                IsOverrideApproved = true;
                this.DialogResult = true;
                this.Close();
            }
        }
    }
}