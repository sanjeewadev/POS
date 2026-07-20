using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.Messages;
using POS.Cashier.UI.Models;
using POS.Cashier.UI.Services;
using POS.Core.Configuration;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Services.Documents;
using POS.Core.Services.Tax;

namespace POS.Cashier.UI.ViewModels
{
    public partial class SalesViewModel : ObservableObject
    {
        private readonly ItemMasterRepository _itemRepository;
        private readonly SalesRepository _salesRepository;
        private readonly SalesDocumentRepository _salesDocumentRepository;
        private readonly TillRepository _tillRepository;
        private readonly IReceiptPrintService _printService;
        private readonly CashDrawerAuditService _drawerAuditService;
        private readonly SalesTaxService _salesTaxService = new();
        private readonly SemaphoreSlim _barcodeProcessingGate = new(1, 1);
        private int _pendingBarcodeOperations;

        public bool IsBarcodeProcessing =>
            Volatile.Read(ref _pendingBarcodeOperations) > 0;

        private bool _isVatRegisteredStore;

        public ObservableCollection<CartItem> Cart { get; } = new();
        public ObservableCollection<PaymentLine> PaymentLines { get; } = new();

        [ObservableProperty] private CartItem? _selectedCartItem;
        [ObservableProperty] private decimal _grossValue = 0.00m;
        [ObservableProperty] private decimal _netValue = 0.00m;
        [ObservableProperty] private decimal _totalDiscount = 0.00m;
        [ObservableProperty] private decimal _lineDiscountTotal = 0.00m;
        [ObservableProperty] private decimal _invoiceDiscountAmount = 0.00m;
        [ObservableProperty] private decimal _taxableAmountTotal = 0.00m;
        [ObservableProperty] private decimal _totalVatAmount = 0.00m;
        [ObservableProperty] private decimal _zeroRatedAmount = 0.00m;
        [ObservableProperty] private decimal _exemptAmount = 0.00m;
        [ObservableProperty] private decimal _outOfScopeAmount = 0.00m;
        [ObservableProperty] private bool _isTaxCalculationReady = false;
        [ObservableProperty] private string _taxSummaryStatusText = "VAT summary pending";
        [ObservableProperty] private int _totalItems = 0;
        [ObservableProperty] private decimal _totalPieces = 0m;

        [ObservableProperty] private bool _isPaymentModeActive = false;
        [ObservableProperty] private PaymentLine? _selectedPaymentLine;
        [ObservableProperty] private decimal _paidTotal = 0m;
        [ObservableProperty] private decimal _balanceDue = 0m;
        [ObservableProperty] private decimal _cashTenderedTotal = 0m;
        [ObservableProperty] private decimal _balanceReturned = 0m;
        [ObservableProperty] private string _paymentStatusText = "Sale mode active.";
        [ObservableProperty] private string _paymentStatusColor = "#003366";

        public bool CanConfirmPaymentSale =>
            IsPaymentModeActive &&
            Cart.Any() &&
            BalanceDue <= 0m &&
            (PaymentLines.Any() || NetValue <= 0m);

        [ObservableProperty] private string _terminalNo = "Pending...";
        [ObservableProperty] private string _terminalDisplayName = "Terminal";
        [ObservableProperty] private string _storeDisplayName = "My Store";
        [ObservableProperty] private string _shiftDisplayText = "Shift -";
        [ObservableProperty] private string _cashierName = "Pending...";
        [ObservableProperty] private string _invoiceNo = "PENDING...";
        [ObservableProperty] private DateTime _currentDate = DateTime.Now;

        [ObservableProperty] private string _terminalInput = string.Empty;
        [ObservableProperty] private string _terminalInputMode = "READY TO SCAN";

        [ObservableProperty] private string _customerName = "Walk-In";
        [ObservableProperty] private int _loyaltyPoints = 0;
        [ObservableProperty] private bool _isWholesaleMode = false;

        public string PricingModeButtonText =>
            IsWholesaleMode ? "Wholesale" : "Retail";
        [ObservableProperty] private CustomerSearchDto? _activeB2BCustomer;

        [ObservableProperty] private bool _isManagerModeActive = false;
        [ObservableProperty] private string _securityStatusMode = "CASHIER MODE";
        [ObservableProperty] private bool _isTerminalLocked = false;

        [ObservableProperty] private string _notificationMessage = string.Empty;
        [ObservableProperty] private string _notificationColor = "#10B981";
        [ObservableProperty] private bool _isNotificationVisible = false;

        private int _currentShiftId = 0;
        public int CurrentShiftId => _currentShiftId;

        private string _receiptPrinterName =
            string.Empty;

        private int _receiptPaperWidth = 80;

        public string ReceiptPrinterName => _receiptPrinterName;

        public int ReceiptPaperWidth => _receiptPaperWidth;

        private bool _autoPrintReceipt;

        private int _receiptCopies = 1;

        private bool _enableCashDrawer;

        private bool _openDrawerAfterCashSale;

