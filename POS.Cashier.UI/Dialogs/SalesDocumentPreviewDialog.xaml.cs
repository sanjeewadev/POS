using Microsoft.Extensions.DependencyInjection;
using POS.Core.Services.Exports;
using System;
using System.Windows;
using System.Windows.Input;

namespace POS.Cashier.UI.Dialogs
{
    public partial class SalesDocumentPreviewDialog : Window
    {
        private readonly string _heading;
        private readonly string _documentReference;
        public bool PrintRequested { get; private set; }

        public SalesDocumentPreviewDialog(
            string heading,
            string documentText,
            string? documentReference = null)
        {
            InitializeComponent();
            _heading = string.IsNullOrWhiteSpace(heading) ? "Document" : heading.Trim();
            _documentReference = string.IsNullOrWhiteSpace(documentReference)
                ? _heading
                : documentReference.Trim();
            HeadingText.Text = _heading.ToUpperInvariant();
            PreviewTextBox.Text = documentText ?? string.Empty;
        }

        private void SavePdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IServiceProvider services = App.Services
                    ?? throw new InvalidOperationException("Cashier services are not available.");
                var authorization = services.GetRequiredService<ExportAuthorizationService>();
                authorization.EnsureOperationalDocumentAllowed();
                var pdf = services.GetRequiredService<PdfExportService>();
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = $"Save {_heading} PDF",
                    FileName = ExportFileNameHelper.Create(_heading, _documentReference, "pdf"),
                    Filter = "PDF files (*.pdf)|*.pdf",
                    DefaultExt = ".pdf",
                    AddExtension = true,
                    OverwritePrompt = true
                };
                if (dialog.ShowDialog(this) != true)
                    return;

                pdf.WriteTextPdf(
                    dialog.FileName,
                    _heading.ToUpperInvariant(),
                    string.Empty,
                    PreviewTextBox.Text,
                    authorization.CurrentUsername,
                    "Generated from the completed transaction snapshot.");
                MessageBox.Show("PDF created successfully.", "Save PDF", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Save PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            PrintRequested = true;
            DialogResult = true;
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            PrintRequested = false;
            DialogResult = false;
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
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
