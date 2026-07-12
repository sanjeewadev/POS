using System.Windows;
using System.Windows.Input;

namespace POS.BackOffice.UI.Views.Dialogs
{
    public partial class SupplierDebitNotePreviewDialog : Window
    {
        public SupplierDebitNotePreviewDialog(string documentText)
        {
            InitializeComponent();
            PreviewTextBox.Text = documentText ?? string.Empty;
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(PreviewTextBox.Text))
                Clipboard.SetText(PreviewTextBox.Text);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            Close();
            e.Handled = true;
        }
    }
}
