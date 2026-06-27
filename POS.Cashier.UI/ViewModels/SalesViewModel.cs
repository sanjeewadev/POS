using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using POS.Cashier.UI.Messages;
using POS.Cashier.UI.Models;
using POS.Core.Models;
using POS.Core.Models.DTOs; // Added for CustomerSearchDto
using POS.Core.Repositories;
using POS.Cashier.UI.Services;

namespace POS.Cashier.UI.ViewModels
{
    public partial class SalesViewModel : ObservableObject
    {
        private readonly ItemMasterRepository _itemRepository;
        private readonly SalesRepository _salesRepository;
        private readonly TillRepository _tillRepository;
        private readonly IReceiptPrintService _printService;

        public ObservableCollection<CartItem> Cart { get; } = new();

        // --- STATE MANAGEMENT PROPERTIES ---
        [ObservableProperty] private decimal _netValue = 0.00m;
        [ObservableProperty] private decimal _totalDiscount = 0.00m;
        [ObservableProperty] private int _totalItems = 0;
        [ObservableProperty] private int _totalPieces = 0;

        [ObservableProperty] private string _terminalNo = "01";
        [ObservableProperty] private string _cashierName = "Pending...";
        [ObservableProperty] private string _invoiceNo = "PENDING...";
        [ObservableProperty] private DateTime _currentDate = DateTime.Now;

        // --- NEW: GLOBAL TERMINAL INPUT (HYBRID DISPLAY) ---
        [ObservableProperty] private string _terminalInput = string.Empty;

        // --- NEW: B2B & WHOLESALE ENGINE ---
        [ObservableProperty] private string _customerName = "Walk-In";
        [ObservableProperty] private int _loyaltyPoints = 0;
        [ObservableProperty] private bool _isWholesaleMode = false;
        [ObservableProperty] private CustomerSearchDto? _activeB2BCustomer;

        // --- NEW: SECURITY & PRIVILEGE ESCALATION PROPERTIES ---
        [ObservableProperty] private bool _isManagerModeActive = false;
        [ObservableProperty] private string _securityStatusMode = "CASHIER MODE";
        [ObservableProperty] private bool _isTerminalLocked = false;

        // --- NEW: TOP-BAR NOTIFICATION ENGINE ---
        [ObservableProperty] private string _notificationMessage = string.Empty;
        [ObservableProperty] private string _notificationColor = "#10B981"; // Default Green
        [ObservableProperty] private bool _isNotificationVisible = false;

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

            Cart.CollectionChanged += (s, e) => RecalculateTotals();
            _ = LoadActiveShiftAsync();

            // EXPRESS ITEMS LISTENER
            WeakReferenceMessenger.Default.Register<AddToCartMessage>(this, (r, m) =>
            {
                System.Media.SystemSounds.Beep.Play();
                _ = ProcessBarcodeAsync(m.Value);
            });

            // TOP-BAR NOTIFICATION LISTENER
            WeakReferenceMessenger.Default.Register<TopBarNotificationMessage>(this, (r, m) =>
            {
                _ = ShowNotificationAsync(m.Value.Message, m.Value.ColorHex);
            });
        }

