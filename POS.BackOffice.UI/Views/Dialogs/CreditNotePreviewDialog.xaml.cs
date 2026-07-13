using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace POS.BackOffice.UI.Views.Dialogs
{
    public partial class CreditNotePreviewDialog : Window
    {
        public CreditNotePreviewDialog(string documentText)
        {
            InitializeComponent();
            PreviewTextBox.Text = documentText ?? string.Empty;
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new System.Windows.Controls.PrintDialog();
                if (dialog.ShowDialog() != true)
                    return;

                var document = new FlowDocument
                {
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    PagePadding = new Thickness(36),
                    ColumnGap = 0,
                    PageWidth = dialog.PrintableAreaWidth,
                    PageHeight = dialog.PrintableAreaHeight,
                    ColumnWidth = dialog.PrintableAreaWidth
                };
                document.Blocks.Add(new Paragraph(new Run(PreviewTextBox.Text))
                {
                    Margin = new Thickness(0)
                });

                dialog.PrintDocument(
                    ((IDocumentPaginatorSource)document).DocumentPaginator,
                    "Customer Credit Note");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Print Credit Note", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(PreviewTextBox.Text))
                Clipboard.SetText(PreviewTextBox.Text);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;
            Close();
            e.Handled = true;
        }
    }
}
