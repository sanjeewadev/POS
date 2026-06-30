using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace POS.Cashier.UI.ViewModels
{
    public partial class CashTenderDialogViewModel : ObservableObject
    {
        [ObservableProperty]
        private decimal _balanceDue = 0m;

        [ObservableProperty]
        private string _tenderedAmountText = string.Empty;

        [ObservableProperty]
        private decimal _appliedAmount = 0m;

        [ObservableProperty]
        private decimal _changeAmount = 0m;

        [ObservableProperty]
        private decimal _remainingAfterCash = 0m;

        [ObservableProperty]
        private string _changeOrRemainingText = "0.00";

        [ObservableProperty]
        private string _statusText = "Enter cash amount.";

        [ObservableProperty]
        private string _statusColorHex = "#003366";

        public event Action<bool>? ActionCompleted;

        public decimal TenderedAmount => ParseMoney(TenderedAmountText);

        public bool CanConfirm => TenderedAmount > 0m && AppliedAmount > 0m;

        public void Initialize(decimal balanceDue, decimal initialTenderedAmount = 0m)
        {
            BalanceDue = Math.Round(balanceDue, 2);

            decimal amountToUse = initialTenderedAmount > 0m
                ? initialTenderedAmount
                : BalanceDue;

            TenderedAmountText = amountToUse.ToString("0.##", CultureInfo.InvariantCulture);

            Recalculate();
        }

        partial void OnBalanceDueChanged(decimal value)
        {
            Recalculate();
        }

        partial void OnTenderedAmountTextChanged(string value)
        {
            Recalculate();
        }

        private void Recalculate()
        {
            decimal tendered = Math.Round(ParseMoney(TenderedAmountText), 2);

            if (BalanceDue <= 0m)
            {
                AppliedAmount = 0m;
                ChangeAmount = 0m;
                RemainingAfterCash = 0m;
                ChangeOrRemainingText = "0.00";
                StatusText = "No balance due.";
                StatusColorHex = "#10B981";
                OnPropertyChanged(nameof(CanConfirm));
                return;
            }

            if (tendered <= 0m)
            {
                AppliedAmount = 0m;
                ChangeAmount = 0m;
                RemainingAfterCash = BalanceDue;
                ChangeOrRemainingText = $"REMAINING {RemainingAfterCash:N2}";
                StatusText = "Enter cash amount.";
                StatusColorHex = "#F59E0B";
                OnPropertyChanged(nameof(CanConfirm));
                return;
            }

            if (tendered >= BalanceDue)
            {
                AppliedAmount = BalanceDue;
                ChangeAmount = Math.Round(tendered - BalanceDue, 2);
                RemainingAfterCash = 0m;
                ChangeOrRemainingText = ChangeAmount > 0m
                    ? $"CHANGE {ChangeAmount:N2}"
                    : "0.00";

                StatusText = ChangeAmount > 0m
                    ? $"Full cash payment. Give change Rs. {ChangeAmount:N2}."
                    : "Exact cash payment.";

                StatusColorHex = "#10B981";
            }
            else
            {
                AppliedAmount = tendered;
                ChangeAmount = 0m;
                RemainingAfterCash = Math.Round(BalanceDue - tendered, 2);
                ChangeOrRemainingText = $"REMAINING {RemainingAfterCash:N2}";
                StatusText = $"Partial cash payment. Remaining Rs. {RemainingAfterCash:N2}.";
                StatusColorHex = "#D97706";
            }

            OnPropertyChanged(nameof(CanConfirm));
        }

        [RelayCommand]
        public void Confirm()
        {
            Recalculate();

            if (!CanConfirm)
            {
                StatusText = "Enter a valid cash amount before confirming.";
                StatusColorHex = "#EF4444";
                return;
            }

            ActionCompleted?.Invoke(true);
        }

        [RelayCommand]
        public void Cancel()
        {
            ActionCompleted?.Invoke(false);
        }

        private static decimal ParseMoney(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0m;

            string cleaned = value.Trim().Replace(",", string.Empty);

            if (decimal.TryParse(
                    cleaned,
                    NumberStyles.Number,
                    CultureInfo.CurrentCulture,
                    out decimal currentCultureValue))
            {
                return currentCultureValue < 0m ? 0m : currentCultureValue;
            }

            if (decimal.TryParse(
                    cleaned,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out decimal invariantValue))
            {
                return invariantValue < 0m ? 0m : invariantValue;
            }

            return 0m;
        }
    }
}