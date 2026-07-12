using System;
using System.Windows;
using POS.Cashier.UI.Services;
using POS.Core.Models.DTOs;
using POS.Core.Services.Documents;

namespace POS.Cashier.UI.Dialogs
{
    public partial class ShiftSummaryDialog : Window
    {
        private readonly ShiftCashSummaryDto _summary;
        private readonly IReceiptPrintService _printService;
        private readonly ShiftReportTextFormatter _formatter;
        private readonly string _printerName;
        private readonly int _paperWidth;

        public ShiftSummaryDialog(
            ShiftCashSummaryDto summary,
            IReceiptPrintService printService,
            ShiftReportTextFormatter formatter,
            string printerName,
            int paperWidth)
        {
            _summary = summary ?? throw new ArgumentNullException(nameof(summary));
            _printService = printService ?? throw new ArgumentNullException(nameof(printService));
            _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
            _printerName = (printerName ?? string.Empty).Trim();
            _paperWidth = paperWidth;

            InitializeComponent();
            DataContext = summary;
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e) => Close();

        private async void PrintBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_printerName))
                    throw new InvalidOperationException("No receipt printer is configured in Terminal Settings.");

                string text = _formatter.FormatXReport(_summary, _paperWidth);
                await _printService.PrintTextAsync(text, _printerName, "POS X Report");
                MessageBox.Show("X Report printed. The shift remains open.", "X Report", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"X Report could not be printed: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
