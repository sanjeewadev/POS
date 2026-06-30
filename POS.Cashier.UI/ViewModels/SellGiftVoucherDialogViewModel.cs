using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Repositories;

namespace POS.Cashier.UI.ViewModels
{
    public partial class SellGiftVoucherDialogViewModel : ObservableObject
    {
        private readonly GiftVoucherRepository _giftVoucherRepository;

        // =========================================================
        // INPUT
        // =========================================================

        [ObservableProperty]
        private string _voucherBarcode = string.Empty;

        // =========================================================
        // SELECTED VOUCHER DETAILS
        // =========================================================

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasValidatedVoucher))]
        private int _giftVoucherId = 0;

        [ObservableProperty]
        private string _voucherNo = string.Empty;

        [ObservableProperty]
        private decimal _voucherAmount = 0m;

        [ObservableProperty]
        private string _voucherStatus = string.Empty;

        [ObservableProperty]
        private DateTime? _expiryDate;

        [ObservableProperty]
        private string _displayDescription = "Gift Voucher";

        // =========================================================
        // STATUS
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusText = "Scan or enter the physical gift voucher barcode.";

        [ObservableProperty]
        private string _statusColorHex = "#003366";

        [ObservableProperty]
        private bool _isVoucherValid = false;

        public bool HasValidatedVoucher =>
            GiftVoucherId > 0 &&
            IsVoucherValid &&
            VoucherAmount > 0m;

        public event Action<bool>? ActionCompleted;

        public SellGiftVoucherDialogViewModel(GiftVoucherRepository giftVoucherRepository)
        {
            _giftVoucherRepository = giftVoucherRepository;
        }

        // =========================================================
        // COMMANDS
        // =========================================================

        [RelayCommand]
        private async Task SearchVoucherAsync()
        {
            if (IsBusy)
                return;

            ResetValidatedVoucherOnly();

            string code = NormalizeText(VoucherBarcode);

            if (string.IsNullOrWhiteSpace(code))
            {
                StatusText = "Voucher barcode is required.";
                StatusColorHex = "#EF4444";
                return;
            }

            try
            {
                IsBusy = true;
                StatusText = "Checking voucher...";
                StatusColorHex = "#3B82F6";

                var result = await _giftVoucherRepository.ValidateVoucherForSaleAsync(code);

                if (!result.IsValid)
                {
                    GiftVoucherId = result.GiftVoucherId;
                    VoucherNo = result.VoucherNo;
                    VoucherAmount = result.VoucherAmount;
                    VoucherStatus = result.Status;
                    ExpiryDate = result.ExpiryDate;
                    IsVoucherValid = false;

                    StatusText = result.Message;
                    StatusColorHex = "#EF4444";
                    return;
                }

                GiftVoucherId = result.GiftVoucherId;
                VoucherNo = result.VoucherNo;
                VoucherBarcode = result.Barcode;
                VoucherAmount = result.VoucherAmount;
                VoucherStatus = result.Status;
                ExpiryDate = result.ExpiryDate;
                DisplayDescription = $"Gift Voucher Rs. {VoucherAmount:N2}";
                IsVoucherValid = true;

                StatusText = $"Voucher ready to sell: {VoucherNo} / Rs. {VoucherAmount:N2}";
                StatusColorHex = "#10B981";
            }
            catch (Exception ex)
            {
                ResetValidatedVoucherOnly();

                StatusText = $"Voucher check failed: {ex.Message}";
                StatusColorHex = "#EF4444";
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(HasValidatedVoucher));
            }
        }

        [RelayCommand]
        private void Confirm()
        {
            if (!HasValidatedVoucher)
            {
                StatusText = "Validate a Created voucher before adding it to the sale.";
                StatusColorHex = "#EF4444";
                return;
            }

            ActionCompleted?.Invoke(true);
        }

        [RelayCommand]
        private void Clear()
        {
            VoucherBarcode = string.Empty;
            ResetValidatedVoucherOnly();

            StatusText = "Scan or enter the physical gift voucher barcode.";
            StatusColorHex = "#003366";
        }

        [RelayCommand]
        private void Cancel()
        {
            ActionCompleted?.Invoke(false);
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private void ResetValidatedVoucherOnly()
        {
            GiftVoucherId = 0;
            VoucherNo = string.Empty;
            VoucherAmount = 0m;
            VoucherStatus = string.Empty;
            ExpiryDate = null;
            DisplayDescription = "Gift Voucher";
            IsVoucherValid = false;

            OnPropertyChanged(nameof(HasValidatedVoucher));
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}