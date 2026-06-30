using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace POS.Cashier.UI.Models
{
    public partial class PaymentLine : ObservableObject
    {
        [ObservableProperty]
        private int _lineNo;

        // Cash, Card, Cheque
        // Later: Customer Credit, Gift Voucher, Credit Note
        [ObservableProperty]
        private string _paymentType = "Cash";

        // Backward compatibility for current Card code.
        // For Card: VISA / MasterCard / AMEX
        // For Cheque: keep empty and use BankOrCardType.
        [ObservableProperty]
        private string _cardType = string.Empty;

        // Generic field used for:
        // Card: VISA / MasterCard / AMEX
        // Cheque: Bank name / Bank + Branch
        [ObservableProperty]
        private string _bankOrCardType = string.Empty;

        // Card: last 6 digits or approval/reference
        // Cheque: cheque number
        [ObservableProperty]
        private string _referenceNo = string.Empty;

        // Amount applied to invoice.
        [ObservableProperty]
        private decimal _amount = 0m;

        // Actual cash received from customer.
        // For Card and Cheque, normally same as Amount.
        [ObservableProperty]
        private decimal _tenderedAmount = 0m;

        // Only useful for cash over-payment.
        [ObservableProperty]
        private decimal _changeAmount = 0m;

        // For Card: payment date/current date.
        // For Cheque: cheque date, including post-dated cheque.
        [ObservableProperty]
        private DateTime? _paymentDate = DateTime.Now;

        [ObservableProperty]
        private DateTime _createdAt = DateTime.Now;

        public string DisplayPaymentType
        {
            get
            {
                if (PaymentType.Equals("Card", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(CardType))
                        return CardType;

                    if (!string.IsNullOrWhiteSpace(BankOrCardType))
                        return BankOrCardType;

                    return "Card";
                }

                if (PaymentType.Equals("Cheque", StringComparison.OrdinalIgnoreCase))
                    return "Cheque";

                if (PaymentType.Equals("GiftVoucher", StringComparison.OrdinalIgnoreCase) ||
    PaymentType.Equals("Gift Voucher", StringComparison.OrdinalIgnoreCase))
                {
                    return "Gift Voucher";
                }

                return PaymentType;
            }
        }

        public string DisplayReference
        {
            get
            {
                if (PaymentType.Equals("Cheque", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(ReferenceNo) &&
                        !string.IsNullOrWhiteSpace(BankOrCardType))
                    {
                        return $"{ReferenceNo} / {BankOrCardType}";
                    }

                    if (!string.IsNullOrWhiteSpace(ReferenceNo))
                        return ReferenceNo;

                    if (!string.IsNullOrWhiteSpace(BankOrCardType))
                        return BankOrCardType;

                    return string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(ReferenceNo))
                    return ReferenceNo;

                if (PaymentType.Equals("Card", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(CardType))
                        return CardType;

                    if (!string.IsNullOrWhiteSpace(BankOrCardType))
                        return BankOrCardType;
                }

                return string.Empty;
            }
        }

        public string PaymentDateDisplay
        {
            get
            {
                if (!PaymentDate.HasValue)
                    return string.Empty;

                return PaymentDate.Value.ToString("yyyy-MM-dd");
            }
        }

        public bool IsCash =>
            PaymentType.Equals("Cash", StringComparison.OrdinalIgnoreCase);

        public bool IsCard =>
            PaymentType.Equals("Card", StringComparison.OrdinalIgnoreCase);

        public bool IsCheque =>
            PaymentType.Equals("Cheque", StringComparison.OrdinalIgnoreCase);

        partial void OnPaymentTypeChanged(string value)
        {
            NotifyDisplayProperties();
            OnPropertyChanged(nameof(IsCash));
            OnPropertyChanged(nameof(IsCard));
            OnPropertyChanged(nameof(IsCheque));
            OnPropertyChanged(nameof(IsGiftVoucher));
        }

        partial void OnCardTypeChanged(string value)
        {
            // Keep old CardType and new BankOrCardType aligned for card payments.
            if (PaymentType.Equals("Card", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(BankOrCardType, value, StringComparison.Ordinal))
            {
                BankOrCardType = value ?? string.Empty;
            }

            NotifyDisplayProperties();
        }

        partial void OnBankOrCardTypeChanged(string value)
        {
            NotifyDisplayProperties();
        }

        partial void OnReferenceNoChanged(string value)
        {
            NotifyDisplayProperties();
        }

        partial void OnPaymentDateChanged(DateTime? value)
        {
            OnPropertyChanged(nameof(PaymentDateDisplay));
        }

        private void NotifyDisplayProperties()
        {
            OnPropertyChanged(nameof(DisplayPaymentType));
            OnPropertyChanged(nameof(DisplayReference));
            OnPropertyChanged(nameof(PaymentDateDisplay));
        }

        // =========================================================
        // GIFT VOUCHER PAYMENT
        // =========================================================

        [ObservableProperty]
        private int _giftVoucherId = 0;

        [ObservableProperty]
        private string _giftVoucherNo = string.Empty;

        [ObservableProperty]
        private string _giftVoucherBarcode = string.Empty;

        [ObservableProperty]
        private decimal _giftVoucherAmount = 0m;

        [ObservableProperty]
        private decimal _giftVoucherForfeitedAmount = 0m;

        public bool IsGiftVoucher =>
            PaymentType.Equals("GiftVoucher", StringComparison.OrdinalIgnoreCase) ||
            PaymentType.Equals("Gift Voucher", StringComparison.OrdinalIgnoreCase);
    }
}