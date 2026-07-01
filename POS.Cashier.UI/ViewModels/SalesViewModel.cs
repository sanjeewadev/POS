using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.Messages;
using POS.Cashier.UI.Models;
using POS.Cashier.UI.Services;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;

namespace POS.Cashier.UI.ViewModels
{
    public partial class SalesViewModel : ObservableObject
    {
        private readonly ItemMasterRepository _itemRepository;
        private readonly SalesRepository _salesRepository;
        private readonly TillRepository _tillRepository;
        private readonly IReceiptPrintService _printService;

        public ObservableCollection<CartItem> Cart { get; } = new();
        public ObservableCollection<PaymentLine> PaymentLines { get; } = new();

        // =========================================================
        // CART / TOTALS
        // =========================================================

        [ObservableProperty]
        private CartItem? _selectedCartItem;

        [ObservableProperty]
        private decimal _grossValue = 0.00m;

        [ObservableProperty]
        private decimal _netValue = 0.00m;

        [ObservableProperty]
        private decimal _totalDiscount = 0.00m;

        [ObservableProperty]
        private int _totalItems = 0;

        [ObservableProperty]
        private decimal _totalPieces = 0m;

        // =========================================================
        // PAYMENT MODE
        // =========================================================

        [ObservableProperty]
        private bool _isPaymentModeActive = false;

        [ObservableProperty]
        private PaymentLine? _selectedPaymentLine;

        [ObservableProperty]
        private decimal _paidTotal = 0m;

        [ObservableProperty]
        private decimal _balanceDue = 0m;

        [ObservableProperty]
        private decimal _cashTenderedTotal = 0m;

        [ObservableProperty]
        private decimal _balanceReturned = 0m;

        [ObservableProperty]
        private string _paymentStatusText = "Sale mode active.";

        [ObservableProperty]
        private string _paymentStatusColor = "#003366";

        public bool CanConfirmPaymentSale =>
            IsPaymentModeActive &&
            Cart.Any() &&
            BalanceDue <= 0m &&
            PaymentLines.Any();

        // =========================================================
        // HEADER / SESSION
        // =========================================================

        [ObservableProperty]
        private string _terminalNo = "01";

        [ObservableProperty]
        private string _cashierName = "Pending...";

        [ObservableProperty]
        private string _invoiceNo = "PENDING...";

        [ObservableProperty]
        private DateTime _currentDate = DateTime.Now;

        // =========================================================
        // FAST TERMINAL INPUT BUFFER
        // =========================================================

        [ObservableProperty]
        private string _terminalInput = string.Empty;

        [ObservableProperty]
        private string _terminalInputMode = "SCAN / QTY";

        // =========================================================
        // CUSTOMER / WHOLESALE / LOYALTY
        // =========================================================

        [ObservableProperty]
        private string _customerName = "Walk-In";

        [ObservableProperty]
        private int _loyaltyPoints = 0;

        [ObservableProperty]
        private bool _isWholesaleMode = false;

        [ObservableProperty]
        private CustomerSearchDto? _activeB2BCustomer;

        // =========================================================
        // SECURITY
        // =========================================================

        [ObservableProperty]
        private bool _isManagerModeActive = false;

        [ObservableProperty]
        private string _securityStatusMode = "CASHIER MODE";

        [ObservableProperty]
        private bool _isTerminalLocked = false;

        // =========================================================
        // NOTIFICATION BAR
        // =========================================================

        [ObservableProperty]
        private string _notificationMessage = string.Empty;

        [ObservableProperty]
        private string _notificationColor = "#10B981";

        [ObservableProperty]
        private bool _isNotificationVisible = false;

        private int _currentShiftId = 0;
        public int CurrentShiftId => _currentShiftId;

        private readonly string _printerName = "POS-80";

        public SalesViewModel(
            ItemMasterRepository itemRepository,
            SalesRepository salesRepository,
            TillRepository tillRepository,
            IReceiptPrintService printService)
        {
            _itemRepository = itemRepository;
            _salesRepository = salesRepository;
            _tillRepository = tillRepository;
            _printService = printService;

            Cart.CollectionChanged += (_, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (CartItem item in e.NewItems)
                        item.PropertyChanged += CartItem_PropertyChanged;
                }

                if (e.OldItems != null)
                {
                    foreach (CartItem item in e.OldItems)
                        item.PropertyChanged -= CartItem_PropertyChanged;
                }

                RecalculateTotals();
            };

            PaymentLines.CollectionChanged += (_, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (PaymentLine payment in e.NewItems)
                        payment.PropertyChanged += PaymentLine_PropertyChanged;
                }

                if (e.OldItems != null)
                {
                    foreach (PaymentLine payment in e.OldItems)
                        payment.PropertyChanged -= PaymentLine_PropertyChanged;
                }

                RenumberPaymentLines();
                RecalculatePaymentTotals();
            };

            _ = LoadActiveShiftAsync();

            WeakReferenceMessenger.Default.Register<AddToCartMessage>(this, (r, m) =>
            {
                System.Media.SystemSounds.Beep.Play();
                _ = AddToCartFromMessageAsync(m.Value);
            });

            WeakReferenceMessenger.Default.Register<TopBarNotificationMessage>(this, (r, m) =>
            {
                _ = ShowNotificationAsync(m.Value.Message, m.Value.ColorHex);
            });
        }

        private void CartItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CartItem.Quantity) ||
                e.PropertyName == nameof(CartItem.UnitPrice) ||
                e.PropertyName == nameof(CartItem.DiscountPercentage) ||
                e.PropertyName == nameof(CartItem.ManualDiscountAmount) ||
                e.PropertyName == nameof(CartItem.DiscountMode) ||
                e.PropertyName == nameof(CartItem.IsManualDiscount) ||
