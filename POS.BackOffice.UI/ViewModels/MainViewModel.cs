using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.Views.Pages.Admin;
using POS.BackOffice.UI.Views.Pages.File;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Utilities;
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly AuthService _authService;
        private readonly StoreSettingsRepository _storeSettingsRepository;
        private readonly DispatcherTimer _clockTimer;

        [ObservableProperty]
        private object? _currentPage;

        [ObservableProperty]
        private string _sessionUserText = "Unknown user";

        [ObservableProperty]
        private string _currentDateTimeText = string.Empty;

        [ObservableProperty]
        private string _applicationVersionText = "Ver: -";

        [ObservableProperty]
        private string _applicationTitleText =
            StoreIdentityDisplayFormatter.BackOfficeProductTitle;

        public MainViewModel(
            IServiceProvider serviceProvider,
            AuthService authService,
            StoreSettingsRepository storeSettingsRepository)
        {
            _serviceProvider = serviceProvider;
            _authService = authService;
            _storeSettingsRepository = storeSettingsRepository;

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

        public async Task RefreshStoreIdentityAsync()
        {
            try
            {
                var settings =
                    await _storeSettingsRepository
                        .GetActiveAsync();

                ApplyStoreIdentity(
                    settings?.StoreName,
                    settings?.LegalName);
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Load management shell store identity",
                    ex);

                ApplicationTitleText =
                    StoreIdentityDisplayFormatter
                        .BackOfficeProductTitle;
            }
        }

        public void ApplyStoreIdentity(
            string? storeName,
            string? legalName)
        {
            ApplicationTitleText =
                StoreIdentityDisplayFormatter.BuildBackOfficeTitle(
                    storeName,
                    legalName);
        }

        private void UpdateClock()
        {
            CurrentDateTimeText =
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void NavigateTo<TPage>()
            where TPage : class
        {
            if (CurrentPage is TPage)
                return;

            try
            {
                object nextPage =
                    _serviceProvider.GetRequiredService<TPage>();

                CurrentPage = nextPage;
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    $"Navigate to {typeof(TPage).FullName}",
                    ex);

                MessageBox.Show(
                    "The selected page could not be opened. BackOffice will remain " +
                    "on the current page. Technical details were saved in the local " +
                    "POS Logs folder.",
                    "Page Could Not Open",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ==========================================
        // 0. APPLICATION / DASHBOARD
        // ==========================================

        [RelayCommand]
        private void NavigateToDashboard() =>
            NavigateTo<DashboardViewModel>();

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
        private void NavigateToStoreSettings() =>
            NavigateTo<StoreSettingsView>();

        [RelayCommand]
        private void NavigateToTerminalSettings() =>
            NavigateTo<TerminalSettingsView>();

        [RelayCommand]
        private void NavigateToBackupRestore() =>
            NavigateTo<BackupRestoreView>();

        [RelayCommand]
        private void NavigateToLicenseManagement() =>
            NavigateTo<LicenseManagementView>();

        [RelayCommand]
        private void NavigateToTerminalManagement() =>
            NavigateTo<TerminalManagementView>();

        [RelayCommand]
        private void NavigateToUserManagement() =>
            NavigateTo<UserManagementViewModel>();

        [RelayCommand]
        private void NavigateToSuspendedTransactionsMonitor() =>
            NavigateTo<SuspendedTransactionsMonitorViewModel>();

        // ==========================================
        // 2. INVENTORY SETUP
        // ==========================================

        [RelayCommand]
        private void NavigateToCategory() =>
            NavigateTo<CategoryViewModel>();

        [RelayCommand]
        private void NavigateToSubCategory() =>
            NavigateTo<SubCategoryViewModel>();

        [RelayCommand]
        private void NavigateToItemProperty() =>
            NavigateTo<ItemPropertyViewModel>();

        [RelayCommand]
        private void NavigateToUnitOfMeasure() =>
            NavigateTo<UnitOfMeasureViewModel>();

        [RelayCommand]
        private void NavigateToTaxRate() =>
            NavigateTo<TaxRateViewModel>();

        [RelayCommand]
        private void NavigateToSupplier() =>
            NavigateTo<SupplierViewModel>();

        [RelayCommand]
        private void NavigateToItemMaster() =>
            NavigateTo<ItemMasterViewModel>();

        // ==========================================
        // 3. INVENTORY OPERATIONS
        // ==========================================

        [RelayCommand]
        private void NavigateToStockAdjustment() =>
            NavigateTo<StockAdjustmentViewModel>();

        [RelayCommand]
        private void NavigateToStockBalance() =>
            NavigateTo<StockBalanceViewModel>();

        [RelayCommand]
        private void NavigateToGoodsReceivedNote() =>
            NavigateTo<GrnViewModel>();

        [RelayCommand]
        private void NavigateToGrnDashboard() =>
            NavigateTo<GrnDashboardViewModel>();

        [RelayCommand]
        private void NavigateToBarcodeManagement() =>
            NavigateTo<BarcodeManagementViewModel>();

        [RelayCommand]
        private void NavigateToBarcodePrinter() =>
            NavigateTo<BarcodePrinterViewModel>();

        [RelayCommand]
        private void NavigateToExpressItemAdmin() =>
            NavigateTo<ExpressItemAdminViewModel>();

        [RelayCommand]
        private void NavigateToSupplierReturn() =>
            NavigateTo<SupplierReturnViewModel>();

        [RelayCommand]
        private void NavigateToPriceManagement() =>
            NavigateTo<PriceManagementViewModel>();

        [RelayCommand]
        private void NavigateToPriceChangeHistory() =>
            NavigateTo<PriceChangeHistoryViewModel>();

        // ==========================================
        // 4. PURCHASING
        // ==========================================

        [RelayCommand]
        private void NavigateToPurchaseOrder() =>
            NavigateTo<PurchaseOrderViewModel>();

        [RelayCommand]
        private void NavigateToPurchaseOrderDashboard() =>
            NavigateTo<PurchaseOrderDashboardViewModel>();

        // ==========================================
        // 5. CRM / WHOLESALE
        // ==========================================

        [RelayCommand]
        private void NavigateToCustomerMaster() =>
            NavigateTo<CustomerMasterViewModel>();

        [RelayCommand]
        private void NavigateToCustomerLedger() =>
            NavigateTo<CustomerLedgerViewModel>();

        [RelayCommand]
        private void NavigateToGiftVoucher() =>
            NavigateTo<GiftVoucherAdminViewModel>();

        [RelayCommand]
        private void NavigateToFreeIssueRuleSetup() =>
            NavigateTo<FreeIssueRuleSetupViewModel>();

        // ==========================================
        // 6. SALES
        // ==========================================

        [RelayCommand]
        private void NavigateToSalesExplorer() =>
            NavigateTo<SalesExplorerViewModel>();

        [RelayCommand]
        private void NavigateToDataImportCenter() =>
            NavigateTo<DataImportCenterViewModel>();

        [RelayCommand]
        private void NavigateToSecurityAudit() =>
            NavigateTo<SecurityAuditViewModel>();

        [RelayCommand]
        private void NavigateToCustomerReturnsAudit() =>
            NavigateTo<CustomerReturnsAuditViewModel>();

        [RelayCommand]
        private void NavigateToItemSalesAnalytics() =>
            NavigateTo<ItemSalesAnalyticsViewModel>();

        // ==========================================
        // 7. FINANCE
        // ==========================================

        [RelayCommand]
        private void NavigateToSupplierLedger() =>
            NavigateTo<SupplierLedgerViewModel>();

        [RelayCommand]
        private void NavigateToSupplierClaims() =>
            NavigateTo<SupplierClaimsViewModel>();

        // ==========================================
        // 8. REPORTS / ANALYTICS
        // ==========================================

        [RelayCommand]
        private void NavigateToFinancialSummary() =>
            NavigateTo<FinancialSummaryViewModel>();

        [RelayCommand]
        private void NavigateToSupplierReport() =>
            NavigateTo<SupplierReportViewModel>();

        [RelayCommand]
        private void NavigateToFloatCashLog() =>
            NavigateTo<FloatCashLogViewModel>();

        [RelayCommand]
        private void NavigateToCashMovementDashboard() =>
            NavigateTo<CashMovementDashboardViewModel>();

        [RelayCommand]
        private void NavigateToShopCostFreeIssueReport() =>
            NavigateTo<ShopCostFreeIssueReportViewModel>();

        [RelayCommand]
        private void NavigateToVatReport() =>
            NavigateTo<VatReportViewModel>();
    }
}
