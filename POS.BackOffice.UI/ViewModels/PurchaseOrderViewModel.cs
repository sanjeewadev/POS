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
    // DTO for Matrix Rapid Entry to avoid polluting the core database model
    public partial class PoMatrixEntryDto : ObservableObject
    {
        public int ItemVariantId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Uom { get; set; } = string.Empty;
        public decimal CurrentSOH { get; set; } = 0m;

        [ObservableProperty]
        private decimal _orderQty = 0m;

        [ObservableProperty]
        private decimal _expectedCost = 0m;

        [ObservableProperty]
        private decimal _lineDiscount = 0m;

        public string TaxCode { get; set; } = "Exempt";
    }

    public partial class PurchaseOrderViewModel : ViewModelBase
    {
        private readonly PoRepository _poRepository;

        // --- ZONE 1: HEADER ---
        [ObservableProperty]
        private Supplier? _selectedSupplier;

        [ObservableProperty]
        private DateTime _orderDate = DateTime.Now;

        [ObservableProperty]
        private DateTime _expectedDate = DateTime.Now.AddDays(7);

        [ObservableProperty]
        private string _currentUser = "Admin"; // Hardcoded for demo, normally pulled from Auth Service

        [ObservableProperty]
        private string _remarks = string.Empty;

        // --- ZONE 2: MATRIX ENTRY ---
        [ObservableProperty]
        private string _scanBarcode = string.Empty;

        [ObservableProperty]
        private PoMatrixEntryDto? _selectedMatrixVariant;

        // --- ZONE 3: EDIT LINE ---
        [ObservableProperty]
        private PoLine? _selectedLine;

        // --- FINANCIAL TOTALS ---
        [ObservableProperty]
        private decimal _globalBillDiscount = 0m;

        [ObservableProperty]
        private decimal _subtotal = 0m;

        [ObservableProperty]
        private decimal _totalDiscountAmount = 0m;

        [ObservableProperty]
        private decimal _totalTaxAmount = 0m;

        [ObservableProperty]
        private decimal _netPayable = 0m;

        // --- COLLECTIONS ---
        public ObservableCollection<Supplier> Suppliers { get; set; } = new();
        public ObservableCollection<PoMatrixEntryDto> ActiveMatrixVariants { get; set; } = new();
        public ObservableCollection<PoLine> PoLines { get; set; } = new();

        public PurchaseOrderViewModel(PoRepository poRepository)
        {
            _poRepository = poRepository;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            var suppliers = await _poRepository.GetActiveSuppliersAsync();
            foreach (var sup in suppliers) Suppliers.Add(sup);

            // Note: In production, scanning a barcode would fetch ItemVariants from ItemMasterRepository
            // and populate the ActiveMatrixVariants collection here.
        }

        // --- FINANCIAL MATH ENGINE ---
        partial void OnGlobalBillDiscountChanged(decimal value) => RecalculateTotals();

        private void RecalculateTotals()
        {
            if (!PoLines.Any())
            {
                Subtotal = 0;
                TotalDiscountAmount = 0;
                TotalTaxAmount = 0;
                NetPayable = 0;
                return;
            }

            // Calculate Base Subtotal (Qty * Expected Cost)
            Subtotal = PoLines.Sum(l => l.OrderQty * l.ExpectedCost);

            // Sum all line-level discounts
            decimal lineDiscounts = PoLines.Sum(l => l.LineDiscount);

            TotalDiscountAmount = lineDiscounts + GlobalBillDiscount;

            // Assume 18% VAT for items marked as such
            TotalTaxAmount = PoLines.Where(l => l.TaxCode == "VAT 18%").Sum(l => ((l.OrderQty * l.ExpectedCost) - l.LineDiscount) * 0.18m);

            NetPayable = Subtotal - TotalDiscountAmount + TotalTaxAmount;
        }

        // --- GRID ACTIONS ---
        [RelayCommand]
        private void AddMatrix()
        {
            var itemsToAdd = ActiveMatrixVariants.Where(v => v.OrderQty > 0).ToList();

            if (!itemsToAdd.Any())
            {
                MessageBox.Show("Please enter an Order Qty for at least one matrix variant.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var item in itemsToAdd)
            {
                var newLine = new PoLine
                {
                    ItemVariantId = item.ItemVariantId,
                    Uom = item.Uom,
                    OrderQty = item.OrderQty,
                    ExpectedCost = item.ExpectedCost,
                    LineDiscount = item.LineDiscount,
                    TaxCode = item.TaxCode,
                    // Note: In XAML you bound to 'Description', 'ItemCode', etc. 
                    // Entity Framework PoLine only saves ItemVariantId. The UI bindings 
                    // will rely on the ItemVariant navigation property once saved/loaded.
                };

                // Calculate Line Total
                newLine.TaxAmount = item.TaxCode == "VAT 18%" ? ((newLine.OrderQty * newLine.ExpectedCost) - newLine.LineDiscount) * 0.18m : 0m;
                newLine.LineTotal = (newLine.OrderQty * newLine.ExpectedCost) - newLine.LineDiscount + newLine.TaxAmount;

                PoLines.Add(newLine);
            }

            ActiveMatrixVariants.Clear();
            RecalculateTotals();
        }

        [RelayCommand]
        private void UpdateMatrixRow()
        {
            if (SelectedMatrixVariant == null) return;
            // The Grid is bound TwoWay, so edits to the DataGrid or the TextBoxes 
            // auto-sync. We just need to trigger a UI refresh if necessary.
        }

        [RelayCommand]
        private void UpdateLine()
        {
            if (SelectedLine != null)
            {
                SelectedLine.TaxAmount = SelectedLine.TaxCode == "VAT 18%" ? ((SelectedLine.OrderQty * SelectedLine.ExpectedCost) - SelectedLine.LineDiscount) * 0.18m : 0m;
                SelectedLine.LineTotal = (SelectedLine.OrderQty * SelectedLine.ExpectedCost) - SelectedLine.LineDiscount + SelectedLine.TaxAmount;
                RecalculateTotals();
            }
        }

        // --- SAVING EXECUTION ---
        [RelayCommand]
        private async Task SaveDraftAsync() => await SaveOrderAsync(isDraft: true);

        // Note: You need to add Command="{Binding ApproveAndSendCommand}" to the specific button in your XAML.
        [RelayCommand]
        private async Task ApproveAndSendAsync() => await SaveOrderAsync(isDraft: false);

        private async Task SaveOrderAsync(bool isDraft)
        {
            if (SelectedSupplier == null)
            {
                MessageBox.Show("Please select a Supplier.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!PoLines.Any())
            {
                MessageBox.Show("Cannot save an empty Purchase Order.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string actionText = isDraft ? "Save as Draft?" : "Approve and Finalize PO?";
            var result = MessageBox.Show(actionText, "Confirm Save", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var header = new PoHeader
                    {
                        PoNumber = $"PO-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}", // Auto-Gen
                        SupplierId = SelectedSupplier.Id,
                        OrderDate = this.OrderDate,
                        ExpectedDate = this.ExpectedDate,
                        Remarks = this.Remarks.Trim(),
                        Subtotal = this.Subtotal,
                        GlobalBillDiscount = this.GlobalBillDiscount,
                        TotalTaxAmount = this.TotalTaxAmount,
                        TotalDiscountAmount = this.TotalDiscountAmount,
                        NetPayable = this.NetPayable,
                        CreatedBy = this.CurrentUser
                    };

                    await _poRepository.SavePurchaseOrderAsync(header, PoLines.ToList(), isDraft);

                    MessageBox.Show($"Purchase Order {(isDraft ? "Draft Saved" : "Approved")} Successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
            SelectedSupplier = null;
            Remarks = string.Empty;
            ScanBarcode = string.Empty;
            GlobalBillDiscount = 0;
            PoLines.Clear();
            ActiveMatrixVariants.Clear();
            RecalculateTotals();
        }
    }
}