        public SalesViewModel(
            ItemMasterRepository itemRepository,
            SalesRepository salesRepository,
            SalesDocumentRepository salesDocumentRepository,
            CashierCartRepository cashierCartRepository,
            TillRepository tillRepository,
            IReceiptPrintService printService,
            CashDrawerAuditService drawerAuditService)
        {
            _itemRepository = itemRepository;
            _salesRepository = salesRepository;
            _salesDocumentRepository = salesDocumentRepository;
            _cashierCartRepository = cashierCartRepository;
            _tillRepository = tillRepository;
            _printService = printService;
            _drawerAuditService = drawerAuditService;

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
                ScheduleCartAutosave();
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

            WeakReferenceMessenger.Default.Register<AddToCartMessage>(this, (r, m) =>
            {
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

                ScheduleCartAutosave();
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

        public async Task ShowNotificationAsync(string message, string colorHex = "#10B981")
        {
            NotificationMessage = message;
            NotificationColor = colorHex;
            IsNotificationVisible = true;

            await Task.Delay(2500);

            IsNotificationVisible = false;
        }

        public void InitializeShiftContext(
            string terminalNo,
            ShiftSession activeShift,
            TerminalSettings terminalSettings,
            StoreSettings storeSettings)
        {
            if (activeShift == null)
                throw new ArgumentNullException(nameof(activeShift));

            if (terminalSettings == null)
            {
                throw new ArgumentNullException(
                    nameof(terminalSettings));
            }

            if (storeSettings == null)
            {
                throw new ArgumentNullException(
                    nameof(storeSettings));
            }

            string safeTerminalNo =
                (terminalNo ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(safeTerminalNo))
            {
                throw new InvalidOperationException(
                    "Terminal number is required.");
            }

            if (!string.Equals(
                    activeShift.TerminalNo,
                    safeTerminalNo,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The active shift does not belong to this terminal.");
            }

            if (!string.Equals(
                    activeShift.Status,
                    "Open",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The selected shift is not open.");
            }

            TerminalNo = safeTerminalNo;

            string terminalName =
                FirstNonEmpty(
                    terminalSettings.TerminalName,
                    $"Terminal {safeTerminalNo}");

            TerminalDisplayName =
                $"{terminalName} ({safeTerminalNo})";

            StoreDisplayName =
                FirstNonEmpty(
                    storeSettings.StoreName,
                    storeSettings.LegalName,
                    "My Store");

            _isVatRegisteredStore =
                storeSettings.IsVatRegistered;

            _currentShiftId = activeShift.Id;
            ShiftDisplayText =
                $"Shift {activeShift.Id} - OPEN";

            CashierName = activeShift.CashierName;

            _receiptPrinterName =
                (terminalSettings
                    .ReceiptPrinterName ??
                 string.Empty).Trim();

            _receiptPaperWidth =
                terminalSettings
                    .ReceiptPaperWidth == 58
                    ? 58
                    : 80;

            _autoPrintReceipt =
                terminalSettings.AutoPrintReceipt;

            _receiptCopies =
                Math.Clamp(
                    terminalSettings
                        .ReceiptCopies,
                    1,
                    3);

            _enableCashDrawer =
                terminalSettings.EnableCashDrawer;

            _openDrawerAfterCashSale =
                terminalSettings
                    .EnableCashDrawer &&
                terminalSettings
                    .OpenDrawerAfterCashSale;
        }

        public async Task LoadActiveShiftAsync()
        {
            try
            {
                var shift = await _tillRepository.GetActiveShiftAsync(TerminalNo);

                if (shift != null)
                {
                    _currentShiftId = shift.Id;
                    ShiftDisplayText =
                        $"Shift {shift.Id} - OPEN";
                    CashierName = shift.CashierName;
                }
                else
                {
                    _currentShiftId = 0;
                    ShiftDisplayText =
                        "NO OPEN SHIFT";
                    CashierName = "NO OPEN SHIFT";
                }
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Load active shift",
                    ex);

                _currentShiftId = 0;
                ShiftDisplayText =
                    "SHIFT ERROR";
                CashierName = "ERROR LOADING SHIFT";
            }
        }

        public void AppendTerminalInput(string value)
        {
            if (IsCheckoutInProgress || string.IsNullOrWhiteSpace(value))
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
            if (IsCheckoutInProgress)
                return;

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

            // In Scan mode every non-empty value is a barcode, PLU or item code.
            // Quantity is changed only after the explicit Quantity command is selected.
            TerminalInput = string.Empty;
            await ProcessBarcodeInOrderAsync(input);
        }

        private async Task ProcessBarcodeInOrderAsync(string barcode)
        {
            Interlocked.Increment(ref _pendingBarcodeOperations);
            OnPropertyChanged(nameof(IsBarcodeProcessing));

            try
            {
                await _barcodeProcessingGate.WaitAsync();

                try
                {
                    await ProcessBarcodeAsync(barcode);
                }
                finally
                {
                    _barcodeProcessingGate.Release();
                }
            }
            finally
            {
                Interlocked.Decrement(ref _pendingBarcodeOperations);
                OnPropertyChanged(nameof(IsBarcodeProcessing));
            }
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

        public void ApplyTerminalInputAsInvoiceDiscount()
        {
            if (IsPaymentModeActive)
            {
                _ = ShowNotificationAsync(
                    "Cancel payment mode before changing invoice discount.",
                    "#F59E0B");
                TerminalInput = string.Empty;
                return;
            }

            if (!Cart.Any())
            {
                _ = ShowNotificationAsync(
                    "Add an item before applying invoice discount.",
                    "#F59E0B");
                TerminalInput = string.Empty;
                return;
            }

            if (Cart.Any(item => item.IsGiftVoucherSale))
            {
                _ = ShowNotificationAsync(
                    "Invoice discount cannot be applied while a Gift Voucher issue line is in the cart.",
                    "#F59E0B");
                TerminalInput = string.Empty;
                return;
            }

            if (!decimal.TryParse(
                    TerminalInput,
                    out decimal amount))
            {
                _ = ShowNotificationAsync(
                    "Enter a valid invoice discount amount.",
                    "#F59E0B");
                TerminalInput = string.Empty;
                return;
            }

            amount = Math.Round(amount, 2);
            decimal availableValue = Math.Round(
                Cart.Sum(item => item.LineAmount),
                2);

            if (amount < 0m || amount > availableValue)
            {
                _ = ShowNotificationAsync(
                    $"Invoice discount must be between Rs. 0.00 and Rs. {availableValue:N2}.",
                    "#EF4444");
                TerminalInput = string.Empty;
                return;
            }

            TerminalInput = string.Empty;
            InvoiceDiscountAmount = amount;
            RecalculateTotals();

            _ = ShowNotificationAsync(
                amount == 0m
                    ? "Invoice discount cleared."
                    : $"Invoice discount applied: Rs. {amount:N2}",
                amount == 0m ? "#F59E0B" : "#10B981");
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

            if (SelectedCartItem.RequiresStockBatch &&
                qty > SelectedCartItem.AvailableBatchStock)
            {
                _ = ShowNotificationAsync(
                    $"Only {SelectedCartItem.AvailableBatchStock:N3} available in selected stock.",
                    "#F59E0B");
                return;
            }

            SelectedCartItem.Quantity = qty;
            RecalculateTotals();

            _ = ShowNotificationAsync(
                $"Quantity updated: {SelectedCartItem.Description} x {qty:N3}",
                "#10B981");
        }

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
                _ = ShowNotificationAsync($"Discount cannot exceed line gross amount Rs. {grossAmount:N2}.", "#EF4444");
                return;
            }

            ClearRuleDiscountSnapshot(item);

            item.DiscountPercentage = 0m;
            item.ManualDiscountAmount = amount;
            item.DiscountMode = amount > 0m ? "Amount" : "None";
            item.IsManualDiscount = amount > 0m;

            RecalculateTotals();
            _ = ShowNotificationAsync(amount == 0m ? "Line discount cleared." : $"Rs. discount applied: Rs. {amount:N2}", "#10B981");
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
            _ = ShowNotificationAsync(percent == 0m ? "Line discount cleared." : $"Percentage discount applied: {percent:N2}%", "#10B981");
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
                _ = ShowNotificationAsync("New Price cannot be applied after discount. Clear discount first by entering 0 and pressing Rs Disc or % Disc.", "#F59E0B");
                return;
            }

            bool requiresManagerApproval = item.MinimumPrice > 0m && newPrice < item.MinimumPrice;
            if (requiresManagerApproval && string.IsNullOrWhiteSpace(approvedBy))
            {
                _ = ShowNotificationAsync($"Manager approval required. Minimum price is Rs. {item.MinimumPrice:N2}.", "#EF4444");
                return;
            }

            decimal originalPrice = item.IsPriceOverridden && item.OriginalUnitPrice > 0m
                ? Math.Round(item.OriginalUnitPrice, 2)
                : Math.Round(item.UnitPrice > 0m ? item.UnitPrice : item.RetailPrice, 2);

            if (originalPrice <= 0m)
                originalPrice = Math.Round(newPrice, 2);

            item.OriginalUnitPrice = originalPrice;

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

            if (requiresManagerApproval)
            {
                item.PriceOverrideApprovedBy = approvedBy.Trim();
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

        private static bool HasBelowMinimumApproval(CartItem item)
        {
            if (item.IsPriceOverridden)
                return !string.IsNullOrWhiteSpace(item.PriceOverrideApprovedBy) && item.PriceOverrideApprovedAt.HasValue;

            if (item.IsRuleDiscount)
                return !string.IsNullOrWhiteSpace(item.DiscountApprovedBy) && item.DiscountApprovedAt.HasValue;

            return false;
        }

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

            RecalculateTotals();

            if (!Cart.Any(item => item.IsGiftVoucherSale) &&
                !IsTaxCalculationReady)
            {
                _ = ShowNotificationAsync(
                    $"Cannot pay: {TaxSummaryStatusText}",
                    "#EF4444");
                return;
            }

            if (Cart.Any(c => c.RequiresStockBatch && c.ItemBatchId <= 0))
            {
                _ = ShowNotificationAsync("Cannot pay: one or more Stock Item lines has no selected stock reference.", "#EF4444");
                return;
            }

            var invalidFreeLine = Cart.FirstOrDefault(c => c.IsFreeItem && c.FreeIssueRuleId <= 0);
            if (invalidFreeLine != null)
            {
                _ = ShowNotificationAsync($"Free issue rule missing: {invalidFreeLine.Description}", "#EF4444");
                return;
            }

            var invalidSupplierClaimLine = Cart.FirstOrDefault(c => c.IsFreeItem && c.IsSupplierRecoverable && c.SupplierId <= 0);
            if (invalidSupplierClaimLine != null)
            {
                _ = ShowNotificationAsync($"Supplier missing for free issue: {invalidSupplierClaimLine.Description}", "#EF4444");
                return;
            }

            var belowMinimumLine = Cart.FirstOrDefault(c => !c.IsFreeItem && c.IsBelowMinimumPrice);
            if (belowMinimumLine != null && !HasBelowMinimumApproval(belowMinimumLine))
            {
                _ = ShowNotificationAsync($"Price below minimum: {belowMinimumLine.Description}", "#EF4444");
                return;
            }

            IsPaymentModeActive = true;
            TerminalInputMode = "PAYMENT";
            TerminalInput = string.Empty;
            RecalculatePaymentTotals();
            PaymentStatusText = NetValue <= 0m
                ? "No payment required. Press Enter to confirm."
                : "Payment mode active. Enter amount and select payment type.";
            PaymentStatusColor = NetValue <= 0m ? "#10B981" : "#003366";
            _ = ShowNotificationAsync("Payment mode active.", "#3B82F6");
        }

        public void CancelPaymentMode()
        {
            if (!IsPaymentModeActive)
                return;

            PaymentLines.Clear();
            SelectedPaymentLine = null;
            IsPaymentModeActive = false;
            TerminalInputMode = "READY TO SCAN";
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

            if (appliedAmount <= 0m || appliedAmount > BalanceDue || tenderedAmount < appliedAmount)
            {
                _ = ShowNotificationAsync("Invalid cash payment amount.", "#EF4444");
                return;
            }

            PaymentLines.Add(new PaymentLine
            {
                PaymentType = "Cash",
                Amount = appliedAmount,
                TenderedAmount = tenderedAmount,
                ChangeAmount = changeAmount,
                PaymentDate = DateTime.Now,
                CreatedAt = DateTime.Now
            });

            SelectedPaymentLine = PaymentLines.LastOrDefault();
            TerminalInput = string.Empty;
            RecalculatePaymentTotals();

            _ = ShowNotificationAsync(
                BalanceDue <= 0m
                    ? $"Cash payment added. Change Rs. {BalanceReturned:N2}."
                    : $"Cash payment added. Balance due Rs. {BalanceDue:N2}.",
                BalanceDue <= 0m ? "#10B981" : "#D97706");
        }

        public void AddConfirmedCardPayment(string cardType, decimal amount, string lastSixDigits, string referenceNo)
        {
            if (!EnsurePaymentModeReady())
                return;

            amount = Math.Round(amount, 2);

            if (amount <= 0m || amount > BalanceDue)
            {
                _ = ShowNotificationAsync("Invalid card payment amount.", "#EF4444");
                return;
            }

            string safeCardType = string.IsNullOrWhiteSpace(cardType) ? "Card" : cardType.Trim();
            string safeLastSix = string.IsNullOrWhiteSpace(lastSixDigits) ? string.Empty : lastSixDigits.Trim();
            string safeReference = string.IsNullOrWhiteSpace(referenceNo) ? safeLastSix : referenceNo.Trim();

            PaymentLines.Add(new PaymentLine
            {
                PaymentType = "Card",
                CardType = safeCardType,
                BankOrCardType = safeCardType,
                CardLastDigits = safeLastSix,
                ReferenceNo = safeReference,
                Amount = amount,
                TenderedAmount = amount,
                ChangeAmount = 0m,
                PaymentDate = DateTime.Now,
                CreatedAt = DateTime.Now
            });

            SelectedPaymentLine = PaymentLines.LastOrDefault();
            TerminalInput = string.Empty;
            RecalculatePaymentTotals();

            _ = ShowNotificationAsync(
                BalanceDue <= 0m
                    ? $"{safeCardType} payment added. Press Enter to complete sale."
                    : $"{safeCardType} payment added. Balance due Rs. {BalanceDue:N2}.",
                BalanceDue <= 0m ? "#10B981" : "#D97706");
        }

        public void AddConfirmedChequePayment(decimal amount, string chequeNo, string bankOrBranch, DateTime chequeDate)
        {
            if (!EnsurePaymentModeReady())
                return;

            amount = Math.Round(amount, 2);
            string safeChequeNo = (chequeNo ?? string.Empty).Trim();
            string safeBankOrBranch = (bankOrBranch ?? string.Empty).Trim();

            if (amount <= 0m || amount > BalanceDue || string.IsNullOrWhiteSpace(safeChequeNo) || string.IsNullOrWhiteSpace(safeBankOrBranch))
            {
                _ = ShowNotificationAsync("Valid cheque details are required.", "#EF4444");
                return;
            }

            PaymentLines.Add(new PaymentLine
            {
                PaymentType = "Cheque",
                BankOrCardType = safeBankOrBranch,
                ReferenceNo = safeChequeNo,
                Amount = amount,
                TenderedAmount = amount,
                ChangeAmount = 0m,
                PaymentDate = chequeDate,
                CreatedAt = DateTime.Now
            });

            SelectedPaymentLine = PaymentLines.LastOrDefault();
            TerminalInput = string.Empty;
            RecalculatePaymentTotals();
            _ = ShowNotificationAsync(BalanceDue <= 0m ? "Cheque payment added." : $"Cheque payment added. Balance due Rs. {BalanceDue:N2}.", BalanceDue <= 0m ? "#10B981" : "#D97706");
        }

        public void AddConfirmedCustomerCreditPayment(decimal amount)
        {
            if (!EnsurePaymentModeReady())
                return;

            CustomerSearchDto? customer = ActiveB2BCustomer;
            if (customer == null)
            {
                _ = ShowNotificationAsync("Customer Credit requires a selected customer.", "#EF4444");
                return;
            }

            amount = Math.Round(amount, 2);
            if (amount <= 0m || amount > BalanceDue)
            {
                _ = ShowNotificationAsync("Invalid Customer Credit amount.", "#EF4444");
                return;
            }

            if (!customer.CanUseCredit)
            {
                _ = ShowNotificationAsync(customer.CreditWarningText, "#EF4444");
                return;
            }

            decimal alreadyAdded = Math.Round(
                PaymentLines
                    .Where(line => line.IsCustomerCredit)
                    .Sum(line => line.Amount),
                2);

            decimal available = Math.Round(
                Math.Max(0m, customer.RemainingCredit - alreadyAdded),
                2);

            if (amount > available)
            {
                _ = ShowNotificationAsync(
                    $"Available customer credit is Rs. {available:N2}.",
                    "#EF4444");
                return;
            }

            PaymentLines.Add(new PaymentLine
            {
                PaymentType = "CustomerCredit",
                BankOrCardType = "Customer Account",
                ReferenceNo = customer.CustomerCode,
                Amount = amount,
                TenderedAmount = amount,
                ChangeAmount = 0m,
                PaymentDate = DateTime.Now,
                CreatedAt = DateTime.Now
            });

            SelectedPaymentLine = PaymentLines.LastOrDefault();
            TerminalInput = string.Empty;
            RecalculatePaymentTotals();

            _ = ShowNotificationAsync(
                BalanceDue <= 0m
                    ? $"Customer Credit added. Due in {Math.Max(0, customer.CreditDays)} day(s). Press Enter to complete sale."
                    : $"Customer Credit added. Balance due Rs. {BalanceDue:N2}.",
                BalanceDue <= 0m ? "#10B981" : "#D97706");
        }

        public void AddConfirmedGiftVoucherPayment(
            int giftVoucherId,
            string voucherNo,
            string voucherBarcode,
            decimal voucherAmount,
            decimal amountToApply,
            decimal forfeitedAmount,
            string authorizedBy)
        {
            if (!EnsurePaymentModeReady())
                return;

            if (Cart.Any(c => c.IsGiftVoucherSale))
            {
                _ = ShowNotificationAsync("Gift voucher cannot be used to buy another gift voucher.", "#EF4444");
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
            string safeAuthorizedBy = (authorizedBy ?? string.Empty).Trim();

            decimal totalConsumed = Math.Round(amountToApply + forfeitedAmount, 2);
            if (amountToApply <= 0m ||
                amountToApply > BalanceDue ||
                voucherAmount <= 0m ||
                forfeitedAmount < 0m ||
                Math.Abs(totalConsumed - voucherAmount) > 0.01m)
            {
                _ = ShowNotificationAsync("A one-time gift voucher must be fully consumed by the applied and forfeited amounts.", "#EF4444");
                return;
            }

            if (forfeitedAmount > 0m && string.IsNullOrWhiteSpace(safeAuthorizedBy))
            {
                _ = ShowNotificationAsync("Manager authorization is required for gift voucher forfeiture.", "#EF4444");
                return;
            }

            string safeVoucherNo = string.IsNullOrWhiteSpace(voucherNo) ? $"GV-{giftVoucherId}" : voucherNo.Trim();
            string safeBarcode = string.IsNullOrWhiteSpace(voucherBarcode) ? safeVoucherNo : voucherBarcode.Trim();

            if (PaymentLines.Any(p => p.IsGiftVoucher && p.GiftVoucherId == giftVoucherId))
            {
                _ = ShowNotificationAsync("This gift voucher is already added to the payment.", "#F59E0B");
                return;
            }

            PaymentLines.Add(new PaymentLine
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
                GiftVoucherAuthorizedBy = forfeitedAmount > 0m ? safeAuthorizedBy : string.Empty,
                PaymentDate = DateTime.Now,
                CreatedAt = DateTime.Now
            });

            SelectedPaymentLine = PaymentLines.LastOrDefault();
            TerminalInput = string.Empty;
            RecalculatePaymentTotals();

            _ = ShowNotificationAsync(
                forfeitedAmount > 0m
                    ? $"Gift voucher added. Applied Rs. {amountToApply:N2}. Forfeited Rs. {forfeitedAmount:N2}."
                    : BalanceDue <= 0m
                        ? "Gift voucher payment added."
                        : $"Gift voucher payment added. Balance due Rs. {BalanceDue:N2}.",
                BalanceDue <= 0m || forfeitedAmount > 0m ? "#10B981" : "#D97706");
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

            if (!PaymentLines.Any() && NetValue > 0m)
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

            return IsPaymentModeActive && Cart.Any() && BalanceDue > 0m;
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
            if (due < 0m) due = 0m;
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
            else if (NetValue <= 0m && PaymentLines.Count == 0)
            {
                PaymentStatusText = "No payment required. Press Enter to confirm.";
                PaymentStatusColor = "#10B981";
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
                line.LineNo = lineNo++;
        }

        public async Task ReloadCashierAsync()
        {
            if (_isCheckoutInProgress)
            {
                await ShowNotificationAsync(
                    "Checkout is already in progress.",
                    "#F59E0B");
                return;
            }

            try
            {
                await FlushCartPersistenceAsync();
                await LoadActiveShiftAsync();
                RecalculateTotals();

                if (IsPaymentModeActive)
                    RecalculatePaymentTotals();

                await ShowNotificationAsync(
                    "Cashier refreshed. The current cart was preserved.",
                    "#10B981");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Reload Cashier",
                    ex);

                await ShowNotificationAsync(
                    $"Refresh failed: {ex.Message}",
                    "#EF4444");
            }
        }

        public void TogglePricingMode()
        {
            if (IsPaymentModeActive)
            {
                _ = ShowNotificationAsync(
                    "Cancel payment mode before changing Retail/Wholesale pricing.",
                    "#F59E0B");
                return;
            }

            if (_isCheckoutInProgress)
                return;

            IsWholesaleMode = !IsWholesaleMode;
            ApplyPricingModeToCart();
            RecalculateTotals();

            string mode = IsWholesaleMode ? "WHOLESALE" : "RETAIL";
            _ = ShowNotificationAsync(
                $"{mode} PRICE MODE ACTIVE",
                IsWholesaleMode ? "#3B82F6" : "#10B981");
        }

        private void ApplyPricingModeToCart()
        {
            foreach (CartItem item in Cart)
            {
                if (item.IsGiftVoucherSale ||
                    item.IsFreeItem ||
                    item.IsManualDiscount ||
                    item.IsRuleDiscount ||
                    item.IsPriceOverridden ||
                    item.DiscountAmount > 0m)
                {
                    continue;
                }

                decimal selectedPrice =
                    IsWholesaleMode && item.WholesalePrice > 0m
                        ? item.WholesalePrice
                        : item.RetailPrice;

                if (selectedPrice <= 0m)
                    continue;

                item.UnitPrice = selectedPrice;
                item.OriginalUnitPrice = selectedPrice;
            }
        }

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
            CustomerName = string.IsNullOrWhiteSpace(customer.DisplayName) ? "Walk-In" : customer.DisplayName;
            IsWholesaleMode = customer.IsWholesale;
            ApplyCustomerPricingToCart(customer);
            RecalculateTotals();

            string message = customer.IsWholesale
                ? customer.IsDiscountEligible ? $"WHOLESALE CUSTOMER LINKED: {CustomerName} | Discount Enabled" : $"WHOLESALE CUSTOMER LINKED: {CustomerName}"
                : customer.IsDiscountEligible ? $"LOYALTY CUSTOMER LINKED: {CustomerName}" : $"CUSTOMER LINKED: {CustomerName}";

            _ = ShowNotificationAsync(message, customer.IsWholesale ? "#3B82F6" : "#10B981");
        }

        public void AttachB2BCustomer(CustomerSearchDto customer) => AttachCustomer(customer);

        public void AttachLoyaltyCustomer(CustomerSearchDto customer) => AttachCustomer(customer);

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
                if (!item.IsGiftVoucherSale && !item.IsFreeItem && !item.IsManualDiscount && !item.IsPriceOverridden && item.RetailPrice > 0m)
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
                if (item.IsGiftVoucherSale || item.IsFreeItem || item.IsManualDiscount || item.IsPriceOverridden)
                    continue;

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

        public void SetManagerMode(bool activate)
        {
            IsManagerModeActive = activate;
            SecurityStatusMode = activate ? "MANAGER MODE ACTIVE" : "CASHIER MODE";
        }

        public bool VerifyActionPermission() => IsManagerModeActive;

        [RelayCommand]
        public async Task AddFloatAsync()
        {
            if (_currentShiftId == 0)
            {
                _ = ShowNotificationAsync("No active shift found.", "#EF4444");
                return;
            }

            string authorizedBy = CashierName;
            Window? owner = Application.Current?.MainWindow;

            var authVM = App.Services!.GetRequiredService<ManagerAuthViewModel>();
            var authDialog = new POS.Cashier.UI.Dialogs.ManagerAuthDialogView(authVM)
            {
                Owner = owner
            };

            if (authDialog.ShowDialog() != true)
                return;

            authorizedBy = authVM.AuthorizedUsername;

            var floatVM = App.Services!.GetRequiredService<FloatCashViewModel>();
            floatVM.Initialize(_currentShiftId, authorizedBy);

            var floatDialog = new POS.Cashier.UI.Dialogs.FloatCashDialog(floatVM)
            {
                Owner = owner
            };
            floatDialog.ShowDialog();

            await Task.CompletedTask;
        }

        [RelayCommand]
        public async Task PrintXReportAsync()
        {
            ShiftCashSummaryDto summary = await _tillRepository
                .GetShiftCashSummaryAsync(_currentShiftId, false)
                ?? throw new InvalidOperationException("The active shift summary could not be loaded.");

            if (string.IsNullOrWhiteSpace(_receiptPrinterName))
                throw new InvalidOperationException("No receipt printer is configured in Terminal Settings.");

            string reportText = App.Services!
                .GetRequiredService<ShiftReportTextFormatter>()
                .FormatXReport(summary, _receiptPaperWidth);

            await _printService.PrintTextAsync(
                reportText,
                _receiptPrinterName,
                "POS X Report");
        }

        private async Task AddToCartFromMessageAsync(AddToCartRequest request)
        {
            if (request == null)
                return;

            if (IsPaymentModeActive)
            {
                _ = ShowNotificationAsync("Cancel payment mode before adding more items.", "#F59E0B");
                return;
            }

            // Exact batch requests must win before variant requests.
            // Product Seek batch row sends both ItemVariantId and ItemBatchId.
            // If we process ItemVariantId first, batch-tracked items are blocked as normal barcode/SKU flow.
            if (request.ItemBatchId > 0)
            {
                await AddBatchToCartAsync(request.ItemBatchId, request.Quantity);
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
                // New GRN barcode rule: exact batch barcode wins first and never opens batch popup.
                var batchByBarcode = await _itemRepository.GetSellableBatchByInternalBarcodeAsync(term);

                if (batchByBarcode != null)
                {
                    await AddBatchToCartAsync(batchByBarcode.ItemBatchId, 1m);
                    return;
                }

                var item = await _itemRepository.GetSellableItemByBarcodeOrSkuAsync(term);

                if (item == null)
                {
                    _ = ShowNotificationAsync($"Unrecognized item/barcode: {term}", "#F59E0B");
                    return;
                }

                if (item.HasBatchTracking)
                {
                    _ = ShowNotificationAsync("Batch item. Scan GRN batch barcode.", "#F59E0B");
                    return;
                }

                await AddVariantToCartAsync(item.VariantId, 1m);
            }
            catch (Exception ex)
            {
                _ = ShowNotificationAsync($"Database Error: {ex.Message}", "#EF4444");
            }
        }

        public async Task AddBatchToCartAsync(int itemBatchId, decimal quantity = 1m)
        {
            if (IsPaymentModeActive)
            {
                _ = ShowNotificationAsync("Cancel payment mode before adding more items.", "#F59E0B");
                return;
            }

            if (itemBatchId <= 0)
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
                var selectedBatch = await _itemRepository.GetSellableBatchByIdAsync(itemBatchId);

                if (selectedBatch == null)
                {
                    _ = ShowNotificationAsync("Selected batch is not sellable.", "#EF4444");
                    return;
                }

                var item = await _itemRepository.GetSellableItemByVariantIdAsync(selectedBatch.ItemVariantId);

                if (item == null)
                {
                    _ = ShowNotificationAsync("Selected item is locked or not sellable.", "#EF4444");
                    return;
                }

                AddSelectedBatchToCart(item, selectedBatch, quantity);
            }
            catch (Exception ex)
            {
                _ = ShowNotificationAsync($"Add batch failed: {ex.Message}", "#EF4444");
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

                if (item.IsService)
                {
                    AddServiceToCart(item, quantity);
                    return;
                }

                if (item.HasBatchTracking)
                {
                    _ = ShowNotificationAsync("Batch item. Scan GRN batch barcode.", "#F59E0B");
                    return;
                }

                var selectedBatch = await _itemRepository.GetGeneralSellableBatchForVariantAsync(item.VariantId);

                if (selectedBatch == null)
                {
                    _ = ShowNotificationAsync($"No sellable stock: {item.DisplayDescription}", "#F59E0B");
                    return;
                }

                AddSelectedBatchToCart(item, selectedBatch, quantity);
            }
            catch (Exception ex)
            {
                _ = ShowNotificationAsync($"Add item failed: {ex.Message}", "#EF4444");
            }
        }

        private void AddSelectedBatchToCart(CashierSellableItemDto item, CashierBatchDto selectedBatch, decimal quantity)
        {
            if (!item.IsStockItem)
            {
                _ = ShowNotificationAsync("Only Stock Items can be added from a stock batch.", "#EF4444");
                return;
            }

            if (selectedBatch.AvailableQty <= 0m)
            {
                _ = ShowNotificationAsync("Selected stock has no available quantity.", "#F59E0B");
                return;
            }

            if (quantity > selectedBatch.AvailableQty)
            {
                _ = ShowNotificationAsync($"Only {selectedBatch.AvailableQty:N3} available in selected stock.", "#F59E0B");
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
                    _ = ShowNotificationAsync($"Only {selectedBatch.AvailableQty:N3} available in selected stock.", "#F59E0B");
                    return;
                }

                existingItem.Quantity += quantity;
                existingItem.AvailableBatchStock = selectedBatch.AvailableQty;
                SelectedCartItem = existingItem;
                RecalculateTotals();
                _ = ShowNotificationAsync($"{item.DisplayDescription} quantity updated.", "#10B981");
                return;
            }

            decimal sellingPrice = IsWholesaleMode && item.WholesalePrice > 0m ? item.WholesalePrice : item.RetailPrice;
            if (sellingPrice <= 0m && selectedBatch.RetailPrice > 0m)
                sellingPrice = selectedBatch.RetailPrice;

            var cartItem = new CartItem
            {
                ItemVariantId = item.VariantId,
                ItemBatchId = selectedBatch.ItemBatchId,
                ItemParentId = item.ItemParentId,
                CategoryId = item.CategoryId,
                SubCategoryId = item.SubCategoryId ?? 0,
                PrimarySupplierId = item.PrimarySupplierId ?? 0,
                SupplierIds = item.SupplierIds.ToList(),
                ItemCode = item.ItemCode,
                SkuCode = item.SkuCode,
                Barcode = string.IsNullOrWhiteSpace(item.Barcode) ? item.SkuCode : item.Barcode,
                Description = item.DisplayDescription,
                VariantDescription = item.VariantDescription,
                Uom = item.Uom,
                ItemType = item.ItemType,
                TaxProfile = item.TaxProfile,
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

            string batchText = string.IsNullOrWhiteSpace(selectedBatch.BatchNo) || selectedBatch.BatchNo.Equals("GENERAL", StringComparison.OrdinalIgnoreCase)
                ? "Stock selected"
                : $"Batch {selectedBatch.BatchNo}";

            _ = ShowNotificationAsync($"Added: {item.DisplayDescription} / {batchText}", "#10B981");
        }

        private void AddServiceToCart(
            CashierSellableItemDto item,
            decimal quantity)
        {
            if (!item.IsService)
            {
                _ = ShowNotificationAsync(
                    "Selected item is not configured as a Service.",
                    "#EF4444");
                return;
            }

            if (quantity <= 0m)
                quantity = 1m;

            var existingItem = Cart.FirstOrDefault(c =>
                !c.IsGiftVoucherSale &&
                !c.IsFreeItem &&
                c.IsService &&
                !c.IsManualDiscount &&
                !c.IsPriceOverridden &&
                c.DiscountAmount <= 0m &&
                c.ItemVariantId == item.VariantId);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
                SelectedCartItem = existingItem;
                RecalculateTotals();

                _ = ShowNotificationAsync(
                    $"{item.DisplayDescription} quantity updated.",
                    "#10B981");
                return;
            }

            decimal sellingPrice =
                IsWholesaleMode && item.WholesalePrice > 0m
                    ? item.WholesalePrice
                    : item.RetailPrice;

            decimal serviceCost =
                item.CostPrice > 0m
                    ? item.CostPrice
                    : item.AverageCost;

            var cartItem = new CartItem
            {
                ItemVariantId = item.VariantId,
                ItemBatchId = 0,
                ItemParentId = item.ItemParentId,
                CategoryId = item.CategoryId,
                SubCategoryId = item.SubCategoryId ?? 0,
                PrimarySupplierId = item.PrimarySupplierId ?? 0,
                SupplierIds = item.SupplierIds.ToList(),
                ItemCode = item.ItemCode,
                SkuCode = item.SkuCode,
                Barcode = string.IsNullOrWhiteSpace(item.Barcode)
                    ? item.SkuCode
                    : item.Barcode,
                Description = item.DisplayDescription,
                VariantDescription = item.VariantDescription,
                Uom = item.Uom,
                ItemType = item.ItemType,
                TaxProfile = item.TaxProfile,
                BatchNo = string.Empty,
                ExpiryDate = null,
                ReceivedDate = null,
                CostPrice = serviceCost,
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
                AvailableBatchStock = 0m
            };

            Cart.Add(cartItem);
            SelectedCartItem = cartItem;
            RecalculateTotals();

            _ = ShowNotificationAsync(
                $"Added Service: {item.DisplayDescription}",
                "#10B981");
        }

