using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.Views.Pages.Admin;
using POS.BackOffice.UI.Views.Pages.File;
using POS.Core.Services;
using System;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly AuthService _authService;
        private readonly DispatcherTimer _clockTimer;

        [ObservableProperty]
        private object? _currentPage;

        [ObservableProperty]
        private string _sessionUserText = "Unknown user";

        [ObservableProperty]
        private string _currentDateTimeText = string.Empty;

        [ObservableProperty]
        private string _applicationVersionText = "Ver: -";

        public MainViewModel(
            IServiceProvider serviceProvider,
            AuthService authService)
        {
            _serviceProvider = serviceProvider;
            _authService = authService;

            RefreshSessionInformation();
            UpdateClock();

            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _clockTimer.Tick += (_, _) => UpdateClock();
            _clockTimer.Start();

            // The modal LoginWindow has already authenticated the user.
            // The management shell therefore opens directly on Dashboard.
            NavigateToDashboard();
        }

        public void RefreshSessionInformation()
        {
            var user = _authService.CurrentUser;

            SessionUserText = user == null
                ? "Unknown user"
                : $"{user.Username} ({user.Role})";

            Version? version =
                Assembly.GetEntryAssembly()?.GetName().Version;

            ApplicationVersionText = version == null
                ? "Ver: -"
                : $"Ver: {version.Major}.{version.Minor}.{version.Build}";
        }

        private void UpdateClock()
        {
            CurrentDateTimeText =
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        // ==========================================
        // 0. APPLICATION / DASHBOARD
        // ==========================================

        [RelayCommand]
        private void NavigateToDashboard()
        {
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<DashboardViewModel>();
        }

        [RelayCommand]
        private void ExitApplication()
        {
            if (Application.Current is App app)
            {
                app.RequestApplicationExit();
                return;
            }

            Application.Current.Shutdown();
        }

        // ==========================================
        // 1. SETTINGS / ADMINISTRATION
        // ==========================================

        [RelayCommand]
        private void NavigateToStoreSettings()
        {
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<StoreSettingsView>();
        }

        [RelayCommand]
        private void NavigateToTerminalSettings()
        {
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<TerminalSettingsView>();
        }

        [RelayCommand]
        private void NavigateToBackupRestore()
        {
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<BackupRestoreView>();
        }

        [RelayCommand]
        private void NavigateToLicenseManagement()
        {
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<LicenseManagementView>();
        }

        [RelayCommand]
        private void NavigateToTerminalManagement()
        {
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<TerminalManagementView>();
        }

        [RelayCommand]
        private void NavigateToUserManagement()
        {
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<UserManagementViewModel>();
        }

        [RelayCommand]
        private void NavigateToSuspendedTransactionsMonitor()
        {
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<
                        SuspendedTransactionsMonitorViewModel>();
        }

        // ==========================================
        // 2. INVENTORY SETUP
        // ==========================================

        [RelayCommand]
        private void NavigateToCategory() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<CategoryViewModel>();

        [RelayCommand]
        private void NavigateToSubCategory() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<SubCategoryViewModel>();

        [RelayCommand]
        private void NavigateToItemProperty() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<ItemPropertyViewModel>();

        [RelayCommand]
        private void NavigateToUnitOfMeasure() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<UnitOfMeasureViewModel>();

        [RelayCommand]
        private void NavigateToTaxRate() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<TaxRateViewModel>();

        [RelayCommand]
        private void NavigateToSupplier() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<SupplierViewModel>();

        [RelayCommand]
        private void NavigateToItemMaster() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<ItemMasterViewModel>();

        // ==========================================
        // 3. INVENTORY OPERATIONS
        // ==========================================

        [RelayCommand]
        private void NavigateToStockAdjustment() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<
                        StockAdjustmentViewModel>();

        [RelayCommand]
        private void NavigateToStockBalance() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<StockBalanceViewModel>();

        [RelayCommand]
        private void NavigateToGoodsReceivedNote() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<GrnViewModel>();

        [RelayCommand]
        private void NavigateToGrnDashboard() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<GrnDashboardViewModel>();

        [RelayCommand]
        private void NavigateToBarcodeManagement() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<
                        BarcodeManagementViewModel>();

        [RelayCommand]
        private void NavigateToBarcodePrinter() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<
                        BarcodePrinterViewModel>();

        [RelayCommand]
        private void NavigateToExpressItemAdmin() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<
                        ExpressItemAdminViewModel>();

        [RelayCommand]
        private void NavigateToSupplierReturn() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<SupplierReturnViewModel>();

        [RelayCommand]
        private void NavigateToPriceManagement() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<PriceManagementViewModel>();

        [RelayCommand]
        private void NavigateToPriceChangeHistory() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<
                        PriceChangeHistoryViewModel>();

        // ==========================================
        // 4. PURCHASING
        // ==========================================

        [RelayCommand]
        private void NavigateToPurchaseOrder() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<PurchaseOrderViewModel>();

        [RelayCommand]
        private void NavigateToPurchaseOrderDashboard() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<
                        PurchaseOrderDashboardViewModel>();

        // ==========================================
        // 5. CRM / WHOLESALE
        // ==========================================

        [RelayCommand]
        private void NavigateToCustomerMaster() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<CustomerMasterViewModel>();

        [RelayCommand]
        private void NavigateToCustomerLedger() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<CustomerLedgerViewModel>();

        [RelayCommand]
        private void NavigateToGiftVoucher() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<GiftVoucherAdminViewModel>();

        [RelayCommand]
        private void NavigateToFreeIssueRuleSetup() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<
                        FreeIssueRuleSetupViewModel>();

        // ==========================================
        // 6. SALES
        // ==========================================

        [RelayCommand]
        private void NavigateToSalesExplorer() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<SalesExplorerViewModel>();

        [RelayCommand]
        private void NavigateToSecurityAudit() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<SecurityAuditViewModel>();

        [RelayCommand]
        private void NavigateToCustomerReturnsAudit() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<
                        CustomerReturnsAuditViewModel>();

        [RelayCommand]
        private void NavigateToItemSalesAnalytics() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<
                        ItemSalesAnalyticsViewModel>();

        [RelayCommand]
        private void NavigateToReceiptLedger() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<ReceiptLedgerViewModel>();

        // ==========================================
        // 7. FINANCE
        // ==========================================

        [RelayCommand]
        private void NavigateToSupplierLedger() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<SupplierLedgerViewModel>();

        [RelayCommand]
        private void NavigateToSupplierClaims() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<SupplierClaimsViewModel>();

        // ==========================================
        // 8. REPORTS / ANALYTICS
        // ==========================================

        [RelayCommand]
        private void NavigateToFinancialSummary() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<FinancialSummaryViewModel>();

        [RelayCommand]
        private void NavigateToSupplierReport() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<SupplierReportViewModel>();

        [RelayCommand]
        private void NavigateToFloatCashLog() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<FloatCashLogViewModel>();

        [RelayCommand]
        private void NavigateToCashMovementDashboard() =>
            CurrentPage =
                _serviceProvider
                    .GetRequiredService<
                        CashMovementDashboardViewModel>();
    }
}
