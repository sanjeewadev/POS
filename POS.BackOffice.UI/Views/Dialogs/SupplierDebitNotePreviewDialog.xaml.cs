using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.Services;
using POS.Core.Services.Exports;
using System;
using System.Windows;
using System.Windows.Input;

namespace POS.BackOffice.UI.Views.Dialogs
{
    public partial class SupplierDebitNotePreviewDialog : Window
    {
        private readonly string _debitNoteNumber;

        public SupplierDebitNotePreviewDialog(string documentText, string debitNoteNumber)
        {
            InitializeComponent();
            PreviewTextBox.Text = documentText ?? string.Empty;
            _debitNoteNumber = string.IsNullOrWhiteSpace(debitNoteNumber) ? "Supplier_Debit_Note" : debitNoteNumber.Trim();
        }

        private void SavePdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IServiceProvider services = App.Services
                    ?? throw new InvalidOperationException("BackOffice services are not available.");
                var authorization = services.GetRequiredService<ExportAuthorizationService>();
                authorization.EnsureOperationalDocumentAllowed();
                var export = services.GetRequiredService<ExportDialogService>();
                export.SaveTextPdf(
                    "Save Supplier Debit Note PDF",
                    ExportFileNameHelper.Build(_debitNoteNumber, ".pdf"),
                    "SUPPLIER DEBIT NOTE",
                    _debitNoteNumber,
                    PreviewTextBox.Text,
                    authorization.CurrentUsername,
                    "Generated from the immutable supplier-return snapshot.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Supplier Debit Note PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
