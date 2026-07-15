using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Enums;
using POS.Core.Repositories;
using POS.Core.Services;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class StockAdjustmentHistoryViewModel : ObservableObject
    {
        private readonly StockAdjustmentRepository _repository;
        private readonly AuthService _authService;

        public StockAdjustmentHistoryViewModel(
            StockAdjustmentRepository repository,
            AuthService authService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        [ObservableProperty]
        private DateTime _fromDate = DateTime.Today.AddDays(-30);

        [ObservableProperty]
        private DateTime _toDate = DateTime.Today;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _selectedStatus = "All";

        [ObservableProperty]
        private StockAdjustmentHistoryRowDto? _selectedAdjustment;

        [ObservableProperty]
        private string _reverseReason = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        public ObservableCollection<string> StatusOptions { get; } = new(new[]
        {
            "All",
            "Posted",
            "Cancelled"
        });

        public ObservableCollection<StockAdjustmentHistoryRowDto> Adjustments { get; } = new();
        public ObservableCollection<StockAdjustmentHistoryLineDto> Lines { get; } = new();

        public bool CanReverseSelected =>
            !IsBusy &&
            SelectedAdjustment?.CanReverse == true &&
            IsCurrentUserManagerOrAdmin();

        partial void OnIsBusyChanged(bool value)
        {
            OnPropertyChanged(nameof(CanReverseSelected));
        }

        partial void OnSelectedAdjustmentChanged(StockAdjustmentHistoryRowDto? value)
        {
            OnPropertyChanged(nameof(CanReverseSelected));
            _ = LoadSelectedDetailAsync(value);
        }

        public async Task InitializeAsync()
        {
            await LoadHistoryAsync();
        }

        [RelayCommand]
        private async Task LoadHistoryAsync()
        {
            if (IsBusy)
                return;

            IsBusy = true;

            try
            {
                var rows = await _repository.SearchHistoryAsync(
                    FromDate,
                    ToDate,
                    SearchText,
                    SelectedStatus);

                Adjustments.Clear();
                foreach (StockAdjustmentHistoryRowDto row in rows)
                    Adjustments.Add(row);

                SelectedAdjustment = Adjustments.FirstOrDefault();
                StatusMessage = $"Loaded {Adjustments.Count} stock adjustment(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = "History load failed.";
                MessageBox.Show(
                    ex.Message,
                    "Stock Adjustment History",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadSelectedDetailAsync(StockAdjustmentHistoryRowDto? selected)
        {
            Lines.Clear();

            if (selected == null)
                return;

            try
            {
                StockAdjustmentHistoryDetailDto? detail =
                    await _repository.GetHistoryDetailAsync(selected.Id);

                if (detail == null || SelectedAdjustment?.Id != selected.Id)
                    return;

                foreach (StockAdjustmentHistoryLineDto line in detail.Lines)
                    Lines.Add(line);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Stock Adjustment Details",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private async Task ReverseSelectedAsync()
        {
            if (IsBusy || SelectedAdjustment == null)
                return;

            if (!CanReverseSelected)
            {
                MessageBox.Show(
                    "Only a posted adjustment can be reversed by an active Manager or Administrator.",
                    "Reversal Not Available",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            string reason = (ReverseReason ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                MessageBox.Show(
                    "Enter a clear reversal reason.",
                    "Reversal Reason Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult confirmation = MessageBox.Show(
                $"REVERSE STOCK ADJUSTMENT {SelectedAdjustment.AdjustmentNo}?\n\n" +
                "This creates opposite inventory movements, marks the original document Cancelled, " +
                "and cannot continue if later stock usage would make any batch negative.",
                "Confirm Stock Adjustment Reversal",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
                return;

            IsBusy = true;

            try
            {
                await _repository.ReverseAdjustmentAsync(
                    SelectedAdjustment.Id,
                    reason,
                    BuildActor());

                ReverseReason = string.Empty;
                StatusMessage = $"Reversed {SelectedAdjustment.AdjustmentNo}.";
                await LoadHistoryCoreAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = "Reversal blocked.";
                MessageBox.Show(
                    ex.Message,
                    "Stock Adjustment Reversal",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadHistoryCoreAsync()
        {
            var rows = await _repository.SearchHistoryAsync(
                FromDate,
                ToDate,
                SearchText,
                SelectedStatus);

            int? selectedId = SelectedAdjustment?.Id;

            Adjustments.Clear();
            foreach (StockAdjustmentHistoryRowDto row in rows)
                Adjustments.Add(row);

            SelectedAdjustment = selectedId.HasValue
                ? Adjustments.FirstOrDefault(x => x.Id == selectedId.Value)
                : Adjustments.FirstOrDefault();
        }

        private StockAdjustmentActorContext BuildActor()
        {
            var user = _authService.CurrentUser
                ?? throw new InvalidOperationException("Sign in again before reversing a stock adjustment.");

            return new StockAdjustmentActorContext
            {
                UserId = user.Id,
                Username = user.Username,
                Role = user.Role
            };
        }

        private bool IsCurrentUserManagerOrAdmin()
        {
            var role = _authService.CurrentUser?.Role;
            return role == UserRole.Manager || role == UserRole.Admin;
        }
    }
}
