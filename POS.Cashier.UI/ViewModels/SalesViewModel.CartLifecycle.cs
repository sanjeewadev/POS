using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using POS.Cashier.UI.Models;
using POS.Cashier.UI.Services;
using POS.Core.Configuration;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Services.Tax;

namespace POS.Cashier.UI.ViewModels
{
    public partial class SalesViewModel
    {
        private readonly CashierCartRepository _cashierCartRepository;
        private readonly SemaphoreSlim _cartSaveSemaphore = new(1, 1);
        private CancellationTokenSource? _cartSaveDebounceCts;
        private Guid _cartToken = Guid.NewGuid();
        private bool _suppressCartPersistence;
        private bool _isCheckoutInProgress;

        public Guid CurrentCartToken => _cartToken;
        public bool IsCheckoutInProgress => _isCheckoutInProgress;

        private void SetCheckoutInProgress(bool value)
        {
            if (_isCheckoutInProgress == value)
                return;

            _isCheckoutInProgress = value;
            OnPropertyChanged(nameof(IsCheckoutInProgress));
        }

        public CashierCartOwnerDto CurrentCartOwner => new()
        {
            TerminalNo = TerminalNo,
            ShiftSessionId = _currentShiftId,
            CashierName = CashierName
        };

        partial void OnInvoiceDiscountAmountChanged(decimal value)
        {
            ScheduleCartAutosave();
        }

        partial void OnActiveB2BCustomerChanged(CustomerSearchDto? value)
        {
            ScheduleCartAutosave();
        }

        partial void OnIsWholesaleModeChanged(bool value)
        {
            OnPropertyChanged(nameof(PricingModeButtonText));
            ScheduleCartAutosave();
        }

        public async Task<CashierCartSessionDto?> GetActiveCartForRecoveryAsync()
        {
            if (_currentShiftId <= 0)
                return null;

            CashierCartSessionDto? active =
                await _cashierCartRepository.GetActiveAsync(CurrentCartOwner);

            if (active == null)
            {
                _cartToken = Guid.NewGuid();
                return null;
            }

            return active;
        }

