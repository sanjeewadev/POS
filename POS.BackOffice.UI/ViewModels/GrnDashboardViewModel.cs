using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.BackOffice.UI.Services;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Core.Services;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class GrnDashboardViewModel : ObservableObject
    {
        private readonly GrnHistoryRepository _grnHistoryRepository;
        private readonly SupplierRepository _supplierRepository;
        private readonly IMessageBoxService _messageBoxService;

        private bool _isInitialized;

        // =========================================================
        // FILTERS
        // =========================================================

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private Supplier? _selectedSupplierFilter;

        [ObservableProperty]
        private DateTime? _filterStartDate;

        [ObservableProperty]
        private DateTime? _filterEndDate;

        // =========================================================
        // SELECTION / DETAILS
        // =========================================================

        [ObservableProperty]
        private GrnSummaryDto? _selectedGrn;

        [ObservableProperty]
        private GrnDetailDto? _viewingGrnDetails;

        // =========================================================
        // COLLECTIONS
        // =========================================================

        public ObservableCollection<GrnSummaryDto> GrnDocuments { get; } = new();

        public ObservableCollection<Supplier> FilterSuppliers { get; } = new();

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        public bool IsDetailsOpen => ViewingGrnDetails != null;

        public GrnDashboardViewModel(
            GrnHistoryRepository grnHistoryRepository,
            SupplierRepository supplierRepository,
            IMessageBoxService messageBoxService)
        {
            _grnHistoryRepository = grnHistoryRepository ?? throw new ArgumentNullException(nameof(grnHistoryRepository));
            _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));

            FilterStartDate = DateTime.Now.AddDays(-30);
            FilterEndDate = DateTime.Now;
        }

        // =========================================================
        // INITIALIZE
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            IsBusy = true;

            try
            {
                await LoadFilterSuppliersAsync();
                await LoadDataInternalAsync();

                _isInitialized = true;
                StatusMessage = $"{GrnDocuments.Count} GRN document(s) loaded.";
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Initialize GRN Dashboard",
                    ex);

                StatusMessage = "Failed to initialize GRN dashboard.";

                _messageBoxService.ShowError(
                    $"Failed to initialize GRN dashboard:\n\n{ex.Message}",
                    "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadFilterSuppliersAsync()
        {
            var suppliers = await _supplierRepository.GetAllAsync();

            FilterSuppliers.Clear();

            foreach (var supplier in suppliers
                         .Where(s => !s.IsDeactivated)
                         .OrderBy(s => s.SupplierName)
                         .ThenBy(s => s.SupplierCode))
            {
                FilterSuppliers.Add(supplier);
            }
        }

        // =========================================================
        // FILTER CHANGE EVENTS
        // =========================================================

        partial void OnSearchTextChanged(string value)
        {
            StatusMessage = "Search text changed. Press Enter or click SEARCH.";
        }

        partial void OnSelectedSupplierFilterChanged(Supplier? value)
        {
            StatusMessage = "Supplier filter changed. Click SEARCH.";
        }

        partial void OnFilterStartDateChanged(DateTime? value)
        {
            StatusMessage = "Date filter changed. Click SEARCH.";
        }

        partial void OnFilterEndDateChanged(DateTime? value)
        {
            StatusMessage = "Date filter changed. Click SEARCH.";
        }

        partial void OnViewingGrnDetailsChanged(GrnDetailDto? value)
        {
            OnPropertyChanged(nameof(IsDetailsOpen));

            CloseDetailsCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsBusyChanged(bool value)
        {
            InitializeCommand.NotifyCanExecuteChanged();
            SearchCommand.NotifyCanExecuteChanged();
            RefreshDatabaseCommand.NotifyCanExecuteChanged();
            ClearFiltersCommand.NotifyCanExecuteChanged();
            ViewDetailsCommand.NotifyCanExecuteChanged();
            CloseDetailsCommand.NotifyCanExecuteChanged();
        }

        // =========================================================
        // LOAD / SEARCH
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task SearchAsync()
        {
            await LoadDataAsync();
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task RefreshDatabaseAsync()
        {
            await LoadDataAsync(refreshSuppliers: true);
        }

        private async Task LoadDataAsync(bool refreshSuppliers = false)
        {
            IsBusy = true;

            try
            {
                if (refreshSuppliers)
                    await LoadFilterSuppliersAsync();

                await LoadDataInternalAsync();
                StatusMessage = $"{GrnDocuments.Count} GRN document(s) loaded.";
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Search blocked.";

                _messageBoxService.ShowWarning(
                    ex.Message,
                    "Filter Error");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Load GRN Dashboard",
                    ex);

                StatusMessage = "Failed to load GRN documents.";

                _messageBoxService.ShowError(
                    $"Failed to load GRN documents:\n\n{ex.Message}",
                    "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadDataInternalAsync()
        {
            if (FilterStartDate.HasValue &&
                FilterEndDate.HasValue &&
                FilterEndDate.Value.Date < FilterStartDate.Value.Date)
            {
                throw new InvalidOperationException(
                    "Filter end date cannot be before start date.");
            }

            var data = await _grnHistoryRepository.GetGrnSummariesAsync(
                SearchText,
                SelectedSupplierFilter?.Id,
                statusFilter: "Posted",
                startDate: FilterStartDate,
                endDate: FilterEndDate,
                showCancelled: false);

            GrnDocuments.Clear();
            foreach (var grn in data)
                GrnDocuments.Add(grn);
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task ClearFiltersAsync()
        {
            SearchText = string.Empty;
            SelectedSupplierFilter = null;
            FilterStartDate = DateTime.Now.AddDays(-30);
            FilterEndDate = DateTime.Now;

            await LoadDataAsync();
        }

        // =========================================================
        // DETAILS
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanViewDetails))]
        private async Task ViewDetailsAsync(GrnSummaryDto? summary)
        {
            if (summary == null)
                return;

            IsBusy = true;

            try
            {
                var fullGrn = await _grnHistoryRepository.GetGrnDetailsAsync(summary.GrnHeaderId);

                if (fullGrn == null)
                {
                    _messageBoxService.ShowWarning(
                        "Selected GRN was not found.",
                        "Not Found");
                    return;
                }

                ViewingGrnDetails = fullGrn;
                SelectedGrn = summary;

                StatusMessage = $"Viewing {fullGrn.GrnNumber}.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load GRN details.";

                _messageBoxService.ShowError(
                    $"Failed to load GRN details:\n\n{ex.Message}",
                    "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanCloseDetails))]
        private void CloseDetails()
        {
            ViewingGrnDetails = null;
            StatusMessage = "Returned to GRN dashboard.";
        }

        private bool CanCloseDetails()
        {
            return !IsBusy && ViewingGrnDetails != null;
        }

        // =========================================================
        // COMMAND STATE
        // =========================================================

        private bool CanRunCommand()
        {
            return !IsBusy;
        }

        private bool CanViewDetails(GrnSummaryDto? summary)
        {
            return !IsBusy && summary != null;
        }
    }
}