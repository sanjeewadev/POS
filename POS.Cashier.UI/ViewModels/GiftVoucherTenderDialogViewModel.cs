using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Repositories;

namespace POS.Cashier.UI.ViewModels
{
    public partial class GiftVoucherTenderDialogViewModel : ObservableObject
    {
        private readonly GiftVoucherRepository _giftVoucherRepository;

        // =========================================================
        // INPUT / INVOICE STATE
        // =========================================================

        [ObservableProperty]
        private decimal _balanceDue = 0m;

        [ObservableProperty]
        private string _voucherBarcode = string.Empty;

        // =========================================================
        // VALIDATED VOUCHER DETAILS
        // =========================================================

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasValidatedVoucher))]
        private int _giftVoucherId = 0;

        [ObservableProperty]
        private string _voucherNo = string.Empty;

        [ObservableProperty]
        private decimal _voucherAmount = 0m;

        [ObservableProperty]
        private decimal _amountToApply = 0m;

        [ObservableProperty]
        private decimal _forfeitedAmount = 0m;

        [ObservableProperty]
        private string _voucherStatus = string.Empty;

        [ObservableProperty]
        private DateTime? _expiryDate;

        [ObservableProperty]
        private bool _requiresManagerApproval = false;

        [ObservableProperty]
        private bool _isManagerApproved = false;

        [ObservableProperty]
        private bool _isVoucherValid = false;

        // =========================================================
        // STATUS
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusText = "Scan or enter gift voucher barcode.";

        [ObservableProperty]
        private string _statusColorHex = "#003366";

        public bool HasValidatedVoucher =>
            GiftVoucherId > 0 &&
            IsVoucherValid &&
            AmountToApply > 0m;

        public bool CanConfirm =>
            HasValidatedVoucher &&
            (!RequiresManagerApproval || IsManagerApproved);

        public event Action<bool>? ActionCompleted;

        public GiftVoucherTenderDialogViewModel(GiftVoucherRepository giftVoucherRepository)
        {
            _giftVoucherRepository = giftVoucherRepository;
        }

        // =========================================================
        // INITIALIZE
        // =========================================================

        public void Initialize(decimal balanceDue)
        {
            BalanceDue = Math.Round(balanceDue, 2);
            ClearInternal(false);

            if (BalanceDue <= 0m)
            {
                StatusText = "Invoice is already fully paid.";
                StatusColorHex = "#EF4444";
                return;
            }

            StatusText = $"Balance due: Rs. {BalanceDue:N2}. Scan or enter voucher barcode.";
            StatusColorHex = "#003366";
        }

        // =========================================================
        // COMMANDS
        // =========================================================

        [RelayCommand]
        private async Task SearchVoucherAsync()
        {
            if (IsBusy)
                return;

            string code = NormalizeText(VoucherBarcode);

            ClearValidatedVoucherOnly();

            if (BalanceDue <= 0m)
            {
                StatusText = "Invoice is already fully paid.";
                StatusColorHex = "#EF4444";
                return;
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                StatusText = "Voucher barcode is required.";
                StatusColorHex = "#EF4444";
                return;
            }

            try
            {
                IsBusy = true;
                StatusText = "Checking gift voucher...";
                StatusColorHex = "#3B82F6";

                var result = await _giftVoucherRepository.ValidateVoucherForRedemptionAsync(
                    code,
                    BalanceDue,
                    allowForfeitWithManagerApproval: false);

                GiftVoucherId = result.GiftVoucherId;
                VoucherNo = result.VoucherNo;
                VoucherBarcode = string.IsNullOrWhiteSpace(result.Barcode)
                    ? code
                    : result.Barcode;

                VoucherAmount = result.VoucherAmount;
                AmountToApply = result.AmountToApply;
                ForfeitedAmount = result.ForfeitedAmount;
                VoucherStatus = result.Status;
                ExpiryDate = result.ExpiryDate;
                RequiresManagerApproval = result.RequiresManagerApproval;
                IsManagerApproved = false;
                IsVoucherValid = result.IsValid;

                if (result.IsValid)
                {
                    StatusText = $"Voucher ready: {VoucherNo} / Apply Rs. {AmountToApply:N2}";
                    StatusColorHex = "#10B981";
                }
                else if (result.RequiresManagerApproval)
                {
                    StatusText = result.Message;
                    StatusColorHex = "#F59E0B";
                }
                else
                {
                    StatusText = result.Message;
                    StatusColorHex = "#EF4444";
                }
            }
            catch (Exception ex)
            {
                ClearValidatedVoucherOnly();

                StatusText = $"Voucher check failed: {ex.Message}";
                StatusColorHex = "#EF4444";
            }
            finally
            {
                IsBusy = false;
                NotifyComputedProperties();
            }
        }

        [RelayCommand]
        private async Task ApproveForfeitAsync()
        {
            if (IsBusy)
                return;

            string code = NormalizeText(VoucherBarcode);

            if (BalanceDue <= 0m)
            {
                StatusText = "Invoice is already fully paid.";
                StatusColorHex = "#EF4444";
                return;
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                StatusText = "Voucher barcode is required.";
                StatusColorHex = "#EF4444";
                return;
            }

            try
            {
                IsBusy = true;
                StatusText = "Rechecking voucher with manager approval...";
                StatusColorHex = "#3B82F6";

                var result = await _giftVoucherRepository.ValidateVoucherForRedemptionAsync(
                    code,
                    BalanceDue,
                    allowForfeitWithManagerApproval: true);

                GiftVoucherId = result.GiftVoucherId;
                VoucherNo = result.VoucherNo;
                VoucherBarcode = string.IsNullOrWhiteSpace(result.Barcode)
                    ? code
                    : result.Barcode;

                VoucherAmount = result.VoucherAmount;
                AmountToApply = result.AmountToApply;
                ForfeitedAmount = result.ForfeitedAmount;
                VoucherStatus = result.Status;
                ExpiryDate = result.ExpiryDate;
                RequiresManagerApproval = false;
                IsManagerApproved = true;
                IsVoucherValid = result.IsValid;

                if (!result.IsValid)
                {
                    StatusText = result.Message;
                    StatusColorHex = "#EF4444";
                    IsManagerApproved = false;
                    return;
                }

                StatusText = $"Manager approved. Apply Rs. {AmountToApply:N2}. Forfeit Rs. {ForfeitedAmount:N2}.";
                StatusColorHex = "#10B981";
            }
            catch (Exception ex)
            {
                StatusText = $"Manager approval failed: {ex.Message}";
                StatusColorHex = "#EF4444";
            }
            finally
            {
                IsBusy = false;
                NotifyComputedProperties();
            }
        }

        [RelayCommand]
        private void Confirm()
        {
            if (!HasValidatedVoucher)
            {
                StatusText = "Validate an active voucher before confirming.";
                StatusColorHex = "#EF4444";
                return;
            }

            if (RequiresManagerApproval && !IsManagerApproved)
            {
                StatusText = "Manager approval is required because voucher value is higher than balance due.";
                StatusColorHex = "#F59E0B";
                return;
            }

            if (AmountToApply <= 0m)
            {
                StatusText = "Voucher applied amount is invalid.";
                StatusColorHex = "#EF4444";
                return;
            }

            if (AmountToApply > BalanceDue)
            {
                StatusText = "Voucher applied amount cannot be greater than balance due.";
                StatusColorHex = "#EF4444";
                return;
            }

            ActionCompleted?.Invoke(true);
        }

        [RelayCommand]
        private void Clear()
        {
            ClearInternal(true);
        }

        [RelayCommand]
        private void Cancel()
        {
            ActionCompleted?.Invoke(false);
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private void ClearInternal(bool resetBarcode)
        {
            if (resetBarcode)
                VoucherBarcode = string.Empty;

            ClearValidatedVoucherOnly();

            StatusText = BalanceDue > 0m
                ? $"Balance due: Rs. {BalanceDue:N2}. Scan or enter voucher barcode."
                : "Scan or enter gift voucher barcode.";

            StatusColorHex = "#003366";
        }

        private void ClearValidatedVoucherOnly()
        {
            GiftVoucherId = 0;
            VoucherNo = string.Empty;
            VoucherAmount = 0m;
            AmountToApply = 0m;
            ForfeitedAmount = 0m;
            VoucherStatus = string.Empty;
            ExpiryDate = null;
            RequiresManagerApproval = false;
            IsManagerApproved = false;
            IsVoucherValid = false;

            NotifyComputedProperties();
        }

        private void NotifyComputedProperties()
        {
            OnPropertyChanged(nameof(HasValidatedVoucher));
            OnPropertyChanged(nameof(CanConfirm));
        }

        partial void OnRequiresManagerApprovalChanged(bool value)
        {
            NotifyComputedProperties();
        }

        partial void OnIsManagerApprovedChanged(bool value)
        {
            NotifyComputedProperties();
        }

        partial void OnIsVoucherValidChanged(bool value)
        {
            NotifyComputedProperties();
        }

        partial void OnAmountToApplyChanged(decimal value)
        {
            NotifyComputedProperties();
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}