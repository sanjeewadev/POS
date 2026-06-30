using System;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace POS.Cashier.UI.ViewModels
{
    public partial class CardTenderDialogViewModel : ObservableObject
    {
        private const string AmountTarget = "Amount";
        private const string LastSixTarget = "LastSix";
        private const string ReferenceTarget = "Reference";

        private bool _isNormalizingText;

        [ObservableProperty]
        private decimal _balanceDue = 0m;

        [ObservableProperty]
        private string _cardType = "VISA";

        [ObservableProperty]
        private string _cardAmountText = string.Empty;

        [ObservableProperty]
        private decimal _cardAmount = 0m;

        [ObservableProperty]
        private string _lastSixDigits = string.Empty;

        [ObservableProperty]
        private string _referenceNo = string.Empty;

        [ObservableProperty]
        private bool _requireLastSixDigits = true;

        [ObservableProperty]
        private string _activeInputTarget = AmountTarget;

        [ObservableProperty]
        private string _statusText = "Enter card amount.";

        [ObservableProperty]
        private string _statusColorHex = "#1A1F71";

        public event Action<bool>? ActionCompleted;

        public string CardTitle =>
            string.IsNullOrWhiteSpace(CardType)
                ? "CARD PAYMENT"
                : $"{CardType} PAYMENT";

        public string ActiveInputText
        {
            get
            {
                return ActiveInputTarget switch
                {
                    LastSixTarget => LastSixDigits,
                    ReferenceTarget => ReferenceNo,
                    _ => CardAmountText
                };
            }
            set
            {
                switch (ActiveInputTarget)
                {
                    case LastSixTarget:
                        LastSixDigits = NormalizeDigits(value, 6);
                        break;

                    case ReferenceTarget:
                        ReferenceNo = NormalizeReference(value);
                        break;

                    default:
                        CardAmountText = value ?? string.Empty;
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
                    LastSixTarget => "C A R D   D I G I T S",
                    ReferenceTarget => "R E F E R E N C E",
                    _ => "C A R D   A M O U N T"
                };
            }
        }

        public string ActiveTargetLabel
        {
            get
            {
                return ActiveInputTarget switch
                {
                    LastSixTarget => "LAST 6 DIGITS",
                    ReferenceTarget => "REFERENCE",
                    _ => "CARD AMOUNT"
                };
            }
        }

        public string ActiveModeText
        {
            get
            {
                return ActiveInputTarget switch
                {
                    LastSixTarget => "CARD",
                    ReferenceTarget => "REF",
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
                    LastSixTarget => 6,
                    ReferenceTarget => 30,
                    _ => 12
                };
            }
        }

        public bool CanConfirm
        {
            get
            {
                if (CardAmount <= 0m)
                    return false;

                if (CardAmount > BalanceDue)
                    return false;

                if (RequireLastSixDigits && LastSixDigits.Length != 6)
                    return false;

                return true;
            }
        }

        public void Initialize(string cardType, decimal balanceDue, decimal initialCardAmount = 0m)
        {
            CardType = string.IsNullOrWhiteSpace(cardType)
                ? "Card"
                : cardType.Trim();

            BalanceDue = Math.Round(balanceDue, 2);

            decimal amountToUse = initialCardAmount > 0m
                ? initialCardAmount
                : BalanceDue;

            CardAmountText = amountToUse.ToString("0.##", CultureInfo.InvariantCulture);

            LastSixDigits = string.Empty;
            ReferenceNo = string.Empty;

            SetActiveInputTarget(AmountTarget);

            Recalculate();
        }

        public void SetAmountInputActive()
        {
            SetActiveInputTarget(AmountTarget);
        }

        public void SetLastSixInputActive()
        {
            SetActiveInputTarget(LastSixTarget);
        }

        public void SetReferenceInputActive()
        {
            SetActiveInputTarget(ReferenceTarget);
        }

        public bool ValidateAmountBeforeMovingNext()
        {
            Recalculate();

            if (CardAmount <= 0m)
            {
                StatusText = "Enter a valid card amount.";
                StatusColorHex = "#EF4444";
                return false;
            }

            if (CardAmount > BalanceDue)
            {
                StatusText = "Card amount cannot be greater than balance due.";
                StatusColorHex = "#EF4444";
                return false;
            }

            return true;
        }

        partial void OnBalanceDueChanged(decimal value)
        {
            Recalculate();
        }

        partial void OnCardTypeChanged(string value)
        {
            OnPropertyChanged(nameof(CardTitle));
        }

        partial void OnCardAmountTextChanged(string value)
        {
            if (_isNormalizingText)
                return;

            Recalculate();

            if (ActiveInputTarget == AmountTarget)
                OnPropertyChanged(nameof(ActiveInputText));
        }

        partial void OnLastSixDigitsChanged(string value)
        {
            if (_isNormalizingText)
                return;

            string normalized = NormalizeDigits(value, 6);

            if (normalized != value)
            {
                _isNormalizingText = true;
                LastSixDigits = normalized;
                _isNormalizingText = false;
            }

            Recalculate();

            if (ActiveInputTarget == LastSixTarget)
                OnPropertyChanged(nameof(ActiveInputText));
        }

        partial void OnReferenceNoChanged(string value)
        {
            if (_isNormalizingText)
                return;

            string normalized = NormalizeReference(value);

            if (normalized != value)
            {
                _isNormalizingText = true;
                ReferenceNo = normalized;
                _isNormalizingText = false;
            }

            if (ActiveInputTarget == ReferenceTarget)
                OnPropertyChanged(nameof(ActiveInputText));
        }

        partial void OnRequireLastSixDigitsChanged(bool value)
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
            CardAmount = Math.Round(ParseMoney(CardAmountText), 2);

            if (BalanceDue <= 0m)
            {
                StatusText = "No balance due.";
                StatusColorHex = "#10B981";
                OnPropertyChanged(nameof(CanConfirm));
                return;
            }

            if (CardAmount <= 0m)
            {
                StatusText = "Enter a valid card amount.";
                StatusColorHex = "#F59E0B";
                OnPropertyChanged(nameof(CanConfirm));
                return;
            }

            if (CardAmount > BalanceDue)
            {
                StatusText = "Card amount cannot be greater than balance due.";
                StatusColorHex = "#EF4444";
                OnPropertyChanged(nameof(CanConfirm));
                return;
            }

            if (RequireLastSixDigits && LastSixDigits.Length == 0)
            {
                StatusText = "Press Enter, then type last 6 card digits.";
                StatusColorHex = "#1A1F71";
                OnPropertyChanged(nameof(CanConfirm));
                return;
            }

            if (RequireLastSixDigits && LastSixDigits.Length < 6)
            {
                StatusText = "Last 6 card digits must contain 6 numbers.";
                StatusColorHex = "#F59E0B";
                OnPropertyChanged(nameof(CanConfirm));
                return;
            }

            if (CardAmount < BalanceDue)
            {
                decimal remaining = Math.Round(BalanceDue - CardAmount, 2);
                StatusText = $"Partial {CardType} payment. Remaining Rs. {remaining:N2}.";
                StatusColorHex = "#D97706";
            }
            else
            {
                StatusText = $"Full {CardType} payment.";
                StatusColorHex = "#10B981";
            }

            OnPropertyChanged(nameof(CanConfirm));
        }

        [RelayCommand]
        public void Confirm()
        {
            Recalculate();

            if (CardAmount <= 0m)
            {
                StatusText = "Enter a valid card amount before confirming.";
                StatusColorHex = "#EF4444";
                return;
            }

            if (CardAmount > BalanceDue)
            {
                StatusText = "Card amount cannot be greater than balance due.";
                StatusColorHex = "#EF4444";
                return;
            }

            if (RequireLastSixDigits && LastSixDigits.Length != 6)
            {
                StatusText = "Enter last 6 card digits before confirming.";
                StatusColorHex = "#EF4444";
                SetLastSixInputActive();
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

        private static string NormalizeDigits(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string digits = new string(value.Where(char.IsDigit).ToArray());

            if (digits.Length > maxLength)
                digits = digits[..maxLength];

            return digits;
        }

        private static string NormalizeReference(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string trimmed = value.Trim();

            return trimmed.Length > 30
                ? trimmed[..30]
                : trimmed;
        }
    }
}