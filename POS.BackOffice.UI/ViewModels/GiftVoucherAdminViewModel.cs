using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using POS.BackOffice.UI.Views.Dialogs;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Services.Documents;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class GiftVoucherAdminViewModel : ObservableObject
    {
        private readonly GiftVoucherRepository _giftVoucherRepository;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly AuthService _authService;
        private readonly GiftVoucherTextFormatter _voucherFormatter;

        public ObservableCollection<GiftVoucherSearchDto> Vouchers { get; } = new();
        public ObservableCollection<GiftVoucherTransaction> VoucherHistory { get; } = new();

        public ObservableCollection<string> StatusFilters { get; } = new()
        {
            "All",
            GiftVoucherStatusCodes.Created,
            GiftVoucherStatusCodes.Active,
            GiftVoucherStatusCodes.Redeemed,
            GiftVoucherStatusCodes.Expired,
            GiftVoucherStatusCodes.Blocked,
            GiftVoucherStatusCodes.Voided
        };

        [ObservableProperty] private GiftVoucherSearchDto? _selectedVoucher;
        [ObservableProperty] private string _selectedFilter = "All";
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private int _generateCount = 10;
        [ObservableProperty] private decimal _generateAmount = 1000m;
        [ObservableProperty] private DateTime? _generateExpiryDate = DateTime.Today.AddYears(1);
        [ObservableProperty] private string _generateBatchNo = string.Empty;
        [ObservableProperty] private string _generateDescription = string.Empty;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusText = "Ready.";
        [ObservableProperty] private string _statusColorHex = "#003366";

        public GiftVoucherAdminViewModel(
            GiftVoucherRepository giftVoucherRepository,
            IDbContextFactory<AppDbContext> contextFactory,
            AuthService authService,
            GiftVoucherTextFormatter voucherFormatter)
        {
            _giftVoucherRepository = giftVoucherRepository ?? throw new ArgumentNullException(nameof(giftVoucherRepository));
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _voucherFormatter = voucherFormatter ?? throw new ArgumentNullException(nameof(voucherFormatter));
        }

        public Task InitializeAsync() => RefreshAsync();

        partial void OnSelectedVoucherChanged(GiftVoucherSearchDto? value) =>
            _ = LoadSelectedHistoryAsync(value);

        partial void OnSelectedFilterChanged(string value) => _ = RefreshAsync();

        [RelayCommand]
        private async Task RefreshAsync()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                SetStatus("Loading gift vouchers...", "#3B82F6");
                int? selectedId = SelectedVoucher?.Id;
                var data = await _giftVoucherRepository.SearchVouchersAsync(
                    NormalizeFilter(SelectedFilter),
                    NormalizeText(SearchText),
                    500);

                Vouchers.Clear();
                foreach (GiftVoucherSearchDto voucher in data)
                    Vouchers.Add(voucher);

                SelectedVoucher = selectedId.HasValue
                    ? Vouchers.FirstOrDefault(row => row.Id == selectedId.Value)
                    : null;
                SetStatus($"Loaded {Vouchers.Count} voucher(s).", "#10B981");
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to load vouchers: {ex.Message}", "#EF4444");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private Task SearchAsync() => RefreshAsync();

        [RelayCommand]
        private async Task GenerateBatchAsync()
        {
            if (IsBusy)
                return;
            if (GenerateCount <= 0 || GenerateCount > 1000)
            {
                SetStatus("Voucher count must be between 1 and 1000.", "#EF4444");
                return;
            }
            if (GenerateAmount <= 0m)
            {
                SetStatus("Voucher amount must be greater than zero.", "#EF4444");
                return;
            }
            if (GenerateExpiryDate.HasValue && GenerateExpiryDate.Value.Date < DateTime.Today)
            {
                SetStatus("Expiry date cannot be in the past.", "#EF4444");
                return;
            }

            if (MessageBox.Show(
                    $"Generate {GenerateCount} one-time gift voucher(s) with value Rs. {GenerateAmount:N2}?",
                    "Generate Gift Voucher Batch",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                IsBusy = true;
                SetStatus("Generating gift voucher batch...", "#3B82F6");
                string description = NormalizeText(GenerateDescription);
                if (string.IsNullOrWhiteSpace(description))
                    description = $"Gift Voucher Rs. {GenerateAmount:N2}";

                var generated = await _giftVoucherRepository.GenerateVoucherBatchAsync(
                    GenerateCount,
                    GenerateAmount,
                    GenerateExpiryDate,
                    CurrentUserName(),
                    GenerateBatchNo,
                    description);

                GenerateBatchNo = string.Empty;
                SetStatus($"Generated {generated.Count} voucher(s). Print each voucher before issuing the batch.", "#10B981");
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to generate vouchers: {ex.Message}", "#EF4444");
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
            SetStatus("Generate form cleared.", "#003366");
        }

        [RelayCommand]
        private async Task PrintVoucherAsync(int giftVoucherId)
        {
            if (IsBusy || giftVoucherId <= 0)
                return;

            try
            {
                IsBusy = true;
                GiftVoucher voucher = await LoadVoucherAsync(giftVoucherId);
                await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
                StoreSettings settings = await context.StoreSettings.AsNoTracking().FirstOrDefaultAsync()
                    ?? new StoreSettings { StoreName = "My Store", CurrencySymbol = "Rs." };

                bool isReprint = voucher.PrintCount > 0;
                string text = _voucherFormatter.Format(voucher, settings, isReprint);
                var document = new FlowDocument
                {
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    PagePadding = new Thickness(36),
                    ColumnGap = 0,
                    ColumnWidth = double.PositiveInfinity
                };
                document.Blocks.Add(new Paragraph(new Run(text)) { Margin = new Thickness(0) });

                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() != true)
                {
                    SetStatus("Voucher printing cancelled.", "#F59E0B");
                    return;
                }

                printDialog.PrintDocument(
                    ((IDocumentPaginatorSource)document).DocumentPaginator,
                    $"Gift Voucher {voucher.VoucherNo}");
                await _giftVoucherRepository.MarkVoucherPrintedAsync(voucher.Id, CurrentUserName());
                SetStatus(isReprint ? "Voucher reprinted and audited." : "Voucher printed and audited.", "#10B981");
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Print Gift Voucher",
                    ex);

                SetStatus($"Voucher printing failed: {ex.Message}", "#EF4444");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task BlockVoucherAsync(int giftVoucherId)
        {
            await RunReasonActionAsync(
                giftVoucherId,
                "BLOCK GIFT VOUCHER",
                "Block",
                (id, user, reason) => _giftVoucherRepository.BlockVoucherAsync(id, user, reason));
        }

        [RelayCommand]
        private async Task UnblockVoucherAsync(int giftVoucherId)
        {
            await RunReasonActionAsync(
                giftVoucherId,
                "UNBLOCK GIFT VOUCHER",
                "Unblock",
                (id, user, reason) => _giftVoucherRepository.UnblockVoucherAsync(id, user, reason));
        }

        [RelayCommand]
        private async Task VoidVoucherAsync(int giftVoucherId)
        {
            await RunReasonActionAsync(
                giftVoucherId,
                "VOID UNSOLD GIFT VOUCHER",
                "Void",
                (id, user, reason) => _giftVoucherRepository.VoidVoucherAsync(id, user, reason));
        }

        [RelayCommand]
        private void ExportCsv()
        {
            if (Vouchers.Count == 0)
            {
                MessageBox.Show("There are no vouchers to export.", "Gift Voucher Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Export Gift Voucher Register",
                    Filter = "CSV files (*.csv)|*.csv",
                    FileName = $"GiftVoucher_Register_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };
                if (dialog.ShowDialog() != true)
                    return;

                var csv = new StringBuilder();
                csv.AppendLine("VoucherNo,Barcode,Value,Redeemed,Forfeited,Status,Batch,Expiry,Created,Activated,SoldInvoice,RedeemedDate,RedeemedInvoice,PrintCount,LastPrintedBy,Remarks");
                foreach (GiftVoucherSearchDto row in Vouchers)
                {
                    csv.AppendLine(string.Join(",", new[]
                    {
                        Csv(row.VoucherNo), Csv(row.Barcode), row.VoucherAmount.ToString("0.00", CultureInfo.InvariantCulture),
                        row.RedeemedAmount.ToString("0.00", CultureInfo.InvariantCulture), row.ForfeitedAmount.ToString("0.00", CultureInfo.InvariantCulture),
                        Csv(row.DisplayStatus), Csv(row.BatchNo), Csv(row.ExpiryDate?.ToString("yyyy-MM-dd") ?? string.Empty),
                        Csv(row.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")), Csv(row.ActivatedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty),
                        Csv(row.SoldInvoiceNo), Csv(row.RedeemedDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty), Csv(row.RedeemedInvoiceNo),
                        row.PrintCount.ToString(CultureInfo.InvariantCulture), Csv(row.LastPrintedBy), Csv(row.Remarks)
                    }));
                }

                File.WriteAllText(
                    dialog.FileName,
                    csv.ToString(),
                    new UTF8Encoding(true));

                SetStatus($"Exported {Vouchers.Count} voucher(s).", "#10B981");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Export Gift Voucher CSV",
                    ex);

                SetStatus("Gift Voucher export failed.", "#EF4444");

                MessageBox.Show(
                    "The Gift Voucher register could not be exported. " +
                    "BackOffice will remain open. Check the selected folder and " +
                    "available disk space, then try again.",
                    "Gift Voucher Export Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task RunReasonActionAsync(
            int giftVoucherId,
            string actionTitle,
            string actionVerb,
            Func<int, string, string, Task> action)
        {
            if (IsBusy || giftVoucherId <= 0)
                return;

            if (!_authService.IsManager)
            {
                MessageBox.Show(
                    "Manager or administrator authorization is required for this gift voucher action.",
                    "Gift Voucher Authorization",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var dialog = new GiftVoucherReasonDialog(actionTitle)
            {
                Owner = Application.Current.MainWindow
            };
            if (dialog.ShowDialog() != true)
                return;

            try
            {
                IsBusy = true;
                SetStatus($"{actionVerb}ing gift voucher...", "#3B82F6");
                await action(giftVoucherId, CurrentUserName(), dialog.Reason);
                SetStatus($"Gift voucher {actionVerb.ToLowerInvariant()}ed and audited.", "#10B981");
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to {actionVerb.ToLowerInvariant()} voucher: {ex.Message}", "#EF4444");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadSelectedHistoryAsync(GiftVoucherSearchDto? voucher)
        {
            VoucherHistory.Clear();
            if (voucher == null)
                return;

            try
            {
                var history = await _giftVoucherRepository.GetVoucherHistoryAsync(voucher.Id);
                foreach (GiftVoucherTransaction row in history)
                    VoucherHistory.Add(row);
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to load voucher history: {ex.Message}", "#EF4444");
            }
        }

        private async Task<GiftVoucher> LoadVoucherAsync(int giftVoucherId)
        {
            GiftVoucherSearchDto selected = Vouchers.FirstOrDefault(row => row.Id == giftVoucherId)
                ?? throw new InvalidOperationException("Select a valid gift voucher.");
            return await _giftVoucherRepository.GetByBarcodeOrVoucherNoAsync(selected.VoucherNo)
                ?? throw new InvalidOperationException("Gift voucher was not found.");
        }

        private string CurrentUserName() =>
            string.IsNullOrWhiteSpace(_authService.CurrentUser?.Username)
                ? Environment.UserName
                : _authService.CurrentUser.Username.Trim();

        private void SetStatus(string text, string color)
        {
            StatusText = text;
            StatusColorHex = color;
        }

        private static string NormalizeFilter(string? filter)
        {
            string value = NormalizeText(filter);
            if (string.IsNullOrWhiteSpace(value)) return "All";
            if (value.Equals("Inactive", StringComparison.OrdinalIgnoreCase)) return GiftVoucherStatusCodes.Created;
            if (value.Equals("Exhausted", StringComparison.OrdinalIgnoreCase)) return GiftVoucherStatusCodes.Redeemed;
            if (value.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)) return GiftVoucherStatusCodes.Voided;
            return value;
        }

        private static string NormalizeText(string? value) => (value ?? string.Empty).Trim();
        private static string Csv(string? value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
    }
}
