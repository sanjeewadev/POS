using System.Windows;
using System.Windows.Controls;
using POS.Core.Configuration;

namespace POS.Cashier.UI.Dialogs
{
    public partial class CartCancellationReasonDialog : Window
    {
        public string ReasonCode { get; private set; } = CashierCartCancellationReasons.CustomerChangedMind;
        public string ReasonText { get; private set; } = "Customer Changed Mind";

        public CartCancellationReasonDialog()
        {
            InitializeComponent();
        }

        private void ReasonComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ValidationText.Text = string.Empty;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (ReasonComboBox.SelectedItem is not ComboBoxItem selected)
            {
                ValidationText.Text = "Select a cancellation reason.";
                return;
            }

            string code = selected.Tag?.ToString() ?? string.Empty;
            string selectedText = selected.Content?.ToString() ?? string.Empty;
            string note = (ReasonTextBox.Text ?? string.Empty).Trim();

            if (code == CashierCartCancellationReasons.Other && string.IsNullOrWhiteSpace(note))
            {
                ValidationText.Text = "A note is required when reason is Other.";
                ReasonTextBox.Focus();
                return;
            }

            ReasonCode = code;
            ReasonText = string.IsNullOrWhiteSpace(note) ? selectedText : note;
            DialogResult = true;
            Close();
        }
    }
}
