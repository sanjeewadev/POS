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
    }

    public partial class SupplierReturnViewModel : ViewModelBase
    {
        private readonly SupplierReturnRepository _returnRepository;

        // =========================================================
        // HEADER
        // =========================================================

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

        // =========================================================
        // DATA COLLECTIONS
        // =========================================================

        public ObservableCollection<Supplier> Suppliers { get; } = new();

        public ObservableCollection<GrnHeader> SupplierInvoices { get; } = new();

        public ObservableCollection<ReturnMatrixDto> ActiveMatrixVariants { get; } = new();

        public ObservableCollection<SupplierReturnLine> ReturnLines { get; } = new();

        public ObservableCollection<string> ReasonCodes { get; } = new();

        [ObservableProperty]
        private SupplierReturnLine? _selectedLine;

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
                    "Database Error",
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

        partial void OnSelectedSupplierChanged(Supplier? value)
        {
            SupplierInvoices.Clear();
            ActiveMatrixVariants.Clear();
            ReturnLines.Clear();

            SelectedInvoice = null;
            RecalculateTotals();

            if (value != null)
            {
                _ = LoadSupplierInvoicesAsync(value.Id);
            }
        }

        partial void OnSelectedInvoiceChanged(GrnHeader? value)
        {
            ActiveMatrixVariants.Clear();
        }

        partial void OnRestockingFeeChanged(decimal value)
        {
            if (value < 0)
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

                StatusMessage = $"Loaded {SupplierInvoices.Count} posted invoice(s) for selected supplier.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load supplier invoices.";

                MessageBox.Show(
                    $"Failed to load supplier invoices:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // LOAD GRN BATCH LINES
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

                StatusMessage = $"Loaded {ActiveMatrixVariants.Count} returnable batch line(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load GRN returnable batches.";

                MessageBox.Show(
                    $"Failed to load invoice history:\n\n{ex.Message}",
                    "Database Error",
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
                if (item.ItemBatchId <= 0)
                {
                    MessageBox.Show(
                        $"Batch is missing for item '{item.Description}'.",
                        "Validation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (item.ReturnQty <= 0)
                {
                    MessageBox.Show(
                        $"Return quantity must be greater than zero for '{item.Description}'.",
                        "Validation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (item.ReturnQty > item.MaxReturnQty)
                {
                    MessageBox.Show(
                        $"Cannot return {item.ReturnQty:N3} of '{item.Description}'. Maximum returnable quantity is {item.MaxReturnQty:N3}.",
                        "Return Quantity Exceeded",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(item.ReasonCode))
                {
                    MessageBox.Show(
                        $"Please select a reason for '{item.Description}'.",
                        "Validation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var existingLine = ReturnLines.FirstOrDefault(l => l.ItemBatchId == item.ItemBatchId);

                if (existingLine != null)
                {
                    decimal newQty = existingLine.ReturnQty + item.ReturnQty;

                    if (newQty > item.MaxReturnQty)
                    {
                        MessageBox.Show(
                            $"Cannot add more quantity for '{item.Description}'. Maximum returnable quantity is {item.MaxReturnQty:N3}.",
                            "Return Quantity Exceeded",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    existingLine.ReturnQty = newQty;
                    existingLine.CreditValue = Math.Round(existingLine.ReturnQty * existingLine.HistoricalCost, 2);
                    existingLine.ReasonCode = item.ReasonCode;
                    existingLine.LineRemarks = item.LineRemarks;

                    RefreshReturnLine(existingLine);
                    continue;
                }

                var newLine = new SupplierReturnLine
                {
                    GrnLineId = item.GrnLineId,
                    ItemVariantId = item.ItemVariantId,
                    ItemBatchId = item.ItemBatchId,

                    BatchNo = item.BatchNo,
                    ExpiryDate = item.ExpiryDate,

                    ReturnQty = item.ReturnQty,
                    HistoricalCost = item.HistoricalCost,
                    CreditValue = Math.Round(item.ReturnQty * item.HistoricalCost, 2),

                    ReasonCode = item.ReasonCode,
                    LineRemarks = item.LineRemarks,
                    LineStatus = "Open",

                    ItemCode = item.ItemCode,
                    Description = item.Description,
                    VariantDescription = item.VariantDescription,
                    CurrentBatchStock = item.CurrentBatchStock,
                    MaxReturnQty = item.MaxReturnQty
                };

                ReturnLines.Add(newLine);
            }

            foreach (var item in itemsToAdd)
                item.ReturnQty = 0m;

            RecalculateTotals();

            StatusMessage = $"Return cart contains {ReturnLines.Count} line(s).";
        }

        [RelayCommand]
        private void RemoveLine(SupplierReturnLine? line)
        {
            if (line == null)
                return;

            ReturnLines.Remove(line);
            RecalculateTotals();

            StatusMessage = $"Removed line. Return cart contains {ReturnLines.Count} line(s).";
        }

        [RelayCommand]
        private void UpdateLine()
        {
            if (SelectedLine == null)
                return;

            if (SelectedLine.ReturnQty <= 0)
            {
                MessageBox.Show(
                    "Return quantity must be greater than zero.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (SelectedLine.ReturnQty > SelectedLine.MaxReturnQty)
            {
                MessageBox.Show(
                    $"Maximum returnable quantity is {SelectedLine.MaxReturnQty:N3}.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedLine.ReasonCode))
            {
                MessageBox.Show(
                    "Reason code is required.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            SelectedLine.CreditValue = Math.Round(SelectedLine.ReturnQty * SelectedLine.HistoricalCost, 2);

            RefreshReturnLine(SelectedLine);
            RecalculateTotals();
        }

        private void RefreshReturnLine(SupplierReturnLine line)
        {
            int index = ReturnLines.IndexOf(line);

            if (index >= 0)
            {
                ReturnLines[index] = line;
                SelectedLine = line;
            }
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
        // SAVE / POST
        // =========================================================

        [RelayCommand]
        private async Task SaveDraftAsync()
        {
            await SaveReturnExecutionAsync(isDraft: true);
        }

        [RelayCommand]
        private async Task PostReturnAsync()
        {
            await SaveReturnExecutionAsync(isDraft: false);
        }

        private async Task SaveReturnExecutionAsync(bool isDraft)
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

            if (!ReturnLines.Any())
            {
                MessageBox.Show(
                    "Cannot save an empty supplier return.",
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

            RecalculateTotals();

            string message = isDraft
                ? "Save this supplier return as a draft?"
                : $"Post supplier return?\n\nThis will deduct physical batch stock and reduce supplier balance by Rs. {NetCredit:N2}.";

            var result = MessageBox.Show(
                message,
                isDraft ? "Save Draft" : "Post Supplier Return",
                MessageBoxButton.YesNo,
                isDraft ? MessageBoxImage.Question : MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            IsBusy = true;

            try
            {
                var header = new SupplierReturnHeader
                {
                    SupplierId = SelectedSupplier.Id,
                    GrnHeaderId = SelectedInvoice?.Id,
                    OriginalInvoiceNo = SelectedInvoice?.SupplierInvoiceNo ?? string.Empty,

                    ReturnDate = ReturnDate,
                    AuthorizedBy = AuthorizedBy.Trim(),
                    Remarks = Remarks.Trim(),

                    GrossCredit = GrossCredit,
                    RestockingFee = RestockingFee,
                    NetCredit = NetCredit,

                    CreatedBy = "Admin",
                    PostedBy = isDraft ? string.Empty : AuthorizedBy.Trim()
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
                        LineStatus = "Open",

                        ItemCode = l.ItemCode,
                        Description = l.Description,
                        VariantDescription = l.VariantDescription,
                        CurrentBatchStock = l.CurrentBatchStock,
                        MaxReturnQty = l.MaxReturnQty
                    })
                    .ToList();

                await _returnRepository.SaveSupplierReturnAsync(header, lines, isDraft);

                MessageBox.Show(
                    isDraft
                        ? "Supplier return draft saved successfully."
                        : "Supplier return posted successfully. Stock and supplier ledger were updated.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Clear();
            }
            catch (Exception ex)
            {
                StatusMessage = "Supplier return save/post failed.";

                MessageBox.Show(
                    $"Database Error:\n\n{ex.Message}",
                    "Supplier Return Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
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
            ReturnLines.Clear();

            RecalculateTotals();

            StatusMessage = "Ready.";
        }
    }
}