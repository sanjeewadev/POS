using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    // DTO for Matrix Rapid Entry to calculate real-time Variances on Batches
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

        [ObservableProperty] private decimal _actualQty = 0m;

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
        private readonly ItemMasterRepository _itemMasterRepository;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        // --- ZONE 1: HEADER & STATE ---
        [ObservableProperty] private DateTime _adjustmentDate = DateTime.Now;
        [ObservableProperty] private string _authorizedBy = "Admin";
        [ObservableProperty] private string _reference = string.Empty;
        [ObservableProperty] private string _remarks = string.Empty;
        [ObservableProperty] private string _documentStatus = "DRAFT / PENDING";
        [ObservableProperty] private bool _isDocumentLocked = false; // Locks UI when posted

        // --- ZONE 2: ENTRY CONSOLE ---
        [ObservableProperty] private string _scanBarcode = string.Empty;

        // --- ZONE 3: GRIDS ---
        public ObservableCollection<AdjustmentMatrixDto> ActiveMatrixVariants { get; set; } = new();
        public ObservableCollection<StockAdjustmentLine> AdjustmentLines { get; set; } = new();

        [ObservableProperty] private StockAdjustmentLine? _selectedLine;

        // --- FINANCIAL TOTALS ---
        [ObservableProperty] private decimal _totalImpact = 0m;

        public StockAdjustmentViewModel(
            StockAdjustmentRepository adjustmentRepository,
            ItemMasterRepository itemMasterRepository,
            IDbContextFactory<AppDbContext> contextFactory)
        {
            _adjustmentRepository = adjustmentRepository;
            _itemMasterRepository = itemMasterRepository;
            _contextFactory = contextFactory;
        }

        // --- BARCODE BATCH SEARCH ENGINE ---
        [RelayCommand]
        private async Task AddItemAsync()
        {
            if (IsDocumentLocked) return;
            if (string.IsNullOrWhiteSpace(ScanBarcode)) return;

            var variant = await _itemMasterRepository.GetItemByBarcodeAsync(ScanBarcode.Trim());

            if (variant == null)
            {
                MessageBox.Show($"Barcode '{ScanBarcode}' not found.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                ScanBarcode = string.Empty;
                return;
            }

            // Fetch the specific active batches for this item
            using var context = await _contextFactory.CreateDbContextAsync();
            var activeBatches = await context.ItemBatches
                .Where(b => b.ItemVariantId == variant.Id && !b.IsDeactivated)
                .ToListAsync();

            if (!activeBatches.Any())
            {
                MessageBox.Show($"No active stock batches found for '{variant.VariantDescription}'.", "No Stock", MessageBoxButton.OK, MessageBoxImage.Information);
                ScanBarcode = string.Empty;
                return;
            }

            ActiveMatrixVariants.Clear();
            foreach (var batch in activeBatches)
            {
                ActiveMatrixVariants.Add(new AdjustmentMatrixDto
                {
                    ItemVariantId = variant.Id,
                    ItemBatchId = batch.Id,
                    ItemCode = variant.ItemParent?.ItemCode ?? "UNKNOWN",
                    VariantDescription = variant.VariantDescription,
                    Description = variant.ItemParent?.ItemName ?? "Unknown Item",
                    BatchNo = batch.BatchNo,
                    ExpiryDate = batch.ExpiryDate,
                    SystemQty = batch.CurrentStock,
                    ActualQty = batch.CurrentStock, // Default to matching system qty
                    UnitCost = batch.CostPrice
                });
            }

            ScanBarcode = string.Empty;
        }

        // --- MATH ENGINE ---
        public void RecalculateImpact()
        {
            if (!AdjustmentLines.Any())
            {
                TotalImpact = 0m;
                return;
            }
            TotalImpact = AdjustmentLines.Sum(l => l.CostImpact);
        }

        // --- GRID ACTIONS ---
        [RelayCommand]
        private void AddMatrix()
        {
            if (IsDocumentLocked) return;

            var itemsToAdd = ActiveMatrixVariants.Where(v => v.Variance != 0).ToList();

            if (!itemsToAdd.Any())
            {
                MessageBox.Show("Please enter an Actual Qty that differs from the System Qty.", "No Variance", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var item in itemsToAdd)
            {
                if (AdjustmentLines.Any(l => l.ItemBatchId == item.ItemBatchId)) continue;

                var newLine = new StockAdjustmentLine
                {
                    ItemBatchId = item.ItemBatchId,
                    ItemCode = item.ItemCode,
                    VariantDescription = item.VariantDescription,
                    Description = item.Description,
                    BatchNo = item.BatchNo,
                    ExpiryDate = item.ExpiryDate,
                    SystemQty = item.SystemQty,
                    ActualQty = item.ActualQty,
                    VarianceQty = item.Variance,
                    UnitCost = item.UnitCost,
                    CostImpact = item.CostImpact,
                    ReasonCode = "Data Entry Error"
                };

                AdjustmentLines.Add(newLine);
            }

            ActiveMatrixVariants.Clear();
            RecalculateImpact();
        }

        [RelayCommand]
        private void RemoveLine(StockAdjustmentLine line)
        {
            if (IsDocumentLocked) return;
            if (line != null)
            {
                AdjustmentLines.Remove(line);
                RecalculateImpact();
            }
        }

        [RelayCommand]
        private void UpdateLine()
        {
            if (IsDocumentLocked) return;
            if (SelectedLine != null)
            {
                SelectedLine.VarianceQty = SelectedLine.ActualQty - SelectedLine.SystemQty;
                SelectedLine.CostImpact = SelectedLine.VarianceQty * SelectedLine.UnitCost;
                RecalculateImpact();
            }
        }

        // --- SAVING EXECUTION ---
        [RelayCommand]
        private async Task SaveDraftAsync() => await SaveAdjustmentExecutionAsync(isDraft: true);

        [RelayCommand]
        private async Task PostAdjustmentAsync() => await SaveAdjustmentExecutionAsync(isDraft: false);

        private async Task SaveAdjustmentExecutionAsync(bool isDraft)
        {
            if (IsDocumentLocked) return;

            if (!AdjustmentLines.Any())
            {
                MessageBox.Show("Cannot save an empty Adjustment.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string actionText = isDraft ? "Save this adjustment as a Draft?" : "CRITICAL: Post Adjustment?\n\nThis will permanently deduct the selected batches and post the loss to the P&L.";
            var result = MessageBox.Show(actionText, "Confirm Save", MessageBoxButton.YesNo, isDraft ? MessageBoxImage.Question : MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var header = new StockAdjustmentHeader
                    {
                        AdjustmentDate = this.AdjustmentDate,
                        AuthorizedBy = this.AuthorizedBy.Trim(),
                        Reference = this.Reference.Trim(),
                        Remarks = this.Remarks.Trim(),
                        TotalImpact = this.TotalImpact,
                        CreatedBy = this.AuthorizedBy
                    };

                    await _adjustmentRepository.SaveAdjustmentAsync(header, AdjustmentLines.ToList(), isDraft);

                    MessageBox.Show(isDraft ? "Draft Saved." : "Adjustment Posted! Physical batches updated.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    if (!isDraft)
                    {
                        IsDocumentLocked = true;
                        DocumentStatus = "POSTED / LOCKED";
                    }
                    else
                    {
                        Clear();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Database Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void Clear()
        {
            Reference = string.Empty;
            Remarks = string.Empty;
            ScanBarcode = string.Empty;
            AdjustmentLines.Clear();
            ActiveMatrixVariants.Clear();
            IsDocumentLocked = false;
            DocumentStatus = "DRAFT / PENDING";
            RecalculateImpact();
        }
    }
}