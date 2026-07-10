using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.Services;
using POS.BackOffice.UI.ViewModels;
using POS.BackOffice.UI.Views;
using POS.BackOffice.UI.Views.Layout;
using POS.BackOffice.UI.Views.Pages.Admin;
using POS.BackOffice.UI.Views.Pages.File;
using POS.Core.Data;
using POS.Core.Models.Licensing;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Services.Backup;
using POS.Core.Services.Licensing;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace POS.BackOffice.UI
{
    public partial class App : System.Windows.Application
    {
        public static IServiceProvider? Services { get; private set; }

        public App()
        {
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // ==========================================
            // DATABASE CONFIGURATION
            // ==========================================
            services.AddDbContextFactory<AppDbContext>(options =>
                options.UseSqlite(DatabasePathProvider.ConnectionString));

            // ==========================================
            // UI SERVICES
            // ==========================================
            services.AddSingleton<IMessageBoxService, MessageBoxService>();

            // ==========================================
            // SECURITY / AUTHENTICATION
            // ==========================================
            services.AddSingleton<AuthService>();

            // ==========================================
            // REPOSITORIES
            // ==========================================
            services.AddTransient<UserRepository>();
            services.AddTransient<CategoryRepository>();
            services.AddTransient<SubCategoryRepository>();
            services.AddTransient<AttributeRepository>();
            services.AddTransient<SupplierRepository>();
            services.AddTransient<ItemMasterRepository>();
            services.AddTransient<UnitOfMeasureRepository>();
            services.AddTransient<GrnRepository>();
            services.AddTransient<GrnHistoryRepository>();
            services.AddTransient<PoRepository>();
            services.AddTransient<StockAdjustmentRepository>();
            services.AddTransient<StockBalanceRepository>();
            services.AddTransient<SupplierReturnRepository>();
            services.AddTransient<ExpressItemRepository>();
            services.AddTransient<CustomerRepository>();
            services.AddTransient<SupplierLedgerRepository>();
            services.AddTransient<SupplierReportRepository>();
            services.AddTransient<FreeItemClaimRepository>();
            services.AddTransient<FloatCashRepository>();
            services.AddTransient<FinancialAnalyticsRepository>();
            services.AddTransient<SecurityAuditRepository>();
            services.AddTransient<MasterSalesAnalyticsRepository>();
            services.AddTransient<SalesAnalyticsRepository>();
            services.AddTransient<GiftVoucherRepository>();
            services.AddTransient<PriceManagementRepository>();
            services.AddTransient<BarcodeManagementRepository>();
            services.AddTransient<BarcodePrinterRepository>();
            services.AddTransient<FreeIssueRuleRepository>();
            services.AddTransient<StoreSettingsRepository>();
            services.AddTransient<TerminalSettingsRepository>();
            services.AddTransient<TerminalManagementRepository>();
            services.AddTransient<BackupRepository>();
            services.AddTransient<LicenseRepository>();
            services.AddTransient<TaxRateRepository>();

            // ==========================================
            // CORE SERVICES
            // ==========================================
            services.AddTransient<POS.Core.Services.IBarcodePrintService, WpfBarcodePrintService>();
            services.AddTransient<BackupService>();

            // ==========================================
            // LICENSING SERVICES
            // BackOffice remains accessible even if license expires.
            // Cashier app should enforce store/terminal license checks.
            // ==========================================
            services.AddTransient<MachineFingerprintService>();
            services.AddTransient<LicenseSignatureService>();
            services.AddTransient<LicenseFileService>();
            services.AddTransient<LicenseManagerService>();

            // ==========================================
            // ROOT / LOGIN VIEWMODELS
            // ==========================================
            services.AddSingleton<MainViewModel>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<FirstRunAdminViewModel>();

            // ==========================================
            // DASHBOARD
            // ==========================================
            services.AddTransient<DashboardViewModel>();

            // ==========================================
            // INVENTORY SETUP VIEWMODELS
            // ==========================================
            services.AddTransient<CategoryViewModel>();
            services.AddTransient<SubCategoryViewModel>();
            services.AddTransient<ItemPropertyViewModel>();
            services.AddTransient<SupplierViewModel>();
            services.AddTransient<ItemMasterViewModel>();
            services.AddTransient<UnitOfMeasureViewModel>();
            services.AddTransient<TaxRateViewModel>();

            // ==========================================
            // INVENTORY OPERATION VIEWMODELS
            // ==========================================
            services.AddTransient<GrnViewModel>();
            services.AddTransient<GrnDashboardViewModel>();
            services.AddTransient<StockAdjustmentViewModel>();
            services.AddTransient<StockBalanceViewModel>();
            services.AddTransient<SupplierReturnViewModel>();
            services.AddTransient<ExpressItemAdminViewModel>();
            services.AddTransient<BarcodeManagementViewModel>();
            services.AddTransient<BarcodePrinterViewModel>();
            services.AddTransient<PriceManagementViewModel>();

            // ==========================================
            // PURCHASING VIEWMODELS
            // ==========================================
            services.AddTransient<PurchaseOrderViewModel>();
            services.AddTransient<PurchaseOrderDashboardViewModel>();

            // ==========================================
            // FINANCE VIEWMODELS
            // ==========================================
            services.AddTransient<SupplierLedgerViewModel>();
            services.AddTransient<SupplierReportViewModel>();
            services.AddTransient<SupplierClaimsViewModel>();

            // ==========================================
            // SALES / REPORTING VIEWMODELS
            // ==========================================
            services.AddTransient<SalesExplorerViewModel>();
            services.AddTransient<FloatCashLogViewModel>();
            services.AddTransient<FinancialSummaryViewModel>();
            services.AddTransient<SecurityAuditViewModel>();
            services.AddTransient<ItemSalesAnalyticsViewModel>();
            services.AddTransient<CashMovementDashboardViewModel>();
            services.AddTransient<CustomerReturnsAuditViewModel>();
            services.AddTransient<ReceiptLedgerViewModel>();

            // ==========================================
            // CRM VIEWMODELS
            // ==========================================
            services.AddTransient<CustomerMasterViewModel>();
            services.AddTransient<CustomerLedgerViewModel>();
            services.AddTransient<GiftVoucherAdminViewModel>();
            services.AddTransient<FreeIssueRuleSetupViewModel>();

            // ==========================================
            // ADMIN VIEWMODELS / VIEWS
            // ==========================================
            services.AddTransient<UserManagementViewModel>();
            services.AddTransient<SuspendedTransactionsMonitorViewModel>();

            services.AddTransient<StoreSettingsViewModel>();
            services.AddTransient<StoreSettingsView>();

            services.AddTransient<TerminalSettingsViewModel>();
            services.AddTransient<TerminalSettingsView>();

            services.AddTransient<TerminalManagementViewModel>();
            services.AddTransient<TerminalManagementView>();

            services.AddTransient<BackupRestoreViewModel>();
            services.AddTransient<BackupRestoreView>();

            services.AddTransient<LicenseManagementViewModel>();
            services.AddTransient<LicenseManagementView>();

            // New
            services.AddTransient<PriceChangeHistoryRepository>();
            services.AddTransient<PriceChangeHistoryViewModel>();

            return services.BuildServiceProvider();
        }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            if (Services == null)
            {
                return;
            }

            var dbFactory = Services.GetRequiredService<IDbContextFactory<AppDbContext>>();

            try
            {
                await using var context = await dbFactory.CreateDbContextAsync();
                await context.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"The POS database could not be initialized.\n\n{ex.Message}",
                    "Database Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Application.Current.Shutdown();
                return;
            }

            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                var userRepository = Services.GetRequiredService<UserRepository>();

                if (!await userRepository.AnyUsersAsync())
                {
                    var setupViewModel = Services.GetRequiredService<FirstRunAdminViewModel>();
                    var setupWindow = new FirstRunAdminWindow(setupViewModel);

                    if (setupWindow.ShowDialog() != true)
                    {
                        Application.Current.Shutdown();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Initial administrator setup could not be checked.\n\n{ex.Message}",
                    "Administrator Setup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Application.Current.Shutdown();
                return;
            }

            var loginViewModel = Services.GetRequiredService<LoginViewModel>();
            var loginWindow = new LoginWindow(loginViewModel);

            if (loginWindow.ShowDialog() == true)
            {
                await ShowLicenseNoticeAsync();

                var mainWindow = new ManagementShellView();
                var mainViewModel = Services.GetRequiredService<MainViewModel>();

                mainWindow.DataContext = mainViewModel;

                Application.Current.ShutdownMode = ShutdownMode.OnLastWindowClose;
                mainWindow.Show();
            }
            else
            {
                Application.Current.Shutdown();
            }
        }
        private static async Task ShowLicenseNoticeAsync()
        {
            if (Services == null)
                return;

            try
            {
                var licenseManager =
                    Services.GetRequiredService<
                        LicenseManagerService>();

                LicenseSummary summary =
                    await licenseManager
                        .GetCurrentLicenseSummaryAsync();

                if (summary.StoreLicenseStatus ==
                    LicenseStatus.Active)
                {
                    return;
                }

                string message;
                MessageBoxImage icon;

                switch (summary.StoreLicenseStatus)
                {
                    case LicenseStatus.ExpiringSoon:
                        message =
                            $"The annual store license expires on " +
                            $"{summary.StoreExpiryDate:yyyy-MM-dd}. " +
                            $"Please renew it before expiry.";
                        icon = MessageBoxImage.Warning;
                        break;

                    case LicenseStatus.ExpiredReadOnly:
                        message =
                            "The annual store license has expired. " +
                            "Cashier terminals are locked, but BackOffice " +
                            "remains available for administration, reports, " +
                            "backup, and license renewal.";
                        icon = MessageBoxImage.Warning;
                        break;

                    case LicenseStatus.Missing:
                        message =
                            "No active store license is installed. " +
                            "Cashier terminals are locked, but BackOffice " +
                            "remains available so that a license can be imported.";
                        icon = MessageBoxImage.Information;
                        break;

                    default:
                        message =
                            "The installed store license is invalid or inactive. " +
                            "Cashier terminals are locked, but BackOffice " +
                            "remains available for license recovery.";
                        icon = MessageBoxImage.Warning;
                        break;
                }

                MessageBox.Show(
                    message,
                    "POS License",
                    MessageBoxButton.OK,
                    icon);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "License status could not be checked. " +
                    "BackOffice will remain available.\n\n" +
                    ex.Message,
                    "License Check",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

    }
}
