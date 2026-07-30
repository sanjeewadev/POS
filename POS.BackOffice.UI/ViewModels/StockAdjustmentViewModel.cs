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
using POS.Core.Services;
using POS.BackOffice.UI.Views.Dialogs;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class AdjustmentMatrixDto : ObservableObject
    {
        public int ItemVariantId { get; set; }
        public int ItemBatchId { get; set; }

        public string ItemCode { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

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

        public bool HasBatchTracking { get; set; }
        public bool HasExpiryTracking { get; set; }
        public bool IsGeneralStockBucket { get; set; }

        public string TrackingText => !HasBatchTracking ? "Average Cost" : HasExpiryTracking ? "Batch + Expiry" : "Batch";

        public string BatchNo { get; set; } = string.Empty;
        public string InternalBatchBarcode { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }

        public string BatchDisplayText => IsGeneralStockBucket ? "GENERAL" : BatchNo;
        public string BatchBarcodeDisplayText => IsGeneralStockBucket ? "-" : InternalBatchBarcode;

        public decimal SystemQty { get; set; } = 0m;

        [ObservableProperty]
        private decimal _unitCost = 0m;

        [ObservableProperty]
        private decimal _actualQty = 0m;

        public decimal Variance => ActualQty - SystemQty;
        public decimal CostImpact => Math.Round(Variance * UnitCost, 2);

        public string VarianceDirection
        {
            get
            {
                if (Variance > 0)
                    return "Increase";

                if (Variance < 0)
                    return "Decrease";

                return "No Change";
            }
        }

        partial void OnActualQtyChanged(decimal value)
        {
            OnPropertyChanged(nameof(Variance));
            OnPropertyChanged(nameof(CostImpact));
            OnPropertyChanged(nameof(VarianceDirection));
        }

        partial void OnUnitCostChanged(decimal value)
        {
            OnPropertyChanged(nameof(CostImpact));
        }
    }

    public partial class StockAdjustmentViewModel : ObservableObject
    {
        private readonly StockAdjustmentRepository _adjustmentRepository;
        private readonly AuthService _authService;
        private readonly StockAdjustmentHistoryViewModel _historyViewModel;

        // ItemMasterRepository and IDbContextFactory are kept for existing DI registration compatibility.
        public StockAdjustmentViewModel(
            StockAdjustmentRepository adjustmentRepository,
            ItemMasterRepository itemMasterRepository,
            IDbContextFactory<AppDbContext> contextFactory,
            AuthService authService,
            StockAdjustmentHistoryViewModel historyViewModel)
        {
            _adjustmentRepository = adjustmentRepository ?? throw new ArgumentNullException(nameof(adjustmentRepository));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _historyViewModel = historyViewModel ?? throw new ArgumentNullException(nameof(historyViewModel));
            AuthorizedBy = GetCurrentUserName();
        }

        [ObservableProperty]
        private DateTime _adjustmentDate = DateTime.Now;

        [ObservableProperty]
        private string _adjustmentMode = "Physical Count Correction";

        [ObservableProperty]
        private string _authorizedBy = string.Empty;

        [ObservableProperty]
        private string _documentStatus = "UNPOSTED";

        [ObservableProperty]
        private bool _isDocumentLocked = false;

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        public bool IsEntryEnabled => !IsDocumentLocked && !IsBusy;

        [ObservableProperty]
        private string _scanBarcode = string.Empty;

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

        [ObservableProperty]
        private decimal _totalImpact = 0m;

        [ObservableProperty]
        private decimal _totalIncreaseQty = 0m;

        [ObservableProperty]
        private decimal _totalDecreaseQty = 0m;

        partial void OnAdjustmentModeChanged(string value)
        {
            StatusMessage = $"Adjustment mode changed to {value}.";
        }

        partial void OnIsDocumentLockedChanged(bool value)
        {
            OnPropertyChanged(nameof(IsEntryEnabled));
        }

        partial void OnIsBusyChanged(bool value)
        {
            OnPropertyChanged(nameof(IsEntryEnabled));
        }

        [RelayCommand]
        private async Task AddItemAsync()
        {
            if (!CanEditDocument())
                return;

            string term = (ScanBarcode ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(term))
                return;

            IsBusy = true;

            try
            {
                var stockRows = await _adjustmentRepository.GetActiveBatchesByBarcodeAsync(term);

                if (!stockRows.Any())
                {
                    MessageBox.Show(
                        $"No active stock row found for '{term}'.\n\nAverage-cost items must have a GENERAL stock bucket.\nBatch-tracked items must have a real GRN batch.",
                        "No Stock Row",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    ScanBarcode = string.Empty;
                    StatusMessage = "No stock row found.";
                    return;
                }

                ActiveMatrixVariants.Clear();

                foreach (var row in stockRows)
                {
                    ActiveMatrixVariants.Add(new AdjustmentMatrixDto
                    {
                        ItemVariantId = row.ItemVariantId,
                        ItemBatchId = row.ItemBatchId,

                        ItemCode = row.ItemCode,
                        VariantDescription = row.VariantDescription,
                        Description = row.Description,

                        HasBatchTracking = row.HasBatchTracking,
                        HasExpiryTracking = row.HasExpiryTracking,
                        IsGeneralStockBucket = row.IsGeneralStockBucket,

                        BatchNo = row.BatchNo,
                        InternalBatchBarcode = row.InternalBatchBarcode,
                        ExpiryDate = row.ExpiryDate,

                        SystemQty = row.SystemQty,
                        ActualQty = row.SystemQty,
                        UnitCost = row.UnitCost
                    });
                }

                ScanBarcode = string.Empty;

                int averageRows = stockRows.Count(r => !r.HasBatchTracking);
                int batchRows = stockRows.Count(r => r.HasBatchTracking);

                StatusMessage = stockRows.Count == 1
                    ? stockRows[0].HasBatchTracking ? "Exact batch stock row loaded." : "GENERAL stock bucket loaded."
                    : $"Loaded {stockRows.Count} stock row(s). Average-cost: {averageRows}, Batch-tracked: {batchRows}.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load stock rows.";

                MessageBox.Show(
                    $"Failed to load stock rows:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void AddMatrix()
        {
            if (!CanEditDocument())
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
                if (item.ActualQty < 0)
                {
                    MessageBox.Show(
                        $"Actual quantity cannot be negative for '{item.DisplayDescription} / {item.BatchDisplayText}'.",
                        "Validation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                if (item.Variance > 0 && item.UnitCost <= 0)
                {
                    MessageBox.Show(
                        $"Enter a positive Unit Cost for stock increase on '{item.DisplayDescription} / {item.BatchDisplayText}'.",
                        "Unit Cost Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                if (AdjustmentMode == "Stock Increase" && item.Variance <= 0)
                {
                    MessageBox.Show(
                        "Stock Increase mode can only queue rows where Actual Qty is greater than System Qty.",
                        "Validation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                if (AdjustmentMode == "Stock Decrease" && item.Variance >= 0)
                {
                    MessageBox.Show(
                        "Stock Decrease mode can only queue rows where Actual Qty is less than System Qty.",
                        "Validation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                if (AdjustmentLines.Any(l => l.ItemBatchId == item.ItemBatchId))
                {
                    MessageBox.Show(
                        $"Stock row '{item.BatchDisplayText}' is already queued. Remove the existing queued row first if you want to change it.",
                        "Duplicate Stock Row",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }
            }

            foreach (var item in changedItems)
            {
                AdjustmentLines.Add(new StockAdjustmentLine
                {
                    ItemBatchId = item.ItemBatchId,
                    ItemVariantId = item.ItemVariantId,

                    ItemCode = item.ItemCode,
                    VariantDescription = item.VariantDescription,
                    Description = item.Description,

                    HasBatchTracking = item.HasBatchTracking,
                    HasExpiryTracking = item.HasExpiryTracking,
                    IsGeneralStockBucket = item.IsGeneralStockBucket,

                    BatchNo = item.BatchNo,
                    InternalBatchBarcode = item.InternalBatchBarcode,
                    ExpiryDate = item.ExpiryDate,

                    SystemQty = item.SystemQty,
                    ActualQty = item.ActualQty,
                    VarianceQty = item.Variance,

                    UnitCost = item.UnitCost,
                    CostImpact = Math.Round(item.CostImpact, 2),

                    ReasonCode = string.Empty,
                    LineRemarks = string.Empty,
                    LineStatus = "Open"
                });
            }

            ActiveMatrixVariants.Clear();
            RecalculateImpact();

            StatusMessage = $"{changedItems.Count} variance line(s) queued.";
        }

        [RelayCommand]
        private void RemoveLine(StockAdjustmentLine? line)
        {
            if (!CanEditDocument() || line == null)
                return;

            AdjustmentLines.Remove(line);
            RecalculateImpact();

            StatusMessage = "Line removed.";
        }

        [RelayCommand]
        private void UpdateLine()
        {
            if (!CanEditDocument() || SelectedLine == null)
                return;

            SelectedLine.VarianceQty = SelectedLine.ActualQty - SelectedLine.SystemQty;
            SelectedLine.CostImpact = Math.Round(SelectedLine.VarianceQty * SelectedLine.UnitCost, 2);

            RecalculateImpact();
        }

        public void RecalculateImpact()
        {
            foreach (var line in AdjustmentLines)
            {
                line.VarianceQty = line.ActualQty - line.SystemQty;
                line.CostImpact = Math.Round(line.VarianceQty * line.UnitCost, 2);
            }

            TotalImpact = Math.Round(AdjustmentLines.Sum(l => l.CostImpact), 2);

            TotalIncreaseQty = AdjustmentLines
                .Where(l => l.VarianceQty > 0)
                .Sum(l => l.VarianceQty);

            TotalDecreaseQty = AdjustmentLines
                .Where(l => l.VarianceQty < 0)
                .Sum(l => Math.Abs(l.VarianceQty));

            CollectionViewSource.GetDefaultView(AdjustmentLines)?.Refresh();
        }

        [RelayCommand]
        private async Task PostAdjustmentAsync()
        {
            if (!CanEditDocument())
                return;

            RecalculateImpact();

            if (!ValidateBeforePost())
                return;

            var result = MessageBox.Show(
                "POST STOCK ADJUSTMENT?\n\nThis will permanently update physical stock and write inventory ledger transactions.\n\nThis action cannot be edited after posting.",
                "Confirm Stock Adjustment Posting",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            IsBusy = true;

            try
            {
                var header = new StockAdjustmentHeader
                {
                    AdjustmentDate = AdjustmentDate,
                    AdjustmentMode = AdjustmentMode.Trim(),
                    AuthorizedBy = AuthorizedBy.Trim(),
                    Reference = string.Empty,
                    Remarks = string.Empty,

                    TotalImpact = TotalImpact,
                    TotalIncreaseQty = TotalIncreaseQty,
                    TotalDecreaseQty = TotalDecreaseQty,

                    CreatedBy = AuthorizedBy.Trim(),
                    PostedBy = AuthorizedBy.Trim()
                };

                var lines = AdjustmentLines
                    .Select(CloneLineForSave)
                    .ToList();

                var savedHeader = await _adjustmentRepository.SaveAdjustmentAsync(
                    header,
                    lines,
                    BuildActor(),
                    isDraft: false);

                MessageBox.Show(
                    $"Stock adjustment posted successfully.\n\nDocument No: {savedHeader.AdjustmentNo}",
                    "Stock Adjustment Posted",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DocumentStatus = "POSTED / LOCKED";
                IsDocumentLocked = true;
                StatusMessage = $"Posted: {savedHeader.AdjustmentNo}";
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Posting blocked.";
                MessageBox.Show(ex.Message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                string message = ex.InnerException?.Message ?? ex.Message;
                StatusMessage = "Posting failed.";
                MessageBox.Show($"Transaction rolled back.\n\n{message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool ValidateBeforePost()
        {
            if (string.IsNullOrWhiteSpace(AdjustmentMode))
            {
                MessageBox.Show("Adjustment mode is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!AdjustmentModes.Contains(AdjustmentMode))
            {
                MessageBox.Show("Invalid adjustment mode.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (AdjustmentDate.Date > DateTime.Now.Date.AddDays(1))
            {
                MessageBox.Show("Adjustment date cannot be in the far future.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (_authService.CurrentUser == null)
            {
                MessageBox.Show("Sign in again before posting a stock adjustment.", "Authorization", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!_authService.IsManager)
            {
                MessageBox.Show("Manager or Administrator privileges are required to post a stock adjustment.", "Authorization", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            AuthorizedBy = GetCurrentUserName();

            if (string.IsNullOrWhiteSpace(AuthorizedBy))
            {
                MessageBox.Show("Authenticated user could not be identified.", "Authorization", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (AuthorizedBy.Trim().Length > 50)
            {
                MessageBox.Show("Authorized By cannot be longer than 50 characters.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!AdjustmentLines.Any())
            {
                MessageBox.Show("Cannot post an empty stock adjustment.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var duplicateBatch = AdjustmentLines.GroupBy(l => l.ItemBatchId).FirstOrDefault(g => g.Count() > 1);
            if (duplicateBatch != null)
            {
                MessageBox.Show("Same stock row cannot be queued twice.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            foreach (var line in AdjustmentLines)
            {
                line.ReasonCode = (line.ReasonCode ?? string.Empty).Trim();
                line.LineRemarks = (line.LineRemarks ?? string.Empty).Trim();
                line.VarianceQty = line.ActualQty - line.SystemQty;
                line.CostImpact = Math.Round(line.VarianceQty * line.UnitCost, 2);

                string rowName = BuildLineName(line);

                if (line.ItemBatchId <= 0)
                {
                    MessageBox.Show("Invalid stock row found in adjustment queue.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (line.VarianceQty == 0)
                {
                    MessageBox.Show($"Line '{rowName}' has no variance.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (line.ActualQty < 0)
                {
                    MessageBox.Show($"Actual quantity cannot be negative for '{rowName}'.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (AdjustmentMode == "Stock Increase" && line.VarianceQty <= 0)
                {
                    MessageBox.Show("Stock Increase mode can only contain positive variance lines.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (AdjustmentMode == "Stock Decrease" && line.VarianceQty >= 0)
                {
                    MessageBox.Show("Stock Decrease mode can only contain negative variance lines.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (line.UnitCost <= 0)
                {
                    MessageBox.Show($"Enter a positive Unit Cost for '{rowName}'.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(line.ReasonCode))
                {
                    MessageBox.Show($"Reason code is required for '{rowName}'.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (!ReasonCodes.Contains(line.ReasonCode))
                {
                    MessageBox.Show($"Invalid reason code '{line.ReasonCode}' for '{rowName}'.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (line.ReasonCode.Length > 50)
                {
                    MessageBox.Show($"Reason code is too long for '{rowName}'.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (line.LineRemarks.Length > 250)
                {
                    MessageBox.Show($"Line remarks are too long for '{rowName}'.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                LineStatus = "Posted"
            };
        }

        [RelayCommand]
        private void Clear()
        {
            if (IsBusy)
                return;

            AdjustmentDate = DateTime.Now;
            AdjustmentMode = "Physical Count Correction";
            AuthorizedBy = GetCurrentUserName();
            ScanBarcode = string.Empty;

            AdjustmentLines.Clear();
            ActiveMatrixVariants.Clear();

            TotalImpact = 0m;
            TotalIncreaseQty = 0m;
            TotalDecreaseQty = 0m;

            SelectedLine = null;
            IsDocumentLocked = false;
            DocumentStatus = "UNPOSTED";
            StatusMessage = "Ready.";
        }

        [RelayCommand]
        private async Task OpenHistoryAsync()
        {
            try
            {
                await _historyViewModel.InitializeAsync();

                var dialog = new StockAdjustmentHistoryDialog
                {
                    DataContext = _historyViewModel,
                    Owner = Application.Current?.MainWindow
                };

                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Stock Adjustment History",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private StockAdjustmentActorContext BuildActor()
        {
            var user = _authService.CurrentUser
                ?? throw new InvalidOperationException("Sign in again before posting a stock adjustment.");

            return new StockAdjustmentActorContext
            {
                UserId = user.Id,
                Username = user.Username,
                Role = user.Role
            };
        }

        private string GetCurrentUserName()
        {
            return (_authService.CurrentUser?.Username ?? string.Empty).Trim();
        }

        private bool CanEditDocument()
        {
            return !IsDocumentLocked && !IsBusy;
        }

        private static string BuildLineName(StockAdjustmentLine line)
        {
            string description = (line.Description ?? string.Empty).Trim();
            string variant = (line.VariantDescription ?? string.Empty).Trim();
            string batch = line.BatchDisplayText;

            if (!string.IsNullOrWhiteSpace(variant) &&
                !variant.Equals("Standard", StringComparison.OrdinalIgnoreCase))
            {
                description = $"{description} - {variant}";
            }

            return !string.IsNullOrWhiteSpace(batch)
                ? $"{description} / {batch}"
                : description;
        }
    }
}
