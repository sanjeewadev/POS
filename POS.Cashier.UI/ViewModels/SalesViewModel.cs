using System;
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
        // Keyboard, scanner, and touch numpad should all write here.
        // =========================================================

        [ObservableProperty]
        private string _terminalInput = string.Empty;

        [ObservableProperty]
        private string _terminalInputMode = "SCAN / QTY";

        // =========================================================
        // CUSTOMER / WHOLESALE
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

        private string _printerName = "POS-80";

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
                e.PropertyName == nameof(CartItem.LineAmount))
            {
                RecalculateTotals();
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

            if (string.IsNullOrWhiteSpace(input))
                return;

            TerminalInput = string.Empty;

            // Practical POS rule:
            // short numeric input + selected cart row = quantity edit.
            // long input = barcode/SKU scan.
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
            if (SelectedCartItem == null)
            {
                _ = ShowNotificationAsync("Select an item before changing quantity.", "#F59E0B");
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

        public void ApplyTerminalInputAsDiscountPercentToSelected()
        {
            if (SelectedCartItem == null)
            {
                _ = ShowNotificationAsync("Select an item before applying discount.", "#F59E0B");
                TerminalInput = string.Empty;
                return;
            }

            if (!decimal.TryParse(TerminalInput, out decimal discount) ||
                discount < 0 ||
                discount > 100)
            {
                _ = ShowNotificationAsync("Enter a valid discount percentage.", "#F59E0B");
                TerminalInput = string.Empty;
                return;
            }

            SelectedCartItem.DiscountPercentage = discount;
            TerminalInput = string.Empty;

            RecalculateTotals();

            _ = ShowNotificationAsync($"Discount applied: {discount:N0}%", "#10B981");
        }

        public void IncreaseSelectedQuantity(decimal amount = 1m)
        {
            if (SelectedCartItem == null)
            {
                _ = ShowNotificationAsync("Select an item first.", "#F59E0B");
                return;
            }

            SetSelectedLineQuantity(SelectedCartItem.Quantity + amount);
        }

        public void DecreaseSelectedQuantity(decimal amount = 1m)
        {
            if (SelectedCartItem == null)
            {
                _ = ShowNotificationAsync("Select an item first.", "#F59E0B");
                return;
            }

            decimal newQty = SelectedCartItem.Quantity - amount;

            if (newQty <= 0)
            {
                RemoveItem(SelectedCartItem);
                return;
            }

            SetSelectedLineQuantity(newQty);
        }

        private void SetSelectedLineQuantity(decimal qty)
        {
            if (SelectedCartItem == null)
                return;

            if (qty <= 0)
            {
                _ = ShowNotificationAsync("Quantity must be greater than zero.", "#F59E0B");
                return;
            }

            if (qty > SelectedCartItem.AvailableStock)
            {
                _ = ShowNotificationAsync(
                    $"Only {SelectedCartItem.AvailableStock:N3} available in stock.",
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
        // CUSTOMER / WHOLESALE
        // =========================================================

        public void AttachB2BCustomer(CustomerSearchDto customer)
        {
            ActiveB2BCustomer = customer;
            CustomerName = string.IsNullOrWhiteSpace(customer.CompanyName)
                ? customer.FullName
                : customer.CompanyName;

            if (customer.CustomerType == "Wholesale")
            {
                IsWholesaleMode = true;

                foreach (var item in Cart)
                {
                    if (item.WholesalePrice > 0)
                        item.UnitPrice = item.WholesalePrice;
                }

                RecalculateTotals();

                _ = ShowNotificationAsync($"WHOLESALE MODE: {CustomerName}", "#3B82F6");
            }
            else
            {
                IsWholesaleMode = false;

                foreach (var item in Cart)
                {
                    if (item.RetailPrice > 0)
                        item.UnitPrice = item.RetailPrice;
                }

                RecalculateTotals();

                _ = ShowNotificationAsync($"Customer linked: {CustomerName}", "#10B981");
            }
        }

        [RelayCommand]
        public void DetachCustomer()
        {
            ActiveB2BCustomer = null;
            CustomerName = "Walk-In";
            IsWholesaleMode = false;

            foreach (var item in Cart)
            {
                if (item.RetailPrice > 0)
                    item.UnitPrice = item.RetailPrice;
            }

            RecalculateTotals();

            _ = ShowNotificationAsync("Customer detached. Retail mode active.", "#F59E0B");
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

                await AddVariantToCartAsync(item.VariantId, 1);
            }
            catch (Exception ex)
            {
                _ = ShowNotificationAsync($"Database Error: {ex.Message}", "#EF4444");
            }
        }

        public async Task AddVariantToCartAsync(int itemVariantId, decimal quantity = 1m)
        {
            if (itemVariantId <= 0)
                return;

            if (quantity <= 0)
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

                if (item.StockOnHand <= 0)
                {
                    _ = ShowNotificationAsync($"Out of stock: {item.DisplayDescription}", "#F59E0B");
                    return;
                }

                var existingItem = Cart.FirstOrDefault(c => c.ItemVariantId == item.VariantId);

                if (existingItem != null)
                {
                    if (existingItem.Quantity + quantity > item.StockOnHand)
                    {
                        _ = ShowNotificationAsync($"Only {item.StockOnHand:N3} available in stock.", "#F59E0B");
                        return;
                    }

                    existingItem.Quantity += quantity;
                    SelectedCartItem = existingItem;
                    RecalculateTotals();

                    _ = ShowNotificationAsync($"{item.DisplayDescription} quantity updated.", "#10B981");
                    return;
                }

                if (quantity > item.StockOnHand)
                {
                    _ = ShowNotificationAsync($"Only {item.StockOnHand:N3} available in stock.", "#F59E0B");
                    return;
                }

                decimal sellingPrice = IsWholesaleMode && item.WholesalePrice > 0
                    ? item.WholesalePrice
                    : item.RetailPrice;

                var cartItem = new CartItem
                {
                    ItemVariantId = item.VariantId,
                    ItemCode = item.ItemCode,
                    SkuCode = item.SkuCode,
                    Barcode = string.IsNullOrWhiteSpace(item.Barcode) ? item.SkuCode : item.Barcode,

                    Description = item.DisplayDescription,
                    VariantDescription = item.VariantDescription,
                    Uom = item.Uom,

                    RetailPrice = item.RetailPrice,
                    WholesalePrice = item.WholesalePrice,
                    MinimumPrice = item.MinimumPrice,
                    MaximumPrice = item.MaximumPrice,

                    UnitPrice = sellingPrice,
                    Quantity = quantity,
                    DiscountPercentage = 0m,
                    ManualDiscountAmount = 0m,
                    AvailableStock = item.StockOnHand
                };

                Cart.Add(cartItem);
                SelectedCartItem = cartItem;

                RecalculateTotals();

                _ = ShowNotificationAsync($"Added: {item.DisplayDescription}", "#10B981");
            }
            catch (Exception ex)
            {
                _ = ShowNotificationAsync($"Add item failed: {ex.Message}", "#EF4444");
            }
        }

        public async Task<bool> AddSpecificItemToCartAsync(string barcode, int quantity)
        {
            if (string.IsNullOrWhiteSpace(barcode) || quantity <= 0)
                return false;

            if (_currentShiftId == 0)
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
                return;
            }

            GrossValue = Math.Round(Cart.Sum(c => c.GrossAmount), 2);
            NetValue = Math.Round(Cart.Sum(c => c.LineAmount), 2);
            TotalDiscount = Math.Round(Cart.Sum(c => c.DiscountAmount), 2);

            TotalItems = Cart.Count;
            TotalPieces = Cart.Sum(c => c.Quantity);
        }

        // =========================================================
        // CHECKOUT ENGINE
        // NOTE:
        // This still needs the next sale-flow update.
        // Current cart stores ItemVariantId, while final posting must allocate FIFO batches.
        // =========================================================

        public async Task<bool> FinalizeCheckoutAsync(string paymentMethod, decimal amountTendered)
        {
            if (!Cart.Any())
                return false;

            if (_currentShiftId == 0)
            {
                _ = ShowNotificationAsync("No active shift found.", "#EF4444");
                return false;
            }

            try
            {
                var header = new SalesHeader
                {
                    ShiftSessionId = _currentShiftId,
                    CashierName = CashierName,
                    TerminalNo = TerminalNo,
                    CustomerName = CustomerName,
                    GrossTotal = GrossValue,
                    TotalDiscount = TotalDiscount,
                    NetTotal = NetValue,
                    PaymentMethod = paymentMethod,
                    AmountTendered = amountTendered,
                    BalanceReturned = amountTendered - NetValue
                };

                // Temporary compatibility only.
                // We will replace this in the next step with FIFO batch allocation.
                var lines = Cart.Select(c => new SalesLine
                {
                    ItemBatchId = c.ItemId,
                    ItemDescription = c.Description,
                    Quantity = c.Quantity,
                    UnitPrice = c.UnitPrice,
                    DiscountPercentage = c.DiscountPercentage,
                    DiscountAmount = c.DiscountAmount,
                    LineTotal = c.LineAmount
                }).ToList();

                var savedReceipt = await _salesRepository.ProcessCheckoutAsync(header, lines);

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

        // =========================================================
        // CART COMMANDS
        // =========================================================

        [RelayCommand]
        public void ClearCart()
        {
            Cart.Clear();

            SelectedCartItem = null;
            TerminalInput = string.Empty;

            ActiveB2BCustomer = null;
            CustomerName = "Walk-In";
            IsWholesaleMode = false;

            LoyaltyPoints = 0;
            InvoiceNo = "PENDING...";

            RecalculateTotals();
        }

        [RelayCommand]
        public void RemoveItem(CartItem? item)
        {
            if (item == null)
                return;

            if (!Cart.Contains(item))
                return;

            int index = Cart.IndexOf(item);

            Cart.Remove(item);

            if (Cart.Any())
            {
                int newIndex = Math.Min(index, Cart.Count - 1);
                SelectedCartItem = Cart[newIndex];
            }
            else
            {
                SelectedCartItem = null;
            }

            RecalculateTotals();
        }

        public void RemoveSelectedItem()
        {
            RemoveItem(SelectedCartItem);
        }

        public void ApplyGlobalDiscount(decimal value, bool isPercentage)
        {
            if (!Cart.Any())
                return;

            if (value < 0)
                return;

            if (isPercentage)
            {
                if (value > 100)
                    value = 100;

                foreach (var item in Cart)
                    item.DiscountPercentage = value;
            }
            else
            {
                decimal cartSubtotal = Cart.Sum(c => c.Quantity * c.UnitPrice);

                if (cartSubtotal <= 0)
                    return;

                decimal discountFactor = value / cartSubtotal;

                foreach (var item in Cart)
                    item.DiscountPercentage = discountFactor * 100m;
            }

            RecalculateTotals();
        }

        public void ApplyFreeItemLogic(dynamic item, string reasonCode)
        {
            if (item is not CartItem cartItem)
                return;

            cartItem.UnitPrice = 0m;
            cartItem.DiscountPercentage = 100m;
            cartItem.ManualDiscountAmount = 0m;
            cartItem.IsFreeItem = true;
            cartItem.FreeReasonCode = reasonCode ?? string.Empty;

            RecalculateTotals();

            _ = ShowNotificationAsync($"Free item applied: {cartItem.Description}", "#10B981");
        }
    }
}