using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class GiftVoucherAdminViewModel : ObservableObject
    {
        private readonly GiftVoucherRepository _giftVoucherRepository;

        // =========================================================
        // GRID
        // =========================================================

        public ObservableCollection<GiftVoucherSearchDto> Vouchers { get; } = new();

        public ObservableCollection<string> StatusFilters { get; } = new()
        {
            "All",
            "Created",
            "Active",
            "Redeemed",
            "Expired",
            "Blocked",
            "Cancelled"
        };

        [ObservableProperty]
        private GiftVoucherSearchDto? _selectedVoucher;

        [ObservableProperty]
        private string _selectedFilter = "All";

        [ObservableProperty]
        private string _searchText = string.Empty;

        // =========================================================
        // GENERATE BATCH INPUTS
        // =========================================================

        [ObservableProperty]
        private int _generateCount = 10;

        [ObservableProperty]
        private decimal _generateAmount = 1000m;

        [ObservableProperty]
        private DateTime? _generateExpiryDate = DateTime.Today.AddYears(1);

        [ObservableProperty]
        private string _generateBatchNo = string.Empty;

        [ObservableProperty]
        private string _generateDescription = string.Empty;

        // =========================================================
        // STATUS
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusText = "Ready.";

        [ObservableProperty]
        private string _statusColorHex = "#003366";

        public GiftVoucherAdminViewModel(GiftVoucherRepository giftVoucherRepository)
        {
            _giftVoucherRepository = giftVoucherRepository;
        }

        // =========================================================
        // INITIALIZE
        // =========================================================

        public async Task InitializeAsync()
        {
            await RefreshAsync();
        }

        // =========================================================
        // COMMANDS
        // =========================================================

        [RelayCommand]
        private async Task RefreshAsync()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                StatusText = "Loading gift vouchers...";
                StatusColorHex = "#3B82F6";

                Vouchers.Clear();

                string filter = NormalizeFilter(SelectedFilter);
                string search = NormalizeText(SearchText);

                var vouchers = await _giftVoucherRepository.SearchVouchersAsync(
                    filter,
                    search,
                    take: 500);

                foreach (var voucher in vouchers)
                    Vouchers.Add(voucher);

                StatusText = $"Loaded {Vouchers.Count} voucher(s).";
                StatusColorHex = "#10B981";
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to load vouchers: {ex.Message}";
                StatusColorHex = "#EF4444";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task GenerateBatchAsync()
        {
            if (IsBusy)
                return;

            if (GenerateCount <= 0)
            {
                StatusText = "Voucher count must be greater than zero.";
                StatusColorHex = "#EF4444";
                return;
            }

            if (GenerateCount > 1000)
            {
                StatusText = "Cannot generate more than 1000 vouchers at once.";
                StatusColorHex = "#EF4444";
                return;
            }

            if (GenerateAmount <= 0m)
            {
                StatusText = "Voucher amount must be greater than zero.";
                StatusColorHex = "#EF4444";
                return;
            }

            if (GenerateExpiryDate.HasValue &&
                GenerateExpiryDate.Value.Date < DateTime.Today)
            {
                StatusText = "Expiry date cannot be in the past.";
                StatusColorHex = "#EF4444";
                return;
            }

            string message =
                $"Generate {GenerateCount} gift voucher(s) with value Rs. {GenerateAmount:N2}?";

            var confirm = MessageBox.Show(
                message,
                "Generate Gift Voucher Batch",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                IsBusy = true;
                StatusText = "Generating gift voucher batch...";
                StatusColorHex = "#3B82F6";

                string description = NormalizeText(GenerateDescription);

                if (string.IsNullOrWhiteSpace(description))
                    description = $"Gift Voucher Rs. {GenerateAmount:N2}";

                var generated = await _giftVoucherRepository.GenerateVoucherBatchAsync(
                    GenerateCount,
                    GenerateAmount,
                    GenerateExpiryDate,
                    createdBy: "Admin",
                    batchNo: GenerateBatchNo,
                    description: description);

                StatusText = $"Generated {generated.Count} gift voucher(s).";
                StatusColorHex = "#10B981";

                GenerateBatchNo = string.Empty;

                await RefreshAsync();
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to generate vouchers: {ex.Message}";
                StatusColorHex = "#EF4444";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task BlockVoucherAsync(int giftVoucherId)
        {
            if (IsBusy)
                return;

            if (giftVoucherId <= 0)
            {
                StatusText = "Select a valid voucher to block.";
                StatusColorHex = "#EF4444";
                return;
            }

            var voucher = Vouchers.FirstOrDefault(v => v.Id == giftVoucherId);

            string voucherText = voucher == null
                ? $"Voucher ID {giftVoucherId}"
                : $"{voucher.VoucherNo} / Rs. {voucher.VoucherAmount:N2}";

            var confirm = MessageBox.Show(
                $"Block this gift voucher?\n\n{voucherText}\n\nBlocked vouchers cannot be sold or redeemed.",
                "Block Gift Voucher",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                IsBusy = true;
                StatusText = "Blocking gift voucher...";
                StatusColorHex = "#3B82F6";

                await _giftVoucherRepository.BlockVoucherAsync(
                    giftVoucherId,
                    blockedBy: "Admin",
                    reason: "Blocked from BackOffice.");

                StatusText = "Gift voucher blocked.";
                StatusColorHex = "#10B981";

                await RefreshAsync();
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to block voucher: {ex.Message}";
                StatusColorHex = "#EF4444";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task CancelVoucherAsync(int giftVoucherId)
        {
            if (IsBusy)
                return;

            if (giftVoucherId <= 0)
            {
                StatusText = "Select a valid voucher to cancel.";
                StatusColorHex = "#EF4444";
                return;
            }

            var voucher = Vouchers.FirstOrDefault(v => v.Id == giftVoucherId);

            string voucherText = voucher == null
                ? $"Voucher ID {giftVoucherId}"
                : $"{voucher.VoucherNo} / Rs. {voucher.VoucherAmount:N2}";

            var confirm = MessageBox.Show(
                $"Cancel this gift voucher?\n\n{voucherText}\n\nCancelled vouchers cannot be sold or redeemed.",
                "Cancel Gift Voucher",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                IsBusy = true;
                StatusText = "Cancelling gift voucher...";
                StatusColorHex = "#3B82F6";

                await _giftVoucherRepository.CancelVoucherAsync(
                    giftVoucherId,
                    cancelledBy: "Admin",
                    reason: "Cancelled from BackOffice.");

                StatusText = "Gift voucher cancelled.";
                StatusColorHex = "#10B981";

                await RefreshAsync();
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to cancel voucher: {ex.Message}";
                StatusColorHex = "#EF4444";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ClearGenerateForm()
        {
            GenerateCount = 10;
            GenerateAmount = 1000m;
            GenerateExpiryDate = DateTime.Today.AddYears(1);
            GenerateBatchNo = string.Empty;
            GenerateDescription = string.Empty;

            StatusText = "Generate form cleared.";
            StatusColorHex = "#003366";
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            await RefreshAsync();
        }

        [RelayCommand]
        private void ExportBarcodes()
        {
            MessageBox.Show(
                "Barcode export/printing will be connected after the voucher create/sell/redeem flow is stable.",
                "Gift Voucher Export",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            StatusText = "Barcode export is not connected yet.";
            StatusColorHex = "#F59E0B";
        }

        // =========================================================
        // PROPERTY CHANGES
        // =========================================================

        partial void OnSelectedFilterChanged(string value)
        {
            _ = RefreshAsync();
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string NormalizeFilter(string? filter)
        {
            string value = NormalizeText(filter);

            if (string.IsNullOrWhiteSpace(value))
                return "All";

            // Legacy filter support from old generated UI.
            if (value.Equals("Inactive", StringComparison.OrdinalIgnoreCase))
                return "Created";

            if (value.Equals("Exhausted", StringComparison.OrdinalIgnoreCase))
                return "Redeemed";

            return value;
        }
    }
}