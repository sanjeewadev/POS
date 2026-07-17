using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using POS.Cashier.UI.Services;
using POS.Core.Models.DTOs;
using POS.Core.Services;
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
        private bool _isPrinting;

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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PrintButton.Focus();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || _isPrinting)
                return;

            Close();
            e.Handled = true;
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!_isPrinting)
                Close();
        }

        private async void PrintBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isPrinting)
                return;

            SetPrintingState(true);

            try
            {
                if (string.IsNullOrWhiteSpace(_printerName))
                    throw new InvalidOperationException("No receipt printer is configured in Terminal Settings.");

                string text = _formatter.FormatXReport(_summary, _paperWidth);
                await _printService.PrintTextAsync(text, _printerName, "POS X Report");
                MessageBox.Show(
                    "X Report printed. The shift remains open.",
                    "X Report",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException("Cashier", "Print X Report", ex);
                MessageBox.Show(
                    $"X Report could not be printed.\n\n{ex.Message}",
                    "Print Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetPrintingState(false);
            }
        }

        private void SetPrintingState(bool isPrinting)
        {
            _isPrinting = isPrinting;
            PrintButton.IsEnabled = !isPrinting;
            CloseButton.IsEnabled = !isPrinting;
            PrintStatusText.Visibility = isPrinting ? Visibility.Visible : Visibility.Collapsed;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_isPrinting)
                e.Cancel = true;

            base.OnClosing(e);
        }
    }
}