        public async Task<bool> AddSpecificItemToCartAsync(string barcode, int quantity)
        {
            if (string.IsNullOrWhiteSpace(barcode) || quantity <= 0)
                return false;

            if (_currentShiftId == 0 || IsPaymentModeActive)
                return false;

            try
            {
                decimal beforeQty = Cart.Sum(c => c.Quantity);

                string term = barcode.Trim();

                var batch =
                    await _itemRepository
                        .GetSellableBatchByInternalBarcodeAsync(term);

                if (batch != null)
                {
                    await AddBatchToCartAsync(
                        batch.ItemBatchId,
                        quantity);
                }
                else
                {
                    var item =
                        await _itemRepository
                            .GetSellableItemByBarcodeOrSkuAsync(term);

                    if (item == null)
                        return false;

                    await AddVariantToCartAsync(
                        item.VariantId,
                        quantity);
                }

                decimal afterQty = Cart.Sum(c => c.Quantity);
                return afterQty > beforeQty;
            }
            catch
            {
                return false;
            }
        }

        public void AddGiftVoucherSaleLine(int giftVoucherId, string voucherNo, string voucherBarcode, decimal voucherAmount, string displayDescription)
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

            if (giftVoucherId <= 0 || voucherAmount <= 0m)
            {
                _ = ShowNotificationAsync("Invalid gift voucher.", "#EF4444");
                return;
            }

