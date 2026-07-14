using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using POS.Cashier.UI.Messages;
using POS.Cashier.UI.Services;
using POS.Core.Configuration;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace POS.Cashier.UI.ViewModels
{
    public partial class CashMovementViewModel : ObservableObject
    {
        private readonly TillRepository _tillRepository;
        private readonly CashDrawerAuditService _drawerAuditService;
        private int _shiftId;
        private string _cashierName = string.Empty;

        [ObservableProperty] private string _movementType = string.Empty;
        [ObservableProperty] private decimal _amount;
        [ObservableProperty] private string _headerTitle = string.Empty;
        [ObservableProperty] private string _themeColorHex = "#003366";
        [ObservableProperty] private string _buttonText = string.Empty;
        [ObservableProperty] private string _selectedReason = string.Empty;
        [ObservableProperty] private string _remarks = string.Empty;

        public ObservableCollection<string> ReasonCategories { get; } = new();
        public event Action<bool>? ActionCompleted;

        public CashMovementViewModel(
            TillRepository tillRepository,
            CashDrawerAuditService drawerAuditService)
        {
            _tillRepository = tillRepository;
            _drawerAuditService = drawerAuditService;
        }

        public void Initialize(string movementType, decimal amount, int shiftId, string cashierName)
        {
            _shiftId = shiftId;
            _cashierName = (cashierName ?? string.Empty).Trim();
            MovementType = movementType;
            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
            ReasonCategories.Clear();

            if (string.Equals(movementType, CashMovementTypeCodes.PaidIn, StringComparison.OrdinalIgnoreCase))
            {
                HeaderTitle = "PAID IN — RECEIVE CASH";
                ThemeColorHex = "#10B981";
                ButtonText = "CONFIRM PAID IN";
                ReasonCategories.Add(CashMovementReasonCodes.ChangeFundAdjustment);
                ReasonCategories.Add(CashMovementReasonCodes.Other);
            }
            else
            {
                HeaderTitle = "PAID OUT — REMOVE CASH";
                ThemeColorHex = "#EF4444";
                ButtonText = "CONFIRM PAID OUT";
                ReasonCategories.Add(CashMovementReasonCodes.StoreExpense);
                ReasonCategories.Add(CashMovementReasonCodes.FloatOut);
                ReasonCategories.Add(CashMovementReasonCodes.Other);
            }

            SelectedReason = ReasonCategories.FirstOrDefault() ?? string.Empty;
        }

        [RelayCommand]
        private async Task ConfirmAsync()
        {
            if (Amount <= 0m)
            {
                MessageBox.Show("Amount must be greater than zero.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(SelectedReason))
            {
                MessageBox.Show("Please select a reason for this cash movement.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (SelectedReason.Equals(CashMovementReasonCodes.Other, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(Remarks))
            {
                MessageBox.Show("Remarks are required when the reason is Other.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool isPaidOut = string.Equals(MovementType, CashMovementTypeCodes.PaidOut, StringComparison.OrdinalIgnoreCase);
            string authorizedBy = _cashierName;

            try
            {
                ShiftCashSummaryDto summary = await _tillRepository.GetShiftCashSummaryAsync(_shiftId, false)
                    ?? throw new InvalidOperationException("The active shift was not found.");

                CashMovement movement = await _tillRepository.RegisterCashMovementDetailedAsync(
                    new CashMovementRegistrationRequest
                    {
                        ShiftSessionId = _shiftId,
                        MovementType = MovementType,
                        Amount = Amount,
                        ReasonCategory = SelectedReason,
                        Remarks = Remarks,
                        CashierName = _cashierName,
                        AuthorizedBy = authorizedBy
                    });

                try
                {
                    await _drawerAuditService.OpenAsync(
                        _shiftId,
                        summary.TerminalNo,
                        summary.CashierName,
                        isPaidOut ? CashDrawerEventTypeCodes.PaidOut : CashDrawerEventTypeCodes.PaidIn,
                        movement.ReasonCategory,
                        movement.Remarks,
                        authorizedBy,
                        cashMovementId: movement.Id);
                }
                catch (Exception drawerEx)
                {
                    MessageBox.Show(
                        $"The movement was saved, but the drawer did not open: {drawerEx.Message}",
                        "Drawer Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                WeakReferenceMessenger.Default.Send(new TopBarNotificationMessage(($"{MovementType}: Rs. {Amount:N2}", ThemeColorHex)));
                ActionCompleted?.Invoke(true);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Cash Movement Blocked", MessageBoxButton.OK, MessageBoxImage.Stop);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"System Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