        public async Task RestoreActiveCartAsync(CashierCartSessionDto session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (!session.Status.Equals(CashierCartStatusCodes.Active, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only an active cart can be resumed.");

            EnsureSessionOwner(session);
            await RestoreSessionIntoUiAsync(session);
        }

        public async Task<IReadOnlyList<CashierCartSessionDto>> GetHeldCartsAsync()
        {
            return await _cashierCartRepository.ListHeldAsync(CurrentCartOwner);
        }

        public async Task<CashierCartSessionDto> SuspendCurrentCartAsync()
        {
            if (!Cart.Any())
                throw new InvalidOperationException("The cart is empty.");

            if (IsPaymentModeActive)
                throw new InvalidOperationException("Cancel payment mode before suspending the cart.");

            await FlushCartPersistenceAsync();

            CashierCartSessionDto held = await _cashierCartRepository.SuspendAsync(
                _cartToken,
                CurrentCartOwner);

            ClearUiForNewCart();
            return held;
        }

        public async Task<CashierCartSessionDto> RecallHeldCartAsync(int cartSessionId)
        {
            if (Cart.Any())
                throw new InvalidOperationException("The current cart must be empty before Recall.");

            if (IsPaymentModeActive)
                throw new InvalidOperationException("Cancel payment mode before Recall.");

            CashierCartSessionDto recalled = await _cashierCartRepository.RecallAsync(
                cartSessionId,
                CurrentCartOwner);

            await RestoreSessionIntoUiAsync(recalled);
            return recalled;
        }

        public async Task CancelRecoveredActiveCartAsync(
            CashierCartSessionDto session,
            string reasonCode,
            string reasonText)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            EnsureSessionOwner(session);
            _cartToken = session.CartToken;

            await _cashierCartRepository.CancelAsync(
                session.CartToken,
                CurrentCartOwner,
                reasonCode,
                reasonText);

            ClearUiForNewCart();
        }

        public async Task CancelCurrentCartAsync(string reasonCode, string reasonText)
        {
            if (!Cart.Any())
            {
                ClearUiForNewCart();
                return;
            }

            if (IsPaymentModeActive)
                CancelPaymentMode();

            await FlushCartPersistenceAsync();
            await _cashierCartRepository.CancelAsync(
                _cartToken,
                CurrentCartOwner,
                reasonCode,
                reasonText);

            ClearUiForNewCart();
        }

        public async Task CancelHeldCartAsync(int cartSessionId, string reasonCode, string reasonText)
        {
            await _cashierCartRepository.CancelHeldAsync(
                cartSessionId,
                CurrentCartOwner,
                reasonCode,
                reasonText);
        }

        public async Task<bool> FlushCartBeforeLogoffAsync()
        {
            try
            {
                await FlushCartPersistenceAsync();
                return true;
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException("Cashier", "Save active cart before logoff", ex);
                await ShowNotificationAsync($"Cart could not be saved: {ex.Message}", "#EF4444");
                return false;
            }
        }

        public async Task FlushCartPersistenceAsync()
        {
            _cartSaveDebounceCts?.Cancel();

            if (Cart.Any())
            {
                await SaveCurrentCartAsync();
                return;
            }

            await CloseEmptyPersistedCartAsync(_cartToken, CancellationToken.None);
        }

        private void ScheduleCartAutosave()
        {
            if (_suppressCartPersistence || _currentShiftId <= 0)
                return;

            _cartSaveDebounceCts?.Cancel();
            _cartSaveDebounceCts?.Dispose();
            _cartSaveDebounceCts = new CancellationTokenSource();
            CancellationToken token = _cartSaveDebounceCts.Token;

            _ = Cart.Any()
                ? DebouncedSaveAsync(token)
                : DebouncedCancelEmptyCartAsync(token, _cartToken);
        }


        private async Task DebouncedCancelEmptyCartAsync(
            CancellationToken token,
            Guid cartToken)
        {
            try
            {
                await Task.Delay(400, token);
                await CloseEmptyPersistedCartAsync(cartToken, token);
            }
            catch (OperationCanceledException)
            {
                // A newer cart edit replaced this request.
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException("Cashier", "Close empty persisted cart", ex);
            }
        }

        private async Task CloseEmptyPersistedCartAsync(
            Guid cartToken,
            CancellationToken cancellationToken)
        {
            await _cartSaveSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (_suppressCartPersistence || Cart.Any() || cartToken != _cartToken)
                    return;

                CashierCartSessionDto? existing =
                    await _cashierCartRepository.GetByTokenAsync(cartToken);

                if (existing != null &&
                    existing.Status.Equals(
                        CashierCartStatusCodes.Active,
                        StringComparison.OrdinalIgnoreCase))
                {
                    await _cashierCartRepository.CancelAsync(
                        cartToken,
                        CurrentCartOwner,
                        CashierCartCancellationReasons.CartEmptied,
                        "The final cart line was removed before payment.");
                }

                _cartToken = Guid.NewGuid();
            }
            finally
            {
                _cartSaveSemaphore.Release();
            }
        }

        private async Task DebouncedSaveAsync(CancellationToken token)
        {
            try
            {
                // This continuation intentionally stays on the WPF synchronization context so
                // ObservableCollection and UI-bound properties are read only from the UI thread.
                await Task.Delay(400, token);
                await SaveCurrentCartAsync();
            }
            catch (OperationCanceledException)
            {
                // A newer edit replaced this save request.
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException("Cashier", "Automatic cart save", ex);
            }
        }

        private async Task<CashierCartSessionDto?> SaveCurrentCartAsync()
        {
            if (_suppressCartPersistence || _currentShiftId <= 0 || !Cart.Any())
                return null;

            await _cartSaveSemaphore.WaitAsync();
            try
            {
                if (_suppressCartPersistence || !Cart.Any())
                    return null;

                CashierCartSaveRequest request = new()
                {
                    CartToken = _cartToken,
                    Owner = CurrentCartOwner,
                    Customer = ActiveB2BCustomer,
                    IsWholesaleMode = IsWholesaleMode,
                    InvoiceDiscountAmount = InvoiceDiscountAmount,
                    GrossTotal = GrossValue,
                    TotalDiscount = TotalDiscount,
                    NetTotal = NetValue,
                    Lines = Cart.Select((item, index) => ToSnapshot(item, index + 1)).ToList()
                };

                return await _cashierCartRepository.SaveActiveAsync(request);
            }
            finally
            {
                _cartSaveSemaphore.Release();
            }
        }

