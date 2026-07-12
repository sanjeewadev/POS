using System.Windows;
using System.Windows.Input;

namespace POS.Cashier.UI.Dialogs
{
    public partial class SalesDocumentPreviewDialog : Window
    {
        public bool PrintRequested { get; private set; }

        public SalesDocumentPreviewDialog(
            string heading,
            string documentText)
        {
            InitializeComponent();

            HeadingText.Text =
                string.IsNullOrWhiteSpace(heading)
                    ? "DOCUMENT PREVIEW"
                    : heading.Trim().ToUpperInvariant();

            PreviewTextBox.Text = documentText ?? string.Empty;
        }

        private void Print_Click(
            object sender,
            RoutedEventArgs e)
        {
            PrintRequested = true;
            DialogResult = true;
            Close();
        }

        private void Close_Click(
            object sender,
            RoutedEventArgs e)
        {
            PrintRequested = false;
            DialogResult = false;
            Close();
        }

        private void Window_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            PrintRequested = false;
            DialogResult = false;
            Close();
            e.Handled = true;
        }
    }
}
