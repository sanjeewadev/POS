using System;
using System.Collections.Generic;
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
    public partial class GrnViewModel : ObservableObject
    {
        private readonly GrnRepository _grnRepository;
        private readonly ItemMasterRepository _itemMasterRepository;
        private readonly PoRepository _poRepository;

        // --- ZONE 1: HEADER & SUPPLIER ---
        [ObservableProperty] private Supplier? _selectedSupplier;
        [ObservableProperty] private bool _isSupplierSelectionEnabled = true;

        [ObservableProperty] private string _supplierInvoiceNo = string.Empty;
        [ObservableProperty] private DateTime _invoiceDate = DateTime.Now;
        [ObservableProperty] private DateTime _receivedDate = DateTime.Now;
        [ObservableProperty] private DateTime _dueDate = DateTime.Now.AddDays(30);
        [ObservableProperty] private string _documentStatus = "DRAFT";
        [ObservableProperty] private string _remarks = string.Empty;

        // --- ZONE 2: PO LINKING ---
        public ObservableCollection<PoHeader> OpenPurchaseOrders { get; set; } = new();
        [ObservableProperty] private PoHeader? _selectedPO;

        // --- ZONE 3: ENTRY MODES ---
        [ObservableProperty] private string _scanBarcode = string.Empty;
        [ObservableProperty] private string _matrixBatchNo = string.Empty;
        [ObservableProperty] private DateTime? _matrixExpiryDate = null;
        [ObservableProperty] private string _matrixFilterText = string.Empty;

        // --- PURIFIED FINANCIAL TOTALS ---
        [ObservableProperty] private decimal _subtotal = 0m;
        [ObservableProperty] private decimal _totalDiscountAmount = 0m;
        [ObservableProperty] private decimal _globalBillDiscount = 0m;
        [ObservableProperty] private decimal _freightAmount = 0m;
        [ObservableProperty] private decimal _netPayable = 0m;

        // --- COLLECTIONS ---
        public ObservableCollection<Supplier> Suppliers { get; set; } = new();
        public ObservableCollection<GrnLine> GrnLines { get; set; } = new();
        public ObservableCollection<ItemMasterSummaryDto> AvailableItems { get; set; } = new();

        private List<GrnLine> _allMatrixVariants = new();
        public ObservableCollection<GrnLine> ActiveMatrixVariants { get; set; } = new();

        [ObservableProperty] private GrnLine? _selectedLine;
        [ObservableProperty] private ItemMasterSummaryDto? _selectedItem;

        public GrnViewModel(GrnRepository grnRepository, ItemMasterRepository itemMasterRepository, PoRepository poRepository)
        {
            _grnRepository = grnRepository;
            _itemMasterRepository = itemMasterRepository;
            _poRepository = poRepository;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            var suppliers = await _grnRepository.GetActiveSuppliersAsync();
            foreach (var sup in suppliers) Suppliers.Add(sup);

            var items = await _itemMasterRepository.GetSummariesAsync();
            foreach (var item in items) AvailableItems.Add(item);

            var openPos = await _poRepository.GetOpenPurchaseOrdersAsync();
            foreach (var po in openPos) OpenPurchaseOrders.Add(po);
        }

        // ==============================================================================
        // --- PO-TO-GRN CONVERSION ENGINE ---
        // ==============================================================================
        [RelayCommand]
        private async Task LoadPoAsync()
        {
            if (SelectedPO == null)
            {
                MessageBox.Show("Please select a Purchase Order from the dropdown to load.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var fullPo = await _grnRepository.GetApprovedPoDetailsAsync(SelectedPO.Id);
            if (fullPo == null) return;

            // Lock the supplier so data integrity is maintained
            SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == fullPo.SupplierId);
            IsSupplierSelectionEnabled = false;

            GrnLines.Clear();

            foreach (var poLine in fullPo.PoLines)
            {
                // Calculate outstanding backorder quantity
                decimal outstandingQty = poLine.OrderQty - poLine.ReceivedQty;

                if (outstandingQty > 0)
                {
                    var grnLine = new GrnLine
                    {
                        ItemVariantId = poLine.ItemVariantId,
                        ItemCode = poLine.ItemVariant?.ItemParent?.ItemCode ?? "UNKNOWN",
                        VariantDescription = poLine.ItemVariant?.VariantDescription ?? string.Empty,
                        Description = poLine.ItemVariant?.ItemParent?.ItemName ?? "Unknown Item",
                        Barcode = poLine.ItemVariant?.Barcode ?? string.Empty,
                        Uom = poLine.Uom,

                        OrderedQty = poLine.OrderQty,
                        ReceivedQty = outstandingQty, // Default to receiving the rest of the order

                        // Pure Cost Mapping (Retail/Wholesale intentionally excluded)
                        UnitCost = poLine.ExpectedCost,
                        LineDiscount = poLine.LineDiscount
                    };

                    GrnLines.Add(grnLine);
                }
            }

            RecalculateTotals();
        }

        // ==============================================================================
        // --- MATRIX ENGINE & FILTERING ---
        // ==============================================================================
        partial void OnSelectedItemChanged(ItemMasterSummaryDto? value)
        {
            if (value == null) return;
            _ = LoadVariantsForGridAsync(value.ParentId);
        }

        partial void OnMatrixFilterTextChanged(string value) => ApplyMatrixFilter();

        private async Task LoadVariantsForGridAsync(int parentId)
        {
            _allMatrixVariants.Clear();
            var variants = await _itemMasterRepository.GetVariantsByParentIdAsync(parentId);

            foreach (var variant in variants)
            {
                var newLine = new GrnLine
                {
                    ItemVariantId = variant.Id,
                    ItemCode = variant.ItemParent?.ItemCode ?? "UNKNOWN",
                    VariantDescription = variant.VariantDescription,
                    Description = variant.ItemParent?.ItemName ?? "Unknown Item",
                    Barcode = variant.Barcode,
                    Uom = variant.ItemParent?.BaseUom ?? "PCS",
                    UnitCost = variant.CostPrice,
                    ReceivedQty = 0
                };

                _allMatrixVariants.Add(newLine);
            }
            ApplyMatrixFilter();
        }

        private void ApplyMatrixFilter()
        {
            ActiveMatrixVariants.Clear();
            var query = _allMatrixVariants.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(MatrixFilterText))
            {
                var search = MatrixFilterText.ToLower();
                query = query.Where(v => v.VariantDescription.ToLower().Contains(search) ||
                                         v.Barcode.ToLower().Contains(search));
            }

            foreach (var item in query) ActiveMatrixVariants.Add(item);
        }

        // ==============================================================================
        // --- BARCODE SEARCH ENGINE ---
        // ==============================================================================
        [RelayCommand]
        private async Task AddItemAsync()
        {
            if (string.IsNullOrWhiteSpace(ScanBarcode)) return;

            var variant = await _itemMasterRepository.GetItemByBarcodeAsync(ScanBarcode.Trim());

            if (variant == null)
            {
                MessageBox.Show($"Barcode '{ScanBarcode}' not found.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                ScanBarcode = string.Empty;
                return;
            }

            var newLine = new GrnLine
            {
                ItemVariantId = variant.Id,
                ItemCode = variant.ItemParent?.ItemCode ?? "UNKNOWN",
                VariantDescription = variant.VariantDescription,
                Description = variant.ItemParent?.ItemName ?? "Unknown Item",
                Barcode = variant.Barcode,
                Uom = variant.ItemParent?.BaseUom ?? "PCS",
                UnitCost = variant.CostPrice,
                ReceivedQty = 1
            };

            GrnLines.Add(newLine);
            ScanBarcode = string.Empty;
            RecalculateTotals();
        }

        // ==============================================================================
        // --- THE DETERMINISTIC FINANCIAL ENGINE ---
        // ==============================================================================
        partial void OnFreightAmountChanged(decimal value) => RecalculateTotals();
        partial void OnGlobalBillDiscountChanged(decimal value) => RecalculateTotals();

        [RelayCommand]
        public void RecalculateTotals()
        {
            if (!GrnLines.Any())
            {
                Subtotal = 0; TotalDiscountAmount = 0; NetPayable = 0;
                return;
            }

            // 1. Raw Line Math (Pure gross cost)
            foreach (var line in GrnLines)
            {
                line.LineTotal = (line.ReceivedQty * line.UnitCost) - line.LineDiscount;
            }

            Subtotal = GrnLines.Sum(l => l.LineTotal);
            decimal lineDiscounts = GrnLines.Sum(l => l.LineDiscount);

            TotalDiscountAmount = lineDiscounts + GlobalBillDiscount;
            NetPayable = Subtotal - GlobalBillDiscount + FreightAmount;

            // 2. Landed Cost Allocation (Proportional Weighting)
            decimal totalBaseValue = GrnLines.Sum(l => l.LineTotal);

            if (totalBaseValue > 0)
            {
                foreach (var line in GrnLines)
                {
                    decimal weightPercentage = line.LineTotal / totalBaseValue;
                    decimal allocatedFreight = FreightAmount * weightPercentage;
                    decimal allocatedGlobalDisc = GlobalBillDiscount * weightPercentage;

                    decimal landedLineTotal = line.LineTotal + allocatedFreight - allocatedGlobalDisc;

                    if (line.ReceivedQty > 0)
                    {
                        line.LandedCost = Math.Round(landedLineTotal / line.ReceivedQty, 2);
                    }
                    else
                    {
                        line.LandedCost = 0;
                    }
                }
            }
        }

        // ==============================================================================
        // --- GRID ACTIONS ---
        // ==============================================================================
        [RelayCommand]
        private void RemoveLine(GrnLine line)
        {
            if (line != null)
            {
                GrnLines.Remove(line);
                RecalculateTotals();
            }
        }

        [RelayCommand]
        private void AddMatrix()
        {
            var itemsToAdd = _allMatrixVariants.Where(v => v.ReceivedQty > 0).ToList();

            if (!itemsToAdd.Any())
            {
                MessageBox.Show("Please enter a Received Qty for at least one matrix variant.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var item in itemsToAdd)
            {
                if (string.IsNullOrWhiteSpace(item.BatchNo) && !string.IsNullOrWhiteSpace(MatrixBatchNo))
                    item.BatchNo = MatrixBatchNo;

                if (MatrixExpiryDate.HasValue)
                    item.ExpiryDate = MatrixExpiryDate;

                GrnLines.Add(item);
            }

            _allMatrixVariants.Clear();
            ActiveMatrixVariants.Clear();
            SelectedItem = null;
            MatrixFilterText = string.Empty;

            RecalculateTotals();
        }

        // ==============================================================================
        // --- POSTING EXECUTION ---
        // ==============================================================================
        [RelayCommand]
        private async Task PostGrnAsync()
        {
            if (SelectedSupplier == null)
            {
                MessageBox.Show("Please select a Supplier.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(SupplierInvoiceNo))
            {
                MessageBox.Show("Supplier Invoice Number is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!GrnLines.Any(l => l.ReceivedQty > 0))
            {
                MessageBox.Show("Cannot post an empty GRN. Please ensure at least one item has a received quantity.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"Post GRN for Rs. {NetPayable:N2}? \nThis will irreversibly update Inventory and Supplier Ledgers.",
                                         "Confirm Post", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var header = new GrnHeader
                    {
                        PurchaseOrderId = SelectedPO?.Id,
                        SupplierId = SelectedSupplier.Id,
                        SupplierInvoiceNo = this.SupplierInvoiceNo.Trim(),
                        InvoiceDate = this.InvoiceDate,
                        ReceivedDate = this.ReceivedDate,
                        DueDate = this.DueDate,
                        Remarks = this.Remarks.Trim(),
                        Subtotal = this.Subtotal,
                        GlobalBillDiscount = this.GlobalBillDiscount,
                        FreightAmount = this.FreightAmount,
                        TotalDiscountAmount = this.TotalDiscountAmount,
                        NetPayable = this.NetPayable,
                        CreatedBy = "Admin" // Note: Wire this to your actual auth service later
                    };

                    // Only send lines that actually have a received quantity
                    var validLines = GrnLines.Where(l => l.ReceivedQty > 0).ToList();

                    await _grnRepository.PostGrnAsync(header, validLines);

                    MessageBox.Show($"GRN Posted Successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    Clear();
                }
                catch (Exception ex)
                {
                    string errorMsg = ex.InnerException?.Message ?? ex.Message;
                    MessageBox.Show($"CRITICAL ERROR: Transaction Rolled Back. \n\n{errorMsg}", "System Protection", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void Clear()
        {
            SelectedPO = null;
            IsSupplierSelectionEnabled = true;

            SelectedSupplier = null;
            SupplierInvoiceNo = string.Empty;
            Remarks = string.Empty;
            ScanBarcode = string.Empty;
            MatrixFilterText = string.Empty;

            GlobalBillDiscount = 0;
            FreightAmount = 0;
            SelectedItem = null;

            GrnLines.Clear();
            _allMatrixVariants.Clear();
            ActiveMatrixVariants.Clear();

            RecalculateTotals();
            DocumentStatus = "DRAFT";
        }
    }
}