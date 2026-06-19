using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using System;
using System.Threading.Tasks;

namespace POS.Cashier.UI.ViewModels.Dialogs
{
    public partial class IssueVoucherModalViewModel : ObservableObject
    {
        private readonly GiftVoucherRepository _repository;

        // ==========================================
        // INPUTS & STATE
        // ==========================================
        [ObservableProperty]
        private string _scannedBarcode = string.Empty;

        [ObservableProperty]
        private string _customerName = string.Empty;

        [ObservableProperty]
        private decimal _voucherValue = 0m;

        [ObservableProperty]
        private bool _isCardFound = false;

        [ObservableProperty]
        private string _statusMessage = "Please scan an inactive Gift Card barcode.";

        // ==========================================
        // CALLBACKS FOR THE MAIN TERMINAL
        // ==========================================
        public Action<string, decimal, string>? OnVoucherAddedToCart;
        public Action? OnCancel;

        public IssueVoucherModalViewModel(GiftVoucherRepository repository)
        {
            _repository = repository;
        }

        [RelayCommand]
        private async Task SearchBarcodeAsync()
        {
            if (string.IsNullOrWhiteSpace(ScannedBarcode)) return;

            try
            {
                // Talk to the repository we built in Phase 3
                var voucher = await _repository.GetInactiveVoucherAsync(ScannedBarcode);

                if (voucher != null)
                {
                    VoucherValue = voucher.InitialAmount;
                    IsCardFound = true;
                    StatusMessage = $"✅ Card Verified! Value: Rs. {VoucherValue:N2}. Ready to add to cart.";
                }
                else
                {
                    IsCardFound = false;
                    VoucherValue = 0;
                    StatusMessage = "❌ Card not found or has already been activated!";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private void AddToCart()
        {
            if (!IsCardFound) return;

            // Pass the barcode, value, and customer name back to the window to close it
            OnVoucherAddedToCart?.Invoke(ScannedBarcode, VoucherValue, CustomerName);
        }

        [RelayCommand]
        private void Cancel() => OnCancel?.Invoke();
    }
}