            voucherAmount = Math.Round(voucherAmount, 2);
            string safeVoucherNo = string.IsNullOrWhiteSpace(voucherNo) ? $"GV-{giftVoucherId}" : voucherNo.Trim();
            string safeBarcode = string.IsNullOrWhiteSpace(voucherBarcode) ? safeVoucherNo : voucherBarcode.Trim();

            if (Cart.Any(c => c.IsGiftVoucherSale && c.GiftVoucherId == giftVoucherId))
            {
                _ = ShowNotificationAsync("This gift voucher is already added to the sale.", "#F59E0B");
                return;
            }

            string description = string.IsNullOrWhiteSpace(displayDescription) ? $"Gift Voucher Rs. {voucherAmount:N2}" : displayDescription.Trim();

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
                ItemType = string.Empty,
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
            _ = ShowNotificationAsync($"Gift voucher added to sale: {safeVoucherNo} / Rs. {voucherAmount:N2}", "#10B981");
        }

        public void ApplyFreeItemLogic(CartItem cartItem, FreeItemApplyResult result)
        {
            if (cartItem == null || result == null)
            {
                _ = ShowNotificationAsync("Free Issue result is missing.", "#EF4444");
                return;
            }

            if (IsPaymentModeActive)
            {
                _ = ShowNotificationAsync("Cancel payment mode before applying Free Issue.", "#F59E0B");
                return;
            }

            if (cartItem.IsGiftVoucherSale || cartItem.IsFreeItem || cartItem.Quantity <= 0m || result.FreeIssueRuleId <= 0)
            {
                _ = ShowNotificationAsync("Free Issue cannot be applied to this line.", "#EF4444");
                return;
            }

            if (cartItem.IsManualDiscount || cartItem.IsRuleDiscount || cartItem.IsPriceOverridden || cartItem.DiscountAmount > 0m)
            {
                _ = ShowNotificationAsync(
                    "Remove the discount or price override before applying Free Issue.",
                    "#F59E0B");
                return;
            }

            decimal freeQuantity = Math.Round(result.FreeQuantity, 3);
            if (freeQuantity <= 0m || freeQuantity > cartItem.Quantity)
            {
                _ = ShowNotificationAsync(
                    $"Free quantity must be between 0.001 and {cartItem.Quantity:N3}.",
                    "#EF4444");
                return;
            }

            string freeIssueType = FreeIssueTypeCodes.Normalize(result.FreeIssueType);
            bool isSupplierRecoverable = freeIssueType == FreeIssueTypeCodes.SupplierClaim;

            if (isSupplierRecoverable && (!result.SupplierId.HasValue || result.SupplierId.Value <= 0))
            {
                _ = ShowNotificationAsync(
                    "Supplier is required for a supplier-funded Free Issue.",
                    "#EF4444");
                return;
            }

            if (result.RequiresAdminApproval &&
                !string.Equals(result.ApprovedRole, FreeIssueApprovalRoleCodes.Administrator, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(result.ApprovedRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                _ = ShowNotificationAsync("Administrator approval is required.", "#EF4444");
                return;
            }

            if ((result.RequiresManagerApproval || result.RequiresAdminApproval) &&
                (result.ApprovedByUserId.GetValueOrDefault() <= 0 || string.IsNullOrWhiteSpace(result.ApprovedBy)))
            {
                _ = ShowNotificationAsync("Authenticated approval is required.", "#EF4444");
                return;
            }

            FreeIssueQuantitySplit quantitySplit = FreeIssueQuantitySplitter.Split(
                cartItem.Quantity,
                freeQuantity);

            CartItem freeLine;
            if (quantitySplit.PaidQuantity > 0m)
            {
                int paidLineIndex = Cart.IndexOf(cartItem);
                cartItem.Quantity = quantitySplit.PaidQuantity;
                freeLine = CreateFreeIssueSplitLine(cartItem, quantitySplit.FreeQuantity);
                Cart.Insert(Math.Max(0, paidLineIndex + 1), freeLine);
            }
            else
            {
                freeLine = cartItem;
            }

            decimal originalUnitPrice = result.OriginalUnitPrice > 0m
                ? Math.Round(result.OriginalUnitPrice, 2)
                : Math.Round(freeLine.UnitPrice > 0m ? freeLine.UnitPrice : freeLine.RetailPrice, 2);
            decimal costValue = Math.Round(freeLine.CostPrice * freeQuantity, 2);
            decimal sellingValue = Math.Round(originalUnitPrice * freeQuantity, 2);
            decimal claimValue = isSupplierRecoverable
                ? Math.Round(result.ClaimValue, 2)
                : 0m;

            freeLine.Quantity = freeQuantity;
            freeLine.OriginalUnitPrice = originalUnitPrice;
            freeLine.UnitPrice = 0m;
            freeLine.DiscountPercentage = 0m;
            freeLine.ManualDiscountAmount = 0m;
            freeLine.DiscountMode = "None";
            freeLine.IsManualDiscount = false;
            freeLine.IsRuleDiscount = false;
            freeLine.DiscountRuleId = 0;
            freeLine.DiscountRuleName = string.Empty;
            freeLine.IsPriceOverridden = false;
            freeLine.PriceOverrideAmount = 0m;
            freeLine.PriceOverrideApprovedBy = string.Empty;
            freeLine.PriceOverrideApprovedAt = null;
            freeLine.IsFreeItem = true;
            freeLine.FreeIssueRuleId = result.FreeIssueRuleId;
            freeLine.FreeIssueRuleName = result.FreeIssueRuleName ?? string.Empty;
            freeLine.FreeIssueType = freeIssueType;
            freeLine.FreeReasonCode = result.FreeReasonCode ?? string.Empty;
            freeLine.FreeReasonText = result.FreeReasonText ?? string.Empty;
            freeLine.FreeApprovedBy = result.ApprovedBy ?? string.Empty;
            freeLine.FreeApprovedAt = result.ApprovedAt;
            freeLine.FreeApprovedByUserId = result.ApprovedByUserId ?? 0;
            freeLine.FreeApprovedRole = result.ApprovedRole ?? string.Empty;
            freeLine.FreeIssueAppliedBy = string.IsNullOrWhiteSpace(result.AppliedBy)
                ? CashierName
                : result.AppliedBy.Trim();
            freeLine.FreeIssueAppliedAt = result.AppliedAt == default ? DateTime.Now : result.AppliedAt;
            freeLine.FreeIssueRuleSnapshotJson = result.RuleSnapshotJson ?? string.Empty;
            freeLine.FreeIssueSnapshotStatus = result.SnapshotStatus ?? FreeIssueSnapshotStatusCodes.LegacyUnknown;
            freeLine.FreeIssueCostValue = costValue;
            freeLine.FreeIssueSellingValue = sellingValue;
            freeLine.IsSupplierRecoverable = isSupplierRecoverable;
            freeLine.SupplierId = result.SupplierId ?? 0;
            freeLine.SupplierName = result.SupplierName ?? string.Empty;
            freeLine.SupplierPromotionReference = result.SupplierPromotionReference ?? string.Empty;
            freeLine.SupplierClaimId = 0;
            freeLine.SupplierClaimReferenceNo = string.Empty;
            freeLine.SupplierClaimStatus = isSupplierRecoverable
                ? SupplierClaimStatusCodes.Draft
                : string.Empty;
            freeLine.SupplierClaimValue = claimValue;

            SelectedCartItem = freeLine;
            RecalculateTotals();
            _ = ShowNotificationAsync(
                isSupplierRecoverable
                    ? $"Supplier-funded Free Issue applied: {freeQuantity:N3} / Claim Rs. {claimValue:N2}"
                    : $"Shop-funded Free Issue applied: {freeQuantity:N3} / Cost Rs. {costValue:N2}",
                "#10B981");
        }

        private static CartItem CreateFreeIssueSplitLine(CartItem source, decimal quantity)
        {
            return new CartItem
            {
                ItemVariantId = source.ItemVariantId,
                ItemBatchId = source.ItemBatchId,
                ItemParentId = source.ItemParentId,
                CategoryId = source.CategoryId,
                SubCategoryId = source.SubCategoryId,
                PrimarySupplierId = source.PrimarySupplierId,
                SupplierIds = source.SupplierIds.ToList(),
                ItemCode = source.ItemCode,
                SkuCode = source.SkuCode,
                Barcode = source.Barcode,
                Description = source.Description,
                VariantDescription = source.VariantDescription,
                Uom = source.Uom,
                ItemType = source.ItemType,
                TaxProfile = source.TaxProfile,
                BatchNo = source.BatchNo,
                ExpiryDate = source.ExpiryDate,
                ReceivedDate = source.ReceivedDate,
                AvailableBatchStock = source.AvailableBatchStock,
                CostPrice = source.CostPrice,
                RetailPrice = source.RetailPrice,
                WholesalePrice = source.WholesalePrice,
                MinimumPrice = source.MinimumPrice,
                MaximumPrice = source.MaximumPrice,
                UnitPrice = source.UnitPrice,
                OriginalUnitPrice = source.OriginalUnitPrice > 0m ? source.OriginalUnitPrice : source.UnitPrice,
                Quantity = quantity,
                DiscountPercentage = 0m,
                ManualDiscountAmount = 0m,
                DiscountMode = "None",
                IsManualDiscount = false,
                IsRuleDiscount = false,
                IsPriceOverridden = false
            };
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
            TerminalInputMode = "READY TO SCAN";
            TerminalInput = string.Empty;
            ActiveB2BCustomer = null;
            CustomerName = "Walk-In";
            IsWholesaleMode = false;
            InvoiceNo = "PENDING...";
            InvoiceDiscountAmount = 0m;
            RecalculateTotals();
            RecalculatePaymentTotals();
            PaymentStatusText = "Sale mode active.";
            PaymentStatusColor = "#003366";
        }

        public void RecalculateTotals()
        {
            TotalItems = Cart.Count;
            TotalPieces = Math.Round(
                Cart.Sum(item => item.Quantity),
                3);

            if (!Cart.Any())
            {
                GrossValue = 0m;
                NetValue = 0m;
                LineDiscountTotal = 0m;
                InvoiceDiscountAmount = 0m;
                TotalDiscount = 0m;
                TaxableAmountTotal = 0m;
                TotalVatAmount = 0m;
                ZeroRatedAmount = 0m;
                ExemptAmount = 0m;
                OutOfScopeAmount = 0m;
                IsTaxCalculationReady = false;
                TaxSummaryStatusText = "VAT summary pending";

                if (IsPaymentModeActive)
                    RecalculatePaymentTotals();

                return;
            }

            bool hasGiftVoucherLine = Cart.Any(item => item.IsGiftVoucherSale);
            bool hasFreeIssueLine = Cart.Any(item => item.IsFreeItem);

            if (hasGiftVoucherLine)
            {
                if (InvoiceDiscountAmount != 0m)
                {
                    InvoiceDiscountAmount = 0m;
                    _ = ShowNotificationAsync(
                        "Invoice discount was cleared because the cart contains a Gift Voucher issue line.",
                        "#F59E0B");
                }

                foreach (CartItem item in Cart)
                {
                    item.InvoiceDiscountAllocation = 0m;
                    item.TaxableAmount = 0m;
                    item.VatAmount = 0m;
                    item.TaxInclusiveAmount = item.LineAmount;
                }

                GrossValue = Math.Round(Cart.Sum(item => item.GrossAmount), 2);
                LineDiscountTotal = Math.Round(Cart.Sum(item => item.DiscountAmount), 2);
                TotalDiscount = LineDiscountTotal;
                NetValue = Math.Round(Cart.Sum(item => item.LineAmount), 2);
                TaxableAmountTotal = 0m;
                TotalVatAmount = 0m;
                ZeroRatedAmount = 0m;
                ExemptAmount = 0m;
                OutOfScopeAmount = 0m;
                IsTaxCalculationReady = false;
                TaxSummaryStatusText = "VAT summary finalized at checkout for Gift Voucher issue lines";

                if (IsPaymentModeActive)
                    RecalculatePaymentTotals();

                return;
            }

            if (hasFreeIssueLine)
            {
                decimal availableForInvoiceDiscountWithFreeLines = Math.Round(
                    Cart.Where(item => !item.IsFreeItem).Sum(item => item.LineAmount),
                    2);

                if (InvoiceDiscountAmount > availableForInvoiceDiscountWithFreeLines)
                {
                    InvoiceDiscountAmount = 0m;
                    _ = ShowNotificationAsync(
                        "Invoice discount was cleared because the paid cart value changed.",
                        "#F59E0B");
                }

                try
                {
                    List<(CartItem Item, int Index)> paidLines = Cart
                        .Select((item, index) => (Item: item, Index: index))
                        .Where(row => !row.Item.IsFreeItem)
                        .ToList();

                    foreach (CartItem freeItem in Cart.Where(item => item.IsFreeItem))
                    {
                        freeItem.InvoiceDiscountAllocation = 0m;
                        freeItem.TaxableAmount = 0m;
                        freeItem.VatAmount = 0m;
                        freeItem.TaxInclusiveAmount = 0m;
                    }

                    if (paidLines.Count == 0)
                    {
                        GrossValue = 0m;
                        LineDiscountTotal = 0m;
                        TotalDiscount = 0m;
                        NetValue = 0m;
                        TaxableAmountTotal = 0m;
                        TotalVatAmount = 0m;
                        ZeroRatedAmount = 0m;
                        ExemptAmount = 0m;
                        OutOfScopeAmount = 0m;
                        IsTaxCalculationReady = true;
                        TaxSummaryStatusText = "Free Issue — zero customer value and zero VAT";
                    }
                    else
                    {
                        List<SalesTaxLineInput> inputs = paidLines
                            .Select(row => new SalesTaxLineInput
                            {
                                LineKey = row.Index,
                                ItemVariantId = row.Item.ItemVariantId,
                                Quantity = row.Item.Quantity,
                                VatInclusiveUnitPrice = row.Item.UnitPrice,
                                LineDiscountAmount = row.Item.DiscountAmount,
                                TaxProfile = row.Item.TaxProfile
                            })
                            .ToList();

                        SalesTaxDocumentResult result = _salesTaxService.CalculateDocument(
                            inputs,
                            InvoiceDiscountAmount,
                            _isVatRegisteredStore);

                        foreach (SalesTaxLineResult lineResult in result.Lines)
                        {
                            CartItem item = Cart[lineResult.LineKey];
                            item.InvoiceDiscountAllocation = lineResult.InvoiceDiscountAllocation;
                            item.TaxableAmount = lineResult.TaxableAmount;
                            item.VatAmount = lineResult.VatAmount;
                            item.TaxInclusiveAmount = lineResult.TaxInclusiveAmount;
                        }

                        GrossValue = result.GrossTotal;
                        LineDiscountTotal = result.LineDiscountTotal;
                        TotalDiscount = result.TotalDiscount;
                        NetValue = result.NetTotal;
                        TaxableAmountTotal = result.StandardRatedAmount;
                        TotalVatAmount = result.TotalVat;
                        ZeroRatedAmount = result.ZeroRatedAmount;
                        ExemptAmount = result.ExemptAmount;
                        OutOfScopeAmount = result.OutOfScopeAmount;
                        IsTaxCalculationReady = true;
                        TaxSummaryStatusText = _isVatRegisteredStore
                            ? "VAT-inclusive paid lines; Free Issue value and VAT are zero"
                            : "Non-VAT store; Free Issue value is zero";
                    }
                }
                catch (Exception ex)
                {
                    IsTaxCalculationReady = false;
                    TaxSummaryStatusText = ex.Message;
                }

                if (IsPaymentModeActive)
                    RecalculatePaymentTotals();

                return;
            }

            decimal availableForInvoiceDiscount = Math.Round(
                Cart.Sum(item => item.LineAmount),
                2);

            if (InvoiceDiscountAmount >
                availableForInvoiceDiscount)
            {
                InvoiceDiscountAmount = 0m;
                _ = ShowNotificationAsync(
                    "Invoice discount was cleared because the cart value changed.",
                    "#F59E0B");
            }

            try
            {
                List<SalesTaxLineInput> inputs = Cart
                    .Select((item, index) =>
                        new SalesTaxLineInput
                        {
                            LineKey = index,
                            ItemVariantId =
                                item.ItemVariantId,
                            Quantity =
                                item.Quantity,
                            VatInclusiveUnitPrice =
                                item.UnitPrice,
                            LineDiscountAmount =
                                item.DiscountAmount,
                            TaxProfile =
                                item.TaxProfile
                        })
                    .ToList();

                SalesTaxDocumentResult result =
                    _salesTaxService.CalculateDocument(
                        inputs,
                        InvoiceDiscountAmount,
                        _isVatRegisteredStore);

                foreach (SalesTaxLineResult lineResult in
                         result.Lines)
                {
                    CartItem item =
                        Cart[lineResult.LineKey];

                    item.InvoiceDiscountAllocation =
                        lineResult.InvoiceDiscountAllocation;
                    item.TaxableAmount =
                        lineResult.TaxableAmount;
                    item.VatAmount =
                        lineResult.VatAmount;
                    item.TaxInclusiveAmount =
                        lineResult.TaxInclusiveAmount;
                }

                GrossValue = result.GrossTotal;
                LineDiscountTotal =
                    result.LineDiscountTotal;
                TotalDiscount = result.TotalDiscount;
                NetValue = result.NetTotal;
                TaxableAmountTotal =
                    result.StandardRatedAmount;
                TotalVatAmount = result.TotalVat;
                ZeroRatedAmount =
                    result.ZeroRatedAmount;
                ExemptAmount = result.ExemptAmount;
                OutOfScopeAmount =
                    result.OutOfScopeAmount;
                IsTaxCalculationReady = true;
                TaxSummaryStatusText =
                    _isVatRegisteredStore
                        ? "VAT-inclusive totals"
                        : "Non-VAT store / Out of Scope";
            }
            catch (Exception ex)
            {
                foreach (CartItem item in Cart)
                {
                    item.InvoiceDiscountAllocation = 0m;
                    item.TaxableAmount = 0m;
                    item.VatAmount = 0m;
                    item.TaxInclusiveAmount =
                        item.LineAmount;
                }

                GrossValue = Math.Round(
                    Cart.Sum(item => item.GrossAmount),
                    2);
                LineDiscountTotal = Math.Round(
                    Cart.Sum(item => item.DiscountAmount),
                    2);
                TotalDiscount = Math.Round(
                    LineDiscountTotal +
                    InvoiceDiscountAmount,
                    2);
                NetValue = Math.Round(
                    Math.Max(
                        0m,
                        Cart.Sum(item => item.LineAmount) -
                        InvoiceDiscountAmount),
                    2);
                TaxableAmountTotal = 0m;
                TotalVatAmount = 0m;
                ZeroRatedAmount = 0m;
                ExemptAmount = 0m;
                OutOfScopeAmount = 0m;
                IsTaxCalculationReady = false;
                TaxSummaryStatusText = ex.Message;
            }

            if (IsPaymentModeActive)
                RecalculatePaymentTotals();
        }

        public async Task<bool> FinalizeCheckoutAsync()
        {
            if (_isCheckoutInProgress)
            {
                _ = ShowNotificationAsync("Checkout is already processing.", "#F59E0B");
                return false;
            }

            if (!Cart.Any())
                return false;

            if (_currentShiftId == 0)
            {
                _ = ShowNotificationAsync("No active shift found.", "#EF4444");
                return false;
            }

            RecalculateTotals();

            if (!Cart.Any(item => item.IsGiftVoucherSale) &&
                !IsTaxCalculationReady)
            {
                _ = ShowNotificationAsync(
                    $"Tax calculation failed: {TaxSummaryStatusText}",
                    "#EF4444");
                return false;
            }

            if (!PaymentLines.Any() && NetValue > 0m)
            {
                _ = ShowNotificationAsync("No payment entered.", "#EF4444");
                return false;
            }

            if (BalanceDue > 0m)
            {
                _ = ShowNotificationAsync($"Balance due: Rs. {BalanceDue:N2}", "#EF4444");
                return false;
            }

            if (Cart.Any(c => c.RequiresStockBatch && c.ItemBatchId <= 0))
            {
                _ = ShowNotificationAsync("One or more Stock Item lines has no selected stock reference.", "#EF4444");
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

            if (Cart.Any(c => c.IsGiftVoucherSale) && PaymentLines.Any(p => p.IsGiftVoucher))
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

            var invalidSupplierClaimLine = Cart.FirstOrDefault(c => c.IsFreeItem && c.IsSupplierRecoverable && c.SupplierId <= 0);
            if (invalidSupplierClaimLine != null)
            {
                _ = ShowNotificationAsync($"Supplier missing for free issue: {invalidSupplierClaimLine.Description}", "#EF4444");
                return false;
            }

            var belowMinimumLine = Cart.FirstOrDefault(c => !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsBelowMinimumPrice);
            if (belowMinimumLine != null && !HasBelowMinimumApproval(belowMinimumLine))
            {
                _ = ShowNotificationAsync($"Price below minimum: {belowMinimumLine.Description}", "#EF4444");
                return false;
            }

            SetCheckoutInProgress(true);
            TerminalInputMode = "PROCESSING";
            TerminalInput = string.Empty;
            PaymentStatusText = "Processing sale. Please wait.";
            PaymentStatusColor = "#0F3B66";

            try
            {
                await FlushCartPersistenceAsync();

                string paymentMethod = PaymentLines.Count == 0
                    ? "No Charge"
                    : PaymentLines.Count == 1
                        ? PaymentLines.First().DisplayPaymentType
                        : "Split";
                CustomerSearchDto? activeCustomer = ActiveB2BCustomer;
                string customerName = activeCustomer == null ? "Walk-In" : activeCustomer.DisplayName;
                string customerCompanyName = activeCustomer?.CompanyName ?? string.Empty;
                string nicOrBr = string.Empty;

                if (activeCustomer != null)
                {
                    nicOrBr = activeCustomer.IsWholesale ? activeCustomer.BusinessRegistrationNumber : activeCustomer.NicNumber;

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
                    IsWholesaleSale = IsWholesaleMode,
                    GrossTotal = GrossValue,
                    TotalDiscount = TotalDiscount,
                    InvoiceDiscountAmount =
                        this.InvoiceDiscountAmount,
                    NetTotal = NetValue,
                    PaymentMethod = paymentMethod,
                    AmountTendered = CashTenderedTotal,
                    BalanceReturned = BalanceReturned,
                    CheckoutToken = _cartToken
                };

                var lines = Cart.Select(c => new SalesLine
                {
                    ItemVariantId = c.IsGiftVoucherSale ? null : c.ItemVariantId,
                    ItemBatchId =
                        c.IsGiftVoucherSale || c.IsService
                            ? null
                            : c.ItemBatchId,
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
                    DiscountRuleId = !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsRuleDiscount && c.DiscountRuleId > 0 ? c.DiscountRuleId : null,
                    DiscountRuleName = !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsRuleDiscount ? c.DiscountRuleName : string.Empty,
                    DiscountReasonId = !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsRuleDiscount && c.DiscountReasonId > 0 ? c.DiscountReasonId : null,
                    DiscountReasonCode = !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsRuleDiscount ? c.DiscountReasonCode : string.Empty,
                    DiscountReasonName = !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsRuleDiscount ? c.DiscountReasonName : string.Empty,
                    DiscountRequiresManagerApproval = !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsRuleDiscount && c.DiscountRequiresManagerApproval,
                    DiscountRequiresAdminApproval = !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsRuleDiscount && c.DiscountRequiresAdminApproval,
                    DiscountApprovedBy = !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsRuleDiscount ? c.DiscountApprovedBy : string.Empty,
                    DiscountApprovedAt = !c.IsGiftVoucherSale && !c.IsFreeItem && c.IsRuleDiscount ? c.DiscountApprovedAt : null,
                    OriginalUnitPrice = c.OriginalUnitPrice > 0m ? c.OriginalUnitPrice : c.UnitPrice,
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
                    FreeIssueAppliedBy = c.IsFreeItem ? c.FreeIssueAppliedBy : string.Empty,
                    FreeIssueAppliedAt = c.IsFreeItem ? c.FreeIssueAppliedAt : null,
                    FreeApprovedByUserId = c.IsFreeItem && c.FreeApprovedByUserId > 0 ? c.FreeApprovedByUserId : null,
                    FreeApprovedRole = c.IsFreeItem ? c.FreeApprovedRole : string.Empty,
                    FreeIssueRuleSnapshotJson = c.IsFreeItem ? c.FreeIssueRuleSnapshotJson : string.Empty,
                    FreeIssueSnapshotStatus = c.IsFreeItem ? c.FreeIssueSnapshotStatus : FreeIssueSnapshotStatusCodes.LegacyUnknown,
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
                    TenderedAmount = p.TenderedAmount > 0m ? p.TenderedAmount : p.Amount,
                    ChangeAmount = p.ChangeAmount,
                    CardLastDigits = p.IsCard ? p.CardLastDigits : string.Empty,
                    ReferenceNo = p.ReferenceNo,
                    BankOrCardType = string.IsNullOrWhiteSpace(p.BankOrCardType) ? p.CardType : p.BankOrCardType,
                    PaymentDate = p.PaymentDate ?? p.CreatedAt,
                    EnteredBy = CashierName,
                    TerminalNo = TerminalNo,
                    GiftVoucherId = p.IsGiftVoucher ? p.GiftVoucherId : null,
                    GiftVoucherNo = p.IsGiftVoucher ? p.GiftVoucherNo : string.Empty,
                    GiftVoucherBarcode = p.IsGiftVoucher ? p.GiftVoucherBarcode : string.Empty,
                    GiftVoucherAmount = p.IsGiftVoucher ? p.GiftVoucherAmount : 0m,
                    GiftVoucherForfeitedAmount = p.IsGiftVoucher ? p.GiftVoucherForfeitedAmount : 0m,
                    GiftVoucherAuthorizedBy = p.IsGiftVoucher ? p.GiftVoucherAuthorizedBy : string.Empty
                }).ToList();

                var savedReceipt =
                    await _salesRepository
                        .ProcessCheckoutAsync(
                            header,
                            lines,
                            payments,
                            _cartToken);

                InvoiceNo =
                    savedReceipt.InvoiceNo;

                string completionMessage =
                    "Payment successful. Ready for next customer.";

                string completionColor =
                    "#10B981";

                try
                {
                    await CompleteReceiptAndDrawerAsync(
                        savedReceipt,
                        payments);
                }
                catch (Exception hardwareException)
                {
                    LocalLogService.WriteException(
                        "Cashier",
                        "Receipt or drawer after completed sale",
                        hardwareException);

                    completionMessage =
                        "Payment was saved, but the receipt printer " +
                        "or cash drawer did not complete. " +
                        "Check Terminal Settings.";

                    completionColor =
                        "#F59E0B";
                }

                CompleteCartUiReset();
                SetManagerMode(false);

                _ = ShowNotificationAsync(
                    completionMessage,
                    completionColor);

                return true;
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Checkout transaction failed",
                    ex);

                _ = ShowNotificationAsync($"Transaction failed: {ex.Message}", "#EF4444");
                return false;
            }
            finally
            {
                SetCheckoutInProgress(false);

                if (IsPaymentModeActive)
                {
                    TerminalInputMode = "PAYMENT";
                    RecalculatePaymentTotals();
                }
                else
                {
                    TerminalInputMode = "READY TO SCAN";
                }
            }
        }

        public async Task<SalesHeader?> GetLastCompletedSaleAsync()
        {
            try
            {
                SalesHeader? sale =
                    await _salesDocumentRepository
                        .GetLastCompletedSaleAsync(TerminalNo);

                if (sale == null)
                {
                    _ = ShowNotificationAsync(
                        "No completed sale was found for this terminal.",
                        "#F59E0B");
                }

                return sale;
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Load last completed sale",
                    ex);

                _ = ShowNotificationAsync(
                    $"Last receipt could not be loaded: {ex.Message}",
                    "#EF4444");

                return null;
            }
        }

        public async Task<PreparedSalesDocument?> PrepareLastReceiptAsync()
        {
            SalesHeader? sale = await GetLastCompletedSaleAsync();

            if (sale == null)
                return null;

            return await _salesDocumentRepository
                .PrepareReceiptAsync(sale.Id);
        }

        public async Task<PreparedSalesDocument?>
            IssueOrPrepareTaxInvoiceAsync(
                TaxInvoiceIssueRequest request)
        {
            try
            {
                return await _salesDocumentRepository
                    .IssueOrPrepareTaxInvoiceAsync(request);
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Issue or prepare Tax Invoice",
                    ex);

                _ = ShowNotificationAsync(
                    ex.Message,
                    "#EF4444");

                return null;
            }
        }

