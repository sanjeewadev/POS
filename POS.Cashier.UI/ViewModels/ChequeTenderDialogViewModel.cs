using System;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace POS.Cashier.UI.ViewModels
{
    public partial class ChequeTenderDialogViewModel : ObservableObject
    {
        private const string AmountTarget = "Amount";
        private const string ChequeNoTarget = "ChequeNo";

        private bool _isNormalizingText;

        [ObservableProperty]
        private decimal _balanceDue = 0m;

        [ObservableProperty]
        private string _chequeAmountText = string.Empty;

        [ObservableProperty]
        private decimal _chequeAmount = 0m;

        [ObservableProperty]
        private string _chequeNo = string.Empty;

        [ObservableProperty]
        private string _bankName = string.Empty;

        [ObservableProperty]
        private string _branchName = string.Empty;

        [ObservableProperty]
        private DateTime _chequeDate = DateTime.Today;

        [ObservableProperty]
        private bool _allowPostDatedCheque = true;

        [ObservableProperty]
        private string _activeInputTarget = AmountTarget;

        [ObservableProperty]
        private string _statusText = "Enter cheque amount.";

        [ObservableProperty]
        private string _statusColorHex = "#92400E";

        public event Action<bool>? ActionCompleted;

        public decimal RemainingAfterCheque =>
            Math.Max(0m, Math.Round(BalanceDue - ChequeAmount, 2));

        public string BankOrBranchText
        {
            get
            {
                string bank = NormalizeFreeText(BankName, 60);
                string branch = NormalizeFreeText(BranchName, 40);

                if (!string.IsNullOrWhiteSpace(bank) && !string.IsNullOrWhiteSpace(branch))
                    return $"{bank} / {branch}";

                if (!string.IsNullOrWhiteSpace(bank))
                    return bank;

                if (!string.IsNullOrWhiteSpace(branch))
                    return branch;

                return string.Empty;
            }
        }

        public string ActiveInputText
        {
            get
            {
                return ActiveInputTarget switch
                {
                    ChequeNoTarget => ChequeNo,
                    _ => ChequeAmountText
                };
            }
            set
            {
                switch (ActiveInputTarget)
                {
                    case ChequeNoTarget:
                        ChequeNo = NormalizeChequeNo(value);
                        break;

                    default:
                        ChequeAmountText = value ?? string.Empty;
                        break;
                }

                OnPropertyChanged(nameof(ActiveInputText));
            }
        }

        public string ActiveNumpadHeader
        {
            get
            {
                return ActiveInputTarget switch
                {
                    ChequeNoTarget => "C H E Q U E   N O",
                    _ => "C H E Q U E   A M O U N T"
                };
            }
        }

        public string ActiveTargetLabel
        {
            get
            {
                return ActiveInputTarget switch
                {
                    ChequeNoTarget => "CHEQUE NUMBER",
                    _ => "CHEQUE AMOUNT"
                };
            }
        }

        public string ActiveModeText
        {
            get
            {
                return ActiveInputTarget switch
                {
                    ChequeNoTarget => "CHQ NO",
                    _ => "AMOUNT"
                };
            }
        }

        public bool ActiveShowDecimal => ActiveInputTarget == AmountTarget;

        public bool ActiveShowDoubleZero => ActiveInputTarget == AmountTarget;

        public int ActiveMaxLength
        {
            get
            {
                return ActiveInputTarget switch
                {
                    ChequeNoTarget => 20,
                    _ => 12
                };
            }
        }

        public bool CanConfirm
        {
            get
            {
                if (ChequeAmount <= 0m)
                    return false;

                if (ChequeAmount > BalanceDue)
                    return false;

                if (string.IsNullOrWhiteSpace(ChequeNo))
                    return false;

                if (string.IsNullOrWhiteSpace(BankName))
                    return false;

                if (!AllowPostDatedCheque && ChequeDate.Date > DateTime.Today)
                    return false;

                return true;
            }
        }

        public void Initialize(decimal balanceDue, decimal initialChequeAmount = 0m)
        {
            BalanceDue = Math.Round(balanceDue, 2);

            decimal amountToUse = initialChequeAmount > 0m
                ? initialChequeAmount
                : BalanceDue;

            ChequeAmountText = amountToUse.ToString("0.##", CultureInfo.InvariantCulture);

            ChequeNo = string.Empty;
            BankName = string.Empty;
            BranchName = string.Empty;
            ChequeDate = DateTime.Today;

            SetAmountInputActive();

            Recalculate();
        }

        public void SetAmountInputActive()
        {
            SetActiveInputTarget(AmountTarget);
        }

        public void SetChequeNoInputActive()
        {
            SetActiveInputTarget(ChequeNoTarget);
        }

        public bool ValidateAmountBeforeMovingNext()
        {
            Recalculate();

            if (ChequeAmount <= 0m)
            {
                StatusText = "Enter a valid cheque amount.";
                StatusColorHex = "#EF4444";
                return false;
            }

            if (ChequeAmount > BalanceDue)
            {
                StatusText = "Cheque amount cannot be greater than balance due.";
                StatusColorHex = "#EF4444";
                return false;
            }

            return true;
        }

        partial void OnBalanceDueChanged(decimal value)
        {
            Recalculate();
        }

        partial void OnChequeAmountTextChanged(string value)
        {
            if (_isNormalizingText)
                return;

            Recalculate();

            if (ActiveInputTarget == AmountTarget)
                OnPropertyChanged(nameof(ActiveInputText));
        }

        partial void OnChequeNoChanged(string value)
        {
            if (_isNormalizingText)
                return;

            string normalized = NormalizeChequeNo(value);

            if (normalized != value)
            {
                _isNormalizingText = true;
                ChequeNo = normalized;
                _isNormalizingText = false;
            }

            Recalculate();

            if (ActiveInputTarget == ChequeNoTarget)
                OnPropertyChanged(nameof(ActiveInputText));
        }

        partial void OnBankNameChanged(string value)
        {
            if (_isNormalizingText)
                return;

            string normalized = NormalizeFreeText(value, 60);

            if (normalized != value)
            {
                _isNormalizingText = true;
                BankName = normalized;
                _isNormalizingText = false;
            }

            Recalculate();
            OnPropertyChanged(nameof(BankOrBranchText));
        }

        partial void OnBranchNameChanged(string value)
        {
            if (_isNormalizingText)
                return;

            string normalized = NormalizeFreeText(value, 40);

            if (normalized != value)
            {
                _isNormalizingText = true;
                BranchName = normalized;
                _isNormalizingText = false;
            }

            OnPropertyChanged(nameof(BankOrBranchText));
        }

        partial void OnChequeDateChanged(DateTime value)
        {
            Recalculate();
        }

        partial void OnAllowPostDatedChequeChanged(bool value)
        {
            Recalculate();
        }

        partial void OnActiveInputTargetChanged(string value)
        {
            NotifyActiveInputProperties();
        }

        private void SetActiveInputTarget(string target)
        {
            ActiveInputTarget = target;
            NotifyActiveInputProperties();
        }

        private void NotifyActiveInputProperties()
        {
            OnPropertyChanged(nameof(ActiveInputText));
            OnPropertyChanged(nameof(ActiveNumpadHeader));
            OnPropertyChanged(nameof(ActiveTargetLabel));
            OnPropertyChanged(nameof(ActiveModeText));
            OnPropertyChanged(nameof(ActiveShowDecimal));
            OnPropertyChanged(nameof(ActiveShowDoubleZero));
            OnPropertyChanged(nameof(ActiveMaxLength));
        }

        private void Recalculate()
        {
            ChequeAmount = Math.Round(ParseMoney(ChequeAmountText), 2);

            if (BalanceDue <= 0m)
            {
                StatusText = "No balance due.";
                StatusColorHex = "#10B981";
                NotifyCalculatedProperties();
                return;
            }

            if (ChequeAmount <= 0m)
            {
                StatusText = "Enter a valid cheque amount.";
                StatusColorHex = "#F59E0B";
                NotifyCalculatedProperties();
                return;
            }

            if (ChequeAmount > BalanceDue)
            {
                StatusText = "Cheque amount cannot be greater than balance due.";
                StatusColorHex = "#EF4444";
                NotifyCalculatedProperties();
                return;
            }

            if (string.IsNullOrWhiteSpace(ChequeNo))
            {
                StatusText = "Enter cheque number.";
                StatusColorHex = "#92400E";
                NotifyCalculatedProperties();
                return;
            }

            if (string.IsNullOrWhiteSpace(BankName))
            {
                StatusText = "Enter bank name.";
                StatusColorHex = "#92400E";
                NotifyCalculatedProperties();
                return;
            }

            if (!AllowPostDatedCheque && ChequeDate.Date > DateTime.Today)
            {
                StatusText = "Post-dated cheque is not allowed.";
                StatusColorHex = "#EF4444";
                NotifyCalculatedProperties();
                return;
            }

            if (ChequeDate.Date > DateTime.Today)
            {
                StatusText = $"Post-dated cheque: {ChequeDate:yyyy-MM-dd}.";
                StatusColorHex = "#D97706";
                NotifyCalculatedProperties();
                return;
            }

            if (ChequeAmount < BalanceDue)
            {
                StatusText = $"Partial cheque payment. Remaining Rs. {RemainingAfterCheque:N2}.";
                StatusColorHex = "#D97706";
            }
            else
            {
                StatusText = "Full cheque payment.";
                StatusColorHex = "#10B981";
            }

            NotifyCalculatedProperties();
        }

        private void NotifyCalculatedProperties()
        {
            OnPropertyChanged(nameof(ChequeAmount));
            OnPropertyChanged(nameof(RemainingAfterCheque));
            OnPropertyChanged(nameof(CanConfirm));
            OnPropertyChanged(nameof(BankOrBranchText));
        }

        [RelayCommand]
        public void Confirm()
        {
            Recalculate();

            if (ChequeAmount <= 0m)
            {
                StatusText = "Enter a valid cheque amount before confirming.";
                StatusColorHex = "#EF4444";
                return;
            }

            if (ChequeAmount > BalanceDue)
            {
                StatusText = "Cheque amount cannot be greater than balance due.";
                StatusColorHex = "#EF4444";
                return;
            }

            if (string.IsNullOrWhiteSpace(ChequeNo))
            {
                StatusText = "Cheque number is required.";
                StatusColorHex = "#EF4444";
                SetChequeNoInputActive();
                return;
            }

            if (string.IsNullOrWhiteSpace(BankName))
            {
                StatusText = "Bank name is required.";
                StatusColorHex = "#EF4444";
                return;
            }

            if (!AllowPostDatedCheque && ChequeDate.Date > DateTime.Today)
            {
                StatusText = "Post-dated cheque is not allowed.";
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

        private static string NormalizeChequeNo(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            // Most cheque numbers are numeric.
            // Keep digits only for clean cashier entry.
            string digits = new string(value.Where(char.IsDigit).ToArray());

            return digits.Length > 20
                ? digits[..20]
                : digits;
        }

        private static string NormalizeFreeText(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string trimmed = value.Trim();

            return trimmed.Length > maxLength
                ? trimmed[..maxLength]
                : trimmed;
        }
    }
}