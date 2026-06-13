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
    // DTO for Matrix Rapid Entry to isolate UI state from Database Models
    public partial class ReturnMatrixDto : ObservableObject
    {
        public int ItemVariantId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public decimal CurrentSOH { get; set; } = 0m;
        public decimal HistoricalCost { get; set; } = 0m;

        [ObservableProperty]
        private decimal _returnQty = 0m;
    }

    public partial class SupplierReturnViewModel : ViewModelBase
    {
        private readonly ReturnRepository _returnRepository;

        // --- ZONE 1: HEADER ---
        [ObservableProperty]
        private Supplier? _selectedSupplier;

        [ObservableProperty]
        private GrnHeader? _selectedInvoice;

        [ObservableProperty]
        private DateTime _returnDate = DateTime.Now;

        [ObservableProperty]
        private string _authorizedBy = "Admin";

        [ObservableProperty]
        private string _remarks = string.Empty;

        // --- ZONE 2: ENTRY CONSOLE ---
        [ObservableProperty]
        private string _scanBarcode = string.Empty;

        // --- ZONE 3: GRIDS ---
        public ObservableCollection<ReturnMatrixDto> ActiveMatrixVariants { get; set; } = new();
        public ObservableCollection<ReturnLine> ReturnLines { get; set; } = new();

        [ObservableProperty]
        private ReturnLine? _selectedLine;

        // --- FINANCIAL TOTALS ---
        [ObservableProperty]
        private decimal _grossCredit = 0m;

        [ObservableProperty]
        private decimal _restockingFee = 0m;

        [ObservableProperty]
        private decimal _netCredit = 0m;

        public ObservableCollection<Supplier> Suppliers { get; set; } = new();
        public ObservableCollection<GrnHeader> SupplierInvoices { get; set; } = new();

        public SupplierReturnViewModel(ReturnRepository returnRepository)
        {
            _returnRepository = returnRepository;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            var suppliers = await _returnRepository.GetActiveSuppliersAsync();
            foreach (var sup in suppliers) Suppliers.Add(sup);
        }

        // --- AUTO-TRIGGERS & MATH ENGINE ---

        partial void OnSelectedSupplierChanged(Supplier? value)
        {
            if (value != null)
            {
                _ = LoadSupplierInvoicesAsync(value.Id);
            }
            else
            {
                SupplierInvoices.Clear();
            }
        }

        private async Task LoadSupplierInvoicesAsync(int supplierId)
        {
            SupplierInvoices.Clear();
            var invoices = await _returnRepository.GetSupplierInvoicesAsync(supplierId);
            foreach (var inv in invoices) SupplierInvoices.Add(inv);
        }

        partial void OnRestockingFeeChanged(decimal value) => RecalculateTotals();

        private void RecalculateTotals()
        {
            if (!ReturnLines.Any())
            {
                GrossCredit = 0m;
                NetCredit = 0m;
                return;
            }

            // Sums the exact historical cost credit values
            GrossCredit = ReturnLines.Sum(l => l.CreditValue);

            // Subtract supplier penalties to get the true ledger impact
            NetCredit = GrossCredit - RestockingFee;
        }

        // --- GRID ACTIONS ---

        [RelayCommand]
        private async Task LoadInvoiceAsync()
        {
            if (SelectedInvoice == null) return;

            // Simulated Logic: In production, this would query the GRN Lines for the selected invoice
            // and populate ActiveMatrixVariants with those exact items and their Historical Landed Costs.
            MessageBox.Show($"Loaded historical items and costs for Invoice: {SelectedInvoice.SupplierInvoiceNo}", "Invoice Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void AddMatrix()
        {
            var itemsToAdd = ActiveMatrixVariants.Where(v => v.ReturnQty > 0).ToList();

            if (!itemsToAdd.Any())
            {
                MessageBox.Show("Please enter a Return Qty for at least one variant.", "No Qty Entered", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var item in itemsToAdd)
            {
                // CRITICAL SHIELD: Stock On Hand Validation
                if (item.ReturnQty > item.CurrentSOH)
                {
                    MessageBox.Show($"Cannot return {item.ReturnQty} units of '{item.VariantDescription}'. You only have {item.CurrentSOH} in stock.", "Stock Limit Exceeded", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Check if already in the grid to prevent duplicates
                if (ReturnLines.Any(l => l.ItemVariantId == item.ItemVariantId))
                    continue;

                var newLine = new ReturnLine
                {
                    ItemVariantId = item.ItemVariantId,
                    ReturnQty = item.ReturnQty,
                    HistoricalCost = item.HistoricalCost,
                    CreditValue = item.ReturnQty * item.HistoricalCost,
                    ReasonCode = "Damaged / Defective" // Default reason
                };

                ReturnLines.Add(newLine);
            }

            ActiveMatrixVariants.Clear();
            RecalculateTotals();
        }

        [RelayCommand]
        private void RemoveLine(ReturnLine line)
        {
            if (line != null)
            {
                ReturnLines.Remove(line);
                RecalculateTotals();
            }
        }

        [RelayCommand]
        private void UpdateLine()
        {
            if (SelectedLine != null)
            {
                // Recalculate Credit Value when the user edits the line directly
                SelectedLine.CreditValue = SelectedLine.ReturnQty * SelectedLine.HistoricalCost;

                RecalculateTotals();

                // Force DataGrid to visually refresh
                var index = ReturnLines.IndexOf(SelectedLine);
                if (index >= 0)
                {
                    ReturnLines[index] = SelectedLine;
                }
            }
        }

        // --- SAVING EXECUTION ---
        [RelayCommand]
        private async Task SaveDraftAsync() => await SaveReturnExecutionAsync(isDraft: true);

        // Map this to the POST button
        [RelayCommand]
        private async Task PostReturnAsync() => await SaveReturnExecutionAsync(isDraft: false);

        private async Task SaveReturnExecutionAsync(bool isDraft)
        {
            if (SelectedSupplier == null)
            {
                MessageBox.Show("Please select a Supplier.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ReturnLines.Any())
            {
                MessageBox.Show("Cannot save an empty Return Note.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string actionText = isDraft ? "Save this Return as a Draft?" : $"CRITICAL: Post Return Note?\nThis will deduct stock and decrease supplier debt by Rs. {NetCredit:N2}.";
            var result = MessageBox.Show(actionText, "Confirm Save", MessageBoxButton.YesNo, isDraft ? MessageBoxImage.Question : MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var header = new ReturnHeader
                    {
                        ReturnNumber = $"RTN-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}",
                        SupplierId = SelectedSupplier.Id,
                        OriginalInvoiceNo = SelectedInvoice?.SupplierInvoiceNo ?? string.Empty,
                        ReturnDate = this.ReturnDate,
                        AuthorizedBy = this.AuthorizedBy.Trim(),
                        Remarks = this.Remarks.Trim(),
                        GrossCredit = this.GrossCredit,
                        RestockingFee = this.RestockingFee,
                        NetCredit = this.NetCredit,
                        CreatedBy = "Admin"
                    };

                    await _returnRepository.SaveSupplierReturnAsync(header, ReturnLines.ToList(), isDraft);

                    MessageBox.Show(isDraft ? "Draft Saved Successfully." : "Debit Note Posted! Physical stock and ledger updated.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
            SelectedInvoice = null;
            Remarks = string.Empty;
            ScanBarcode = string.Empty;
            RestockingFee = 0m;

            ReturnLines.Clear();
            ActiveMatrixVariants.Clear();
            RecalculateTotals();
        }
    }
}