        private async Task RestoreSessionIntoUiAsync(CashierCartSessionDto session)
        {
            _suppressCartPersistence = true;
            try
            {
                ClearCart();
                _cartToken = session.CartToken;

                foreach (CashierCartLineSnapshotDto snapshot in session.Lines.OrderBy(l => l.LineNumber))
                    Cart.Add(FromSnapshot(snapshot));

                ActiveB2BCustomer = session.Customer;
                CustomerName = session.Customer?.DisplayName ?? "Walk-In";
                IsWholesaleMode = session.IsWholesaleMode;
                InvoiceDiscountAmount = Math.Round(session.InvoiceDiscountAmount, 2);
                SelectedCartItem = Cart.LastOrDefault();
                PaymentLines.Clear();
                IsPaymentModeActive = false;
                TerminalInputMode = "READY TO SCAN";
                PaymentStatusText = "Recovered cart. Payment must be entered again.";
                PaymentStatusColor = "#D97706";
                RecalculateTotals();
                RecalculatePaymentTotals();
            }
            finally
            {
                _suppressCartPersistence = false;
            }

            // Re-save once after deserialization so the stored totals/revision are current.
            await SaveCurrentCartAsync();
        }

        private void ClearUiForNewCart()
        {
            _suppressCartPersistence = true;
            try
            {
                ClearCart();
                _cartToken = Guid.NewGuid();
            }
            finally
            {
                _suppressCartPersistence = false;
            }
        }

        private void CompleteCartUiReset()
        {
            ClearUiForNewCart();
        }

