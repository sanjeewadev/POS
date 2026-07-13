using System;
using System.Collections.Generic;
using System.Windows;

namespace POS.BackOffice.UI.Dialogs
{
    public partial class SupplierClaimActionDialog : Window
    {
        private readonly bool _isSettlement;

        public string PrimaryValue => _isSettlement
            ? TypeComboBox.SelectedItem?.ToString()?.Trim() ?? string.Empty
            : ReasonTextBox.Text.Trim();
        public string ReferenceValue => ReferenceTextBox.Text.Trim();
        public string Remarks => RemarksTextBox.Text.Trim();

        private SupplierClaimActionDialog(bool isSettlement)
        {
            InitializeComponent();
            _isSettlement = isSettlement;
        }

        public static SupplierClaimActionDialog ForSettlement()
        {
            var dialog = new SupplierClaimActionDialog(true);
            dialog.Title = "Settle Supplier Claim";
            dialog.HeadingText.Text = "Settle submitted supplier claim";
            dialog.TypeComboBox.ItemsSource = new List<string>
            {
                "Credit Note", "Replacement Stock", "Cash", "Other"
            };
            dialog.TypeComboBox.SelectedIndex = 0;
            dialog.ReasonTextBox.Visibility = Visibility.Collapsed;
            dialog.ReferenceLabel.Visibility = Visibility.Visible;
            dialog.ReferenceTextBox.Visibility = Visibility.Visible;
            return dialog;
        }

        public static SupplierClaimActionDialog ForRejection()
        {
            var dialog = new SupplierClaimActionDialog(false);
            dialog.Title = "Reject Supplier Claim";
            dialog.HeadingText.Text = "Reject supplier claim";
            dialog.TypeComboBox.Visibility = Visibility.Collapsed;
            dialog.ReasonTextBox.Visibility = Visibility.Visible;
            dialog.ReferenceLabel.Visibility = Visibility.Collapsed;
            dialog.ReferenceTextBox.Visibility = Visibility.Collapsed;
            return dialog;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PrimaryValue))
            {
                MessageBox.Show(
                    _isSettlement ? "Select a settlement type." : "Enter a rejection reason.",
                    Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_isSettlement && string.IsNullOrWhiteSpace(ReferenceValue))
            {
                MessageBox.Show(
                    "Enter the supplier settlement reference.",
                    Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
