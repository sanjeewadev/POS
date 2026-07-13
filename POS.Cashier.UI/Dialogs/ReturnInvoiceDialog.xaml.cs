using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using POS.Cashier.UI.ViewModels;
using POS.Core.Models.DTOs;
using POS.Core.Services;

namespace POS.Cashier.UI.Dialogs
{
    public partial class ReturnInvoiceDialog : Window
    {
        private readonly CustomerReturnViewModel _viewModel;
        private bool _isWorking;

        public ReturnInvoiceDialog(CustomerReturnViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel ??
                throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;

            Loaded += (_, _) => InvoiceSearchTextBox.Focus();
        }

        private async void FindInvoiceButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await RunAsync(_viewModel.LoadInvoiceAsync);
        }

        private async void InvoiceSearchTextBox_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            e.Handled = true;
            await RunAsync(_viewModel.LoadInvoiceAsync);
        }

        private async void CompleteReturnButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isWorking)
                return;

            MessageBoxResult confirmation = MessageBox.Show(
                $"Complete a return settlement of LKR {_viewModel.TotalRefundAmount:N2}?\n\n" +
                "The settlement will follow the original Customer Credit, Gift Voucher and Cash funding. " +
                "The original sale will remain unchanged and a Credit Note will be created.",
                "Confirm Customer Return",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
                return;

            _isWorking = true;
            try
            {
                CustomerReturnProcessResult result =
                    await _viewModel.ProcessReturnAsync();

                string preview =
                    await _viewModel.BuildCreditNotePreviewAsync(
                        result.ReturnHeader);

                var previewDialog = new SalesDocumentPreviewDialog(
                    "Customer Credit Note",
                    preview,
                    result.CreditNoteNo)
                {
                    Owner = this
                };

                bool? previewResult = previewDialog.ShowDialog();

                if (previewResult == true && previewDialog.PrintRequested)
                {
                    try
                    {
                        await _viewModel.PrintCreditNoteAsync(
                            result.ReturnHeader);
                    }
                    catch (Exception printException)
                    {
                        LocalLogService.WriteException(
                            "Cashier",
                            "Customer Credit Note print",
                            printException);

                        MessageBox.Show(
                            "The return was completed, but the Credit Note could not be printed.\n\n" +
                            printException.Message,
                            "Print Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }

                string settlementMessage =
                    $"Return completed.\nCredit Note: {result.CreditNoteNo}\n" +
                    $"Account credit: LKR {result.AccountCreditAmount:N2}\n" +
                    $"Replacement voucher: LKR {result.GiftVoucherRefundAmount:N2}" +
                    (string.IsNullOrWhiteSpace(result.ReplacementGiftVoucherNo)
                        ? string.Empty
                        : $" ({result.ReplacementGiftVoucherNo})") +
                    $"\nCash refund: LKR {result.CashRefundAmount:N2}";

                MessageBox.Show(
                    settlementMessage,
                    "Customer Return Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Customer return",
                    ex);

                MessageBox.Show(
                    ex.Message,
                    "Customer Return Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _isWorking = false;
            }
        }

        private void CancelButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async Task RunAsync(Func<Task> action)
        {
            if (_isWorking)
                return;

            _isWorking = true;
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Customer return invoice lookup",
                    ex);

                MessageBox.Show(
                    ex.Message,
                    "Customer Return",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                _isWorking = false;
            }
        }
    }
}
