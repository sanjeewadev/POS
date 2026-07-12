using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using POS.Cashier.UI.Messages;
using POS.Cashier.UI.Services;
using POS.Core.Configuration;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;

namespace POS.Cashier.UI.ViewModels
{
    public partial class FloatCashViewModel : ObservableObject
    {
        private readonly TillRepository _tillRepository;
        private readonly CashDrawerAuditService _drawerAuditService;
        private int _shiftId;
        private string _authorizedBy = string.Empty;

        [ObservableProperty] private int? _qty5000;
        [ObservableProperty] private int? _qty1000;
        [ObservableProperty] private int? _qty500;
        [ObservableProperty] private int? _qty100;
        [ObservableProperty] private int? _qty50;
        [ObservableProperty] private int? _qty20;
        [ObservableProperty] private int? _qty10;
        [ObservableProperty] private int? _qty5;

        [ObservableProperty] private decimal _currentSystemFloat;
        [ObservableProperty] private string _referenceNote = string.Empty;

        public event Action<bool>? ActionCompleted;

        public FloatCashViewModel(
            TillRepository tillRepository,
            CashDrawerAuditService drawerAuditService)
        {
            _tillRepository = tillRepository;
            _drawerAuditService = drawerAuditService;
        }

        public void Initialize(int shiftId, string authorizedBy)
        {
            _shiftId = shiftId;
            _authorizedBy = (authorizedBy ?? string.Empty).Trim();
            _ = LoadCurrentFloatAsync();
        }

        private async Task LoadCurrentFloatAsync()
        {
            if (_shiftId > 0)
                CurrentSystemFloat = await _tillRepository.GetCurrentFloatBalanceAsync(_shiftId);
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
        private Task FloatInAsync() => RegisterFloatAsync(true);

        [RelayCommand]
        private Task FloatOutAsync() => RegisterFloatAsync(false);

        private async Task RegisterFloatAsync(bool isFloatIn)
        {
            if (GrandTotal <= 0m)
            {
                MessageBox.Show("Please count the cash before submitting.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_shiftId <= 0)
                return;
            if (string.IsNullOrWhiteSpace(_authorizedBy))
            {
                MessageBox.Show("Manager authorization is required.", "Authorization", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ShiftCashSummaryDto summary = await _tillRepository.GetShiftCashSummaryAsync(_shiftId, false)
                    ?? throw new InvalidOperationException("The active shift was not found.");

                CashMovement movement = await _tillRepository.RegisterCashMovementDetailedAsync(
                    new CashMovementRegistrationRequest
                    {
                        ShiftSessionId = _shiftId,
                        MovementType = isFloatIn ? CashMovementTypeCodes.PaidIn : CashMovementTypeCodes.PaidOut,
                        Amount = GrandTotal,
                        ReasonCategory = isFloatIn ? CashMovementReasonCodes.FloatIn : CashMovementReasonCodes.FloatOut,
                        Remarks = (ReferenceNote ?? string.Empty).Trim(),
                        CashierName = summary.CashierName,
                        AuthorizedBy = _authorizedBy
                    });

                try
                {
                    await _drawerAuditService.OpenAsync(
                        _shiftId,
                        summary.TerminalNo,
                        summary.CashierName,
                        isFloatIn ? CashDrawerEventTypeCodes.FloatIn : CashDrawerEventTypeCodes.FloatOut,
                        movement.ReasonCategory,
                        movement.Remarks,
                        _authorizedBy,
                        cashMovementId: movement.Id);
                }
                catch (Exception drawerEx)
                {
                    MessageBox.Show(
                        $"The cash movement was saved, but the drawer did not open: {drawerEx.Message}",
                        "Drawer Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                WeakReferenceMessenger.Default.Send(new TopBarNotificationMessage((
                    isFloatIn
                        ? $"Float Added: Rs. {GrandTotal:N2}"
                        : $"Safe Drop: Rs. {GrandTotal:N2}",
                    isFloatIn ? "#10B981" : "#F59E0B")));
                ActionCompleted?.Invoke(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Float Cash Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
