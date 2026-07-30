using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.BackOffice.UI.Views.Dialogs;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Services.Documents;
using POS.Core.Utilities;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class ReturnMatrixDto : ObservableObject
    {
        public int GrnHeaderId { get; set; }
        public int GrnLineId { get; set; }

        public int ItemVariantId { get; set; }
        public int ItemBatchId { get; set; }

        public string GrnNumber { get; set; } = string.Empty;
        public string SupplierInvoiceNo { get; set; } = string.Empty;

        public string ItemCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;

        public bool HasBatchTracking { get; set; }
        public bool HasExpiryTracking { get; set; }
        public bool IsGeneralStockBucket { get; set; }

        public string TrackingText
        {
            get
            {
                if (!HasBatchTracking)
                    return "Average Cost";

                return HasExpiryTracking ? "Batch + Expiry" : "Batch";
            }
        }

        public string BatchNo { get; set; } = string.Empty;
        public string InternalBatchBarcode { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }

        public string BatchDisplayText =>
            IsGeneralStockBucket ? "GENERAL" : BatchNo;

        public string BatchBarcodeDisplayText =>
            IsGeneralStockBucket ? "-" : InternalBatchBarcode;

        public decimal ReceivedQty { get; set; }
        public decimal AlreadyReturnedQty { get; set; }
        public decimal CurrentBatchStock { get; set; }
        public decimal MaxReturnQty { get; set; }

        public decimal HistoricalCost { get; set; }
        public decimal OriginalCreditAmount { get; set; }
        public decimal PreviouslyReturnedCreditAmount { get; set; }
        public decimal MaxReturnCredit { get; set; }
        public decimal CreditUnitValue { get; set; }
        public string TaxDisplayText { get; set; } = string.Empty;

        [ObservableProperty]
        private decimal _returnQty = 0m;

        [ObservableProperty]
        private string _reasonCode = "Damaged / Defective";

        [ObservableProperty]
        private string _lineRemarks = string.Empty;

        public decimal CreditValue => CalculateCreditValue(
            OriginalCreditAmount,
            ReceivedQty,
            MaxReturnQty,
            MaxReturnCredit,
            ReturnQty);

        public string DisplayDescription
        {
            get
            {
                if (string.IsNullOrWhiteSpace(VariantDescription) ||
                    VariantDescription.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                {
                    return Description;
                }

                return $"{Description} - {VariantDescription}";
            }
        }

        private static decimal CalculateCreditValue(
            decimal originalCredit,
            decimal receivedQuantity,
            decimal maximumReturnQuantity,
            decimal maximumReturnCredit,
            decimal returnQuantity)
        {
            if (returnQuantity <= 0m || receivedQuantity <= 0m)
                return 0m;

            const decimal tolerance = 0.0005m;
            if (Math.Abs(returnQuantity - maximumReturnQuantity) <= tolerance)
                return maximumReturnCredit;

            decimal proportional = decimal.Round(
                originalCredit * returnQuantity / receivedQuantity,
                2,
                MidpointRounding.AwayFromZero);

            return Math.Min(maximumReturnCredit, Math.Max(0m, proportional));
        }

        partial void OnReturnQtyChanged(decimal value)
        {
            if (value < 0)
            {
                ReturnQty = 0m;
                return;
            }

            OnPropertyChanged(nameof(CreditValue));
        }
    }

    public partial class SupplierReturnLineEntryDto : ObservableObject
    {
        public int GrnLineId { get; set; }

        public int ItemVariantId { get; set; }
        public int ItemBatchId { get; set; }

        public string ItemCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;

        public bool HasBatchTracking { get; set; }
        public bool HasExpiryTracking { get; set; }
        public bool IsGeneralStockBucket { get; set; }

        public string TrackingText
        {
            get
            {
                if (!HasBatchTracking)
                    return "Average Cost";

                return HasExpiryTracking ? "Batch + Expiry" : "Batch";
            }
        }

        public string BatchNo { get; set; } = string.Empty;
        public string InternalBatchBarcode { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }

        public string BatchDisplayText =>
            IsGeneralStockBucket ? "GENERAL" : BatchNo;

        public string BatchBarcodeDisplayText =>
            IsGeneralStockBucket ? "-" : InternalBatchBarcode;

        public decimal HistoricalCost { get; set; }
        public decimal OriginalCreditAmount { get; set; }
        public decimal PreviouslyReturnedCreditAmount { get; set; }
        public decimal MaxReturnCredit { get; set; }
        public decimal CreditUnitValue { get; set; }
        public string TaxDisplayText { get; set; } = string.Empty;

        public decimal ReceivedQty { get; set; }
        public decimal AlreadyReturnedQty { get; set; }
        public decimal CurrentBatchStock { get; set; }
        public decimal MaxReturnQty { get; set; }

        [ObservableProperty]
        private decimal _returnQty = 0m;

        [ObservableProperty]
        private string _reasonCode = string.Empty;

        [ObservableProperty]
        private string _lineRemarks = string.Empty;

        public decimal CreditValue => CalculateCreditValue(
            OriginalCreditAmount,
            ReceivedQty,
            MaxReturnQty,
            MaxReturnCredit,
            ReturnQty);

        public string DisplayDescription
        {
            get
            {
                if (string.IsNullOrWhiteSpace(VariantDescription) ||
                    VariantDescription.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                {
                    return Description;
                }

                return $"{Description} - {VariantDescription}";
            }
        }

        private static decimal CalculateCreditValue(
            decimal originalCredit,
            decimal receivedQuantity,
            decimal maximumReturnQuantity,
            decimal maximumReturnCredit,
            decimal returnQuantity)
        {
            if (returnQuantity <= 0m || receivedQuantity <= 0m)
                return 0m;

            const decimal tolerance = 0.0005m;
            if (Math.Abs(returnQuantity - maximumReturnQuantity) <= tolerance)
                return maximumReturnCredit;

            decimal proportional = decimal.Round(
                originalCredit * returnQuantity / receivedQuantity,
                2,
                MidpointRounding.AwayFromZero);

            return Math.Min(maximumReturnCredit, Math.Max(0m, proportional));
        }

        partial void OnReturnQtyChanged(decimal value)
        {
            if (value < 0)
            {
                ReturnQty = 0m;
                return;
            }

            OnPropertyChanged(nameof(CreditValue));
        }
    }

    public partial class SupplierReturnViewModel : ViewModelBase
    {
        private readonly SupplierReturnRepository _returnRepository;
        private readonly AuthService _authService;
        private readonly SupplierDebitNoteTextFormatter _debitNoteFormatter;

        // =========================================================
        // HEADER
        // =========================================================

        [ObservableProperty]
        private SupplierLookupDto? _selectedSupplier;

        [ObservableProperty]
        private SupplierInvoiceLookupDto? _selectedInvoice;

        [ObservableProperty]
        private DateTime _returnDate = DateTime.Now;

        [ObservableProperty]
        private string _authorizedBy = string.Empty;

        [ObservableProperty]
        private string _remarks = string.Empty;

        // =========================================================
        // DATA COLLECTIONS
        // =========================================================

        public ObservableCollection<SupplierLookupDto> Suppliers { get; } = new();

        public ObservableCollection<SupplierInvoiceLookupDto> SupplierInvoices { get; } = new();

        public ObservableCollection<ReturnMatrixDto> ActiveMatrixVariants { get; } = new();

        public ObservableCollection<SupplierReturnLineEntryDto> ReturnLines { get; } = new();

        public ObservableCollection<string> ReasonCodes { get; } = new();

        [ObservableProperty]
        private SupplierReturnLineEntryDto? _selectedLine;

        // =========================================================
        // FINANCIAL TOTALS
        // =========================================================

        [ObservableProperty]
        private decimal _grossCredit = 0m;

        [ObservableProperty]
        private decimal _restockingFee = 0m;

        [ObservableProperty]
        private decimal _netCredit = 0m;

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        public SupplierReturnViewModel(
            SupplierReturnRepository returnRepository,
            AuthService authService,
            SupplierDebitNoteTextFormatter debitNoteFormatter)
        {
            _returnRepository = returnRepository ?? throw new ArgumentNullException(nameof(returnRepository));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _debitNoteFormatter = debitNoteFormatter ?? throw new ArgumentNullException(nameof(debitNoteFormatter));
            AuthorizedBy = GetCurrentUserName();

            foreach (var reason in _returnRepository.GetReasonCodes())
                ReasonCodes.Add(reason);

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            IsBusy = true;

            try
            {
                Suppliers.Clear();

                var suppliers = await _returnRepository.GetActiveSuppliersAsync();

                foreach (var supplier in suppliers)
                    Suppliers.Add(supplier);

                StatusMessage = $"Loaded {Suppliers.Count} supplier(s) with posted GRNs.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to initialize supplier return page.";

                MessageBox.Show(
                    $"Failed to load suppliers:\n\n{ex.Message}",
                    "Supplier Return",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // AUTO EVENTS
        // =========================================================

        partial void OnSelectedSupplierChanged(SupplierLookupDto? value)
        {
            SupplierInvoices.Clear();
            ActiveMatrixVariants.Clear();
            ClearReturnLines();

            SelectedInvoice = null;
            RecalculateTotals();

            if (value != null)
                _ = LoadSupplierInvoicesAsync(value.Id);
        }

        partial void OnSelectedInvoiceChanged(SupplierInvoiceLookupDto? value)
        {
            ActiveMatrixVariants.Clear();
            ClearReturnLines();
            RecalculateTotals();

            if (value == null)
                StatusMessage = "Select a supplier invoice / GRN.";
        }

        partial void OnRestockingFeeChanged(decimal value)
        {
            if (value != 0m)
                RestockingFee = 0m;

            RecalculateTotals();
        }

        private async Task LoadSupplierInvoicesAsync(int supplierId)
        {
            IsBusy = true;

            try
            {
                SupplierInvoices.Clear();

                var invoices = await _returnRepository.GetSupplierInvoicesAsync(supplierId);

                foreach (var invoice in invoices)
                    SupplierInvoices.Add(invoice);

                StatusMessage = SupplierInvoices.Count == 0
                    ? "No returnable posted GRNs found for selected supplier."
                    : $"Loaded {SupplierInvoices.Count} returnable posted GRN(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load supplier invoices.";

                MessageBox.Show(
                    $"Failed to load supplier invoices:\n\n{ex.Message}",
                    "Supplier Return",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // LOAD GRN RETURNABLE STOCK ROWS
        // =========================================================

        [RelayCommand]
        private async Task LoadInvoiceAsync()
        {
            if (SelectedSupplier == null)
            {
                MessageBox.Show(
                    "Please select a supplier first.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (SelectedInvoice == null)
            {
                MessageBox.Show(
                    "Please select a posted GRN / supplier invoice.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;

            try
            {
                ActiveMatrixVariants.Clear();
                ClearReturnLines();
                RecalculateTotals();

                var rows = await _returnRepository.GetReturnableBatchesForGrnAsync(SelectedInvoice.Id);

                foreach (var row in rows)
                {
                    ActiveMatrixVariants.Add(new ReturnMatrixDto
                    {
                        GrnHeaderId = row.GrnHeaderId,
                        GrnLineId = row.GrnLineId,

                        ItemVariantId = row.ItemVariantId,
                        ItemBatchId = row.ItemBatchId,

                        GrnNumber = row.GrnNumber,
                        SupplierInvoiceNo = row.SupplierInvoiceNo,

                        ItemCode = row.ItemCode,
                        Description = row.Description,
                        VariantDescription = row.VariantDescription,

                        HasBatchTracking = row.HasBatchTracking,
                        HasExpiryTracking = row.HasExpiryTracking,
                        IsGeneralStockBucket = row.IsGeneralStockBucket,

                        BatchNo = row.BatchNo,
                        InternalBatchBarcode = row.InternalBatchBarcode,
                        ExpiryDate = row.ExpiryDate,

                        ReceivedQty = row.ReceivedQty,
                        AlreadyReturnedQty = row.AlreadyReturnedQty,
                        CurrentBatchStock = row.CurrentBatchStock,
                        MaxReturnQty = row.MaxReturnQty,

                        HistoricalCost = row.HistoricalCost,
                        OriginalCreditAmount = row.OriginalCreditAmount,
                        PreviouslyReturnedCreditAmount = row.PreviouslyReturnedCreditAmount,
                        MaxReturnCredit = row.MaxReturnCredit,
                        CreditUnitValue = row.CreditUnitValue,
                        TaxDisplayText = row.TaxDisplayText,
                        ReturnQty = 0m,
                        ReasonCode = ReasonCodes.FirstOrDefault() ?? "Damaged / Defective"
                    });
                }

                StatusMessage = ActiveMatrixVariants.Count == 0
                    ? "Selected GRN has no returnable stock rows."
                    : $"Loaded {ActiveMatrixVariants.Count} returnable stock row(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load GRN returnable stock rows.";

                MessageBox.Show(
                    $"Failed to load invoice return lines:\n\n{ex.Message}",
                    "Supplier Return",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // ADD TO RETURN CART
        // =========================================================

        [RelayCommand]
        private void AddMatrix()
        {
            var itemsToAdd = ActiveMatrixVariants
                .Where(v => v.ReturnQty > 0)
                .ToList();

            if (!itemsToAdd.Any())
            {
                MessageBox.Show(
                    "Enter a return quantity for at least one stock row.",
                    "No Quantity",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            foreach (var item in itemsToAdd)
            {
                if (!ValidateMatrixLine(item))
                    return;
            }

            foreach (var item in itemsToAdd)
            {
                var existingLine = ReturnLines.FirstOrDefault(l =>
                    l.GrnLineId == item.GrnLineId &&
                    l.ItemBatchId == item.ItemBatchId);

                if (existingLine != null)
                {
                    decimal newQty = existingLine.ReturnQty + item.ReturnQty;

                    if (newQty > item.MaxReturnQty)
                    {
                        MessageBox.Show(
                            $"Cannot add more quantity for '{item.DisplayDescription}'. Maximum returnable quantity is {QuantityDisplayFormatter.Format(item.MaxReturnQty)}.",
                            "Return Quantity Exceeded",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    existingLine.ReturnQty = newQty;
                    existingLine.ReasonCode = item.ReasonCode;
                    existingLine.LineRemarks = item.LineRemarks;

                    continue;
                }

                var newLine = new SupplierReturnLineEntryDto
                {
                    GrnLineId = item.GrnLineId,

                    ItemVariantId = item.ItemVariantId,
                    ItemBatchId = item.ItemBatchId,

                    ItemCode = item.ItemCode,
                    Description = item.Description,
                    VariantDescription = item.VariantDescription,

                    HasBatchTracking = item.HasBatchTracking,
                    HasExpiryTracking = item.HasExpiryTracking,
                    IsGeneralStockBucket = item.IsGeneralStockBucket,

                    BatchNo = item.BatchNo,
                    InternalBatchBarcode = item.InternalBatchBarcode,
                    ExpiryDate = item.ExpiryDate,

                    ReturnQty = item.ReturnQty,
                    HistoricalCost = item.HistoricalCost,
                    OriginalCreditAmount = item.OriginalCreditAmount,
                    PreviouslyReturnedCreditAmount = item.PreviouslyReturnedCreditAmount,
                    MaxReturnCredit = item.MaxReturnCredit,
                    CreditUnitValue = item.CreditUnitValue,
                    ReceivedQty = item.ReceivedQty,
                    AlreadyReturnedQty = item.AlreadyReturnedQty,
                    TaxDisplayText = item.TaxDisplayText,

                    ReasonCode = item.ReasonCode,
                    LineRemarks = item.LineRemarks,

                    CurrentBatchStock = item.CurrentBatchStock,
                    MaxReturnQty = item.MaxReturnQty
                };

                AddReturnLine(newLine);
            }

            foreach (var item in itemsToAdd)
                item.ReturnQty = 0m;

            RecalculateTotals();

            StatusMessage = $"Return cart contains {ReturnLines.Count} line(s).";
        }

        private bool ValidateMatrixLine(ReturnMatrixDto item)
        {
            if (item.GrnLineId <= 0)
            {
                MessageBox.Show($"GRN line is missing for item '{item.DisplayDescription}'.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (item.ItemBatchId <= 0)
            {
                MessageBox.Show($"Stock row is missing for item '{item.DisplayDescription}'.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (item.ReturnQty <= 0)
            {
                MessageBox.Show($"Return quantity must be greater than zero for '{item.DisplayDescription}'.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (item.ReturnQty > item.MaxReturnQty)
            {
                MessageBox.Show($"Cannot return {QuantityDisplayFormatter.Format(item.ReturnQty)} of '{item.DisplayDescription}'. Maximum returnable quantity is {QuantityDisplayFormatter.Format(item.MaxReturnQty)}.", "Return Quantity Exceeded", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (item.HistoricalCost <= 0)
            {
                MessageBox.Show($"Historical cost is missing for '{item.DisplayDescription}'.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (item.CreditValue <= 0m)
            {
                MessageBox.Show($"Original supplier credit value is missing for '{item.DisplayDescription}'.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(item.ReasonCode))
            {
                MessageBox.Show($"Please select a reason for '{item.DisplayDescription}'.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void AddReturnLine(SupplierReturnLineEntryDto line)
        {
            line.PropertyChanged += ReturnLine_PropertyChanged;
            ReturnLines.Add(line);
        }

        private void RemoveReturnLine(SupplierReturnLineEntryDto line)
        {
            line.PropertyChanged -= ReturnLine_PropertyChanged;
            ReturnLines.Remove(line);
        }

        private void ReturnLine_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SupplierReturnLineEntryDto.ReturnQty) ||
                e.PropertyName == nameof(SupplierReturnLineEntryDto.CreditValue))
            {
                RecalculateTotals();
            }
        }

        [RelayCommand]
        private void RemoveLine(SupplierReturnLineEntryDto? line)
        {
            if (line == null)
                return;

            RemoveReturnLine(line);
            RecalculateTotals();

            StatusMessage = $"Removed line. Return cart contains {ReturnLines.Count} line(s).";
        }

        [RelayCommand]
        private void UpdateLine()
        {
            if (SelectedLine == null)
                return;

            if (!ValidateCartLine(SelectedLine))
                return;

            RecalculateTotals();
            StatusMessage = "Return line updated.";
        }

        // =========================================================
        // TOTALS
        // =========================================================

        private void RecalculateTotals()
        {
            GrossCredit = Math.Round(ReturnLines.Sum(l => l.CreditValue), 2);
            RestockingFee = 0m;
            NetCredit = GrossCredit;
        }

        // =========================================================
        // POST
        // =========================================================

        [RelayCommand]
        private async Task PostReturnAsync()
        {
            await PostReturnExecutionAsync();
        }

        private async Task PostReturnExecutionAsync()
        {
            if (SelectedSupplier == null)
            {
                MessageBox.Show("Please select a supplier.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedInvoice == null)
            {
                MessageBox.Show("Please select a posted GRN / supplier invoice.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ReturnLines.Any())
            {
                MessageBox.Show("Cannot post an empty supplier return.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(AuthorizedBy))
            {
                MessageBox.Show("Authorized by is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var line in ReturnLines)
            {
                if (!ValidateCartLine(line))
                    return;
            }

            RecalculateTotals();

            if (GrossCredit <= 0)
            {
                MessageBox.Show("Gross return value must be greater than zero.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Post supplier return?\n\nThis will deduct exact stock rows and reduce supplier balance by Rs. {NetCredit:N2}.",
                "Post Supplier Return",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            IsBusy = true;

            try
            {
                var header = new SupplierReturnHeader
                {
                    SupplierId = SelectedSupplier.Id,
                    GrnHeaderId = SelectedInvoice.Id,
                    OriginalInvoiceNo = SelectedInvoice.SupplierInvoiceNo,

                    ReturnDate = ReturnDate,
                    AuthorizedBy = AuthorizedBy.Trim(),
                    Remarks = Remarks.Trim(),

                    GrossCredit = GrossCredit,
                    RestockingFee = 0m,
                    NetCredit = NetCredit,

                    Status = "Posted",
                    CreatedBy = AuthorizedBy.Trim(),
                    PostedBy = AuthorizedBy.Trim()
                };

                var lines = ReturnLines
                    .Select(l => new SupplierReturnLine
                    {
                        GrnLineId = l.GrnLineId,

                        ItemVariantId = l.ItemVariantId,
                        ItemBatchId = l.ItemBatchId,

                        BatchNo = l.BatchNo,
                        ExpiryDate = l.ExpiryDate,

                        ReturnQty = l.ReturnQty,
                        HistoricalCost = l.HistoricalCost,
                        CreditValue = l.CreditValue,

                        ReasonCode = l.ReasonCode,
                        LineRemarks = l.LineRemarks,
                        LineStatus = "Posted",

                        ItemCode = l.ItemCode,
                        Description = l.Description,
                        VariantDescription = l.VariantDescription,
                        CurrentBatchStock = l.CurrentBatchStock,
                        MaxReturnQty = l.MaxReturnQty
                    })
                    .ToList();

                SupplierReturnPostResult postResult =
                    await _returnRepository.PostSupplierReturnAsync(header, lines);

                string documentText = _debitNoteFormatter.Format(postResult.DebitNote);
                var preview = new SupplierDebitNotePreviewDialog(
                    documentText,
                    postResult.DebitNote.DebitNoteNumber)
                {
                    Owner = Application.Current?.MainWindow
                };
                preview.ShowDialog();

                MessageBox.Show(
                    $"Supplier return {postResult.ReturnHeader.ReturnNumber} posted successfully. Stock and supplier ledger were updated.",
                    "Supplier Return",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Clear();
            }
            catch (Exception ex)
            {
                StatusMessage = "Supplier return post failed.";

                MessageBox.Show(
                    $"Supplier return failed:\n\n{ex.Message}",
                    "Supplier Return Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool ValidateCartLine(SupplierReturnLineEntryDto line)
        {
            if (line.GrnLineId <= 0)
            {
                MessageBox.Show($"GRN line is missing for item '{line.DisplayDescription}'.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (line.ItemBatchId <= 0)
            {
                MessageBox.Show($"Stock row is missing for item '{line.DisplayDescription}'.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (line.ReturnQty <= 0)
            {
                MessageBox.Show($"Return quantity must be greater than zero for '{line.DisplayDescription}'.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (line.ReturnQty > line.MaxReturnQty)
            {
                MessageBox.Show($"Cannot return {QuantityDisplayFormatter.Format(line.ReturnQty)} for '{line.DisplayDescription}'. Maximum returnable quantity is {QuantityDisplayFormatter.Format(line.MaxReturnQty)}.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (line.HistoricalCost <= 0)
            {
                MessageBox.Show($"Historical cost is missing for '{line.DisplayDescription}'.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (line.CreditValue <= 0m)
            {
                MessageBox.Show($"Original supplier credit value is missing for '{line.DisplayDescription}'.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(line.ReasonCode))
            {
                MessageBox.Show($"Reason code is required for '{line.DisplayDescription}'.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        // =========================================================
        // CLEAR
        // =========================================================

        [RelayCommand]
        private void Clear()
        {
            SelectedSupplier = null;
            SelectedInvoice = null;

            ReturnDate = DateTime.Now;
            AuthorizedBy = GetCurrentUserName();
            Remarks = string.Empty;

            RestockingFee = 0m;

            SupplierInvoices.Clear();
            ActiveMatrixVariants.Clear();
            ClearReturnLines();

            RecalculateTotals();

            StatusMessage = "Ready.";
        }


        private string GetCurrentUserName()
        {
            string username = (_authService.CurrentUser?.Username ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(username))
                return username;

            string fullName = (_authService.CurrentUser?.FullName ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(fullName) ? "BackOffice" : fullName;
        }

        private void ClearReturnLines()
        {
            foreach (var line in ReturnLines.ToList())
                line.PropertyChanged -= ReturnLine_PropertyChanged;

            ReturnLines.Clear();
        }
    }
}
