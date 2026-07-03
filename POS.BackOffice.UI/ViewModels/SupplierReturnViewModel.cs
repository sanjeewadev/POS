using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;

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

        public string BatchNo { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }

        public decimal ReceivedQty { get; set; }
        public decimal AlreadyReturnedQty { get; set; }
        public decimal CurrentBatchStock { get; set; }
        public decimal MaxReturnQty { get; set; }

        public decimal HistoricalCost { get; set; }

        [ObservableProperty]
        private decimal _returnQty = 0m;

        [ObservableProperty]
        private string _reasonCode = "Damaged / Defective";

        [ObservableProperty]
        private string _lineRemarks = string.Empty;

        public decimal CreditValue => Math.Round(ReturnQty * HistoricalCost, 2);

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

        public string BatchNo { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }

        public decimal HistoricalCost { get; set; }

        public decimal CurrentBatchStock { get; set; }
        public decimal MaxReturnQty { get; set; }

        [ObservableProperty]
        private decimal _returnQty = 0m;

        [ObservableProperty]
        private string _reasonCode = string.Empty;

        [ObservableProperty]
        private string _lineRemarks = string.Empty;

        public decimal CreditValue => Math.Round(ReturnQty * HistoricalCost, 2);

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
        private string _authorizedBy = "Admin";

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

        public SupplierReturnViewModel(SupplierReturnRepository returnRepository)
        {
            _returnRepository = returnRepository;

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

                StatusMessage = $"Loaded {Suppliers.Count} active supplier(s).";
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
            if (value < 0)
            {
                RestockingFee = 0m;
                return;
            }

            if (GrossCredit > 0 && value > GrossCredit)
            {
                RestockingFee = GrossCredit;
                return;
            }

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
        // LOAD GRN RETURNABLE BATCH LINES
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

                        BatchNo = row.BatchNo,
                        ExpiryDate = row.ExpiryDate,

                        ReceivedQty = row.ReceivedQty,
                        AlreadyReturnedQty = row.AlreadyReturnedQty,
                        CurrentBatchStock = row.CurrentBatchStock,
                        MaxReturnQty = row.MaxReturnQty,

                        HistoricalCost = row.HistoricalCost,
                        ReturnQty = 0m,
                        ReasonCode = ReasonCodes.FirstOrDefault() ?? "Damaged / Defective"
                    });
                }

                StatusMessage = ActiveMatrixVariants.Count == 0
                    ? "Selected GRN has no returnable batch lines."
                    : $"Loaded {ActiveMatrixVariants.Count} returnable batch line(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load GRN returnable batches.";

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
                    "Enter a return quantity for at least one batch line.",
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
                            $"Cannot add more quantity for '{item.DisplayDescription}'. Maximum returnable quantity is {item.MaxReturnQty:N3}.",
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

                    BatchNo = item.BatchNo,
                    ExpiryDate = item.ExpiryDate,

                    ReturnQty = item.ReturnQty,
                    HistoricalCost = item.HistoricalCost,

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
                MessageBox.Show(
                    $"GRN line is missing for item '{item.DisplayDescription}'.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (item.ItemBatchId <= 0)
            {
                MessageBox.Show(
                    $"Batch is missing for item '{item.DisplayDescription}'.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (item.ReturnQty <= 0)
            {
                MessageBox.Show(
                    $"Return quantity must be greater than zero for '{item.DisplayDescription}'.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (item.ReturnQty > item.MaxReturnQty)
            {
                MessageBox.Show(
                    $"Cannot return {item.ReturnQty:N3} of '{item.DisplayDescription}'. Maximum returnable quantity is {item.MaxReturnQty:N3}.",
                    "Return Quantity Exceeded",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (item.HistoricalCost <= 0)
            {
                MessageBox.Show(
                    $"Historical cost is missing for '{item.DisplayDescription}'.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(item.ReasonCode))
            {
                MessageBox.Show(
                    $"Please select a reason for '{item.DisplayDescription}'.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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

        // Temporary compatibility with the old XAML OK button.
        // The final XAML will remove the OK button because totals now auto recalculate.
        [RelayCommand]
        private void UpdateLine()
        {
            if (SelectedLine == null)
                return;

            if (!ValidateCartLine(SelectedLine))
                return;

            OnPropertyChanged(nameof(ReturnLines));
            RecalculateTotals();

            StatusMessage = "Return line updated.";
        }

        // =========================================================
        // TOTALS
        // =========================================================

        private void RecalculateTotals()
        {
            GrossCredit = Math.Round(ReturnLines.Sum(l => l.CreditValue), 2);

            if (RestockingFee < 0)
                RestockingFee = 0m;

            if (RestockingFee > GrossCredit && GrossCredit > 0)
                RestockingFee = GrossCredit;

            NetCredit = Math.Round(GrossCredit - RestockingFee, 2);

            if (NetCredit < 0)
                NetCredit = 0m;
        }

        // =========================================================
        // POST
        // =========================================================

        // Temporary compatibility with old XAML.
        // Supplier Return draft is intentionally disabled.
        [RelayCommand]
        private void SaveDraft()
        {
            MessageBox.Show(
                "Draft supplier returns are not supported in this version. Please post the supplier return directly.",
                "Draft Disabled",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        [RelayCommand]
        private async Task PostReturnAsync()
        {
            await PostReturnExecutionAsync();
        }

        private async Task PostReturnExecutionAsync()
        {
            if (SelectedSupplier == null)
            {
                MessageBox.Show(
                    "Please select a supplier.",
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

            if (!ReturnLines.Any())
            {
                MessageBox.Show(
                    "Cannot post an empty supplier return.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(AuthorizedBy))
            {
                MessageBox.Show(
                    "Authorized by is required.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
                MessageBox.Show(
                    "Gross return value must be greater than zero.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (RestockingFee > GrossCredit)
            {
                MessageBox.Show(
                    "Restocking fee cannot be greater than gross return value.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Post supplier return?\n\nThis will deduct exact batch stock and reduce supplier balance by Rs. {NetCredit:N2}.",
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
                    RestockingFee = RestockingFee,
                    NetCredit = NetCredit,

                    Status = "Posted",
                    CreatedBy = "Admin",
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
                        CreditValue = Math.Round(l.ReturnQty * l.HistoricalCost, 2),

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

                await _returnRepository.PostSupplierReturnAsync(header, lines);

                MessageBox.Show(
                    "Supplier return posted successfully. Stock and supplier ledger were updated.",
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
                MessageBox.Show(
                    $"GRN line is missing for item '{line.DisplayDescription}'.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (line.ItemBatchId <= 0)
            {
                MessageBox.Show(
                    $"Batch is missing for item '{line.DisplayDescription}'.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (line.ReturnQty <= 0)
            {
                MessageBox.Show(
                    $"Return quantity must be greater than zero for '{line.DisplayDescription}'.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (line.ReturnQty > line.MaxReturnQty)
            {
                MessageBox.Show(
                    $"Cannot return {line.ReturnQty:N3} for '{line.DisplayDescription}'. Maximum returnable quantity is {line.MaxReturnQty:N3}.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (line.HistoricalCost <= 0)
            {
                MessageBox.Show(
                    $"Historical cost is missing for '{line.DisplayDescription}'.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(line.ReasonCode))
            {
                MessageBox.Show(
                    $"Reason code is required for '{line.DisplayDescription}'.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
            AuthorizedBy = "Admin";
            Remarks = string.Empty;

            RestockingFee = 0m;

            SupplierInvoices.Clear();
            ActiveMatrixVariants.Clear();
            ClearReturnLines();

            RecalculateTotals();

            StatusMessage = "Ready.";
        }

        private void ClearReturnLines()
        {
            foreach (var line in ReturnLines.ToList())
                line.PropertyChanged -= ReturnLine_PropertyChanged;

            ReturnLines.Clear();
        }
    }
}