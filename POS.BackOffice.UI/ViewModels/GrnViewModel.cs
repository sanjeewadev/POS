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
    // ==========================================
    // THE LIVE GRID LINE WRAPPER
    // ==========================================
    // This allows the grid cells to instantly trigger math updates when typed in
    public partial class GrnLineItem : ObservableObject
    {
        public int ItemId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        [ObservableProperty] private int _receivedQty = 1;
        [ObservableProperty] private int _focQty = 0;
        [ObservableProperty] private decimal _unitCost = 0;
        [ObservableProperty] private decimal _lineDiscount = 0;
        [ObservableProperty] private bool _isTaxable = false;

        // Constants for business rules (e.g., 18% VAT)
        private const decimal VAT_RATE = 0.18m;

        // Auto-calculated Line Properties
        public decimal LineTax => IsTaxable ? ((ReceivedQty * UnitCost) - LineDiscount) * VAT_RATE : 0;
        public decimal LineTotal => (ReceivedQty * UnitCost) - LineDiscount + LineTax;

        // When any of the core numbers change, we notify the UI that the Total also changed
        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.PropertyName == nameof(ReceivedQty) ||
                e.PropertyName == nameof(UnitCost) ||
                e.PropertyName == nameof(LineDiscount) ||
                e.PropertyName == nameof(IsTaxable))
            {
                OnPropertyChanged(nameof(LineTax));
                OnPropertyChanged(nameof(LineTotal));
            }
        }
    }

    // ==========================================
    // THE MAIN GRN VIEW MODEL
    // ==========================================
    public partial class GrnViewModel : ObservableObject
    {
        private readonly SupplierRepository _supplierRepository;
        private readonly ItemRepository _itemRepository;
        private readonly GrnRepository _grnRepository;

        // --- Header Properties ---
        [ObservableProperty] private string _documentStatus = "DRAFT / RECEIVING";
        [ObservableProperty] private ObservableCollection<Supplier> _suppliers = new();
        [ObservableProperty] private Supplier? _selectedSupplier;
        [ObservableProperty] private string _supplierInvoiceNo = string.Empty;
        [ObservableProperty] private DateTime _receivedDate = DateTime.Now;
        [ObservableProperty] private DateTime _dueDate = DateTime.Now;

        // --- Scanner Properties ---
        [ObservableProperty] private string _barcodeSearchInput = string.Empty;
        [ObservableProperty] private string _quickQtyInput = "1";
        [ObservableProperty] private string _quickCostInput = "0";

        // --- The DataGrid ---
        public ObservableCollection<GrnLineItem> GrnLines { get; set; } = new();

        // --- Footer Properties ---
        [ObservableProperty] private string _remarks = string.Empty;
        [ObservableProperty] private decimal _globalBillDiscount = 0;
        [ObservableProperty] private decimal _freightAmount = 0;

        // Calculated Footer Totals
        [ObservableProperty] private decimal _subtotal = 0;
        [ObservableProperty] private decimal _totalDiscountAmount = 0;
        [ObservableProperty] private decimal _totalTaxAmount = 0;
        [ObservableProperty] private decimal _netPayable = 0;

        public GrnViewModel(SupplierRepository supplierRepository, ItemRepository itemRepository, GrnRepository grnRepository)
        {
            _supplierRepository = supplierRepository;
            _itemRepository = itemRepository;
            _grnRepository = grnRepository;

            // Wire up the spreadsheet logic!
            GrnLines.CollectionChanged += GrnLines_CollectionChanged;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            // One-Shot Loading for Suppliers
            var supps = await _supplierRepository.GetAllAsync();
            Suppliers = new ObservableCollection<Supplier>(supps.Where(s => s.IsActive));
        }

        // ==========================================
        // AUTO-TRIGGERS (The Smart Logic)
        // ==========================================

        // When Supplier changes, automatically calculate the Due Date!
        partial void OnSelectedSupplierChanged(Supplier? value)
        {
            if (value != null)
            {
                DueDate = ReceivedDate.AddDays(value.DefaultCreditDays);
            }
        }

        // When Global Discount or Freight changes, recalculate everything
        partial void OnGlobalBillDiscountChanged(decimal value) => CalculateGrandTotals();
        partial void OnFreightAmountChanged(decimal value) => CalculateGrandTotals();

        // ==========================================
        // SPREADSHEET WIRING
        // ==========================================

        private void GrnLines_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // If items are added, listen to them for cell changes
            if (e.NewItems != null)
            {
                foreach (GrnLineItem item in e.NewItems)
                {
                    item.PropertyChanged += GridCell_Changed;
                }
            }
            // If items are removed, stop listening
            if (e.OldItems != null)
            {
                foreach (GrnLineItem item in e.OldItems)
                {
                    item.PropertyChanged -= GridCell_Changed;
                }
            }
            CalculateGrandTotals();
        }

        private void GridCell_Changed(object? sender, PropertyChangedEventArgs e)
        {
            // Only recalculate if a number that affects the total changes
            if (e.PropertyName == nameof(GrnLineItem.LineTotal))
            {
                CalculateGrandTotals();
            }
        }

        private void CalculateGrandTotals()
        {
            // 1. Sum up the exact line values
            Subtotal = GrnLines.Sum(x => x.ReceivedQty * x.UnitCost);

            decimal sumOfLineDiscounts = GrnLines.Sum(x => x.LineDiscount);
            TotalDiscountAmount = sumOfLineDiscounts + GlobalBillDiscount;

            TotalTaxAmount = GrnLines.Sum(x => x.LineTax);

            // 2. Net Payable = (Subtotal - Discounts) + Freight + Taxes
            NetPayable = (Subtotal - TotalDiscountAmount) + FreightAmount + TotalTaxAmount;
        }

        // ==========================================
        // COMMANDS
        // ==========================================

        [RelayCommand]
        private async Task AddItemAsync()
        {
            if (string.IsNullOrWhiteSpace(BarcodeSearchInput)) return;

            // Use our lightning-fast repository to find the item
            var allItems = await _itemRepository.GetAllAsync();
            var matchedItem = allItems.FirstOrDefault(i =>
                i.Barcode == BarcodeSearchInput || i.ItemCode == BarcodeSearchInput);

            if (matchedItem == null)
            {
                MessageBox.Show("Item not found in Master Database.", "Scan Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                BarcodeSearchInput = string.Empty;
                return;
            }

            // Parse inputs safely
            int.TryParse(QuickQtyInput, out int parsedQty);
            decimal.TryParse(QuickCostInput, out decimal parsedCost);

            // If they didn't type a cost, use the default cost from Item Master
            decimal finalCost = parsedCost > 0 ? parsedCost : matchedItem.CostPrice;

            var newLine = new GrnLineItem
            {
                ItemId = matchedItem.Id,
                ItemCode = matchedItem.ItemCode,
                Description = matchedItem.Description,
                ReceivedQty = parsedQty > 0 ? parsedQty : 1,
                UnitCost = finalCost,
                IsTaxable = false // Defaults to false, user can check the box in the grid
            };

            GrnLines.Add(newLine);

            // Reset scanner for the next item
            BarcodeSearchInput = string.Empty;
            QuickQtyInput = "1";
            QuickCostInput = "0";
        }

        [RelayCommand]
        private void RemoveLine(GrnLineItem line)
        {
            if (line != null)
            {
                GrnLines.Remove(line);
            }
        }

        [RelayCommand]
        private void Clear()
        {
            SelectedSupplier = null;
            SupplierInvoiceNo = string.Empty;
            Remarks = string.Empty;
            GrnLines.Clear();
            GlobalBillDiscount = 0;
            FreightAmount = 0;
            DocumentStatus = "DRAFT / RECEIVING";
        }

        [RelayCommand]
        private void SaveDraft()
        {
            MessageBox.Show("Draft saved locally. Stock has not been updated.", "Draft Mode", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private async Task PostGrnAsync()
        {
            // 1. Strict Validation
            if (SelectedSupplier == null)
            {
                MessageBox.Show("Please select a Supplier.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(SupplierInvoiceNo))
            {
                MessageBox.Show("Please enter the Supplier Invoice Number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (GrnLines.Count == 0)
            {
                MessageBox.Show("You cannot post an empty GRN.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show($"Post this GRN?\nTotal Debt: Rs. {NetPayable:N2}\n\nThis will lock the document and update item costs.", "Confirm Post", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                // 2. Map the UI Data to the Database Entity (GrnHeader)
                var newGrn = new GrnHeader
                {
                    // We use a timestamp for the internal GRN Number
                    GrnNumber = "GRN" + DateTime.Now.ToString("yyMMddHHmmss"),
                    SupplierInvoiceNo = this.SupplierInvoiceNo,
                    SupplierId = SelectedSupplier.Id,
                    ReceivedDate = this.ReceivedDate,
                    DueDate = this.DueDate,
                    Remarks = this.Remarks,
                    Status = "POSTED",

                    Subtotal = this.Subtotal,
                    BillDiscountAmount = this.GlobalBillDiscount,
                    FreightAmount = this.FreightAmount,
                    TaxAmount = this.TotalTaxAmount,
                    NetPayable = this.NetPayable
                };

                // 3. Map the Grid Rows (GrnLineItem) to the Database Entities (GrnDetail)
                foreach (var line in GrnLines)
                {
                    newGrn.GrnDetails.Add(new GrnDetail
                    {
                        ItemId = line.ItemId,
                        ReceivedQty = line.ReceivedQty,
                        FocQty = line.FocQty,
                        UnitCost = line.UnitCost,
                        IsTaxable = line.IsTaxable,
                        LineTaxAmount = line.LineTax,
                        LineDiscount = line.LineDiscount,
                        LineTotal = line.LineTotal
                    });
                }

                // 4. Send it to the Engine!
                await _grnRepository.PostGrnAsync(newGrn);

                // 5. Success
                MessageBox.Show($"GRN Posted Successfully!\nReference: {newGrn.GrnNumber}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                Clear(); // Reset the UI for the next truck delivery
            }
            catch (Exception ex)
            {
                MessageBox.Show($"CRITICAL ERROR: Transaction Rolled Back.\n\nDetails: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}