        // ==========================================
        // NOTIFICATION ANIMATION ENGINE
        // ==========================================
        public async Task ShowNotificationAsync(string message, string colorHex = "#10B981")
        {
            NotificationMessage = message;
            NotificationColor = colorHex;
            IsNotificationVisible = true;

            await Task.Delay(3000);
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

        // ==========================================
        // WHOLESALE & B2B ENGINE
        // ==========================================
        public void AttachB2BCustomer(CustomerSearchDto customer)
        {
            ActiveB2BCustomer = customer;
            CustomerName = string.IsNullOrWhiteSpace(customer.CompanyName) ? customer.FullName : customer.CompanyName;

            if (customer.CustomerType == "Wholesale")
            {
                IsWholesaleMode = true;
                _ = ShowNotificationAsync($"WHOLESALE MODE ACTIVATED: {CustomerName}", "#3B82F6"); // Blue Alert

                // TODO: Future Implementation! 
                // If ItemVariant has a "WholesalePrice" column, loop through Cart here 
                // and swap unit prices from Retail to Wholesale.
            }
            else
            {
                IsWholesaleMode = false;
                _ = ShowNotificationAsync($"Retail Account Linked: {CustomerName}", "#10B981"); // Green Alert
            }
        }

        [RelayCommand]
        public void DetachCustomer()
        {
            ActiveB2BCustomer = null;
            CustomerName = "Walk-In";
            IsWholesaleMode = false;

            // TODO: If you swapped prices to wholesale, swap them back to retail here!
            RecalculateTotals();

            _ = ShowNotificationAsync("Customer detached. Returned to Standard Retail Mode.", "#F59E0B"); // Orange Alert
        }

        // ==========================================
        // SECURITY & SECURITY ELEVATION ENGINES
        // ==========================================
        public void SetManagerMode(bool activate)
        {
            IsManagerModeActive = activate;
            SecurityStatusMode = activate ? "⚠️ MANAGER MODE ACTIVE" : "CASHIER MODE";
        }

        public bool VerifyActionPermission()
        {
            if (IsManagerModeActive) return true;
            return false;
        }

        // ==========================================
        // TERMINAL INPUT COMMANDS
        // ==========================================
        [RelayCommand]
        public void ClearTerminalInput()
        {
            TerminalInput = string.Empty;
        }

        // ==========================================
        // SHIFT & SESSION TASK MANAGEMENT COMMANDS
        // ==========================================
        [RelayCommand]
        public async Task AddFloatAsync()
        {
            if (_currentShiftId == 0)
            {
                _ = ShowNotificationAsync("No active shift found. Please log in properly.", "#EF4444");
                return;
            }

            if (!VerifyActionPermission())
            {
                var authVM = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<POS.Cashier.UI.ViewModels.ManagerAuthViewModel>(App.Services!);
                var authDialog = new POS.Cashier.UI.Dialogs.ManagerAuthDialogView(authVM);

                if (authDialog.ShowDialog() != true) return;
            }

            var floatVM = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<POS.Cashier.UI.ViewModels.FloatCashViewModel>(App.Services!);
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
                MessageBox.Show($"Expected Cash in Drawer: Rs. {summary.ExpectedCash:N2}\n\n(Printing X-Report...)", "X-Report", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        public async Task ProcessZReportAsync()
        {
            // Logic handled when Shift Close dialog is finalized
        }

        // ==========================================
        // SCANNER ENGINE
        // ==========================================
        public async Task ProcessBarcodeAsync(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return;

            if (_currentShiftId == 0)
            {
                _ = ShowNotificationAsync("Action Blocked: No active shift found in database.", "#EF4444");
                return;
            }

            var existingItem = Cart.FirstOrDefault(c => c.Barcode == barcode);
            if (existingItem != null)
            {
                existingItem.Quantity++;
                RecalculateTotals();
                return;
            }

            try
            {
                var dbItem = await _itemRepository.GetItemByBarcodeAsync(barcode);

                if (dbItem == null)
                {
                    _ = ShowNotificationAsync($"Unrecognized Barcode: {barcode}", "#F59E0B");
                    return;
                }

                // TODO: If IsWholesaleMode == true, use dbItem.WholesalePrice instead!
                Cart.Add(new CartItem
                {
                    ItemId = dbItem.Id,
                    Barcode = dbItem.SkuCode ?? barcode,
                    Description = dbItem.ItemParent?.ItemName ?? "Unknown Item",
                    UnitPrice = dbItem.RetailPrice,
                    Quantity = 1,
                    DiscountPercentage = 0
                });
            }
            catch (Exception ex)
            {
                _ = ShowNotificationAsync($"Database Error: {ex.Message}", "#EF4444");
            }
        }

        // ==========================================
        // MATH ENGINE
        // ==========================================
        public void RecalculateTotals()
        {
            if (Cart == null || !Cart.Any())
            {
                NetValue = 0m;
                TotalDiscount = 0m;
                TotalItems = 0;
                TotalPieces = 0;
                return;
            }

            NetValue = Cart.Sum(c => c.LineAmount);
            TotalItems = Cart.Count;
            TotalPieces = Cart.Sum(c => c.Quantity);
            TotalDiscount = Cart.Sum(c => (c.UnitPrice * c.Quantity) - c.LineAmount);
        }

        // ==========================================
        // CHECKOUT ENGINE
        // ==========================================
        public async Task<bool> FinalizeCheckoutAsync(string paymentMethod, decimal amountTendered)
        {
            if (!Cart.Any()) return false;

            if (_currentShiftId == 0)
            {
                _ = ShowNotificationAsync("No active shift found. Please log in properly.", "#EF4444");
                return false;
            }

            try
            {
                var header = new SalesHeader
                {
                    ShiftSessionId = _currentShiftId,
                    CashierName = this.CashierName,
                    TerminalNo = this.TerminalNo,
                    CustomerName = this.CustomerName, // Optionally link ActiveB2BCustomer.Id to a DB column here
                    GrossTotal = Cart.Sum(c => c.UnitPrice * c.Quantity),
                    TotalDiscount = this.TotalDiscount,
                    NetTotal = this.NetValue,
                    PaymentMethod = paymentMethod,
                    AmountTendered = amountTendered,
                    BalanceReturned = amountTendered - this.NetValue
                };

                var lines = Cart.Select(c => new SalesLine
                {
                    ItemBatchId = c.ItemId,
                    ItemDescription = c.Description,
                    Quantity = c.Quantity,
                    UnitPrice = c.UnitPrice,
                    DiscountPercentage = c.DiscountPercentage,
                    DiscountAmount = (c.UnitPrice * c.Quantity) - c.LineAmount,
                    LineTotal = c.LineAmount
                }).ToList();

                var savedReceipt = await _salesRepository.ProcessCheckoutAsync(header, lines);

                await _printService.PrintReceiptAsync(savedReceipt, _printerName);

                _ = ShowNotificationAsync("Payment Successful! Ready for next customer.", "#10B981");

                ClearCart();
                SetManagerMode(false);

                return true;
            }
            catch (Exception ex)
            {
                _ = ShowNotificationAsync($"Transaction Failed: {ex.Message}", "#EF4444");
                return false;
            }
        }

        // ==========================================
        // CART & DIALOG MANIPULATION
        // ==========================================
        [RelayCommand]
        public void ClearCart()
        {
            Cart.Clear();

            // Wipe B2B / Wholesale state
            ActiveB2BCustomer = null;
            CustomerName = "Walk-In";
            IsWholesaleMode = false;

            LoyaltyPoints = 0;
            InvoiceNo = "PENDING...";
        }

        [RelayCommand]
        public void RemoveItem(CartItem item)
        {
            if (item != null && Cart.Contains(item)) Cart.Remove(item);
        }

        public void ApplyGlobalDiscount(decimal value, bool isPercentage)
        {
            if (!Cart.Any()) return;

            if (isPercentage)
            {
                foreach (var item in Cart) item.DiscountPercentage = value;
            }
            else
            {
                decimal cartSubtotal = Cart.Sum(c => c.Quantity * c.UnitPrice);
                if (cartSubtotal == 0) return;

                decimal discountFactor = value / cartSubtotal;
                foreach (var item in Cart) item.DiscountPercentage = discountFactor * 100m;
            }
            RecalculateTotals();
        }

        public async Task<bool> AddSpecificItemToCartAsync(string barcode, int quantity)
        {
            if (string.IsNullOrWhiteSpace(barcode) || quantity <= 0) return false;
            if (_currentShiftId == 0) return false;

            try
            {
                var existingItem = Cart.FirstOrDefault(c => c.Barcode == barcode);
                if (existingItem != null)
                {
                    existingItem.Quantity += quantity;
                    RecalculateTotals();
                    return true;
                }

                var dbItem = await _itemRepository.GetItemByBarcodeAsync(barcode);
                if (dbItem == null) return false;

                // TODO: Check IsWholesaleMode here too for price swap!
                Cart.Add(new CartItem
                {
                    ItemId = dbItem.Id,
                    Barcode = dbItem.SkuCode ?? barcode,
                    Description = dbItem.ItemParent?.ItemName ?? "Unknown Item",
                    UnitPrice = dbItem.RetailPrice,
                    Quantity = quantity,
                    DiscountPercentage = 0
                });

                RecalculateTotals();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void ApplyFreeItemLogic(dynamic item, string reasonCode)
        {
            item.UnitPrice = 0m;
            item.LineAmount = 0m;
            item.DiscountPercentage = 100m;
            item.FreeReasonCode = reasonCode;
        }
    }
}