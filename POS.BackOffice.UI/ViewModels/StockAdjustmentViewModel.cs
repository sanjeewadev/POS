using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class AdjustmentMatrixDto : ObservableObject
    {
        public int ItemVariantId { get; set; }
        public int ItemBatchId { get; set; }

        public string ItemCode { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public string BatchNo { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }

        public decimal SystemQty { get; set; } = 0m;
        public decimal UnitCost { get; set; } = 0m;

        [ObservableProperty]
        private decimal _actualQty = 0m;

        public decimal Variance => ActualQty - SystemQty;

        public decimal CostImpact => Variance * UnitCost;

        partial void OnActualQtyChanged(decimal value)
        {
            OnPropertyChanged(nameof(Variance));
            OnPropertyChanged(nameof(CostImpact));
        }
    }

    public partial class StockAdjustmentViewModel : ObservableObject
    {
        private readonly StockAdjustmentRepository _adjustmentRepository;

        // Keep these constructor dependencies for DI compatibility with your current project.
        private readonly ItemMasterRepository _itemMasterRepository;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        // =========================================================
        // HEADER / STATE
        // =========================================================

        [ObservableProperty]
        private DateTime _adjustmentDate = DateTime.Now;

        [ObservableProperty]
        private string _adjustmentMode = "Physical Count Correction";

        [ObservableProperty]
        private string _authorizedBy = "Admin";

        [ObservableProperty]
        private string _reference = string.Empty;

        [ObservableProperty]
        private string _remarks = string.Empty;

        [ObservableProperty]
        private string _documentStatus = "DRAFT / PENDING";

        [ObservableProperty]
        private bool _isDocumentLocked = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        // =========================================================
        // ENTRY
        // =========================================================

        [ObservableProperty]
        private string _scanBarcode = string.Empty;

        // =========================================================
        // COLLECTIONS
        // =========================================================

        public ObservableCollection<string> AdjustmentModes { get; } = new(new[]
        {
            "Physical Count Correction",
            "Stock Increase",
            "Stock Decrease"
        });

        public ObservableCollection<AdjustmentMatrixDto> ActiveMatrixVariants { get; } = new();

        public ObservableCollection<StockAdjustmentLine> AdjustmentLines { get; } = new();

        public ObservableCollection<string> ReasonCodes { get; } = new(new[]
        {
            "Data Entry Error",
            "Damaged / Broken",
            "Expired / Spoiled",
            "Stolen / Missing",
            "Found Stock",
            "Opening Balance",
            "Audit Correction",
            "Internal Use",
            "Promotional Giveaway",
            "Supplier Free Issue"
        });

        [ObservableProperty]
        private StockAdjustmentLine? _selectedLine;

        // =========================================================
        // TOTALS
        // =========================================================

        [ObservableProperty]
        private decimal _totalImpact = 0m;

        [ObservableProperty]
        private decimal _totalIncreaseQty = 0m;

        [ObservableProperty]
        private decimal _totalDecreaseQty = 0m;

        public StockAdjustmentViewModel(
            StockAdjustmentRepository adjustmentRepository,
            ItemMasterRepository itemMasterRepository,
            IDbContextFactory<AppDbContext> contextFactory)
        {
            _adjustmentRepository = adjustmentRepository;
            _itemMasterRepository = itemMasterRepository;
            _contextFactory = contextFactory;
        }

        partial void OnAdjustmentModeChanged(string value)
        {
            StatusMessage = $"Adjustment mode changed to {value}.";
        }

        // =========================================================
        // BARCODE / BATCH SEARCH
        // =========================================================

        [RelayCommand]
        private async Task AddItemAsync()
        {
            if (IsDocumentLocked)
                return;

            string term = (ScanBarcode ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(term))
                return;

            try
            {
                var batches = await _adjustmentRepository.GetActiveBatchesByBarcodeAsync(term);

                if (!batches.Any())
                {
                    MessageBox.Show(
                        $"No active stock batches found for '{term}'.",
                        "No Stock Batch",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    ScanBarcode = string.Empty;
                    return;
                }

                ActiveMatrixVariants.Clear();

                foreach (var batch in batches)
                {
                    ActiveMatrixVariants.Add(new AdjustmentMatrixDto
                    {
                        ItemVariantId = batch.ItemVariantId,
                        ItemBatchId = batch.ItemBatchId,
                        ItemCode = batch.ItemCode,
                        VariantDescription = batch.VariantDescription,
                        Description = batch.Description,
                        BatchNo = batch.BatchNo,
                        ExpiryDate = batch.ExpiryDate,
                        SystemQty = batch.SystemQty,
                        ActualQty = batch.SystemQty,
                        UnitCost = batch.UnitCost
                    });
                }

                ScanBarcode = string.Empty;
                StatusMessage = $"{ActiveMatrixVariants.Count} active batch(es) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load batches.";

                MessageBox.Show(
                    $"Failed to load item batches:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // =========================================================
        // QUEUE LINES
        // =========================================================

        [RelayCommand]
        private void AddMatrix()
        {
            if (IsDocumentLocked)
                return;

            var changedItems = ActiveMatrixVariants
                .Where(v => v.Variance != 0)
                .ToList();

            if (!changedItems.Any())
            {
                MessageBox.Show(
                    "Enter an Actual Qty that differs from the System Qty.",
                    "No Variance",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            foreach (var item in changedItems)
            {
                if (AdjustmentLines.Any(l => l.ItemBatchId == item.ItemBatchId))
                    continue;

                var line = new StockAdjustmentLine
                {
                    ItemBatchId = item.ItemBatchId,
                    ItemVariantId = item.ItemVariantId,

                    ItemCode = item.ItemCode,
                    VariantDescription = item.VariantDescription,
                    Description = item.Description,
                    BatchNo = item.BatchNo,
                    ExpiryDate = item.ExpiryDate,

                    SystemQty = item.SystemQty,
                    ActualQty = item.ActualQty,
                    VarianceQty = item.Variance,

                    UnitCost = item.UnitCost,
                    CostImpact = Math.Round(item.CostImpact, 2),

                    ReasonCode = string.Empty,
                    LineRemarks = string.Empty,
                    LineStatus = "Open"
                };

                AdjustmentLines.Add(line);
            }

            ActiveMatrixVariants.Clear();
            RecalculateImpact();

            StatusMessage = "Variance line(s) queued.";
        }

        [RelayCommand]
        private void RemoveLine(StockAdjustmentLine? line)
        {
            if (IsDocumentLocked || line == null)
                return;

            AdjustmentLines.Remove(line);
            RecalculateImpact();

            StatusMessage = "Line removed.";
        }

        [RelayCommand]
        private void UpdateLine()
        {
            if (IsDocumentLocked || SelectedLine == null)
                return;

            SelectedLine.VarianceQty = SelectedLine.ActualQty - SelectedLine.SystemQty;
            SelectedLine.CostImpact = Math.Round(SelectedLine.VarianceQty * SelectedLine.UnitCost, 2);

            RecalculateImpact();
        }

        // =========================================================
        // TOTALS
        // =========================================================

        public void RecalculateImpact()
        {
            foreach (var line in AdjustmentLines)
            {
                line.VarianceQty = line.ActualQty - line.SystemQty;
                line.CostImpact = Math.Round(line.VarianceQty * line.UnitCost, 2);
            }

            TotalImpact = Math.Round(AdjustmentLines.Sum(l => l.CostImpact), 2);
            TotalIncreaseQty = AdjustmentLines.Where(l => l.VarianceQty > 0).Sum(l => l.VarianceQty);
            TotalDecreaseQty = AdjustmentLines.Where(l => l.VarianceQty < 0).Sum(l => Math.Abs(l.VarianceQty));

            CollectionViewSource.GetDefaultView(AdjustmentLines)?.Refresh();
        }

        // =========================================================
        // SAVE / POST
        // =========================================================

        [RelayCommand]
        private async Task SaveDraftAsync()
        {
            await SaveAdjustmentExecutionAsync(isDraft: true);
        }

        [RelayCommand]
        private async Task PostAdjustmentAsync()
        {
            await SaveAdjustmentExecutionAsync(isDraft: false);
        }

        private async Task SaveAdjustmentExecutionAsync(bool isDraft)
        {
            if (IsDocumentLocked)
                return;

            RecalculateImpact();

            if (!ValidateBeforeSave(isDraft))
                return;

            string actionText = isDraft
                ? "Save this stock adjustment as a draft?"
                : "POST STOCK ADJUSTMENT?\n\nThis will permanently update physical batch stock and write an inventory ledger transaction.";

            var result = MessageBox.Show(
                actionText,
                isDraft ? "Confirm Draft" : "Confirm Posting",
                MessageBoxButton.YesNo,
                isDraft ? MessageBoxImage.Question : MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                var header = new StockAdjustmentHeader
                {
                    AdjustmentDate = AdjustmentDate,
                    AdjustmentMode = AdjustmentMode,
                    AuthorizedBy = AuthorizedBy.Trim(),
                    Reference = Reference.Trim(),
                    Remarks = Remarks.Trim(),
                    TotalImpact = TotalImpact,
                    TotalIncreaseQty = TotalIncreaseQty,
                    TotalDecreaseQty = TotalDecreaseQty,
                    CreatedBy = AuthorizedBy.Trim(),
                    PostedBy = AuthorizedBy.Trim()
                };

                var lines = AdjustmentLines
                    .Select(CloneLineForSave)
                    .ToList();

                var savedHeader = await _adjustmentRepository.SaveAdjustmentAsync(header, lines, isDraft);

                MessageBox.Show(
                    isDraft
                        ? $"Draft saved successfully.\n\nDocument No: {savedHeader.AdjustmentNo}"
                        : $"Stock adjustment posted successfully.\n\nDocument No: {savedHeader.AdjustmentNo}",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                if (isDraft)
                {
                    Clear();
                }
                else
                {
                    DocumentStatus = "POSTED / LOCKED";
                    IsDocumentLocked = true;
                    StatusMessage = $"Posted: {savedHeader.AdjustmentNo}";
                }
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Posting blocked.";

                MessageBox.Show(
                    ex.Message,
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                string message = ex.InnerException?.Message ?? ex.Message;
                StatusMessage = "Save failed.";

                MessageBox.Show(
                    $"Transaction rolled back.\n\n{message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool ValidateBeforeSave(bool isDraft)
        {
            if (string.IsNullOrWhiteSpace(AdjustmentMode))
            {
                MessageBox.Show("Adjustment mode is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(AuthorizedBy))
            {
                MessageBox.Show("Authorized By is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!isDraft && string.IsNullOrWhiteSpace(Reference))
            {
                MessageBox.Show("Reference / reason document is required before posting.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!AdjustmentLines.Any())
            {
                MessageBox.Show("Cannot save an empty stock adjustment.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var duplicateBatch = AdjustmentLines
                .GroupBy(l => l.ItemBatchId)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicateBatch != null)
            {
                MessageBox.Show("Same batch cannot be queued twice.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            foreach (var line in AdjustmentLines)
            {
                line.ReasonCode = (line.ReasonCode ?? string.Empty).Trim();
                line.LineRemarks = (line.LineRemarks ?? string.Empty).Trim();
                line.VarianceQty = line.ActualQty - line.SystemQty;
                line.CostImpact = Math.Round(line.VarianceQty * line.UnitCost, 2);

                if (line.VarianceQty == 0)
                {
                    MessageBox.Show(
                        $"Line '{line.Description} / {line.BatchNo}' has no variance.",
                        "Validation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                if (line.ActualQty < 0)
                {
                    MessageBox.Show(
                        $"Actual quantity cannot be negative for '{line.Description} / {line.BatchNo}'.",
                        "Validation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                if (AdjustmentMode == "Stock Increase" && line.VarianceQty <= 0)
                {
                    MessageBox.Show(
                        "Stock Increase mode can only contain positive variance lines.",
                        "Validation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                if (AdjustmentMode == "Stock Decrease" && line.VarianceQty >= 0)
                {
                    MessageBox.Show(
                        "Stock Decrease mode can only contain negative variance lines.",
                        "Validation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                if (!isDraft && string.IsNullOrWhiteSpace(line.ReasonCode))
                {
                    MessageBox.Show(
                        $"Reason code is required for '{line.Description} / {line.BatchNo}'.",
                        "Validation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        private static StockAdjustmentLine CloneLineForSave(StockAdjustmentLine line)
        {
            return new StockAdjustmentLine
            {
                ItemBatchId = line.ItemBatchId,
                ItemVariantId = line.ItemVariantId,

                SystemQty = line.SystemQty,
                ActualQty = line.ActualQty,
                VarianceQty = line.VarianceQty,

                ReasonCode = line.ReasonCode?.Trim() ?? string.Empty,
                LineRemarks = line.LineRemarks?.Trim() ?? string.Empty,

                UnitCost = line.UnitCost,
                CostImpact = line.CostImpact,
                LineStatus = line.LineStatus
            };
        }

        // =========================================================
        // CLEAR
        // =========================================================

        [RelayCommand]
        private void Clear()
        {
            AdjustmentDate = DateTime.Now;
            AdjustmentMode = "Physical Count Correction";
            AuthorizedBy = "Admin";
            Reference = string.Empty;
            Remarks = string.Empty;
            ScanBarcode = string.Empty;

            AdjustmentLines.Clear();
            ActiveMatrixVariants.Clear();

            TotalImpact = 0m;
            TotalIncreaseQty = 0m;
            TotalDecreaseQty = 0m;

            SelectedLine = null;
            IsDocumentLocked = false;
            DocumentStatus = "DRAFT / PENDING";
            StatusMessage = "Ready.";
        }
    }
}