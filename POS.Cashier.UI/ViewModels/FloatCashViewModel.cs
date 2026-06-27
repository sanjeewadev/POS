using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using POS.Cashier.UI.Messages;
using POS.Core.Repositories;
using POS.Core.Services;

namespace POS.Cashier.UI.ViewModels
{
    public partial class FloatCashViewModel : ObservableObject
    {
        private readonly TillRepository _tillRepository;
        private readonly AuthService _authService;
        private int _shiftId;

        [ObservableProperty] private int? _qty5000;
        [ObservableProperty] private int? _qty1000;
        [ObservableProperty] private int? _qty500;
        [ObservableProperty] private int? _qty100;
        [ObservableProperty] private int? _qty50;
        [ObservableProperty] private int? _qty20;
        [ObservableProperty] private int? _qty10;
        [ObservableProperty] private int? _qty5;

        [ObservableProperty] private decimal _currentSystemFloat = 0m;
        [ObservableProperty] private string _referenceNote = string.Empty;

        public event Action<bool>? ActionCompleted;

        public FloatCashViewModel(TillRepository tillRepository, AuthService authService)
        {
            _tillRepository = tillRepository;
            _authService = authService;
        }

        public void Initialize(int shiftId)
        {
            _shiftId = shiftId;
            _ = LoadCurrentFloatAsync();
        }

        private async Task LoadCurrentFloatAsync()
        {
            if (_shiftId > 0)
            {
                CurrentSystemFloat = await _tillRepository.GetCurrentFloatBalanceAsync(_shiftId);
            }
        }

        public decimal Total5000 => (Qty5000 ?? 0) * 5000m;
        public decimal Total1000 => (Qty1000 ?? 0) * 1000m;
        public decimal Total500 => (Qty500 ?? 0) * 500m;
        public decimal Total100 => (Qty100 ?? 0) * 100m;
        public decimal Total50 => (Qty50 ?? 0) * 50m;
        public decimal Total20 => (Qty20 ?? 0) * 20m;
        public decimal Total10 => (Qty10 ?? 0) * 10m;
        public decimal Total5 => (Qty5 ?? 0) * 5m;

        public decimal GrandTotal => Total5000 + Total1000 + Total500 + Total100 + Total50 + Total20 + Total10 + Total5;

        partial void OnQty5000Changed(int? value) => Recalculate();
        partial void OnQty1000Changed(int? value) => Recalculate();
        partial void OnQty500Changed(int? value) => Recalculate();
        partial void OnQty100Changed(int? value) => Recalculate();
        partial void OnQty50Changed(int? value) => Recalculate();
        partial void OnQty20Changed(int? value) => Recalculate();
        partial void OnQty10Changed(int? value) => Recalculate();
        partial void OnQty5Changed(int? value) => Recalculate();

        private void Recalculate()
        {
            OnPropertyChanged(nameof(Total5000));
            OnPropertyChanged(nameof(Total1000));
            OnPropertyChanged(nameof(Total500));
            OnPropertyChanged(nameof(Total100));
            OnPropertyChanged(nameof(Total50));
            OnPropertyChanged(nameof(Total20));
            OnPropertyChanged(nameof(Total10));
            OnPropertyChanged(nameof(Total5));
            OnPropertyChanged(nameof(GrandTotal));
        }

        [RelayCommand]
        private async Task FloatInAsync()
        {
            if (GrandTotal <= 0)
            {
                MessageBox.Show("Please count the cash before submitting.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_shiftId == 0) return;

            try
            {
                string managerName = _authService.CurrentUser?.FullName ?? "Unknown Manager";
                await _tillRepository.InjectFloatAsync(_shiftId, GrandTotal, managerName);

                // NEW: Broadcast to the Top-Bar (Green Color)
                WeakReferenceMessenger.Default.Send(new TopBarNotificationMessage(($"Float Added: Rs. {GrandTotal:N2}", "#10B981")));
                ActionCompleted?.Invoke(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task FloatOutAsync()
        {
            if (GrandTotal <= 0)
            {
                MessageBox.Show("Please count the cash before submitting.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_shiftId == 0) return;

            try
            {
                string managerName = _authService.CurrentUser?.FullName ?? "Unknown Manager";
                await _tillRepository.WithdrawFloatAsync(_shiftId, GrandTotal, managerName);

                // NEW: Broadcast to the Top-Bar (Orange Color)
                WeakReferenceMessenger.Default.Send(new TopBarNotificationMessage(($"Safe Drop: Rs. {GrandTotal:N2}", "#F59E0B")));
                ActionCompleted?.Invoke(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}