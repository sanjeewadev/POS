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
    public partial class GrnViewModel : ViewModelBase
    {
        private readonly GrnRepository _grnRepository;

        // --- ZONE 1: HEADER & SUPPLIER ---
        [ObservableProperty] private Supplier? _selectedSupplier;
        [ObservableProperty] private string _supplierInvoiceNo = string.Empty;
        [ObservableProperty] private DateTime _invoiceDate = DateTime.Now;
        [ObservableProperty] private DateTime _receivedDate = DateTime.Now;
        [ObservableProperty] private DateTime _dueDate = DateTime.Now.AddDays(30);
        [ObservableProperty] private string _documentStatus = "DRAFT";
        [ObservableProperty] private string _remarks = string.Empty;

        // --- ZONE 3: ENTRY MODES ---
        [ObservableProperty] private string _scanBarcode = string.Empty;
        [ObservableProperty] private string _matrixBatchNo = string.Empty;
        [ObservableProperty] private DateTime _matrixExpiryDate = DateTime.Now.AddYears(1);

        // --- FINANCIAL TOTALS ---
        [ObservableProperty] private decimal _subtotal = 0m;
        [ObservableProperty] private decimal _totalDiscountAmount = 0m;
        [ObservableProperty] private decimal _globalBillDiscount = 0m;
        [ObservableProperty] private decimal _freightAmount = 0m;
        [ObservableProperty] private decimal _netPayable = 0m;

        // --- COLLECTIONS ---
        public ObservableCollection<Supplier> Suppliers { get; set; } = new();
        public ObservableCollection<GrnLine> GrnLines { get; set; } = new();

        // This holds the variants of a selected parent item for Rapid Grid Entry
        public ObservableCollection<GrnLine> ActiveMatrixVariants { get; set; } = new();

        [ObservableProperty] private GrnLine? _selectedLine;

        public GrnViewModel(GrnRepository grnRepository)
        {
            _grnRepository = grnRepository;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            var suppliers = await _grnRepository.GetActiveSuppliersAsync();
            foreach (var sup in suppliers) Suppliers.Add(sup);
        }

        // --- THE FINANCIAL ENGINE: Dynamic Recalculation ---

        // Triggers automatically when Freight or Global Discount changes in the UI
        partial void OnFreightAmountChanged(decimal value) => RecalculateTotals();
        partial void OnGlobalBillDiscountChanged(decimal value) => RecalculateTotals();

        private void RecalculateTotals()
        {
            if (!GrnLines.Any())
            {
                Subtotal = 0;
                TotalDiscountAmount = 0;
                NetPayable = 0;
                return;
            }

            // 1. Calculate Base Subtotal
            Subtotal = GrnLines.Sum(l => l.LineTotal);
            decimal lineDiscounts = GrnLines.Sum(l => l.LineDiscount);

            TotalDiscountAmount = lineDiscounts + GlobalBillDiscount;
            NetPayable = Subtotal - GlobalBillDiscount + FreightAmount;

            // 2. Recalculate Landed Cost for every item (Proportional Value Distribution)
            // If you pay Rs. 1000 in freight, the most expensive items absorb a higher % of that freight
            decimal totalBaseValue = GrnLines.Sum(l => l.UnitCost * l.ReceivedQty);

            if (totalBaseValue > 0)
            {
                foreach (var line in GrnLines)
                {
                    decimal lineBaseValue = line.UnitCost * line.ReceivedQty;
                    decimal weightPercentage = lineBaseValue / totalBaseValue;

                    decimal allocatedFreight = FreightAmount * weightPercentage;
                    decimal allocatedGlobalDisc = GlobalBillDiscount * weightPercentage;

                    // Line Landed Total = (UnitCost * Qty) + Allocated Freight - Allocated Global Disc
                    decimal landedLineTotal = lineBaseValue + allocatedFreight - allocatedGlobalDisc;

                    // Divide by physical quantity to get per-unit Landed Cost
                    decimal totalPhysicalQty = line.ReceivedQty + line.FocQty; // Include FOC if amortizing

                    if (totalPhysicalQty > 0)
                    {
                        line.LandedCost = Math.Round(landedLineTotal / totalPhysicalQty, 2);
                    }
                }
            }
        }

        // --- GRID ACTIONS ---

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
            // Moves items from the Rapid Entry grid down into the main GRN ledger
            var itemsToAdd = ActiveMatrixVariants.Where(v => v.ReceivedQty > 0 || v.FocQty > 0).ToList();

            if (!itemsToAdd.Any())
            {
                MessageBox.Show("Please enter a Received Qty for at least one matrix variant.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var item in itemsToAdd)
            {
                // Calculate individual line total before adding
                item.LineTotal = (item.ReceivedQty * item.UnitCost) - item.LineDiscount;

                GrnLines.Add(item);
            }

            ActiveMatrixVariants.Clear();
            RecalculateTotals();
        }

        // --- POSTING EXECUTION ---

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

            if (!GrnLines.Any())
            {
                MessageBox.Show("Cannot post an empty GRN. Please add items.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                        GrnNumber = $"GRN-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}", // Auto-generate
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
                        NetPayable = this.NetPayable
                    };

                    await _grnRepository.PostGrnAsync(header, GrnLines.ToList());

                    MessageBox.Show($"GRN Posted Successfully! \nInventory and Accounts Payable Updated.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    Clear();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"CRITICAL ERROR: Transaction Rolled Back. \n\n{ex.Message}", "System Protection", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void Clear()
        {
            SelectedSupplier = null;
            SupplierInvoiceNo = string.Empty;
            Remarks = string.Empty;
            ScanBarcode = string.Empty;

            GlobalBillDiscount = 0;
            FreightAmount = 0;

            GrnLines.Clear();
            ActiveMatrixVariants.Clear();
            RecalculateTotals();

            DocumentStatus = "DRAFT";
        }
    }
}