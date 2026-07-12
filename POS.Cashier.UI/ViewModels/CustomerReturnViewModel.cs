using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using POS.Cashier.UI.Services;
using POS.Core.Configuration;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services.Returns;

namespace POS.Cashier.UI.ViewModels
{
    public partial class CustomerReturnViewModel : ObservableObject
    {
        private readonly CustomerReturnRepository _repository;
        private readonly CustomerReturnAllocationCalculator _allocationCalculator;
        private readonly IReceiptPrintService _printService;

        private int _shiftSessionId;
        private string _terminalNo = string.Empty;
        private string _cashierName = string.Empty;
        private string _printerName = string.Empty;
        private int _paperWidth = 80;

        [ObservableProperty] private string _invoiceSearchText = string.Empty;
        [ObservableProperty] private string _invoiceSummary = "Enter a completed invoice number.";
        [ObservableProperty] private string _returnReason = "Customer return";
        [ObservableProperty] private string _authorizedBy = string.Empty;
        [ObservableProperty] private decimal _totalRefundAmount;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private CustomerReturnInvoiceDto? _loadedInvoice;

        public ObservableCollection<CustomerReturnLineEntry> InvoiceLines { get; } = new();

        public CustomerReturnViewModel(
            CustomerReturnRepository repository,
            CustomerReturnAllocationCalculator allocationCalculator,
            IReceiptPrintService printService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _allocationCalculator = allocationCalculator ?? throw new ArgumentNullException(nameof(allocationCalculator));
            _printService = printService ?? throw new ArgumentNullException(nameof(printService));
        }

        public void InitializeContext(
            int shiftSessionId,
            string terminalNo,
            string cashierName,
            string printerName,
            int paperWidth)
        {
            _shiftSessionId = shiftSessionId;
            _terminalNo = (terminalNo ?? string.Empty).Trim();
            _cashierName = (cashierName ?? string.Empty).Trim();
            _printerName = (printerName ?? string.Empty).Trim();
            _paperWidth = paperWidth <= 58 ? 58 : 80;
            AuthorizedBy = _cashierName;
        }

        public async Task LoadInvoiceAsync()
        {
            if (IsBusy)
                return;

            string invoiceNo = (InvoiceSearchText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(invoiceNo))
                throw new InvalidOperationException("Enter the original invoice number.");

            IsBusy = true;
            try
            {
                CustomerReturnInvoiceDto? invoice =
                    await _repository.FindCompletedSaleAsync(invoiceNo);

                LoadedInvoice = invoice;
                ClearLines();

                if (invoice == null)
                {
                    InvoiceSummary = "Completed invoice not found.";
                    return;
                }

                foreach (CustomerReturnableLineDto line in invoice.Lines)
                {
                    var entry = new CustomerReturnLineEntry(
                        line,
                        _allocationCalculator);
                    entry.PropertyChanged += Entry_PropertyChanged;
                    InvoiceLines.Add(entry);
                }

                InvoiceSummary =
                    $"{invoice.InvoiceNo} | {invoice.TransactionDate:yyyy-MM-dd HH:mm} | " +
                    $"{invoice.CustomerName} | Net {invoice.NetTotal:N2}";

                RecalculateTotal();
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task<CustomerReturnProcessResult> ProcessReturnAsync()
        {
            if (IsBusy)
                throw new InvalidOperationException("The return is already being processed.");

            CustomerReturnInvoiceDto invoice = LoadedInvoice
                ?? throw new InvalidOperationException("Find the original invoice first.");

            CustomerReturnLineEntry[] selected = InvoiceLines
                .Where(line => line.IsSelected && line.ReturnQuantity > 0m)
                .ToArray();

            if (selected.Length == 0)
                throw new InvalidOperationException("Select at least one return line and enter a quantity.");

            if (selected.Any(line => !line.Source.IsReturnable))
                throw new InvalidOperationException("A selected line is not returnable.");

            if (string.IsNullOrWhiteSpace(ReturnReason))
                throw new InvalidOperationException("Enter a return reason.");

            if (string.IsNullOrWhiteSpace(AuthorizedBy))
                throw new InvalidOperationException("Enter the authorized user.");

            IsBusy = true;
            try
            {
                return await _repository.ProcessReturnAsync(
                    new CustomerReturnRequest
                    {
                        SalesHeaderId = invoice.SalesHeaderId,
                        ShiftSessionId = _shiftSessionId,
                        TerminalNo = _terminalNo,
                        CashierName = _cashierName,
                        AuthorizedBy = AuthorizedBy,
                        ReturnReason = ReturnReason,
                        Lines = selected
                            .Select(line => new CustomerReturnRequestLine
                            {
                                SalesLineId = line.Source.SalesLineId,
                                Quantity = line.ReturnQuantity
                            })
                            .ToList()
                    });
            }
            finally
            {
                IsBusy = false;
            }
        }

        public Task<string> BuildCreditNotePreviewAsync(
            CustomerReturnHeader returnHeader)
        {
            return _printService.BuildCreditNotePreviewAsync(
                returnHeader,
                _paperWidth,
                SalesDocumentCopyLabels.Original);
        }

        public Task PrintCreditNoteAsync(
            CustomerReturnHeader returnHeader)
        {
            if (string.IsNullOrWhiteSpace(_printerName))
                throw new InvalidOperationException("Receipt printer is not configured.");

            return _printService.PrintCreditNoteAsync(
                returnHeader,
                _printerName,
                _paperWidth,
                SalesDocumentCopyLabels.Original);
        }

        private void Entry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CustomerReturnLineEntry.IsSelected) ||
                e.PropertyName == nameof(CustomerReturnLineEntry.ReturnQuantity) ||
                e.PropertyName == nameof(CustomerReturnLineEntry.EstimatedRefundAmount))
            {
                RecalculateTotal();
            }
        }

