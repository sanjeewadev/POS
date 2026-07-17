using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
            ReasonComboBox.SelectedIndex = 0;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ReasonComboBox.Focus();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            DialogResult = false;
            e.Handled = true;
        }

        private void ReasonComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ValidationText != null)
                ValidationText.Text = string.Empty;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (ReasonComboBox.SelectedItem is not ComboBoxItem selected)
            {
                ValidationText.Text = "Select a cancellation reason.";
                ReasonComboBox.Focus();
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
        }
    }
}