e.PropertyName == nameof(CartItem.IsPriceOverridden) ||
e.PropertyName == nameof(CartItem.PriceOverrideAmount) ||
e.PropertyName == nameof(CartItem.IsRuleDiscount) ||
e.PropertyName == nameof(CartItem.DiscountRuleId) ||
e.PropertyName == nameof(CartItem.DiscountReasonId) ||
e.PropertyName == nameof(CartItem.LineAmount) ||
                e.PropertyName == nameof(CartItem.AvailableBatchStock) ||
                e.PropertyName == nameof(CartItem.IsFreeItem))
            {
                RecalculateTotals();

                if (IsPaymentModeActive)
                    RecalculatePaymentTotals();
            }
        }

        private void PaymentLine_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PaymentLine.Amount) ||
                e.PropertyName == nameof(PaymentLine.TenderedAmount) ||
                e.PropertyName == nameof(PaymentLine.ChangeAmount))
            {
                RecalculatePaymentTotals();
            }
        }

        // =========================================================
        // NOTIFICATION
        // =========================================================

        public async Task ShowNotificationAsync(string message, string colorHex = "#10B981")
        {
            NotificationMessage = message;
            NotificationColor = colorHex;
            IsNotificationVisible = true;

            await Task.Delay(2500);

            IsNotificationVisible = false;
        }

        public async Task LoadActiveShiftAsync()
        {
            try
            {
                var shift = await _tillRepository.GetActiveShiftAsync(TerminalNo);

                if (shift != null)
                {
                    _currentShiftId = shift.Id;
                    CashierName = shift.CashierName;
                }
                else
                {
                    _currentShiftId = 0;
                    CashierName = "NO OPEN SHIFT";
                }
            }
            catch
            {
                _currentShiftId = 0;
                CashierName = "ERROR LOADING SHIFT";
            }
        }

        // =========================================================
        // TERMINAL INPUT ENGINE
        // =========================================================

        public void AppendTerminalInput(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            TerminalInput += value;
        }

        public void BackspaceTerminalInput()
        {
            if (string.IsNullOrEmpty(TerminalInput))
                return;

            TerminalInput = TerminalInput[..^1];
        }

        [RelayCommand]
        public void ClearTerminalInput()
        {
            TerminalInput = string.Empty;
        }

        public async Task HandleTerminalEnterAsync()
        {
            string input = (TerminalInput ?? string.Empty).Trim();

            if (IsPaymentModeActive)
            {
                if (BalanceDue <= 0m)
                {
                    await ConfirmSaleFromPaymentModeAsync();
                    return;
                }

                if (!string.IsNullOrWhiteSpace(input))
                {
                    _ = ShowNotificationAsync("Select payment type after entering amount.", "#F59E0B");
                    return;
                }

                _ = ShowNotificationAsync("Payment is not complete.", "#F59E0B");
                return;
            }

            if (string.IsNullOrWhiteSpace(input))
                return;

            TerminalInput = string.Empty;

            if (SelectedCartItem != null &&
                IsLikelyQuantityInput(input) &&
                decimal.TryParse(input, out decimal qty))
            {
                SetSelectedLineQuantity(qty);
                return;
            }

            await ProcessBarcodeAsync(input);
        }

        private static bool IsLikelyQuantityInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            if (input.Length > 4)
                return false;

            return decimal.TryParse(input, out decimal value) &&
                   value > 0 &&
                   value <= 999m;
        }

        public void ApplyTerminalInputAsQuantityToSelected()
        {
            if (IsPaymentModeActive)
            {
                _ = ShowNotificationAsync("Cancel payment mode before changing quantity.", "#F59E0B");
                TerminalInput = string.Empty;
                return;
            }

            if (SelectedCartItem == null)
            {
                _ = ShowNotificationAsync("Select an item before changing quantity.", "#F59E0B");
                TerminalInput = string.Empty;
                return;
            }

            if (SelectedCartItem.IsFreeItem)
            {
                _ = ShowNotificationAsync("Change quantity before applying free issue. Remove and re-add if needed.", "#F59E0B");
                TerminalInput = string.Empty;
                return;
            }

            if (!decimal.TryParse(TerminalInput, out decimal qty) || qty <= 0)
            {
                _ = ShowNotificationAsync("Enter a valid quantity.", "#F59E0B");
                TerminalInput = string.Empty;
                return;
            }

            TerminalInput = string.Empty;
            SetSelectedLineQuantity(qty);
        }

        public void ApplyTerminalInputAsFixedDiscountToSelected()
        {
            if (!decimal.TryParse(TerminalInput, out decimal amount))
            {
                _ = ShowNotificationAsync("Enter a valid rupee discount amount.", "#F59E0B");
                TerminalInput = string.Empty;
                return;
            }

            TerminalInput = string.Empty;
            ApplyFixedDiscountToSelected(amount);
        }

        public void ApplyTerminalInputAsDiscountPercentToSelected()
        {
            if (!decimal.TryParse(TerminalInput, out decimal percent))
            {
                _ = ShowNotificationAsync("Enter a valid discount percentage.", "#F59E0B");
                TerminalInput = string.Empty;
                return;
            }

            TerminalInput = string.Empty;
            ApplyPercentDiscountToSelected(percent);
        }

        private static void ClearRuleDiscountSnapshot(CartItem item)
        {
            if (item == null)
                return;

            item.IsRuleDiscount = false;

            item.DiscountRuleId = 0;
            item.DiscountRuleName = string.Empty;

            item.DiscountReasonId = 0;
            item.DiscountReasonCode = string.Empty;
            item.DiscountReasonName = string.Empty;

            item.DiscountRequiresManagerApproval = false;
            item.DiscountRequiresAdminApproval = false;

            item.DiscountApprovedBy = string.Empty;
            item.DiscountApprovedAt = null;
        }

        public void ApplyTerminalInputAsPriceOverrideToSelected()
        {
            if (!decimal.TryParse(TerminalInput, out decimal newPrice))
            {
                _ = ShowNotificationAsync("Enter a valid new price.", "#F59E0B");
                TerminalInput = string.Empty;
                return;
            }

            TerminalInput = string.Empty;
            ApplyNewPriceToSelected(newPrice);
        }

        // =========================================================
        // QUANTITY
        // =========================================================

        public void IncreaseSelectedQuantity(decimal amount = 1m)
        {
            if (IsPaymentModeActive)
            {
                _ = ShowNotificationAsync("Cancel payment mode before changing quantity.", "#F59E0B");
                return;
            }

            if (SelectedCartItem == null)
            {
                _ = ShowNotificationAsync("Select an item first.", "#F59E0B");
                return;
            }

            SetSelectedLineQuantity(SelectedCartItem.Quantity + amount);
        }

        public void DecreaseSelectedQuantity(decimal amount = 1m)
        {
            if (IsPaymentModeActive)
            {
                _ = ShowNotificationAsync("Cancel payment mode before changing quantity.", "#F59E0B");
                return;
            }

            if (SelectedCartItem == null)
            {
                _ = ShowNotificationAsync("Select an item first.", "#F59E0B");
                return;
            }

            decimal newQty = SelectedCartItem.Quantity - amount;

            if (newQty <= 0m)
            {
                RemoveSelectedItem();
                return;
            }

            SetSelectedLineQuantity(newQty);
        }

        private void SetSelectedLineQuantity(decimal qty)
        {
            if (SelectedCartItem == null)
                return;

            if (SelectedCartItem.IsFreeItem)
            {
                _ = ShowNotificationAsync("Change quantity before applying free issue. Remove and re-add if needed.", "#F59E0B");
                return;
            }

            if (SelectedCartItem.IsGiftVoucherSale && qty != 1m)
            {
                _ = ShowNotificationAsync("Gift voucher quantity must be 1.", "#F59E0B");
                return;
            }

            if (qty <= 0m)
            {
                _ = ShowNotificationAsync("Quantity must be greater than zero.", "#F59E0B");
                return;
            }

            if (!SelectedCartItem.IsGiftVoucherSale && qty > SelectedCartItem.AvailableBatchStock)
            {
                _ = ShowNotificationAsync(
                    $"Only {SelectedCartItem.AvailableBatchStock:N3} available in selected batch.",
                    "#F59E0B");
                return;
            }

            SelectedCartItem.Quantity = qty;
            RecalculateTotals();

            _ = ShowNotificationAsync(
                $"Quantity updated: {SelectedCartItem.Description} x {qty:N3}",
                "#10B981");
        }

        // =========================================================
        // DISCOUNT / NEW PRICE
        // =========================================================

        private bool CanModifySelectedLineForPriceOrDiscount(string actionName)
        {
            if (IsPaymentModeActive)
            {
                _ = ShowNotificationAsync($"Cancel payment mode before {actionName}.", "#F59E0B");
                TerminalInput = string.Empty;
                return false;
            }

            if (SelectedCartItem == null)
            {
                _ = ShowNotificationAsync($"Select an item before {actionName}.", "#F59E0B");
                TerminalInput = string.Empty;
                return false;
            }

            if (SelectedCartItem.IsGiftVoucherSale)
            {
                _ = ShowNotificationAsync("Gift voucher sale line cannot be discounted or price changed.", "#EF4444");
                TerminalInput = string.Empty;
                return false;
            }

            if (SelectedCartItem.IsFreeItem)
            {
                _ = ShowNotificationAsync("Free item line cannot be discounted or price changed.", "#EF4444");
                TerminalInput = string.Empty;
                return false;
            }

            if (SelectedCartItem.Quantity <= 0m)
            {
                _ = ShowNotificationAsync("Selected item quantity must be greater than zero.", "#EF4444");
                TerminalInput = string.Empty;
                return false;
            }

            return true;
        }

        public void ApplyFixedDiscountToSelected(decimal amount)
        {
            if (!CanModifySelectedLineForPriceOrDiscount("applying fixed discount"))
                return;

            var item = SelectedCartItem!;

            amount = Math.Round(amount, 2);

            if (amount < 0m)
            {
                _ = ShowNotificationAsync("Discount amount cannot be negative.", "#EF4444");
                return;
            }

            if (item.IsPriceOverridden && amount > 0m)
            {
                _ = ShowNotificationAsync("Discount cannot be applied after New Price. Clear/re-add the item if needed.", "#F59E0B");
                return;
            }

            decimal grossAmount = Math.Round(item.GrossAmount, 2);

            if (amount > grossAmount)
            {
                _ = ShowNotificationAsync(
                    $"Discount cannot exceed line gross amount Rs. {grossAmount:N2}.",
                    "#EF4444");

                return;
            }

            ClearRuleDiscountSnapshot(item);

            item.DiscountPercentage = 0m;
            item.ManualDiscountAmount = amount;
            item.DiscountMode = amount > 0m ? "Amount" : "None";
            item.IsManualDiscount = amount > 0m;

            RecalculateTotals();

            _ = ShowNotificationAsync(
                amount == 0m
                    ? "Line discount cleared."
                    : $"Rs. discount applied: Rs. {amount:N2}",
                "#10B981");
        }

        public void ApplyPercentDiscountToSelected(decimal percent)
        {
            if (!CanModifySelectedLineForPriceOrDiscount("applying percentage discount"))
                return;

            var item = SelectedCartItem!;

            percent = Math.Round(percent, 2);

            if (percent < 0m || percent > 100m)
            {
                _ = ShowNotificationAsync("Discount percentage must be between 0 and 100.", "#EF4444");
                return;
            }

            if (item.IsPriceOverridden && percent > 0m)
            {
                _ = ShowNotificationAsync("Discount cannot be applied after New Price. Clear/re-add the item if needed.", "#F59E0B");
                return;
            }

            ClearRuleDiscountSnapshot(item);

            item.ManualDiscountAmount = 0m;
            item.DiscountPercentage = percent;
            item.DiscountMode = percent > 0m ? "Percent" : "None";
            item.IsManualDiscount = percent > 0m;

            RecalculateTotals();

            _ = ShowNotificationAsync(
                percent == 0m
                    ? "Line discount cleared."
                    : $"Percentage discount applied: {percent:N2}%",
                "#10B981");
        }

        public void ApplyNewPriceToSelected(decimal newPrice, string approvedBy = "")
        {
            if (!CanModifySelectedLineForPriceOrDiscount("changing price"))
                return;

            var item = SelectedCartItem!;

            newPrice = Math.Round(newPrice, 2);

            if (newPrice < 0m)
            {
                _ = ShowNotificationAsync("New price cannot be negative.", "#EF4444");
                return;
            }

            if (item.IsManualDiscount || item.DiscountAmount > 0m)
            {
                _ = ShowNotificationAsync(
                    "New Price cannot be applied after discount. Clear discount first by entering 0 and pressing Rs Disc or % Disc.",
                    "#F59E0B");

                return;
            }

            if (item.MinimumPrice > 0m && newPrice < item.MinimumPrice && !IsManagerModeActive)
            {
                _ = ShowNotificationAsync(
                    $"Manager approval required. Minimum price is Rs. {item.MinimumPrice:N2}.",
                    "#EF4444");

                return;
            }

            decimal originalPrice;

            if (item.IsPriceOverridden && item.OriginalUnitPrice > 0m)
            {
                originalPrice = Math.Round(item.OriginalUnitPrice, 2);
            }
            else
            {
                originalPrice = Math.Round(item.UnitPrice, 2);
                item.OriginalUnitPrice = originalPrice;
            }

            if (originalPrice <= 0m)
            {
                originalPrice = item.RetailPrice > 0m
                    ? Math.Round(item.RetailPrice, 2)
                    : Math.Round(newPrice, 2);

                item.OriginalUnitPrice = originalPrice;
            }

            if (newPrice == originalPrice)
            {
                item.UnitPrice = originalPrice;
                item.IsPriceOverridden = false;
                item.PriceOverrideAmount = 0m;
                item.PriceOverrideApprovedBy = string.Empty;
                item.PriceOverrideApprovedAt = null;

                RecalculateTotals();

                _ = ShowNotificationAsync("New Price cleared. Original price restored.", "#10B981");
                return;
            }

            item.UnitPrice = newPrice;
            item.IsPriceOverridden = true;
            item.PriceOverrideAmount = Math.Round(originalPrice - newPrice, 2);

            if (item.MinimumPrice > 0m && newPrice < item.MinimumPrice)
            {
                item.PriceOverrideApprovedBy = string.IsNullOrWhiteSpace(approvedBy)
                    ? "Manager Mode"
                    : approvedBy.Trim();

                item.PriceOverrideApprovedAt = DateTime.Now;
            }
            else
            {
                item.PriceOverrideApprovedBy = string.Empty;
                item.PriceOverrideApprovedAt = null;
            }

            RecalculateTotals();

            _ = ShowNotificationAsync($"New price applied: Rs. {newPrice:N2}", "#10B981");
        }

        public void ApplyDiscountRuleToSelected(CartItem cartItem, DiscountRuleApplyResult result)
        {
            if (cartItem == null)
            {
                _ = ShowNotificationAsync("Select an item before applying discount rule.", "#F59E0B");
                return;
            }

            SelectedCartItem = cartItem;

            if (!CanModifySelectedLineForPriceOrDiscount("applying discount rule"))
                return;

            if (result == null)
            {
                _ = ShowNotificationAsync("Discount rule result is missing.", "#EF4444");
                return;
            }

            if (cartItem.IsGiftVoucherSale)
            {
                _ = ShowNotificationAsync("Gift voucher sale line cannot use discount rule.", "#EF4444");
                return;
            }

            if (cartItem.IsFreeItem)
            {
                _ = ShowNotificationAsync("Free item line cannot use discount rule.", "#EF4444");
                return;
            }

            if (cartItem.IsPriceOverridden)
            {
                _ = ShowNotificationAsync("Discount rule cannot be applied after New Price.", "#EF4444");
                return;
            }

            if (result.DiscountRuleId <= 0)
            {
                _ = ShowNotificationAsync("Valid discount rule is required.", "#EF4444");
                return;
            }

            decimal discountAmount = Math.Round(result.DiscountAmount, 2);
            decimal grossAmount = Math.Round(cartItem.GrossAmount, 2);

            if (discountAmount <= 0m)
            {
                _ = ShowNotificationAsync("Discount amount must be greater than zero.", "#EF4444");
                return;
            }

            if (discountAmount > grossAmount)
            {
                _ = ShowNotificationAsync("Discount cannot exceed line gross amount.", "#EF4444");
                return;
            }

            bool approvalRequired =
                result.RequiresManagerApproval ||
                result.RequiresAdminApproval;

            if (approvalRequired && string.IsNullOrWhiteSpace(result.ApprovedBy))
            {
                _ = ShowNotificationAsync("Approval name is required for this discount rule.", "#EF4444");
                return;
            }

            string discountType = string.IsNullOrWhiteSpace(result.DiscountType)
                ? "Percent"
                : result.DiscountType.Trim();

            if (cartItem.OriginalUnitPrice <= 0m)
                cartItem.OriginalUnitPrice = cartItem.UnitPrice;

            // Rule discount is still calculated using existing cart fields:
            // Percent rule -> DiscountPercentage
            // Amount rule  -> ManualDiscountAmount
            if (discountType.Equals("Amount", StringComparison.OrdinalIgnoreCase))
            {
                cartItem.DiscountPercentage = 0m;
                cartItem.ManualDiscountAmount = discountAmount;
            }
            else
            {
                decimal percent = Math.Round(result.DiscountValue, 2);

                if (percent <= 0m || percent > 100m)
                {
                    _ = ShowNotificationAsync("Rule percentage must be between 0 and 100.", "#EF4444");
                    return;
                }

                cartItem.ManualDiscountAmount = 0m;
                cartItem.DiscountPercentage = percent;
            }

            cartItem.DiscountMode = "Rule";
            cartItem.IsManualDiscount = true;
            cartItem.IsRuleDiscount = true;

            cartItem.DiscountRuleId = result.DiscountRuleId;
            cartItem.DiscountRuleName = result.DiscountRuleName ?? string.Empty;

            cartItem.DiscountReasonId = result.DiscountReasonId ?? 0;
            cartItem.DiscountReasonCode = result.DiscountReasonCode ?? string.Empty;
            cartItem.DiscountReasonName = result.DiscountReasonName ?? string.Empty;

            cartItem.DiscountRequiresManagerApproval = result.RequiresManagerApproval;
            cartItem.DiscountRequiresAdminApproval = result.RequiresAdminApproval;

            cartItem.DiscountApprovedBy = approvalRequired
                ? result.ApprovedBy.Trim()
                : string.Empty;

            cartItem.DiscountApprovedAt = approvalRequired
                ? result.ApprovedAt ?? DateTime.Now
                : null;

            RecalculateTotals();

            _ = ShowNotificationAsync(
                $"Discount rule applied: Rs. {discountAmount:N2}",
                "#10B981");
        }

        // =========================================================
        // PAYMENT MODE
        // =========================================================

        public void EnterPaymentMode()
        {
            if (!Cart.Any())
            {
                _ = ShowNotificationAsync("Cart is empty.", "#F59E0B");
                return;
            }

            if (_currentShiftId == 0)
            {
                _ = ShowNotificationAsync("No active shift found.", "#EF4444");
                return;
            }

            if (Cart.Any(c => !c.IsGiftVoucherSale && c.ItemBatchId <= 0))
            {
                _ = ShowNotificationAsync("Cannot pay: one or more cart lines has no selected batch.", "#EF4444");
                return;
            }

            var invalidFreeLine = Cart.FirstOrDefault(c => c.IsFreeItem && c.FreeIssueRuleId <= 0);

            if (invalidFreeLine != null)
            {
                _ = ShowNotificationAsync($"Free issue rule missing: {invalidFreeLine.Description}", "#EF4444");
                return;
            }

            var invalidSupplierClaimLine = Cart.FirstOrDefault(c =>
                c.IsFreeItem &&
                c.IsSupplierRecoverable &&
                c.SupplierId <= 0);

            if (invalidSupplierClaimLine != null)
            {
                _ = ShowNotificationAsync($"Supplier missing for free issue: {invalidSupplierClaimLine.Description}", "#EF4444");
                return;
            }

            var belowMinimumLine = Cart.FirstOrDefault(c => c.IsBelowMinimumPrice);

            if (belowMinimumLine != null && !IsManagerModeActive)
            {
                _ = ShowNotificationAsync($"Price below minimum: {belowMinimumLine.Description}", "#EF4444");
                return;
            }

            IsPaymentModeActive = true;
            TerminalInputMode = "PAYMENT";
            TerminalInput = string.Empty;

            RecalculatePaymentTotals();

            PaymentStatusText = "Payment mode active. Enter amount and select payment type.";
            PaymentStatusColor = "#003366";

            _ = ShowNotificationAsync("Payment mode active.", "#3B82F6");
        }

        public void CancelPaymentMode()
        {
            if (!IsPaymentModeActive)
                return;

            PaymentLines.Clear();
            SelectedPaymentLine = null;

            IsPaymentModeActive = false;
            TerminalInputMode = "SCAN / QTY";
            TerminalInput = string.Empty;

            RecalculatePaymentTotals();

            PaymentStatusText = "Payment cancelled. Sale mode active.";
            PaymentStatusColor = "#F59E0B";

            _ = ShowNotificationAsync("Payment mode cancelled.", "#F59E0B");
        }

        public void AddConfirmedCashPayment(decimal appliedAmount, decimal tenderedAmount, decimal changeAmount)
        {
            if (!EnsurePaymentModeReady())
                return;

            appliedAmount = Math.Round(appliedAmount, 2);
            tenderedAmount = Math.Round(tenderedAmount, 2);
            changeAmount = Math.Round(changeAmount, 2);

            if (appliedAmount <= 0m)
            {
                _ = ShowNotificationAsync("Invalid cash payment amount.", "#EF4444");
                return;
            }

            if (appliedAmount > BalanceDue)
            {
                _ = ShowNotificationAsync("Cash applied amount cannot be greater than balance due.", "#EF4444");
                return;
            }

            if (tenderedAmount < appliedAmount)
            {
                _ = ShowNotificationAsync("Cash tendered cannot be lower than applied cash amount.", "#EF4444");
                return;
            }

            var paymentLine = new PaymentLine
            {
                PaymentType = "Cash",
                Amount = appliedAmount,
                TenderedAmount = tenderedAmount,
                ChangeAmount = changeAmount,
                PaymentDate = DateTime.Now,
                CreatedAt = DateTime.Now
            };

            PaymentLines.Add(paymentLine);
            SelectedPaymentLine = paymentLine;
            TerminalInput = string.Empty;

            RecalculatePaymentTotals();

            if (BalanceDue <= 0m)
            {
                _ = ShowNotificationAsync(
                    $"Cash payment added. Change Rs. {BalanceReturned:N2}. Press Enter to complete sale.",
                    "#10B981");
            }
            else
            {
                _ = ShowNotificationAsync(
                    $"Cash payment added. Balance due Rs. {BalanceDue:N2}.",
                    "#D97706");
            }
        }

        public void AddConfirmedCardPayment(
            string cardType,
            decimal amount,
            string lastSixDigits,
            string referenceNo)
        {
            if (!EnsurePaymentModeReady())
                return;

            amount = Math.Round(amount, 2);

            if (amount <= 0m)
            {
                _ = ShowNotificationAsync("Invalid card payment amount.", "#EF4444");
                return;
            }

            if (amount > BalanceDue)
            {
                _ = ShowNotificationAsync("Card amount cannot be greater than balance due.", "#EF4444");
                return;
            }

            string safeCardType = string.IsNullOrWhiteSpace(cardType)
                ? "Card"
                : cardType.Trim();

            string safeLastSix = string.IsNullOrWhiteSpace(lastSixDigits)
                ? string.Empty
                : lastSixDigits.Trim();

            string safeReference = string.IsNullOrWhiteSpace(referenceNo)
                ? safeLastSix
                : referenceNo.Trim();

            var paymentLine = new PaymentLine
            {
                PaymentType = "Card",
                CardType = safeCardType,
                BankOrCardType = safeCardType,
                ReferenceNo = safeReference,
                Amount = amount,
                TenderedAmount = amount,
                ChangeAmount = 0m,
                PaymentDate = DateTime.Now,
                CreatedAt = DateTime.Now
            };

            PaymentLines.Add(paymentLine);
            SelectedPaymentLine = paymentLine;
            TerminalInput = string.Empty;

            RecalculatePaymentTotals();

            if (BalanceDue <= 0m)
            {
                _ = ShowNotificationAsync(
                    $"{safeCardType} payment added. Press Enter to complete sale.",
                    "#10B981");
            }
            else
            {
                _ = ShowNotificationAsync(
                    $"{safeCardType} payment added. Balance due Rs. {BalanceDue:N2}.",
                    "#D97706");
            }
        }

        public void AddConfirmedChequePayment(
            decimal amount,
            string chequeNo,
            string bankOrBranch,
            DateTime chequeDate)
        {
            if (!EnsurePaymentModeReady())
                return;

            amount = Math.Round(amount, 2);

            if (amount <= 0m)
            {
                _ = ShowNotificationAsync("Invalid cheque payment amount.", "#EF4444");
                return;
            }

            if (amount > BalanceDue)
            {
                _ = ShowNotificationAsync("Cheque amount cannot be greater than balance due.", "#EF4444");
                return;
            }

            string safeChequeNo = string.IsNullOrWhiteSpace(chequeNo)
                ? string.Empty
                : chequeNo.Trim();

            string safeBankOrBranch = string.IsNullOrWhiteSpace(bankOrBranch)
                ? string.Empty
                : bankOrBranch.Trim();

            if (string.IsNullOrWhiteSpace(safeChequeNo))
            {
                _ = ShowNotificationAsync("Cheque number is required.", "#EF4444");
                return;
            }

            if (string.IsNullOrWhiteSpace(safeBankOrBranch))
            {
                _ = ShowNotificationAsync("Bank or branch is required.", "#EF4444");
                return;
            }

            var paymentLine = new PaymentLine
            {
                PaymentType = "Cheque",
                BankOrCardType = safeBankOrBranch,
                ReferenceNo = safeChequeNo,
                Amount = amount,
                TenderedAmount = amount,
                ChangeAmount = 0m,
                PaymentDate = chequeDate,
                CreatedAt = DateTime.Now
            };

            PaymentLines.Add(paymentLine);
            SelectedPaymentLine = paymentLine;
            TerminalInput = string.Empty;

            RecalculatePaymentTotals();

            if (BalanceDue <= 0m)
            {
                _ = ShowNotificationAsync("Cheque payment added. Press Enter to complete sale.", "#10B981");
            }
            else
            {
                _ = ShowNotificationAsync(
                    $"Cheque payment added. Balance due Rs. {BalanceDue:N2}.",
                    "#D97706");
            }
        }

        public void AddConfirmedGiftVoucherPayment(
            int giftVoucherId,
            string voucherNo,
            string voucherBarcode,
            decimal voucherAmount,
            decimal amountToApply,
            decimal forfeitedAmount)
        {
            if (!EnsurePaymentModeReady())
                return;

            if (Cart.Any(c => c.IsGiftVoucherSale))
            {
                _ = ShowNotificationAsync(
                    "Gift voucher cannot be used to buy another gift voucher.",
                    "#EF4444");

                return;
            }

            if (giftVoucherId <= 0)
            {
                _ = ShowNotificationAsync("Invalid gift voucher.", "#EF4444");
                return;
            }

            amountToApply = Math.Round(amountToApply, 2);
            voucherAmount = Math.Round(voucherAmount, 2);
            forfeitedAmount = Math.Round(forfeitedAmount, 2);

            if (amountToApply <= 0m)
            {
                _ = ShowNotificationAsync("Gift voucher payment amount is invalid.", "#EF4444");
                return;
            }

            if (amountToApply > BalanceDue)
            {
                _ = ShowNotificationAsync("Gift voucher amount cannot be greater than balance due.", "#EF4444");
                return;
            }

            if (voucherAmount <= 0m)
            {
                _ = ShowNotificationAsync("Gift voucher value is invalid.", "#EF4444");
                return;
            }

            if (forfeitedAmount < 0m)
                forfeitedAmount = 0m;

            if (Math.Round(amountToApply + forfeitedAmount, 2) > voucherAmount)
            {
                _ = ShowNotificationAsync(
                    "Gift voucher applied plus forfeited amount cannot exceed voucher value.",
                    "#EF4444");

                return;
            }

            string safeVoucherNo = string.IsNullOrWhiteSpace(voucherNo)
                ? $"GV-{giftVoucherId}"
                : voucherNo.Trim();

            string safeBarcode = string.IsNullOrWhiteSpace(voucherBarcode)
                ? safeVoucherNo
                : voucherBarcode.Trim();

            bool alreadyUsed = PaymentLines.Any(p =>
                p.IsGiftVoucher &&
                p.GiftVoucherId == giftVoucherId);

            if (alreadyUsed)
            {
                _ = ShowNotificationAsync("This gift voucher is already added to the payment.", "#F59E0B");
                return;
            }

            var paymentLine = new PaymentLine
            {
                PaymentType = "GiftVoucher",
                BankOrCardType = "Gift Voucher",
                ReferenceNo = safeVoucherNo,

                Amount = amountToApply,
                TenderedAmount = amountToApply,
                ChangeAmount = 0m,

                GiftVoucherId = giftVoucherId,
                GiftVoucherNo = safeVoucherNo,
                GiftVoucherBarcode = safeBarcode,
                GiftVoucherAmount = voucherAmount,
                GiftVoucherForfeitedAmount = forfeitedAmount,

                PaymentDate = DateTime.Now,
                CreatedAt = DateTime.Now
            };

            PaymentLines.Add(paymentLine);
            SelectedPaymentLine = paymentLine;
            TerminalInput = string.Empty;

            RecalculatePaymentTotals();

            if (forfeitedAmount > 0m)
            {
                _ = ShowNotificationAsync(
                    $"Gift voucher added. Applied Rs. {amountToApply:N2}. Forfeited Rs. {forfeitedAmount:N2}.",
                    "#10B981");
                return;
            }

            if (BalanceDue <= 0m)
            {
                _ = ShowNotificationAsync(
                    "Gift voucher payment added. Press Enter to complete sale.",
                    "#10B981");
            }
            else
            {
                _ = ShowNotificationAsync(
                    $"Gift voucher payment added. Balance due Rs. {BalanceDue:N2}.",
                    "#D97706");
            }
        }

        public void RemoveSelectedPaymentLine()
        {
            if (SelectedPaymentLine == null)
            {
                _ = ShowNotificationAsync("Select a payment row to remove.", "#F59E0B");
                return;
            }

            PaymentLines.Remove(SelectedPaymentLine);
            SelectedPaymentLine = PaymentLines.LastOrDefault();

            RecalculatePaymentTotals();

            _ = ShowNotificationAsync("Payment row removed.", "#F59E0B");
        }

        public async Task ConfirmSaleFromPaymentModeAsync()
        {
            if (!IsPaymentModeActive)
            {
                EnterPaymentMode();
                return;
            }

            if (!PaymentLines.Any())
            {
                _ = ShowNotificationAsync("No payment entered.", "#EF4444");
                return;
            }

            if (BalanceDue > 0m)
            {
                _ = ShowNotificationAsync($"Balance due: Rs. {BalanceDue:N2}", "#EF4444");
                return;
            }

            await FinalizeCheckoutAsync();
        }

        private bool EnsurePaymentModeReady()
        {
            if (!IsPaymentModeActive)
                EnterPaymentMode();

            return IsPaymentModeActive &&
                   Cart.Any() &&
                   BalanceDue > 0m;
        }

        private void RecalculatePaymentTotals()
        {
            decimal paid = Math.Round(PaymentLines.Sum(p => p.Amount), 2);
            decimal cashTendered = Math.Round(PaymentLines.Where(p => p.IsCash).Sum(p => p.TenderedAmount), 2);
            decimal change = Math.Round(PaymentLines.Where(p => p.IsCash).Sum(p => p.ChangeAmount), 2);

            PaidTotal = paid;
            CashTenderedTotal = cashTendered;
            BalanceReturned = change;

            decimal due = Math.Round(NetValue - paid, 2);

            if (due < 0m)
                due = 0m;

            BalanceDue = due;

            if (!IsPaymentModeActive)
            {
                PaymentStatusText = "Sale mode active.";
                PaymentStatusColor = "#003366";
            }
            else if (BalanceDue > 0m)
            {
                PaymentStatusText = $"Balance due: Rs. {BalanceDue:N2}";
                PaymentStatusColor = "#D97706";
            }
            else
            {
                PaymentStatusText = $"Fully paid. Change: Rs. {BalanceReturned:N2}. Press Enter to confirm.";
                PaymentStatusColor = "#10B981";
            }

            OnPropertyChanged(nameof(CanConfirmPaymentSale));
        }

        private void RenumberPaymentLines()
        {
            int lineNo = 1;

            foreach (var line in PaymentLines)
            {
                line.LineNo = lineNo;
                lineNo++;
            }
        }

        // =========================================================
        // CUSTOMER
        // =========================================================

        public void AttachCustomer(CustomerSearchDto customer)
        {
            if (customer == null)
                return;

            if (IsPaymentModeActive)
            {
                _ = ShowNotificationAsync("Cancel payment mode before changing customer.", "#F59E0B");
                return;
            }

            if (!customer.IsActive)
            {
                _ = ShowNotificationAsync("Customer account is inactive.", "#EF4444");
                return;
            }

            ActiveB2BCustomer = customer;

            CustomerName = string.IsNullOrWhiteSpace(customer.DisplayName)
                ? "Walk-In"
                : customer.DisplayName;

            IsWholesaleMode = customer.IsWholesale;

            ApplyCustomerPricingToCart(customer);
            RecalculateTotals();

            string message;

            if (customer.IsWholesale)
            {
                message = customer.IsDiscountEligible
                    ? $"WHOLESALE CUSTOMER LINKED: {CustomerName} | Discount Enabled"
                    : $"WHOLESALE CUSTOMER LINKED: {CustomerName}";
            }
            else
            {
                message = customer.IsDiscountEligible
                    ? $"LOYALTY CUSTOMER LINKED: {CustomerName}"
                    : $"CUSTOMER LINKED: {CustomerName}";
            }

            _ = ShowNotificationAsync(message, customer.IsWholesale ? "#3B82F6" : "#10B981");
        }

        public void AttachB2BCustomer(CustomerSearchDto customer)
        {
            AttachCustomer(customer);
        }

        public void AttachLoyaltyCustomer(CustomerSearchDto customer)
        {
            AttachCustomer(customer);
        }

        public void DetachCustomer()
        {
            if (IsPaymentModeActive)
            {
                _ = ShowNotificationAsync("Cancel payment mode before changing customer.", "#F59E0B");
                return;
            }

            ActiveB2BCustomer = null;
            CustomerName = "Walk-In";
            IsWholesaleMode = false;

            foreach (var item in Cart)
            {
                if (!item.IsGiftVoucherSale &&
                    !item.IsFreeItem &&
                    !item.IsManualDiscount &&
                    !item.IsPriceOverridden &&
                    item.RetailPrice > 0m)
                {
                    item.UnitPrice = item.RetailPrice;
                    item.OriginalUnitPrice = item.RetailPrice;
                }
            }

            RecalculateTotals();

            _ = ShowNotificationAsync("Customer removed. Walk-in sale active.", "#64748B");
        }

        private void ApplyCustomerPricingToCart(CustomerSearchDto customer)
        {
            foreach (var item in Cart)
            {
                if (item.IsGiftVoucherSale ||
                    item.IsFreeItem ||
                    item.IsManualDiscount ||
                    item.IsPriceOverridden)
                {
                    continue;
                }

                if (customer.IsWholesale && item.WholesalePrice > 0m)
                {
                    item.UnitPrice = item.WholesalePrice;
                    item.OriginalUnitPrice = item.WholesalePrice;
                }
                else if (item.RetailPrice > 0m)
                {
                    item.UnitPrice = item.RetailPrice;
                    item.OriginalUnitPrice = item.RetailPrice;
                }
            }
        }

        // =========================================================
        // SECURITY
        // =========================================================

        public void SetManagerMode(bool activate)
        {
            IsManagerModeActive = activate;
            SecurityStatusMode = activate ? "MANAGER MODE ACTIVE" : "CASHIER MODE";
        }

        public bool VerifyActionPermission()
        {
            return IsManagerModeActive;
        }

        // =========================================================
        // SHIFT COMMANDS
        // =========================================================

        [RelayCommand]
        public async Task AddFloatAsync()
        {
            if (_currentShiftId == 0)
            {
                _ = ShowNotificationAsync("No active shift found.", "#EF4444");
                return;
            }

            if (!VerifyActionPermission())
            {
                var authVM = App.Services!.GetRequiredService<ManagerAuthViewModel>();
                var authDialog = new POS.Cashier.UI.Dialogs.ManagerAuthDialogView(authVM);

                if (authDialog.ShowDialog() != true)
                    return;
            }

            var floatVM = App.Services!.GetRequiredService<FloatCashViewModel>();
            floatVM.Initialize(_currentShiftId);

            var floatDialog = new POS.Cashier.UI.Dialogs.FloatCashDialog(floatVM);
            floatDialog.ShowDialog();

            await Task.CompletedTask;
        }

        [RelayCommand]
        public async Task PrintXReportAsync()
        {
            var summary = await _tillRepository.GetShiftSummaryAsync(_currentShiftId);

            if (summary != null)
            {
                MessageBox.Show(
                    $"Expected Cash in Drawer: Rs. {summary.ExpectedCash:N2}\n\nPrinting X-Report...",
                    "X-Report",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        public Task ProcessZReportAsync()
        {
            return Task.CompletedTask;
        }

        // =========================================================
        // SCANNER / SEEK ADD-TO-CART ENGINE
        // =========================================================

        private async Task AddToCartFromMessageAsync(AddToCartRequest request)
        {
            if (request == null)
                return;

            if (IsPaymentModeActive)
            {
                _ = ShowNotificationAsync("Cancel payment mode before adding more items.", "#F59E0B");
                return;
            }

            if (request.ItemVariantId > 0)
            {
                await AddVariantToCartAsync(request.ItemVariantId, request.Quantity);
                return;
            }

            string fallbackCode = !string.IsNullOrWhiteSpace(request.Barcode)
                ? request.Barcode
                : request.SkuCode;

            await ProcessBarcodeAsync(fallbackCode);
        }

        public async Task ProcessBarcodeAsync(string barcode)
        {
            if (IsPaymentModeActive)
            {
                _ = ShowNotificationAsync("Cancel payment mode before scanning items.", "#F59E0B");
                return;
            }

            string term = (barcode ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(term))
                return;

            if (_currentShiftId == 0)
            {
                _ = ShowNotificationAsync("Action blocked: No active shift found.", "#EF4444");
                return;
            }

            try
            {
                var item = await _itemRepository.GetSellableItemByBarcodeOrSkuAsync(term);

                if (item == null)
                {
                    _ = ShowNotificationAsync($"Unrecognized item: {term}", "#F59E0B");
                    return;
                }

                await AddVariantToCartAsync(item.VariantId, 1m);
            }
            catch (Exception ex)
            {
                _ = ShowNotificationAsync($"Database Error: {ex.Message}", "#EF4444");
            }
        }

        public async Task AddVariantToCartAsync(int itemVariantId, decimal quantity = 1m)
        {
            if (IsPaymentModeActive)
            {
                _ = ShowNotificationAsync("Cancel payment mode before adding more items.", "#F59E0B");
                return;
            }

            if (itemVariantId <= 0)
                return;

            if (quantity <= 0m)
                quantity = 1m;

            if (_currentShiftId == 0)
            {
                _ = ShowNotificationAsync("Action blocked: No active shift found.", "#EF4444");
                return;
            }

            try
            {
                var item = await _itemRepository.GetSellableItemByVariantIdAsync(itemVariantId);

                if (item == null)
                {
                    _ = ShowNotificationAsync("Selected item is locked or not sellable.", "#EF4444");
                    return;
                }

                var batches = await _itemRepository.GetSellableBatchesByVariantIdAsync(item.VariantId);

                if (!batches.Any())
                {
                    _ = ShowNotificationAsync($"No sellable batch stock: {item.DisplayDescription}", "#F59E0B");
                    return;
                }

                CashierBatchDto? selectedBatch;

                if (batches.Count == 1)
                {
                    selectedBatch = batches[0];

                    if (selectedBatch.AvailableQty < quantity)
                    {
                        _ = ShowNotificationAsync(
                            $"Only {selectedBatch.AvailableQty:N3} available in selected batch.",
                            "#F59E0B");
                        return;
                    }
                }
                else
                {
                    selectedBatch = ShowBatchSelectionDialog(item.DisplayDescription, batches, quantity);

                    if (selectedBatch == null)
                    {
                        _ = ShowNotificationAsync("Batch selection cancelled.", "#F59E0B");
                        return;
                    }
                }

                AddSelectedBatchToCart(item, selectedBatch, quantity);
            }
            catch (Exception ex)
            {
                _ = ShowNotificationAsync($"Add item failed: {ex.Message}", "#EF4444");
            }
        }

        private CashierBatchDto? ShowBatchSelectionDialog(
            string itemDescription,
            List<CashierBatchDto> batches,
            decimal requestedQty)
        {
            var viewModel = new BatchSelectionViewModel(itemDescription, batches, requestedQty);
            var dialog = new POS.Cashier.UI.Dialogs.BatchSelectionDialog(viewModel);

            Window? owner = Application.Current.Windows
                .OfType<Window>()
                .FirstOrDefault(w => w.IsActive);

            if (owner != null)
                dialog.Owner = owner;

            bool? result = dialog.ShowDialog();

            return result == true
                ? dialog.SelectedBatch
                : null;
        }

        private void AddSelectedBatchToCart(
            CashierSellableItemDto item,
            CashierBatchDto selectedBatch,
            decimal quantity)
        {
            if (selectedBatch.AvailableQty <= 0m)
            {
                _ = ShowNotificationAsync("Selected batch has no available stock.", "#F59E0B");
                return;
            }

            if (quantity > selectedBatch.AvailableQty)
            {
                _ = ShowNotificationAsync(
                    $"Only {selectedBatch.AvailableQty:N3} available in selected batch.",
                    "#F59E0B");
                return;
            }

            var existingItem = Cart.FirstOrDefault(c =>
                !c.IsGiftVoucherSale &&
                !c.IsFreeItem &&
                !c.IsManualDiscount &&
                !c.IsPriceOverridden &&
                c.DiscountAmount <= 0m &&
                c.ItemVariantId == item.VariantId &&
                c.ItemBatchId == selectedBatch.ItemBatchId);

            if (existingItem != null)
            {
                if (existingItem.Quantity + quantity > selectedBatch.AvailableQty)
                {
                    _ = ShowNotificationAsync(
                        $"Only {selectedBatch.AvailableQty:N3} available in selected batch.",
                        "#F59E0B");
                    return;
                }

                existingItem.Quantity += quantity;
                existingItem.AvailableBatchStock = selectedBatch.AvailableQty;
                SelectedCartItem = existingItem;
                RecalculateTotals();

                _ = ShowNotificationAsync($"{item.DisplayDescription} quantity updated.", "#10B981");
                return;
            }

            decimal sellingPrice = IsWholesaleMode && item.WholesalePrice > 0m
                ? item.WholesalePrice
                : item.RetailPrice;

            if (sellingPrice <= 0m && selectedBatch.RetailPrice > 0m)
                sellingPrice = selectedBatch.RetailPrice;

            var cartItem = new CartItem
            {
                ItemVariantId = item.VariantId,
                ItemBatchId = selectedBatch.ItemBatchId,

                ItemCode = item.ItemCode,
                SkuCode = item.SkuCode,
                Barcode = string.IsNullOrWhiteSpace(item.Barcode) ? item.SkuCode : item.Barcode,

                Description = item.DisplayDescription,
                VariantDescription = item.VariantDescription,
                Uom = item.Uom,

                BatchNo = selectedBatch.BatchNo,
                ExpiryDate = selectedBatch.ExpiryDate,
                ReceivedDate = selectedBatch.ReceivedDate,

                CostPrice = selectedBatch.CostPrice,
                RetailPrice = item.RetailPrice,
                WholesalePrice = item.WholesalePrice,
                MinimumPrice = item.MinimumPrice,
                MaximumPrice = item.MaximumPrice,

                UnitPrice = sellingPrice,
                OriginalUnitPrice = sellingPrice,

                Quantity = quantity,
                DiscountPercentage = 0m,
                ManualDiscountAmount = 0m,
                DiscountMode = "None",
                IsManualDiscount = false,

                IsPriceOverridden = false,
                PriceOverrideAmount = 0m,
                PriceOverrideApprovedBy = string.Empty,
                PriceOverrideApprovedAt = null,

                AvailableBatchStock = selectedBatch.AvailableQty
            };

            Cart.Add(cartItem);
            SelectedCartItem = cartItem;

            RecalculateTotals();

            string batchText = string.IsNullOrWhiteSpace(selectedBatch.BatchNo)
                ? "Batch selected"
                : $"Batch {selectedBatch.BatchNo}";

            _ = ShowNotificationAsync($"Added: {item.DisplayDescription} / {batchText}", "#10B981");
        }

        public async Task<bool> AddSpecificItemToCartAsync(string barcode, int quantity)
        {
            if (string.IsNullOrWhiteSpace(barcode) || quantity <= 0)
                return false;

            if (_currentShiftId == 0)
                return false;

            if (IsPaymentModeActive)
                return false;

            try
            {
                var item = await _itemRepository.GetSellableItemByBarcodeOrSkuAsync(barcode.Trim());

                if (item == null)
                    return false;

                decimal beforeQty = Cart.Sum(c => c.Quantity);

                await AddVariantToCartAsync(item.VariantId, quantity);

                decimal afterQty = Cart.Sum(c => c.Quantity);

                return afterQty > beforeQty;
            }
            catch
            {
                return false;
            }
        }

        // =========================================================
        // GIFT VOUCHER SALE LINE
        // =========================================================

        public void AddGiftVoucherSaleLine(
            int giftVoucherId,
            string voucherNo,
            string voucherBarcode,
            decimal voucherAmount,
            string displayDescription)
        {
            if (IsPaymentModeActive)
            {
                _ = ShowNotificationAsync("Cancel payment mode before selling a gift voucher.", "#F59E0B");
                return;
            }

            if (_currentShiftId == 0)
            {
                _ = ShowNotificationAsync("Action blocked: No active shift found.", "#EF4444");
                return;
            }

            if (giftVoucherId <= 0)
            {
                _ = ShowNotificationAsync("Invalid gift voucher.", "#EF4444");
                return;
            }

            voucherAmount = Math.Round(voucherAmount, 2);

            if (voucherAmount <= 0m)
            {
                _ = ShowNotificationAsync("Gift voucher amount is invalid.", "#EF4444");
                return;
            }

            string safeVoucherNo = string.IsNullOrWhiteSpace(voucherNo)
                ? $"GV-{giftVoucherId}"
                : voucherNo.Trim();

            string safeBarcode = string.IsNullOrWhiteSpace(voucherBarcode)
                ? safeVoucherNo
                : voucherBarcode.Trim();

            bool alreadyInCart = Cart.Any(c =>
                c.IsGiftVoucherSale &&
                c.GiftVoucherId == giftVoucherId);

            if (alreadyInCart)
            {
                _ = ShowNotificationAsync("This gift voucher is already added to the sale.", "#F59E0B");
                return;
            }

            string description = string.IsNullOrWhiteSpace(displayDescription)
                ? $"Gift Voucher Rs. {voucherAmount:N2}"
                : displayDescription.Trim();

            var cartItem = new CartItem
            {
                IsGiftVoucherSale = true,
                GiftVoucherId = giftVoucherId,
                GiftVoucherNo = safeVoucherNo,
                GiftVoucherBarcode = safeBarcode,

                ItemVariantId = 0,
                ItemBatchId = 0,

                ItemCode = "GIFT-VOUCHER",
                SkuCode = "GV-SALE",
                Barcode = safeBarcode,

                Description = $"{description} / {safeVoucherNo}",
                VariantDescription = "Gift Voucher",
                Uom = "VOU",

                BatchNo = string.Empty,
                ExpiryDate = null,
                ReceivedDate = null,

                CostPrice = 0m,
                RetailPrice = voucherAmount,
                WholesalePrice = voucherAmount,
                MinimumPrice = voucherAmount,
                MaximumPrice = voucherAmount,

                UnitPrice = voucherAmount,
                OriginalUnitPrice = voucherAmount,

                Quantity = 1m,
                DiscountPercentage = 0m,
                ManualDiscountAmount = 0m,
                DiscountMode = "None",
                IsManualDiscount = false,

                IsPriceOverridden = false,
                PriceOverrideAmount = 0m,
                PriceOverrideApprovedBy = string.Empty,
                PriceOverrideApprovedAt = null,

                AvailableBatchStock = 1m
            };

            Cart.Add(cartItem);
            SelectedCartItem = cartItem;

            RecalculateTotals();

            _ = ShowNotificationAsync(
                $"Gift voucher added to sale: {safeVoucherNo} / Rs. {voucherAmount:N2}",
                "#10B981");
        }

        // =========================================================
        // FREE ISSUE / CART ACTIONS
        // =========================================================

        public void ApplyFreeItemLogic(CartItem cartItem, FreeItemApplyResult result)
        {
            if (cartItem == null)
            {
                _ = ShowNotificationAsync("Please select an item before applying free issue.", "#F59E0B");
                return;
            }

            if (result == null)
            {
                _ = ShowNotificationAsync("Free issue result is missing.", "#EF4444");
                return;
            }

            if (IsPaymentModeActive)
            {
                _ = ShowNotificationAsync("Cancel payment mode before applying free issue.", "#F59E0B");
                return;
            }

            if (cartItem.IsGiftVoucherSale)
            {
                _ = ShowNotificationAsync("Gift voucher sale line cannot be made free.", "#EF4444");
                return;
            }

            if (cartItem.Quantity <= 0m)
            {
                _ = ShowNotificationAsync("Free issue quantity must be greater than zero.", "#EF4444");
                return;
            }

            if (result.FreeIssueRuleId <= 0)
            {
                _ = ShowNotificationAsync("A valid BackOffice free issue rule is required.", "#EF4444");
                return;
            }

            string freeIssueType = (result.FreeIssueType ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(freeIssueType))
                freeIssueType = "ShopCost";

            bool isSupplierRecoverable =
                freeIssueType.Equals("SupplierClaim", StringComparison.OrdinalIgnoreCase);

            if (isSupplierRecoverable && (!result.SupplierId.HasValue || result.SupplierId.Value <= 0))
            {
                _ = ShowNotificationAsync("Supplier is required for supplier recoverable free issue.", "#EF4444");
                return;
            }

            decimal originalUnitPrice = result.OriginalUnitPrice > 0m
                ? Math.Round(result.OriginalUnitPrice, 2)
                : Math.Round(cartItem.UnitPrice > 0m ? cartItem.UnitPrice : cartItem.RetailPrice, 2);

            decimal costValue = result.FreeIssueCostValue > 0m
                ? Math.Round(result.FreeIssueCostValue, 2)
                : Math.Round(cartItem.CostPrice * cartItem.Quantity, 2);

            decimal sellingValue = result.FreeIssueSellingValue > 0m
                ? Math.Round(result.FreeIssueSellingValue, 2)
                : Math.Round(originalUnitPrice * cartItem.Quantity, 2);

            decimal claimValue = isSupplierRecoverable
                ? Math.Round(result.ClaimValue, 2)
                : 0m;

            cartItem.OriginalUnitPrice = originalUnitPrice;

            cartItem.UnitPrice = 0m;
            cartItem.DiscountPercentage = 0m;
            cartItem.ManualDiscountAmount = 0m;
            cartItem.DiscountMode = "None";
            cartItem.IsManualDiscount = false;

            cartItem.IsPriceOverridden = false;
            cartItem.PriceOverrideAmount = 0m;
            cartItem.PriceOverrideApprovedBy = string.Empty;
            cartItem.PriceOverrideApprovedAt = null;

            cartItem.IsFreeItem = true;
            cartItem.FreeIssueRuleId = result.FreeIssueRuleId;
            cartItem.FreeIssueRuleName = result.FreeIssueRuleName ?? string.Empty;
            cartItem.FreeIssueType = isSupplierRecoverable ? "SupplierClaim" : "ShopCost";
            cartItem.FreeReasonCode = result.FreeReasonCode ?? string.Empty;
            cartItem.FreeReasonText = result.FreeReasonText ?? string.Empty;
            cartItem.FreeApprovedBy = result.ApprovedBy ?? string.Empty;
            cartItem.FreeApprovedAt = result.ApprovedAt;

            cartItem.FreeIssueCostValue = costValue;
            cartItem.FreeIssueSellingValue = sellingValue;

            cartItem.IsSupplierRecoverable = isSupplierRecoverable;
            cartItem.SupplierId = result.SupplierId ?? 0;
            cartItem.SupplierName = result.SupplierName ?? string.Empty;
            cartItem.SupplierPromotionReference = result.SupplierPromotionReference ?? string.Empty;
            cartItem.SupplierClaimReferenceNo = result.SupplierClaimReferenceNo ?? string.Empty;
            cartItem.SupplierClaimStatus = isSupplierRecoverable ? "Pending" : string.Empty;
            cartItem.SupplierClaimValue = claimValue;

            RecalculateTotals();

            string message = isSupplierRecoverable
                ? $"Free item applied as supplier recoverable claim: Rs. {claimValue:N2}"
                : $"Free item applied as shop cost: Rs. {costValue:N2}";

            _ = ShowNotificationAsync(message, "#10B981");
        }

        public void RemoveSelectedItem()
        {
            if (SelectedCartItem == null)
                return;

            RemoveItem(SelectedCartItem);
        }

        [RelayCommand]
        private void RemoveItem(CartItem? item)
        {
            if (item == null)
                return;

            if (IsPaymentModeActive)
            {
                _ = ShowNotificationAsync("Cancel payment mode before removing items.", "#F59E0B");
                return;
            }

            Cart.Remove(item);
            SelectedCartItem = Cart.LastOrDefault();

            RecalculateTotals();

            _ = ShowNotificationAsync("Item removed.", "#F59E0B");
        }

        [RelayCommand]
        public void ClearCart()
        {
            Cart.Clear();
            PaymentLines.Clear();

            SelectedCartItem = null;
            SelectedPaymentLine = null;

            IsPaymentModeActive = false;
            TerminalInputMode = "SCAN / QTY";
            TerminalInput = string.Empty;

            ActiveB2BCustomer = null;
            CustomerName = "Walk-In";
            IsWholesaleMode = false;

            InvoiceNo = "PENDING...";

            RecalculateTotals();
            RecalculatePaymentTotals();

            PaymentStatusText = "Sale mode active.";
            PaymentStatusColor = "#003366";
        }

        // =========================================================
        // TOTALS
        // =========================================================

        public void RecalculateTotals()
        {
            if (!Cart.Any())
            {
                GrossValue = 0m;
                NetValue = 0m;
                TotalDiscount = 0m;
                TotalItems = 0;
                TotalPieces = 0m;

                if (IsPaymentModeActive)
                    RecalculatePaymentTotals();

                return;
            }

            GrossValue = Math.Round(Cart.Sum(c => c.GrossAmount), 2);
            NetValue = Math.Round(Cart.Sum(c => c.LineAmount), 2);
            TotalDiscount = Math.Round(Cart.Sum(c => c.DiscountAmount), 2);

            TotalItems = Cart.Count;
            TotalPieces = Math.Round(Cart.Sum(c => c.Quantity), 3);

            if (IsPaymentModeActive)
                RecalculatePaymentTotals();
        }

        // =========================================================
        // FINAL CHECKOUT
        // =========================================================

        public async Task<bool> FinalizeCheckoutAsync()
        {
            if (!Cart.Any())
                return false;

            if (_currentShiftId == 0)
            {
                _ = ShowNotificationAsync("No active shift found.", "#EF4444");
                return false;
            }

            if (!PaymentLines.Any())
            {
                _ = ShowNotificationAsync("No payment entered.", "#EF4444");
                return false;
            }

            if (BalanceDue > 0m)
            {
                _ = ShowNotificationAsync($"Balance due: Rs. {BalanceDue:N2}", "#EF4444");
                return false;
            }

            if (Cart.Any(c => !c.IsGiftVoucherSale && c.ItemBatchId <= 0))
            {
                _ = ShowNotificationAsync("One or more cart lines has no selected batch.", "#EF4444");
                return false;
            }

            if (Cart.Any(c => c.IsGiftVoucherSale && c.GiftVoucherId <= 0))
            {
                _ = ShowNotificationAsync("One or more gift voucher sale lines has no voucher reference.", "#EF4444");
                return false;
            }

            if (Cart.Any(c => c.IsGiftVoucherSale && c.Quantity != 1m))
            {
                _ = ShowNotificationAsync("Gift voucher sale quantity must be 1.", "#EF4444");
                return false;
            }

            bool sellingGiftVoucher = Cart.Any(c => c.IsGiftVoucherSale);
            bool payingByGiftVoucher = PaymentLines.Any(p => p.IsGiftVoucher);

            if (sellingGiftVoucher && payingByGiftVoucher)
            {
                _ = ShowNotificationAsync("Gift voucher cannot be used to buy another gift voucher.", "#EF4444");
                return false;
            }

            var invalidFreeLine = Cart.FirstOrDefault(c => c.IsFreeItem && c.FreeIssueRuleId <= 0);

            if (invalidFreeLine != null)
            {
                _ = ShowNotificationAsync($"Free issue rule missing: {invalidFreeLine.Description}", "#EF4444");
                return false;
            }

            var invalidSupplierClaimLine = Cart.FirstOrDefault(c =>
                c.IsFreeItem &&
                c.IsSupplierRecoverable &&
                c.SupplierId <= 0);

            if (invalidSupplierClaimLine != null)
            {
                _ = ShowNotificationAsync($"Supplier missing for free issue: {invalidSupplierClaimLine.Description}", "#EF4444");
                return false;
            }

            var belowMinimumLine = Cart.FirstOrDefault(c => !c.IsGiftVoucherSale && c.IsBelowMinimumPrice);

            if (belowMinimumLine != null && !IsManagerModeActive)
            {
                _ = ShowNotificationAsync(
                    $"Price below minimum: {belowMinimumLine.Description}",
                    "#EF4444");

                return false;
            }

            try
            {
                string paymentMethod = PaymentLines.Count == 1
                    ? PaymentLines.First().DisplayPaymentType
                    : "Split";

                CustomerSearchDto? activeCustomer = ActiveB2BCustomer;

                string customerName = activeCustomer == null
                    ? "Walk-In"
                    : activeCustomer.DisplayName;

                string customerCompanyName = activeCustomer?.CompanyName ?? string.Empty;

                string nicOrBr = string.Empty;

                if (activeCustomer != null)
                {
                    nicOrBr = activeCustomer.IsWholesale
                        ? activeCustomer.BusinessRegistrationNumber
                        : activeCustomer.NicNumber;

                    if (activeCustomer.IsWholesale && string.IsNullOrWhiteSpace(nicOrBr))
                        nicOrBr = activeCustomer.VatRegistrationNumber;
                }

                var header = new SalesHeader
                {
                    ShiftSessionId = _currentShiftId,
                    CashierName = CashierName,
                    TerminalNo = TerminalNo,

                    CustomerMasterId = activeCustomer?.Id,
                    CustomerCode = activeCustomer?.CustomerCode ?? string.Empty,
                    CustomerName = customerName,
                    CustomerCompanyName = customerCompanyName,
                    CustomerPhone = activeCustomer?.Phone ?? string.Empty,
                    CustomerType = activeCustomer == null ? "Walk-In" : activeCustomer.DisplayCustomerType,
                    CustomerNicOrBrNumber = nicOrBr,
                    CustomerIsDiscountEligible = activeCustomer?.IsDiscountEligible ?? false,
                    CustomerIsCreditEnabled = activeCustomer?.IsCreditEnabled ?? false,
                    CustomerCreditStatus = activeCustomer?.CreditStatus ?? "None",
                    IsWholesaleSale = activeCustomer?.IsWholesale ?? false,

                    GrossTotal = GrossValue,
                    TotalDiscount = TotalDiscount,
                    NetTotal = NetValue,
                    PaymentMethod = paymentMethod,
                    AmountTendered = CashTenderedTotal,
                    BalanceReturned = BalanceReturned
                };

                var lines = Cart.Select(c => new SalesLine
                {
                    ItemVariantId = c.IsGiftVoucherSale ? null : c.ItemVariantId,
                    ItemBatchId = c.IsGiftVoucherSale ? null : c.ItemBatchId,

                    SkuCode = c.IsGiftVoucherSale ? "GV-SALE" : c.SkuCode,
                    Barcode = c.Barcode,
                    ItemDescription = c.Description,
                    BatchNo = c.BatchNo,
                    ExpiryDate = c.ExpiryDate,
                    Uom = c.Uom,

                    Quantity = c.Quantity,
                    UnitPrice = c.UnitPrice,
                    CostPrice = c.CostPrice,
                    GrossAmount = c.GrossAmount,

                    DiscountPercentage = c.IsGiftVoucherSale || c.IsFreeItem ? 0m : c.DiscountPercentage,
                    DiscountAmount = c.IsGiftVoucherSale || c.IsFreeItem ? 0m : c.DiscountAmount,
                    ManualDiscountAmount = c.IsGiftVoucherSale || c.IsFreeItem ? 0m : c.ManualDiscountAmount,
                    DiscountMode = c.IsGiftVoucherSale || c.IsFreeItem ? "None" : c.DiscountMode,
                    IsManualDiscount = !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsManualDiscount,

                    IsRuleDiscount = !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsRuleDiscount,
                    DiscountRuleId = !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsRuleDiscount && c.DiscountRuleId > 0
    ? c.DiscountRuleId
    : null,
                    DiscountRuleName = !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsRuleDiscount
    ? c.DiscountRuleName
    : string.Empty,
                    DiscountReasonId = !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsRuleDiscount && c.DiscountReasonId > 0
    ? c.DiscountReasonId
    : null,
                    DiscountReasonCode = !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsRuleDiscount
    ? c.DiscountReasonCode
    : string.Empty,
                    DiscountReasonName = !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsRuleDiscount
    ? c.DiscountReasonName
    : string.Empty,
                    DiscountRequiresManagerApproval = !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsRuleDiscount && c.DiscountRequiresManagerApproval,
                    DiscountRequiresAdminApproval = !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsRuleDiscount && c.DiscountRequiresAdminApproval,
                    DiscountApprovedBy = !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsRuleDiscount
    ? c.DiscountApprovedBy
    : string.Empty,
                    DiscountApprovedAt = !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsRuleDiscount
    ? c.DiscountApprovedAt
    : null,

                    OriginalUnitPrice = c.OriginalUnitPrice > 0m
    ? c.OriginalUnitPrice
    : c.UnitPrice,

                    IsPriceOverridden = !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsPriceOverridden,
                    PriceOverrideAmount = !c.IsGiftVoucherSale && !c.IsFreeItem ? c.PriceOverrideAmount : 0m,
                    PriceOverrideApprovedBy = !c.IsGiftVoucherSale && !c.IsFreeItem ? c.PriceOverrideApprovedBy : string.Empty,
                    PriceOverrideApprovedAt = !c.IsGiftVoucherSale && !c.IsFreeItem ? c.PriceOverrideApprovedAt : null,

                    LineTotal = c.LineAmount,
                    ProfitAmount = c.ProfitAmount,

                    IsGiftVoucherSale = c.IsGiftVoucherSale,
                    GiftVoucherId = c.IsGiftVoucherSale ? c.GiftVoucherId : null,
                    GiftVoucherNo = c.IsGiftVoucherSale ? c.GiftVoucherNo : string.Empty,
                    GiftVoucherBarcode = c.IsGiftVoucherSale ? c.GiftVoucherBarcode : string.Empty,

                    IsFreeItem = c.IsFreeItem,
                    FreeIssueRuleId = c.IsFreeItem && c.FreeIssueRuleId > 0 ? c.FreeIssueRuleId : null,
                    FreeIssueRuleName = c.IsFreeItem ? c.FreeIssueRuleName : string.Empty,
                    FreeIssueType = c.IsFreeItem ? c.FreeIssueType : string.Empty,
                    FreeReasonCode = c.IsFreeItem ? c.FreeReasonCode : string.Empty,
                    FreeReasonText = c.IsFreeItem ? c.FreeReasonText : string.Empty,
                    FreeApprovedBy = c.IsFreeItem ? c.FreeApprovedBy : string.Empty,
                    FreeApprovedAt = c.IsFreeItem ? c.FreeApprovedAt : null,
                    FreeIssueCostValue = c.IsFreeItem ? c.FreeIssueCostValue : 0m,
                    FreeIssueSellingValue = c.IsFreeItem ? c.FreeIssueSellingValue : 0m,
                    IsSupplierRecoverable = c.IsFreeItem && c.IsSupplierRecoverable,
                    SupplierId = c.IsFreeItem && c.SupplierId > 0 ? c.SupplierId : null,
                    SupplierName = c.IsFreeItem ? c.SupplierName : string.Empty,
                    SupplierPromotionReference = c.IsFreeItem ? c.SupplierPromotionReference : string.Empty,
                    SupplierClaimId = c.IsFreeItem && c.SupplierClaimId > 0 ? c.SupplierClaimId : null,
                    SupplierClaimStatus = c.IsFreeItem ? c.SupplierClaimStatus : string.Empty,
                    SupplierClaimReferenceNo = c.IsFreeItem ? c.SupplierClaimReferenceNo : string.Empty,
                    SupplierClaimValue = c.IsFreeItem ? c.SupplierClaimValue : 0m
                }).ToList();

                var payments = PaymentLines.Select(p => new SalesPayment
                {
                    PaymentType = p.PaymentType,
                    Amount = p.Amount,
                    ReferenceNo = p.ReferenceNo,
                    BankOrCardType = string.IsNullOrWhiteSpace(p.BankOrCardType)
                        ? p.CardType
                        : p.BankOrCardType,
                    PaymentDate = p.PaymentDate ?? p.CreatedAt,

                    GiftVoucherId = p.IsGiftVoucher ? p.GiftVoucherId : null,
                    GiftVoucherNo = p.IsGiftVoucher ? p.GiftVoucherNo : string.Empty,
                    GiftVoucherBarcode = p.IsGiftVoucher ? p.GiftVoucherBarcode : string.Empty,
                    GiftVoucherAmount = p.IsGiftVoucher ? p.GiftVoucherAmount : 0m,
                    GiftVoucherForfeitedAmount = p.IsGiftVoucher ? p.GiftVoucherForfeitedAmount : 0m
                }).ToList();

                var savedReceipt = await _salesRepository.ProcessCheckoutAsync(header, lines, payments);

                InvoiceNo = savedReceipt.InvoiceNo;

                await _printService.PrintReceiptAsync(savedReceipt, _printerName);

                _ = ShowNotificationAsync("Payment successful. Ready for next customer.", "#10B981");

                ClearCart();
                SetManagerMode(false);

                return true;
            }
            catch (Exception ex)
            {
                _ = ShowNotificationAsync($"Transaction failed: {ex.Message}", "#EF4444");
                return false;
            }
        }
        public async Task<bool> PrintCurrentCartQuotationAsync()
        {
            if (IsPaymentModeActive)
            {
                _ = ShowNotificationAsync("Cancel payment mode before printing quotation.", "#F59E0B");
                return false;
            }

            if (!Cart.Any())
            {
                _ = ShowNotificationAsync("Cannot print quotation. Cart is empty.", "#F59E0B");
                return false;
            }

            try
            {
                RecalculateTotals();

                var request = new QuotationPrintRequest
                {
                    QuotationNo = GenerateTemporaryQuotationNo(),
                    QuotationDate = DateTime.Now,
                    CashierName = CashierName,
                    TerminalNo = TerminalNo,
                    CustomerName = string.IsNullOrWhiteSpace(CustomerName) ? "Walk-In" : CustomerName,
                    GrossTotal = GrossValue,
                    TotalDiscount = TotalDiscount,
                    NetTotal = NetValue,
                    Lines = Cart.Select((item, index) => new QuotationPrintLine
                    {
                        LineNo = index + 1,
                        ItemDescription = item.Description,
                        SkuCode = item.SkuCode,
                        Barcode = item.Barcode,
                        Uom = item.Uom,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        DiscountAmount = item.DiscountAmount,
                        LineTotal = item.LineAmount
                    }).ToList()
                };

                await _printService.PrintQuotationAsync(request, _printerName);

                _ = ShowNotificationAsync(
                    "Quotation printed. No sale saved and no stock deducted.",
                    "#10B981");

                return true;
            }
            catch (Exception ex)
            {
                _ = ShowNotificationAsync(
                    $"Quotation print failed: {ex.Message}",
                    "#EF4444");

                return false;
            }
        }

        private static string GenerateTemporaryQuotationNo()
        {
            return $"QT-{DateTime.Now:yyyyMMdd-HHmmss}";
        }

    }
}