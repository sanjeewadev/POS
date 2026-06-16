using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Cashier.UI.Models;
using POS.Core.Models;
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

        [ObservableProperty] private string _customerName = "Walk-In";
        [ObservableProperty] private int _loyaltyPoints = 0;

        [ObservableProperty] private string _terminalNo = "01";
        [ObservableProperty] private string _cashierName = "Pending...";
        [ObservableProperty] private string _invoiceNo = "PENDING...";
        [ObservableProperty] private DateTime _currentDate = DateTime.Now;

        // --- NEW: SECURITY & PRIVILEGE ESCALATION PROPERTIES ---
        [ObservableProperty] private bool _isManagerModeActive = false;
        [ObservableProperty] private string _securityStatusMode = "CASHIER MODE";
        [ObservableProperty] private bool _isTerminalLocked = false;

        private int _currentShiftId = 0;
        public int CurrentShiftId => _currentShiftId; // Accessible by other dialogs if needed
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
        // SECURITY & SECURITY ELEVATION ENGINES
        // ==========================================

        /// <summary>
        /// Explicitly toggles persistent manager administration sessions.
        /// </summary>
        public void SetManagerMode(bool activate)
        {
            IsManagerModeActive = activate;
            SecurityStatusMode = activate ? "⚠️ MANAGER MODE ACTIVE" : "CASHIER MODE";
        }

        /// <summary>
        /// Native intercept handler to quickly evaluate authorization permissions.
        /// </summary>
        public bool VerifyActionPermission()
        {
            // If already inside an active manager session, seamlessly allow the action
            if (IsManagerModeActive) return true;

            // Otherwise, trigger the native override intercept request
            return false;
        }

        // ==========================================
        // SHIFT & SESSION TASK MANAGEMENT COMMANDS
        // ==========================================

        [RelayCommand]
        public async Task AddFloatAsync()
        {
            // Shift menu calls this directly
            var floatDialog = new POS.Cashier.UI.Views.Dialogs.CashDenominationDialog();
            if (floatDialog.ShowDialog() == true)
            {
                decimal floatAmount = floatDialog.FinalTotal;
                await _tillRepository.InjectFloatAsync(_currentShiftId, floatAmount, IsManagerModeActive ? "Manager" : "Cashier");
                MessageBox.Show($"Float of Rs. {floatAmount:N2} injected successfully.", "Float Added", MessageBoxButton.OK, MessageBoxImage.Information);
            }
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
            var counterDialog = new POS.Cashier.UI.Views.Dialogs.CashDenominationDialog();
            if (counterDialog.ShowDialog() == true)
            {
                decimal countedCash = counterDialog.FinalTotal;
                var summary = await _tillRepository.GetShiftSummaryAsync(_currentShiftId);
                decimal expectedCash = summary?.ExpectedCash ?? 0m;

                var summaryDialog = new POS.Cashier.UI.Views.Dialogs.ZReportSummaryDialog(expectedCash, countedCash);
                if (summaryDialog.ShowDialog() == true)
                {
                    await _tillRepository.CloseShiftAsync(_currentShiftId, countedCash);
                    MessageBox.Show("Shift Closed successfully. Z-Report Printed. Logging out.", "Shift Closed", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Drop security state before window shutdown
                    SetManagerMode(false);
                    Application.Current.MainWindow.Close();
                }
            }
        }

        // ==========================================
        // SCANNER ENGINE
        // ==========================================
        public async Task ProcessBarcodeAsync(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return;

            if (_currentShiftId == 0)
            {
                MessageBox.Show("Critical Error: No active shift found in database. Please log out and contact a manager.", "Action Blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    MessageBox.Show($"Unrecognized Barcode: {barcode}", "Item Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

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
                MessageBox.Show($"Database Error: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("No active shift found. Please log in properly.", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            try
            {
                var header = new SalesHeader
                {
                    ShiftSessionId = _currentShiftId,
                    CashierName = this.CashierName,
                    TerminalNo = this.TerminalNo,
                    CustomerName = this.CustomerName,
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

                MessageBox.Show("Payment Successful! Ready for next customer.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                ClearCart();

                // AUTOMATIC AUTO-LOCK INTERCEPT: Strips temporary privilege elevation instantly when a transaction finishes
                SetManagerMode(false);

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Transaction Failed: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            CustomerName = "Walk-In";
            LoyaltyPoints = 0;
            InvoiceNo = "PENDING...";
        }

        [RelayCommand]
        public void RemoveItem(CartItem item)
        {
            if (item != null && Cart.Contains(item)) Cart.Remove(item);
        }

        public void AssignCustomer(string name, int points)
        {
            CustomerName = string.IsNullOrWhiteSpace(name) ? "Walk-In" : name;
            LoyaltyPoints = points;
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

        // ==========================================
        // MANUAL ENTRY & SEEK ENGINE
        // ==========================================
        // ==========================================
        // MANUAL ENTRY & SEEK ENGINE
        // ==========================================
        public async Task<bool> AddSpecificItemToCartAsync(string barcode, int quantity)
        {
            if (string.IsNullOrWhiteSpace(barcode) || quantity <= 0) return false;

            // 1. HARD GATE: Prevent adding if the shift is closed, but fail silently.
            if (_currentShiftId == 0) return false;

            try
            {
                // 2. QUICK ADD: If already in cart, just bump the quantity
                var existingItem = Cart.FirstOrDefault(c => c.Barcode == barcode);
                if (existingItem != null)
                {
                    existingItem.Quantity += quantity;
                    RecalculateTotals();
                    return true; // Success
                }

                // 3. NEW ITEM: Fetch from database
                var dbItem = await _itemRepository.GetItemByBarcodeAsync(barcode);

                // Fail silently if not found (the UI already validated it, so this is a double-check)
                if (dbItem == null) return false;

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
                return true; // Success
            }
            catch
            {
                // CRITICAL FAIL-SAFE: If the database glitches, catch it silently 
                // so the POS doesn't lock up with an unhandled exception during a rush.
                return false;
            }
        }
    }

}