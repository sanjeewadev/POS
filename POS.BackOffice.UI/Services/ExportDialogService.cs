using Microsoft.Win32;
using POS.Core.Models.DTOs;
using POS.Core.Services.Exports;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace POS.BackOffice.UI.Services
{
    public sealed class ExportDialogService
    {
        private readonly PdfExportService _pdf;
        private readonly CsvExportService _csv;

        public ExportDialogService(PdfExportService pdf, CsvExportService csv)
        {
            _pdf = pdf ?? throw new ArgumentNullException(nameof(pdf));
            _csv = csv ?? throw new ArgumentNullException(nameof(csv));
        }

        public string? SaveTextPdf(
            string dialogTitle,
            string defaultFileName,
            string documentTitle,
            string subtitle,
            string text,
            string generatedBy,
            string footerText = "")
        {
            string? path = ChoosePath(dialogTitle, defaultFileName, "PDF files (*.pdf)|*.pdf", ".pdf");
            if (path == null)
                return null;

            _pdf.WriteTextPdf(path, documentTitle, subtitle, text, generatedBy, footerText);
            ShowSuccess(path, "PDF Export");
            return path;
        }

        public string? SaveTablePdf(
            string dialogTitle,
            string defaultFileName,
            PdfTableDocumentDto document)
        {
            string? path = ChoosePath(dialogTitle, defaultFileName, "PDF files (*.pdf)|*.pdf", ".pdf");
            if (path == null)
                return null;

            _pdf.WriteTablePdf(path, document);
            ShowSuccess(path, "PDF Export");
            return path;
        }

        public async Task<string?> SaveCsvAsync(
            string dialogTitle,
            string defaultFileName,
            string csv)
        {
            string? path = ChoosePath(dialogTitle, defaultFileName, "CSV files (*.csv)|*.csv", ".csv");
            if (path == null)
                return null;

            await _csv.WriteUtf8BomAsync(path, csv);
            ShowSuccess(path, "CSV Export");
            return path;
        }

        private static string? ChoosePath(string title, string defaultName, string filter, string extension)
        {
            var dialog = new SaveFileDialog
            {
                Title = title,
                FileName = defaultName,
                Filter = filter,
                DefaultExt = extension,
                AddExtension = true,
                OverwritePrompt = true
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private static void ShowSuccess(string path, string title)
        {
            MessageBox.Show(
                $"Export created successfully.\n\n{path}",
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