        private void RecalculateTotal()
        {
            TotalRefundAmount = decimal.Round(
                InvoiceLines
                    .Where(line => line.IsSelected)
                    .Sum(line => line.EstimatedRefundAmount),
                2,
                MidpointRounding.AwayFromZero);
        }

        private void ClearLines()
        {
            foreach (CustomerReturnLineEntry line in InvoiceLines)
                line.PropertyChanged -= Entry_PropertyChanged;

            InvoiceLines.Clear();
            TotalRefundAmount = 0m;
        }
    }

    public partial class CustomerReturnLineEntry : ObservableObject
    {
        private readonly CustomerReturnAllocationCalculator _calculator;

        public CustomerReturnableLineDto Source { get; }

        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private decimal _returnQuantity;
        [ObservableProperty] private decimal _estimatedRefundAmount;
        [ObservableProperty] private string _validationMessage = string.Empty;

        public string ItemDescription => Source.ItemDescription;
        public string ItemType => Source.ItemType;
        public string BatchNo => Source.BatchNo;
        public string Uom => Source.Uom;
        public decimal SoldQuantity => Source.SoldQuantity;
        public decimal PreviouslyReturnedQuantity => Source.PreviouslyReturnedQuantity;
        public decimal RemainingQuantity => Source.RemainingQuantity;
        public bool IsReturnable => Source.IsReturnable;
        public string BlockReason => Source.BlockReason;

        public CustomerReturnLineEntry(
            CustomerReturnableLineDto source,
            CustomerReturnAllocationCalculator calculator)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
            ValidationMessage = source.BlockReason;
        }

        partial void OnIsSelectedChanged(bool value)
        {
            if (!value)
            {
                ReturnQuantity = 0m;
                EstimatedRefundAmount = 0m;
                return;
            }

            if (!Source.IsReturnable)
            {
                IsSelected = false;
                ValidationMessage = Source.BlockReason;
                return;
            }

            if (ReturnQuantity <= 0m)
                ReturnQuantity = Source.RemainingQuantity;
        }

        partial void OnReturnQuantityChanged(decimal value)
        {
            if (value <= 0m)
            {
                EstimatedRefundAmount = 0m;
                ValidationMessage = Source.BlockReason;
                return;
            }

            try
            {
                CustomerReturnAllocationResult allocation =
                    _calculator.Calculate(
                        new CustomerReturnAllocationInput
                        {
                            SoldQuantity = Source.SoldQuantity,
                            PreviouslyReturnedQuantity = Source.PreviouslyReturnedQuantity,
                            RequestedQuantity = value,
                            OriginalGrossAmount = Source.OriginalGrossAmount,
                            OriginalDiscountAmount = Source.OriginalDiscountAmount,
                            OriginalRefundAmount = Source.OriginalRefundAmount,
                            PreviouslyRefundedAmount = Source.PreviouslyRefundedAmount,
                            OriginalTaxableAmount = Source.OriginalTaxableAmount,
                            OriginalVatAmount = Source.OriginalVatAmount,
                            OriginalTaxInclusiveAmount = Source.OriginalTaxInclusiveAmount,
                            PreviouslyReturnedTaxableAmount = Source.PreviouslyReturnedTaxableAmount,
                            PreviouslyReturnedVatAmount = Source.PreviouslyReturnedVatAmount,
                            PreviouslyReturnedTaxInclusiveAmount = Source.PreviouslyReturnedTaxInclusiveAmount,
                            TaxSnapshotStatus = Source.TaxSnapshotStatus
                        });

                EstimatedRefundAmount = allocation.RefundAmount;
                ValidationMessage = string.Empty;

                if (!IsSelected)
                    IsSelected = true;
            }
            catch (Exception ex)
            {
                EstimatedRefundAmount = 0m;
                ValidationMessage = ex.Message;
            }
        }
    }
}
