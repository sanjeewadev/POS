using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Enums;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsMenuVisible))]
        private object? _currentPage;

        public bool IsMenuVisible
        {
            get
            {
                if (CurrentPage != null && CurrentPage.GetType().Name == "LoginViewModel")
                {
                    return false;
                }
                return true;
            }
        }

        public MainViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            LoadLoginScreen();
        }

        private void LoadLoginScreen()
        {
            var loginVM = _serviceProvider.GetRequiredService<LoginViewModel>();
            loginVM.LoginSuccessful += HandleSuccessfulLogin;
            CurrentPage = loginVM;
        }

        private void HandleSuccessfulLogin(UserRole role)
        {
            // Route users to the main dashboard or item master upon login
            NavigateToItemMaster();
        }

        // ==========================================
        // 1. INVENTORY SETUP COMMANDS (Active)
        // ==========================================
        [RelayCommand]
        private void NavigateToCategory() => CurrentPage = _serviceProvider.GetRequiredService<CategoryViewModel>();

        [RelayCommand]
        private void NavigateToSubCategory() => CurrentPage = _serviceProvider.GetRequiredService<SubCategoryViewModel>();

        [RelayCommand]
        private void NavigateToItemProperty() => CurrentPage = _serviceProvider.GetRequiredService<ItemPropertyViewModel>();

        [RelayCommand]
        private void NavigateToUnitOfMeasure() => CurrentPage = _serviceProvider.GetRequiredService<UnitOfMeasureViewModel>();

        [RelayCommand]
        private void NavigateToSupplier() => CurrentPage = _serviceProvider.GetRequiredService<SupplierViewModel>();

        [RelayCommand]
        private void NavigateToItemMaster() => CurrentPage = _serviceProvider.GetRequiredService<ItemMasterViewModel>();

        // ==========================================
        // 2. INVENTORY OPERATIONS COMMANDS (Active)
        // ==========================================
        [RelayCommand]
        private void NavigateToStockAdjustment() => CurrentPage = _serviceProvider.GetRequiredService<StockAdjustmentViewModel>();

        [RelayCommand]
        private void NavigateToStockBalance() => CurrentPage = _serviceProvider.GetRequiredService<StockBalanceViewModel>();

        [RelayCommand]
        private void NavigateToGoodsReceivedNote() => CurrentPage = _serviceProvider.GetRequiredService<GrnViewModel>();

        // ==========================================
        // 3. PURCHASING COMMANDS (Active)
        // ==========================================
        [RelayCommand]
        private void NavigateToPurchaseOrder() => CurrentPage = _serviceProvider.GetRequiredService<PurchaseOrderViewModel>();

        [RelayCommand]
        private void NavigateToSupplierReturn() => CurrentPage = _serviceProvider.GetRequiredService<SupplierReturnViewModel>();

        // ==========================================
        // 4. CRM & WHOLESALE COMMANDS (Active)
        // ==========================================
        [RelayCommand]
        private void NavigateToCustomerMaster() => CurrentPage = _serviceProvider.GetRequiredService<CustomerMasterViewModel>();

        [RelayCommand]
        private void NavigateToCustomerLedger() => CurrentPage = _serviceProvider.GetRequiredService<CustomerLedgerViewModel>();

        [RelayCommand]
        private void NavigateToLoyaltyRewards() => CurrentPage = _serviceProvider.GetRequiredService<LoyaltyDiscountAdminViewModel>();

        [RelayCommand]
        private void NavigateToGiftVoucher() => ShowComingSoon("Gift Voucher Setup");

        [RelayCommand]
        private void NavigateToFloatCash() => CurrentPage = _serviceProvider.GetRequiredService<FloatCashLogViewModel>();

        // ==========================================
        // 5. ADMIN & POS CONFIGURATION (Active)
        // ==========================================
        [RelayCommand]
        private void NavigateToUserManagement() => CurrentPage = _serviceProvider.GetRequiredService<UserManagementViewModel>();

        [RelayCommand]
        private void NavigateToExpressItemAdmin() => CurrentPage = _serviceProvider.GetRequiredService<ExpressItemAdminViewModel>();

        [RelayCommand]
        private void NavigateToPosHardwareSettings() => ShowComingSoon("POS Hardware & Peripherals Configuration");

        [RelayCommand]
        private void NavigateToAccessControl() => ShowComingSoon("Security & Access Control Settings");


        // ==========================================
        // 6. SALES COMMANDS (Active)
        // ==========================================
        [RelayCommand]
        private void NavigateToQuotation() => CurrentPage = _serviceProvider.GetRequiredService<QuotationManagerViewModel>();

        [RelayCommand]
        private void NavigateToSalesExplorer() => CurrentPage = _serviceProvider.GetRequiredService<SalesExplorerViewModel>();

        [RelayCommand]
        private void NavigateToSecurityAudit() => CurrentPage = _serviceProvider.GetRequiredService<SecurityAuditViewModel>();

        [RelayCommand]
        private void NavigateToCustomerReturn() => ShowComingSoon("Customer Returns & Credit Notes");

        [RelayCommand]
        private void NavigateToWholesaleInvoice() => ShowComingSoon("Direct Back-Office Wholesale Invoicing");

        // ==========================================
        // 7. FINANCE COMMANDS (Active)
        // ==========================================
        [RelayCommand]
        private void NavigateToCashMovementDashboard() => CurrentPage = _serviceProvider.GetRequiredService<CashMovementDashboardViewModel>();

        [RelayCommand]
        private void NavigateToSupplierLedger() => CurrentPage = _serviceProvider.GetRequiredService<SupplierLedgerViewModel>();

        [RelayCommand]
        private void NavigateToBankDeposit() => ShowComingSoon("Bank Deposit Processing");

        [RelayCommand]
        private void NavigateToCashBook() => ShowComingSoon("Cash Book (View Paid In / Paid Out records here)");

        [RelayCommand]
        private void NavigateToPaymentVoucher() => ShowComingSoon("Supplier Payment Vouchers");

        // ==========================================
        // 8. REPORTS & ANALYTICS COMMANDS (Active)
        // ==========================================
        [RelayCommand]
        private void NavigateToFinancialSummary() => CurrentPage = _serviceProvider.GetRequiredService<FinancialSummaryViewModel>();

        [RelayCommand]
        private void NavigateToItemSalesAnalytics() => CurrentPage = _serviceProvider.GetRequiredService<ItemSalesAnalyticsViewModel>();

        [RelayCommand]
        private void NavigateToSupplierReport() => CurrentPage = _serviceProvider.GetRequiredService<SupplierReportViewModel>();

        [RelayCommand]
        private void NavigateToInventoryReport() => ShowComingSoon("Advanced Inventory Analytics");


        // ==========================================
        // 5. ADMIN & POS CONFIGURATION (Active)
        // ==========================================
        [RelayCommand]
        private void NavigateToStoreConfiguration() => CurrentPage = _serviceProvider.GetRequiredService<StoreConfigurationViewModel>();

        // ==========================================
        // HELPER METHOD
        // ==========================================
        private void ShowComingSoon(string moduleName)
        {
            MessageBox.Show($"The '{moduleName}' module is currently in development.\nIt will be available in an upcoming update.",
                            "Module Not Active",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
        }
    }
}