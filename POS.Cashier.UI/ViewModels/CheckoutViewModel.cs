using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;

namespace POS.Cashier.UI.ViewModels
{
    public partial class CheckoutViewModel : ObservableObject
    {
        // ==========================================
        // 1. HEADER & CRM PROPERTIES
        // ==========================================
        [ObservableProperty]
        private string _invoiceNo = string.Empty;

        [ObservableProperty]
        private string _customerName = "Walk-In";

        [ObservableProperty]
        private int _loyaltyPoints = 0;

        // ==========================================
        // 2. FINANCIAL MATH PROPERTIES
        // ==========================================
        [ObservableProperty]
        private decimal _totalDue;

        [ObservableProperty]
        private decimal _totalTendered;

        [ObservableProperty]
        private decimal _balanceAmount;

        [ObservableProperty]
        private string _balanceLabel = "REMAINING";

        [ObservableProperty]
        private bool _isChangeDue = false;

        [ObservableProperty]
        private string _tenderedInput = "0";

        // ==========================================
        // 3. QUICK TENDER BUTTONS (DYNAMIC)
        // ==========================================
        [ObservableProperty]
        private decimal _quickTender1;

        [ObservableProperty]
        private decimal _quickTender2;

        [ObservableProperty]
        private decimal _quickTender3;

        // ==========================================
        // 4. THE PAYMENT BASKET
        // ==========================================
        public ObservableCollection<SalesPayment> AppliedPayments { get; } = new();

        // Used to signal the Window to close
        public Action<bool>? RequestClose { get; set; }

        // ==========================================
        // INITIALIZATION
        // ==========================================
        public CheckoutViewModel(string invoiceNo, string customerName, int loyaltyPoints, decimal netTotal)
        {
            InvoiceNo = invoiceNo;
            CustomerName = customerName;
            LoyaltyPoints = loyaltyPoints;
            TotalDue = netTotal;

            GenerateQuickTenderAmounts(netTotal);
            RecalculateTotals();
        }

        // Calculates smart cash buttons based on the bill (e.g., if bill is 1450 -> 1500, 2000, 5000)
        private void GenerateQuickTenderAmounts(decimal total)
        {
            if (total <= 500) { QuickTender1 = 500; QuickTender2 = 1000; QuickTender3 = 5000; }
            else if (total <= 1000) { QuickTender1 = 1000; QuickTender2 = 2000; QuickTender3 = 5000; }
            else if (total <= 1500) { QuickTender1 = 1500; QuickTender2 = 2000; QuickTender3 = 5000; }
            else if (total <= 2000) { QuickTender1 = 2000; QuickTender2 = 5000; QuickTender3 = 10000; }
            else { QuickTender1 = Math.Ceiling(total / 1000) * 1000; QuickTender2 = QuickTender1 + 1000; QuickTender3 = QuickTender1 + 5000; }
        }

        // ==========================================
        // CORE LOGIC: ADDING PAYMENTS
        // ==========================================
        [RelayCommand]
        public void AddPayment(string paymentType)
        {
            decimal remainingBalance = TotalDue - TotalTendered;
            if (remainingBalance <= 0) return; // Bill is already paid!

            // If cashier didn't type anything, assume they want to pay the exact remaining balance
            decimal amountToApply = decimal.TryParse(TenderedInput, out decimal parsed) && parsed > 0
                ? parsed
                : remainingBalance;

            // ENTERPRISE SECURITY: Block Over-tendering on Non-Cash transactions
            if (paymentType != "Cash" && amountToApply > remainingBalance)
            {
                MessageBox.Show($"You cannot over-tender a {paymentType} payment. Maximum allowed is Rs. {remainingBalance:N2}.",
                                "Invalid Tender Amount", MessageBoxButton.OK, MessageBoxImage.Warning);
                amountToApply = remainingBalance; // Hard-cap it
            }

            string reference = "";

            // AUDIT TRAIL: Trigger sub-dialogs for secure payment types
            if (paymentType == "Card")
            {
                // TODO: Pop Card Details Dialog
                reference = "Visa - *4582";
            }
            else if (paymentType == "Cheque")
            {
                // TODO: Pop Cheque Details Dialog
                reference = "CHQ: 8854921";
            }
            else if (paymentType == "Credit Note")
            {
                // TODO: Pop Credit Note Scanner Dialog
                reference = "CN000125";
            }

            // Create the database record
            var payment = new SalesPayment
            {
                PaymentType = paymentType,
                Amount = amountToApply,
                ReferenceNo = reference,
                CreatedAt = DateTime.Now
            };

            AppliedPayments.Add(payment);

            // Reset input and run the math engine
            TenderedInput = "0";
            RecalculateTotals();
        }

        // ==========================================
        // CORE LOGIC: QUICK TENDER CASH SPEED KEYS
        // ==========================================
        [RelayCommand]
        public void QuickTender(string valueStr)
        {
            decimal remainingBalance = TotalDue - TotalTendered;
            if (remainingBalance <= 0) return;

            decimal amount = 0;
            if (valueStr == "Exact")
            {
                amount = remainingBalance;
            }
            else if (decimal.TryParse(valueStr, out decimal parsed))
            {
                amount = parsed;
            }

            // Force input display to update and instantly apply as Cash
            TenderedInput = amount.ToString();
            AddPayment("Cash");
        }

        // ==========================================
        // REMOVAL & MATH ENGINE
        // ==========================================
        [RelayCommand]
        public void RemovePayment(SalesPayment payment)
        {
            if (payment != null)
            {
                AppliedPayments.Remove(payment);
                RecalculateTotals();
            }
        }

        private void RecalculateTotals()
        {
            TotalTendered = AppliedPayments.Sum(p => p.Amount);

            decimal diff = TotalDue - TotalTendered;

            if (diff <= 0)
            {
                // Paid in full (or overpaid with cash)
                BalanceAmount = Math.Abs(diff); // Show positive number for change
                BalanceLabel = "CHANGE DUE";
                IsChangeDue = true;
            }
            else
            {
                // Still owe money
                BalanceAmount = diff;
                BalanceLabel = "REMAINING";
                IsChangeDue = false;
            }

            // Trigger the UI to re-evaluate if the Complete button should be green
            CompleteSaleCommand.NotifyCanExecuteChanged();
        }

        // ==========================================
        // FINALIZATION ROUTING
        // ==========================================
        [RelayCommand]
        public void Cancel()
        {
            RequestClose?.Invoke(false);
        }

        // This button is strictly locked until the math balances out!
        private bool CanCompleteSale()
        {
            return TotalTendered >= TotalDue;
        }

        [RelayCommand(CanExecute = nameof(CanCompleteSale))]
        public async Task CompleteSaleAsync()
        {
            // At this point, the math is perfectly verified.
            // The system will now hand the 'AppliedPayments' list back to the main Sales Repository 
            // to permanently save the receipt to the SQLite database.

            RequestClose?.Invoke(true);
            await Task.CompletedTask;
        }
    }
}