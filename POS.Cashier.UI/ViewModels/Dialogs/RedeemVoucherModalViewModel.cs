using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using System;
using System.Threading.Tasks;

namespace POS.Cashier.UI.ViewModels.Dialogs
{
    public partial class RedeemVoucherModalViewModel : ObservableObject
    {
        private readonly GiftVoucherRepository _repository;

        // ==========================================
        // INPUTS & STATE
        // ==========================================
        [ObservableProperty]
        private string _scannedBarcode = string.Empty;

        [ObservableProperty]
        private decimal _amountDue; // The cart total passed in from the main screen

        // ==========================================
        // SMART MATH RESULTS
        // ==========================================
        [ObservableProperty] private decimal _voucherBalance;
        [ObservableProperty] private decimal _amountToApply;
        [ObservableProperty] private decimal _forfeitedAmount;

        // ==========================================
        // UI VISIBILITY TRIGGERS (Replaces MessageBox!)
        // ==========================================
        [ObservableProperty] private string _statusMessage = "Scan Gift Voucher barcode to check balance.";
        [ObservableProperty] private bool _isCardValid = false;

        // This toggles the bright warning box in the XAML
        [ObservableProperty] private bool _isWarningVisible = false;

        // This enables the final "Confirm Payment" button only after scanning
        [ObservableProperty] private bool _isAwaitingConfirmation = false;

        private int _validVoucherId;

        // ==========================================
        // CALLBACKS FOR THE MAIN TERMINAL
        // ==========================================
        public Action<int, string, decimal>? OnVoucherApproved;
        public Action? OnCancel;

        public RedeemVoucherModalViewModel(GiftVoucherRepository repository)
        {
            _repository = repository;
        }

        // =================================================================================
        // ACTION: Initialize (Called by the main terminal right before the modal opens)
        // =================================================================================
        public void Initialize(decimal currentAmountDue)
        {
            AmountDue = currentAmountDue;
            ScannedBarcode = string.Empty;
            IsCardValid = false;
            IsWarningVisible = false;
            IsAwaitingConfirmation = false;
            VoucherBalance = 0;
            AmountToApply = 0;
            ForfeitedAmount = 0;
            StatusMessage = $"Remaining Bill: Rs. {AmountDue:N2}. Scan customer barcode...";
        }

        // =================================================================================
        // ACTION: Verify Card & Do the Math
        // =================================================================================
        [RelayCommand]
        private async Task SearchBarcodeAsync()
        {
            if (string.IsNullOrWhiteSpace(ScannedBarcode)) return;

            try
            {
                // 1. Talk to the database securely
                var result = await _repository.ValidateVoucherForPaymentAsync(ScannedBarcode);

                if (!result.IsValid)
                {
                    ResetValidationState($"❌ {result.ErrorMessage}");
                    return;
                }

                _validVoucherId = result.VoucherId;
                VoucherBalance = result.AvailableBalance;
                IsCardValid = true;
                IsAwaitingConfirmation = true;

                // 2. THE SMART MATH (Single-Use Rule Execution)
                if (VoucherBalance > AmountDue)
                {
                    // Scenario A: Voucher is bigger than the bill (Triggers Breakage Profit)
                    AmountToApply = AmountDue;
                    ForfeitedAmount = VoucherBalance - AmountDue;
                    IsWarningVisible = true; // Turns on the XAML warning box!
                    StatusMessage = $"✅ Card Valid. Warning: Customer will lose Rs. {ForfeitedAmount:N2}.";
                }
                else
                {
                    // Scenario B: Bill is bigger than (or equal to) the voucher
                    AmountToApply = VoucherBalance;
                    ForfeitedAmount = 0;
                    IsWarningVisible = false;
                    StatusMessage = $"✅ Card Valid. Will apply Rs. {AmountToApply:N2} to bill.";
                }
            }
            catch (Exception ex)
            {
                ResetValidationState($"Error: {ex.Message}");
            }
        }

        // =================================================================================
        // ACTION: Confirm & Send to Pending Bucket
        // =================================================================================
        [RelayCommand]
        private void ConfirmRedemption()
        {
            if (!IsAwaitingConfirmation) return;

            // Close modal and pass the validated data back to the main terminal's Pending Bucket
            OnVoucherApproved?.Invoke(_validVoucherId, ScannedBarcode, AmountToApply);
        }

        [RelayCommand]
        private void Cancel() => OnCancel?.Invoke();

        // Helper to keep the UI clean if a scan fails
        private void ResetValidationState(string message)
        {
            IsCardValid = false;
            IsWarningVisible = false;
            IsAwaitingConfirmation = false;
            StatusMessage = message;
        }
    }
}