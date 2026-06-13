using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    // DTO for Matrix Rapid Entry to calculate real-time Variances
    public partial class AdjustmentMatrixDto : ObservableObject
    {
        public int ItemVariantId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public decimal SystemQty { get; set; } = 0m;
        public decimal UnitCost { get; set; } = 0m;

        [ObservableProperty]
        private decimal _actualQty = 0m;

        // Auto-calculated properties
        public decimal Variance => ActualQty - SystemQty;
        public decimal CostImpact => Variance * UnitCost;

        partial void OnActualQtyChanged(decimal value)
        {
            // Triggers the UI to refresh the math fields instantly when the user types
            OnPropertyChanged(nameof(Variance));
            OnPropertyChanged(nameof(CostImpact));
        }
    }

    public partial class StockAdjustmentViewModel : ViewModelBase
    {
        private readonly StockAdjustmentRepository _adjustmentRepository;

        // --- ZONE 1: HEADER ---
        [ObservableProperty]
        private DateTime _adjustmentDate = DateTime.Now;

        [ObservableProperty]
        private string _authorizedBy = "Admin"; // Generally pulled from Auth Context

        [ObservableProperty]
        private string _reference = string.Empty;

        [ObservableProperty]
        private string _remarks = string.Empty;

        // --- ZONE 2: ENTRY CONSOLE ---
        [ObservableProperty]
        private string _scanBarcode = string.Empty;

        // --- ZONE 3: GRIDS ---
        public ObservableCollection<AdjustmentMatrixDto> ActiveMatrixVariants { get; set; } = new();
        public ObservableCollection<StockAdjustmentLine> AdjustmentLines { get; set; } = new();

        [ObservableProperty]
        private StockAdjustmentLine? _selectedLine;

        // --- FINANCIAL TOTALS ---
        [ObservableProperty]
        private decimal _totalImpact = 0m;

        public StockAdjustmentViewModel(StockAdjustmentRepository adjustmentRepository)
        {
            _adjustmentRepository = adjustmentRepository;
        }

        // --- MATH ENGINE ---
        private void RecalculateImpact()
        {
            if (!AdjustmentLines.Any())
            {
                TotalImpact = 0m;
                return;
            }

            // Sums the cost impact. Negative means a financial loss to the company.
            TotalImpact = AdjustmentLines.Sum(l => l.CostImpact);
        }

        // --- GRID ACTIONS ---
        [RelayCommand]
        private void AddMatrix()
        {
            // Only add items where the user actually entered a variance (Actual differs from System)
            var itemsToAdd = ActiveMatrixVariants.Where(v => v.Variance != 0).ToList();

            if (!itemsToAdd.Any())
            {
                MessageBox.Show("Please enter an Actual Qty that differs from the System Qty for at least one variant.", "No Variance", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var item in itemsToAdd)
            {
                // Prevent duplicate lines for the same item
                if (AdjustmentLines.Any(l => l.ItemVariantId == item.ItemVariantId))
                    continue;

                var newLine = new StockAdjustmentLine
                {
                    ItemVariantId = item.ItemVariantId,
                    SystemQty = item.SystemQty,
                    ActualQty = item.ActualQty,
                    VarianceQty = item.Variance,
                    UnitCost = item.UnitCost,
                    CostImpact = item.CostImpact,
                    ReasonCode = "Data Entry Error" // Default reason, can be updated in the edit line UI
                    // Note: Description, ItemCode, VariantDescription are UI bindings handled by DB Nav properties later,
                    // but for rapid UI prototyping, you may want a wrapper class for the main grid as well.
                };

                AdjustmentLines.Add(newLine);
            }

            ActiveMatrixVariants.Clear();
            RecalculateImpact();
        }

        [RelayCommand]
        private void RemoveLine(StockAdjustmentLine line)
        {
            if (line != null)
            {
                AdjustmentLines.Remove(line);
                RecalculateImpact();
            }
        }

        [RelayCommand]
        private void UpdateLine()
        {
            if (SelectedLine != null)
            {
                // Recalculate Variance and Impact when the user edits the line directly
                SelectedLine.VarianceQty = SelectedLine.ActualQty - SelectedLine.SystemQty;
                SelectedLine.CostImpact = SelectedLine.VarianceQty * SelectedLine.UnitCost;

                RecalculateImpact();

                // Force DataGrid to refresh the specific row visually
                var index = AdjustmentLines.IndexOf(SelectedLine);
                if (index >= 0)
                {
                    AdjustmentLines[index] = SelectedLine;
                }
            }
        }

        // --- SAVING EXECUTION ---
        [RelayCommand]
        private async Task SaveDraftAsync() => await SaveAdjustmentExecutionAsync(isDraft: true);

        // Note: Map this to your "POST ADJUSTMENT" button in XAML
        [RelayCommand]
        private async Task PostAdjustmentAsync() => await SaveAdjustmentExecutionAsync(isDraft: false);

        private async Task SaveAdjustmentExecutionAsync(bool isDraft)
        {
            if (!AdjustmentLines.Any())
            {
                MessageBox.Show("Cannot save an empty Adjustment. Please add variances.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string actionText = isDraft ? "Save this adjustment as a Draft?" : "CRITICAL: Post Adjustment? This will permanently overwrite physical stock levels and log P&L impacts.";
            var result = MessageBox.Show(actionText, "Confirm Save", MessageBoxButton.YesNo, isDraft ? MessageBoxImage.Question : MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var header = new StockAdjustmentHeader
                    {
                        AdjustmentNo = $"ADJ-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}",
                        AdjustmentDate = this.AdjustmentDate,
                        AuthorizedBy = this.AuthorizedBy.Trim(),
                        Reference = this.Reference.Trim(),
                        Remarks = this.Remarks.Trim(),
                        TotalImpact = this.TotalImpact,
                        CreatedBy = "Admin"
                    };

                    await _adjustmentRepository.SaveAdjustmentAsync(header, AdjustmentLines.ToList(), isDraft);

                    MessageBox.Show(isDraft ? "Draft Saved Successfully." : "Adjustment Posted! Physical stock updated.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    Clear();
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
            RecalculateImpact();
        }
    }
}