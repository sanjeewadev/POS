using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Configuration;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Services.Documents;
using POS.Core.Services.Exports;
using POS.BackOffice.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class SupplierClaimsViewModel : ObservableObject
    {
        private readonly FreeItemClaimRepository _claimRepository;
        private readonly AuthService _authService;
        private readonly OperationalExportBuilder _exportBuilder;
        private readonly ExportDialogService _exportDialog;
        private readonly ExportAuthorizationService _authorization;

        public ObservableCollection<FreeItemClaimSearchDto> Claims { get; } = new();
        public ObservableCollection<FreeIssueLookupDto> Suppliers { get; } = new();
        public ObservableCollection<SupplierClaimGroupSummaryDto> GroupedSummaries { get; } = new();
        public ObservableCollection<string> StatusOptions { get; } = new()
        {
            "All",
            SupplierClaimStatusCodes.Draft,
            SupplierClaimStatusCodes.Submitted,
            SupplierClaimStatusCodes.Settled,
            SupplierClaimStatusCodes.Rejected
        };

        [ObservableProperty] private DateTime? _startDate = DateTime.Today.AddDays(-30);
        [ObservableProperty] private DateTime? _endDate = DateTime.Today;
        [ObservableProperty] private string _selectedStatus = "All";
        [ObservableProperty] private FreeIssueLookupDto? _selectedSupplier;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private FreeItemClaimSearchDto? _selectedClaim;
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusText = "Ready.";
        [ObservableProperty] private string _statusColorHex = "#374151";
        [ObservableProperty] private int _totalClaimCount;
        [ObservableProperty] private decimal _totalQuantity;
        [ObservableProperty] private decimal _returnedQuantity;
        [ObservableProperty] private decimal _netClaimValue;
        [ObservableProperty] private int _draftCount;
        [ObservableProperty] private int _submittedCount;
        [ObservableProperty] private int _settledCount;
        [ObservableProperty] private int _rejectedCount;

        public bool CanSubmit => SelectedClaim?.ClaimStatus == SupplierClaimStatusCodes.Draft;
        public bool CanSettle => SelectedClaim?.ClaimStatus == SupplierClaimStatusCodes.Submitted;
        public bool CanReject =>
            SelectedClaim?.ClaimStatus == SupplierClaimStatusCodes.Draft ||
            SelectedClaim?.ClaimStatus == SupplierClaimStatusCodes.Submitted;

        public SupplierClaimsViewModel(
            FreeItemClaimRepository claimRepository,
            AuthService authService,
            OperationalExportBuilder exportBuilder,
            ExportDialogService exportDialog,
            ExportAuthorizationService authorization)
        {
            _claimRepository = claimRepository ?? throw new ArgumentNullException(nameof(claimRepository));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _exportBuilder = exportBuilder ?? throw new ArgumentNullException(nameof(exportBuilder));
            _exportDialog = exportDialog ?? throw new ArgumentNullException(nameof(exportDialog));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        }

        partial void OnSelectedClaimChanged(FreeItemClaimSearchDto? value)
        {
            OnPropertyChanged(nameof(CanSubmit));
            OnPropertyChanged(nameof(CanSettle));
            OnPropertyChanged(nameof(CanReject));
        }

        public async Task InitializeAsync()
        {
            await RunBusyAsync(async () =>
            {
                await LoadSuppliersAsync();
                await LoadClaimsCoreAsync();
            });
        }

        [RelayCommand]
        public Task LoadClaimsAsync() => RunBusyAsync(LoadClaimsCoreAsync);

        [RelayCommand]
        private async Task SubmitAsync()
        {
            if (!CanSubmit || SelectedClaim == null)
                throw new InvalidOperationException("Select a Draft claim.");

            await RunBusyAsync(async () =>
            {
                await _claimRepository.MarkSubmittedAsync(
                    SelectedClaim.Id,
                    CurrentUsername(),
                    "Submitted from Supplier Claims page.");
                await LoadClaimsCoreAsync();
                SetStatus("Supplier claim submitted.", "#166534");
            });
        }

        public Task SettleSelectedAsync(string settlementType, string reference, string remarks) =>
            RunBusyAsync(async () =>
            {
                if (!CanSettle || SelectedClaim == null)
                    throw new InvalidOperationException("Select a Submitted claim.");

                await _claimRepository.MarkSettledAsync(
                    SelectedClaim.Id,
                    CurrentUsername(),
                    settlementType,
                    reference,
                    remarks);
                await LoadClaimsCoreAsync();
                SetStatus("Supplier claim settled.", "#166534");
            });

        public Task RejectSelectedAsync(string reason, string remarks) =>
            RunBusyAsync(async () =>
            {
                if (!CanReject || SelectedClaim == null)
                    throw new InvalidOperationException("Select a Draft or Submitted claim.");

                await _claimRepository.MarkRejectedAsync(
                    SelectedClaim.Id,
                    CurrentUsername(),
                    reason,
                    remarks);
                await LoadClaimsCoreAsync();
                SetStatus("Supplier claim rejected.", "#166534");
            });

        [RelayCommand]
        private async Task ExportCsvAsync()
        {
            await RunBusyAsync(async () =>
            {
                _authorization.EnsureBulkFinancialExportAllowed();
                var rows = await _claimRepository.GetSupplierClaimExportRowsAsync(
                    StartDate, EndDate, SelectedSupplier?.Id, SelectedStatus, SearchText);
                if (rows.Count == 0)
                    throw new InvalidOperationException("There are no supplier claims to export for the selected filters.");

                string csv = new SupplierClaimCsvFormatter().Format(rows);
                string? path = await _exportDialog.SaveCsvAsync(
                    "Save Supplier Claims CSV",
                    ExportFileNameHelper.Build($"Supplier_Claims_{DateTime.Now:yyyyMMdd_HHmm}", ".csv"),
                    csv);
                SetStatus(path == null ? "CSV export cancelled." : $"Exported {rows.Count} claim(s) to CSV.",
                    path == null ? "#374151" : "#166534");
            });
        }

        [RelayCommand]
        private Task ExportPdfAsync() => RunBusyAsync(() =>
        {
            _authorization.EnsureBulkFinancialExportAllowed();
            PdfTableDocumentDto document = _exportBuilder.BuildSupplierClaimStatement(
                Claims.ToList(), StartDate, EndDate, _authorization.CurrentUsername);
            string? path = _exportDialog.SaveTablePdf(
                "Save Supplier Claim Statement PDF",
                ExportFileNameHelper.Build($"Supplier_Claims_{DateTime.Now:yyyyMMdd_HHmm}", ".pdf"),
                document);
            SetStatus(path == null ? "PDF export cancelled." : $"Exported {Claims.Count} claim(s) to PDF.",
                path == null ? "#374151" : "#166534");
            return Task.CompletedTask;
        });

        private async Task LoadSuppliersAsync()
        {
            var rows = await _claimRepository.GetSupplierLookupsAsync();
            Suppliers.Clear();
            foreach (FreeIssueLookupDto row in rows)
                Suppliers.Add(row);
        }

        private async Task LoadClaimsCoreAsync()
        {
            FreeItemClaimSearchDto? prior = SelectedClaim;
            var rows = await _claimRepository.SearchClaimsAsync(
                StartDate,
                EndDate,
                SelectedStatus,
                SelectedSupplier?.Id,
                SearchText,
                take: 5000);
            Claims.Clear();
            foreach (FreeItemClaimSearchDto row in rows)
                Claims.Add(row);

            var groups = await _claimRepository.GetGroupedSummaryAsync(
                StartDate,
                EndDate,
                SelectedSupplier?.Id,
                SelectedStatus,
                SearchText);
            GroupedSummaries.Clear();
            foreach (SupplierClaimGroupSummaryDto group in groups)
                GroupedSummaries.Add(group);

            FreeIssueSummaryDto summary = await _claimRepository.GetFreeIssueSummaryAsync(
                StartDate,
                EndDate,
                SelectedSupplier?.Id,
                SelectedStatus,
                SearchText);
            TotalClaimCount = summary.TotalClaimCount;
            TotalQuantity = summary.TotalQuantity;
            ReturnedQuantity = summary.ReturnedQuantity;
            NetClaimValue = summary.NetClaimValue;
            DraftCount = summary.DraftCount;
            SubmittedCount = summary.SubmittedCount;
            SettledCount = summary.SettledCount;
            RejectedCount = summary.RejectedCount;

            SelectedClaim = prior == null ? Claims.FirstOrDefault() : Claims.FirstOrDefault(row => row.Id == prior.Id);
            SetStatus($"Loaded {Claims.Count} supplier claim(s).", "#166534");
        }

        private async Task RunBusyAsync(Func<Task> action)
        {
            if (IsLoading)
                return;

            try
            {
                IsLoading = true;
                await action();
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, "#B91C1C");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private string CurrentUsername() =>
            _authService.CurrentUser?.Username?.Trim() is { Length: > 0 } username
                ? username
                : throw new InvalidOperationException("A signed-in BackOffice user is required.");

        private void SetStatus(string text, string color)
        {
            StatusText = text;
            StatusColorHex = color;
        }


    }
}