        private void EnsureSessionOwner(CashierCartSessionDto session)
        {
            if (session.ShiftSessionId != _currentShiftId ||
                !session.TerminalNo.Equals(TerminalNo, StringComparison.OrdinalIgnoreCase) ||
                !session.CashierName.Equals(CashierName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The cart belongs to a different terminal, shift or cashier.");
            }
        }

        private static CashierCartLineSnapshotDto ToSnapshot(CartItem item, int lineNumber)
        {
            return new CashierCartLineSnapshotDto
            {
                LineNumber = lineNumber,
                LineType = item.IsGiftVoucherSale
                    ? CashierCartLineTypeCodes.GiftVoucherSale
                    : item.IsService
                        ? CashierCartLineTypeCodes.Service
                        : CashierCartLineTypeCodes.StockItem,
                ItemVariantId = item.ItemVariantId,
                ItemBatchId = item.ItemBatchId,
                ItemParentId = item.ItemParentId,
                CategoryId = item.CategoryId,
                SubCategoryId = item.SubCategoryId,
                PrimarySupplierId = item.PrimarySupplierId,
                SupplierIds = item.SupplierIds.ToList(),
                ItemCode = item.ItemCode,
                SkuCode = item.SkuCode,
                Barcode = item.Barcode,
                Description = item.Description,
                VariantDescription = item.VariantDescription,
                Uom = item.Uom,
                ItemType = item.ItemType,
                TaxProfile = item.TaxProfile,
                BatchNo = item.BatchNo,
                ExpiryDate = item.ExpiryDate,
                ReceivedDate = item.ReceivedDate,
                AvailableBatchStock = item.AvailableBatchStock,
                CostPrice = item.CostPrice,
                RetailPrice = item.RetailPrice,
                WholesalePrice = item.WholesalePrice,
                MinimumPrice = item.MinimumPrice,
                MaximumPrice = item.MaximumPrice,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                DiscountPercentage = item.DiscountPercentage,
                ManualDiscountAmount = item.ManualDiscountAmount,
                DiscountMode = item.DiscountMode,
                IsManualDiscount = item.IsManualDiscount,
                IsPriceOverridden = item.IsPriceOverridden,
                PriceOverrideAmount = item.PriceOverrideAmount,
                PriceOverrideApprovedBy = item.PriceOverrideApprovedBy,
                PriceOverrideApprovedAt = item.PriceOverrideApprovedAt,
                IsFreeItem = item.IsFreeItem,
                FreeIssueRuleId = item.FreeIssueRuleId,
                FreeIssueRuleName = item.FreeIssueRuleName,
                FreeIssueType = item.FreeIssueType,
                FreeReasonCode = item.FreeReasonCode,
                FreeReasonText = item.FreeReasonText,
                FreeApprovedBy = item.FreeApprovedBy,
                FreeApprovedAt = item.FreeApprovedAt,
                FreeIssueAppliedBy = item.FreeIssueAppliedBy,
                FreeIssueAppliedAt = item.FreeIssueAppliedAt,
                FreeApprovedByUserId = item.FreeApprovedByUserId,
                FreeApprovedRole = item.FreeApprovedRole,
                FreeIssueRuleSnapshotJson = item.FreeIssueRuleSnapshotJson,
                FreeIssueSnapshotStatus = item.FreeIssueSnapshotStatus,
                OriginalUnitPrice = item.OriginalUnitPrice,
                FreeIssueCostValue = item.FreeIssueCostValue,
                FreeIssueSellingValue = item.FreeIssueSellingValue,
                IsSupplierRecoverable = item.IsSupplierRecoverable,
                SupplierId = item.SupplierId,
                SupplierName = item.SupplierName,
                SupplierPromotionReference = item.SupplierPromotionReference,
                SupplierClaimId = item.SupplierClaimId,
                SupplierClaimStatus = item.SupplierClaimStatus,
                SupplierClaimReferenceNo = item.SupplierClaimReferenceNo,
                SupplierClaimValue = item.SupplierClaimValue,
                InvoiceDiscountAllocation = item.InvoiceDiscountAllocation,
                TaxableAmount = item.TaxableAmount,
                VatAmount = item.VatAmount,
                TaxInclusiveAmount = item.TaxInclusiveAmount,
                IsGiftVoucherSale = item.IsGiftVoucherSale,
                GiftVoucherId = item.GiftVoucherId,
                GiftVoucherNo = item.GiftVoucherNo,
                GiftVoucherBarcode = item.GiftVoucherBarcode,
                IsRuleDiscount = item.IsRuleDiscount,
                DiscountRuleId = item.DiscountRuleId,
                DiscountRuleName = item.DiscountRuleName,
                DiscountReasonId = item.DiscountReasonId,
                DiscountReasonCode = item.DiscountReasonCode,
                DiscountReasonName = item.DiscountReasonName,
                DiscountApprovedBy = item.DiscountApprovedBy,
                DiscountApprovedAt = item.DiscountApprovedAt,
                DiscountRequiresManagerApproval = item.DiscountRequiresManagerApproval,
                DiscountRequiresAdminApproval = item.DiscountRequiresAdminApproval
            };
        }

        private static CartItem FromSnapshot(CashierCartLineSnapshotDto snapshot)
        {
            SalesTaxProfile profile = snapshot.TaxProfile ?? new SalesTaxProfile();

            return new CartItem
            {
                ItemVariantId = snapshot.ItemVariantId,
                ItemBatchId = snapshot.ItemBatchId,
                ItemParentId = snapshot.ItemParentId,
                CategoryId = snapshot.CategoryId,
                SubCategoryId = snapshot.SubCategoryId,
                PrimarySupplierId = snapshot.PrimarySupplierId,
                SupplierIds = snapshot.SupplierIds?.ToList() ?? new List<int>(),
                ItemCode = snapshot.ItemCode,
                SkuCode = snapshot.SkuCode,
                Barcode = snapshot.Barcode,
                Description = snapshot.Description,
                VariantDescription = snapshot.VariantDescription,
                Uom = snapshot.Uom,
                ItemType = snapshot.ItemType,
                TaxProfile = profile,
                BatchNo = snapshot.BatchNo,
                ExpiryDate = snapshot.ExpiryDate,
                ReceivedDate = snapshot.ReceivedDate,
                AvailableBatchStock = snapshot.AvailableBatchStock,
                CostPrice = snapshot.CostPrice,
                RetailPrice = snapshot.RetailPrice,
                WholesalePrice = snapshot.WholesalePrice,
                MinimumPrice = snapshot.MinimumPrice,
                MaximumPrice = snapshot.MaximumPrice,
                UnitPrice = snapshot.UnitPrice,
                Quantity = snapshot.Quantity,
                DiscountPercentage = snapshot.DiscountPercentage,
                ManualDiscountAmount = snapshot.ManualDiscountAmount,
                DiscountMode = snapshot.DiscountMode,
                IsManualDiscount = snapshot.IsManualDiscount,
                IsPriceOverridden = snapshot.IsPriceOverridden,
                PriceOverrideAmount = snapshot.PriceOverrideAmount,
                PriceOverrideApprovedBy = snapshot.PriceOverrideApprovedBy,
                PriceOverrideApprovedAt = snapshot.PriceOverrideApprovedAt,
                IsFreeItem = snapshot.IsFreeItem,
                FreeIssueRuleId = snapshot.FreeIssueRuleId,
                FreeIssueRuleName = snapshot.FreeIssueRuleName,
                FreeIssueType = snapshot.FreeIssueType,
                FreeReasonCode = snapshot.FreeReasonCode,
                FreeReasonText = snapshot.FreeReasonText,
                FreeApprovedBy = snapshot.FreeApprovedBy,
                FreeApprovedAt = snapshot.FreeApprovedAt,
                FreeIssueAppliedBy = snapshot.FreeIssueAppliedBy,
                FreeIssueAppliedAt = snapshot.FreeIssueAppliedAt,
                FreeApprovedByUserId = snapshot.FreeApprovedByUserId,
                FreeApprovedRole = snapshot.FreeApprovedRole,
                FreeIssueRuleSnapshotJson = snapshot.FreeIssueRuleSnapshotJson,
                FreeIssueSnapshotStatus = snapshot.FreeIssueSnapshotStatus,
                OriginalUnitPrice = snapshot.OriginalUnitPrice,
                FreeIssueCostValue = snapshot.FreeIssueCostValue,
                FreeIssueSellingValue = snapshot.FreeIssueSellingValue,
                IsSupplierRecoverable = snapshot.IsSupplierRecoverable,
                SupplierId = snapshot.SupplierId,
                SupplierName = snapshot.SupplierName,
                SupplierPromotionReference = snapshot.SupplierPromotionReference,
                SupplierClaimId = snapshot.SupplierClaimId,
                SupplierClaimStatus = snapshot.SupplierClaimStatus,
                SupplierClaimReferenceNo = snapshot.SupplierClaimReferenceNo,
                SupplierClaimValue = snapshot.SupplierClaimValue,
                InvoiceDiscountAllocation = snapshot.InvoiceDiscountAllocation,
                TaxableAmount = snapshot.TaxableAmount,
                VatAmount = snapshot.VatAmount,
                TaxInclusiveAmount = snapshot.TaxInclusiveAmount,
                IsGiftVoucherSale = snapshot.IsGiftVoucherSale,
                GiftVoucherId = snapshot.GiftVoucherId,
                GiftVoucherNo = snapshot.GiftVoucherNo,
                GiftVoucherBarcode = snapshot.GiftVoucherBarcode,
                IsRuleDiscount = snapshot.IsRuleDiscount,
                DiscountRuleId = snapshot.DiscountRuleId,
                DiscountRuleName = snapshot.DiscountRuleName,
                DiscountReasonId = snapshot.DiscountReasonId,
                DiscountReasonCode = snapshot.DiscountReasonCode,
                DiscountReasonName = snapshot.DiscountReasonName,
                DiscountApprovedBy = snapshot.DiscountApprovedBy,
                DiscountApprovedAt = snapshot.DiscountApprovedAt,
                DiscountRequiresManagerApproval = snapshot.DiscountRequiresManagerApproval,
                DiscountRequiresAdminApproval = snapshot.DiscountRequiresAdminApproval
            };
        }
    }
}
