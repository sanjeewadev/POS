using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace POS.Cashier.UI.ViewModels
{
    public partial class PaymentDialogViewModel : ObservableObject
    {
        public ObservableCollection<string> PaymentTypes { get; } = new()
        {
            "Cash",
            "Card"
        };

        [ObservableProperty]
        private string _selectedPaymentType = "Cash";

        [ObservableProperty]
        private decimal _netTotal = 0m;

        [ObservableProperty]
        private decimal _amountTendered = 0m;

        [ObservableProperty]
        private decimal _balanceReturned = 0m;

        [ObservableProperty]
        private string _statusText = "Enter payment amount.";

        [ObservableProperty]
        private string _statusColorHex = "#003366";

        public event Action<bool>? ActionCompleted;

        public void Initialize(decimal netTotal, decimal initialTendered)
        {
            NetTotal = Math.Round(netTotal, 2);

            if (initialTendered <= 0)
                initialTendered = NetTotal;

            AmountTendered = Math.Round(initialTendered, 2);

            RecalculateBalance();
            UpdateStatus();
        }

        partial void OnSelectedPaymentTypeChanged(string value)
        {
            if (!string.Equals(value, "Cash", StringComparison.OrdinalIgnoreCase))
            {
                AmountTendered = NetTotal;
            }

            RecalculateBalance();
            UpdateStatus();
            ConfirmCommand.NotifyCanExecuteChanged();
        }

        partial void OnAmountTenderedChanged(decimal value)
        {
            RecalculateBalance();
            UpdateStatus();
            ConfirmCommand.NotifyCanExecuteChanged();
        }

        partial void OnNetTotalChanged(decimal value)
        {
            RecalculateBalance();
            UpdateStatus();
            ConfirmCommand.NotifyCanExecuteChanged();
        }

        private void RecalculateBalance()
        {
            if (string.Equals(SelectedPaymentType, "Cash", StringComparison.OrdinalIgnoreCase))
            {
                BalanceReturned = Math.Round(AmountTendered - NetTotal, 2);
            }
            else
            {
                BalanceReturned = 0m;
            }
        }

        private void UpdateStatus()
        {
            if (NetTotal <= 0)
            {
                StatusText = "Invalid invoice total.";
                StatusColorHex = "#DC3545";
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedPaymentType))
            {
                StatusText = "Select payment type.";
                StatusColorHex = "#D97706";
                return;
            }

            if (string.Equals(SelectedPaymentType, "Cash", StringComparison.OrdinalIgnoreCase))
            {
                if (AmountTendered < NetTotal)
                {
                    StatusText = "Cash tendered is lower than net total.";
                    StatusColorHex = "#DC3545";
                    return;
                }

                StatusText = $"Cash payment ready. Balance: Rs. {BalanceReturned:N2}";
                StatusColorHex = "#10B981";
                return;
            }

            StatusText = $"{SelectedPaymentType} payment ready.";
            StatusColorHex = "#10B981";
        }

        [RelayCommand]
        private void ExactAmount()
        {
            AmountTendered = NetTotal;
        }

        [RelayCommand(CanExecute = nameof(CanConfirm))]
        private void Confirm()
        {
            if (!CanConfirm())
                return;

            ActionCompleted?.Invoke(true);
        }

        private bool CanConfirm()
        {
            if (NetTotal <= 0)
                return false;

            if (string.IsNullOrWhiteSpace(SelectedPaymentType))
                return false;

            if (string.Equals(SelectedPaymentType, "Cash", StringComparison.OrdinalIgnoreCase))
                return AmountTendered >= NetTotal;

            return true;
        }

        [RelayCommand]
        private void Cancel()
        {
            ActionCompleted?.Invoke(false);
        }
    }
}