        public Task<string> BuildDocumentPreviewAsync(
            PreparedSalesDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            if (string.Equals(
                    document.DocumentType,
                    SalesDocumentTypes.TaxInvoice,
                    StringComparison.Ordinal))
            {
                if (!document.TaxInvoiceIssuedAtUtc.HasValue)
                {
                    throw new InvalidOperationException(
                        "Tax Invoice issue time is missing.");
                }

                return _printService.BuildTaxInvoicePreviewAsync(
                    document.Sale,
                    document.TaxInvoiceIssuedAtUtc.Value,
                    _receiptPaperWidth,
                    document.CopyLabel);
            }

            return _printService.BuildReceiptPreviewAsync(
                document.Sale,
                _receiptPaperWidth,
                document.CopyLabel);
        }

        public async Task<bool> PrintPreparedDocumentAsync(
            PreparedSalesDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            if (string.IsNullOrWhiteSpace(_receiptPrinterName))
            {
                _ = ShowNotificationAsync(
                    "Receipt printer is not configured.",
                    "#EF4444");
                return false;
            }

            try
            {
                if (string.Equals(
                        document.DocumentType,
                        SalesDocumentTypes.TaxInvoice,
                        StringComparison.Ordinal))
                {
                    if (!document.TaxInvoiceIssuedAtUtc.HasValue)
                    {
                        throw new InvalidOperationException(
                            "Tax Invoice issue time is missing.");
                    }

                    await _printService.PrintTaxInvoiceAsync(
                        document.Sale,
                        document.TaxInvoiceIssuedAtUtc.Value,
                        _receiptPrinterName,
                        _receiptPaperWidth,
                        document.CopyLabel);
                }
                else
                {
                    await _printService.PrintReceiptAsync(
                        document.Sale,
                        _receiptPrinterName,
                        _receiptPaperWidth,
                        document.CopyLabel);
                }
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Sales document printing",
                    ex);

                try
                {
                    await _salesDocumentRepository.RecordPrintResultAsync(
                        document.Sale.Id,
                        document.DocumentType,
                        document.DocumentNumber,
                        false,
                        CashierName,
                        TerminalNo,
                        _receiptPrinterName,
                        ex.Message);
                }
                catch (Exception auditException)
                {
                    LocalLogService.WriteException(
                        "Cashier",
                        "Sales document print-failure audit",
                        auditException);
                }

                _ = ShowNotificationAsync(
                    "Document could not be printed. The completed sale was not duplicated.",
                    "#EF4444");

                return false;
            }

            try
            {
                await _salesDocumentRepository.RecordPrintResultAsync(
                    document.Sale.Id,
                    document.DocumentType,
                    document.DocumentNumber,
                    true,
                    CashierName,
                    TerminalNo,
                    _receiptPrinterName);
            }
            catch (Exception auditException)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Successful sales document print audit",
                    auditException);

                _ = ShowNotificationAsync(
                    "Document printed, but its print audit could not be saved.",
                    "#F59E0B");

                return false;
            }

            _ = ShowNotificationAsync(
                $"{document.CopyLabel} document printed.",
                "#10B981");

            return true;
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
                        DiscountAmount = item.FinalDiscountAmount,
                        LineTotal = item.FinalLineAmount
                    }).ToList()
                };

                if (string.IsNullOrWhiteSpace(
                        _receiptPrinterName))
                {
                    throw new InvalidOperationException(
                        "No receipt printer is configured in Terminal Settings.");
                }

                await _printService
                    .PrintQuotationAsync(
                        request,
                        _receiptPrinterName,
                        _receiptPaperWidth);
                _ = ShowNotificationAsync("Quotation printed. No sale saved and no stock deducted.", "#10B981");
                return true;
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Quotation printing",
                    ex);

                _ = ShowNotificationAsync(
                    "Quotation could not be printed. " +
                    "Check Terminal Settings.",
                    "#EF4444");

                return false;
            }
        }

        private async Task CompleteReceiptAndDrawerAsync(
            SalesHeader savedReceipt,
            IReadOnlyCollection<SalesPayment> payments)
        {
            bool cashWasAccepted =
                payments.Any(
                    payment =>
                        payment.Amount > 0m &&
                        string.Equals(
                            payment.PaymentType,
                            "Cash",
                            StringComparison
                                .OrdinalIgnoreCase));

            if (_autoPrintReceipt &&
                string.IsNullOrWhiteSpace(
                    _receiptPrinterName))
            {
                throw new InvalidOperationException(
                    "Receipt printer is not configured.");
            }

            if (_autoPrintReceipt)
            {
                for (int copy = 0;
                     copy < _receiptCopies;
                     copy++)
                {
                    PreparedSalesDocument document =
                        await _salesDocumentRepository
                            .PrepareReceiptAsync(savedReceipt.Id);

                    bool printed =
                        await PrintPreparedDocumentAsync(document);

                    if (!printed)
                    {
                        throw new InvalidOperationException(
                            "Automatic receipt printing failed.");
                    }
                }
            }

            if (_enableCashDrawer &&
                _openDrawerAfterCashSale &&
                cashWasAccepted)
            {
                try
                {
                    await _drawerAuditService.OpenAsync(
                        _currentShiftId,
                        TerminalNo,
                        CashierName,
                        CashDrawerEventTypeCodes.CashSale,
                        "Automatic cash-sale drawer open",
                        savedReceipt.InvoiceNo,
                        string.Empty,
                        salesHeaderId: savedReceipt.Id);
                }
                catch (Exception ex)
                {
                    LocalLogService.WriteException(
                        "Cashier",
                        "Automatic cash drawer open",
                        ex);

                    _ = ShowNotificationAsync(
                        "Sale completed, but the cash drawer did not open.",
                        "#F59E0B");
                }
            }
        }

        public async Task OpenAuditedDrawerAsync(
            string eventType,
            string reason,
            string note,
            string authorizedBy)
        {
            if (_currentShiftId <= 0)
                throw new InvalidOperationException("No active shift found.");

            await _drawerAuditService.OpenAsync(
                _currentShiftId,
                TerminalNo,
                CashierName,
                eventType,
                reason,
                note,
                authorizedBy);
        }

        private static string FirstNonEmpty(
            params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(
                        value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static string GenerateTemporaryQuotationNo()
        {
            return $"QT-{DateTime.Now:yyyyMMdd-HHmmss}";
        }
    }
}
