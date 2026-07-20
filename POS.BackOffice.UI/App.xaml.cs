using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.Services;
using POS.BackOffice.UI.ViewModels;
using POS.BackOffice.UI.Views;
using POS.BackOffice.UI.Views.Layout;
using POS.BackOffice.UI.Views.Pages.Admin;
using POS.BackOffice.UI.Views.Pages.File;
using POS.Core.Data;
using POS.Core.Data.Configuration;
using POS.Core.Interfaces;
using POS.Core.Models.Licensing;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Services.Backup;
using POS.Core.Services.Licensing;
using POS.Core.Services.Documents;
using POS.Core.Services.Exports;
using POS.Core.Services.Returns;
using POS.Hardware.Services;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace POS.BackOffice.UI
{
    public partial class App : System.Windows.Application
    {
        public static IServiceProvider? Services { get; private set; }

        private bool _exitApproved;
        private bool _fatalErrorShown;
        private Exception? _serviceConfigurationError;

        public App()
        {
            RegisterGlobalExceptionHandlers();

            try
            {
                Services = ConfigureServices();
            }
            catch (Exception ex)
            {
                _serviceConfigurationError = ex;
            }
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // ==========================================
            // DATABASE CONFIGURATION
            // Default remains standalone SQLite until a locally encrypted
            // central SQL Server profile is created by the approved setup flow.
            // ==========================================
            var databaseSettingsStore =
                new DatabaseConnectionSettingsStore();

            DatabaseConnectionSettings databaseSettings =
                databaseSettingsStore.LoadOrDefault();

            databaseSettings =
                LocalServerConnectionProfileRecovery
                    .RepairIfApplicable(
                        databaseSettingsStore,
                        databaseSettings,
                        "BackOffice");

            services.AddSingleton(databaseSettings);
            services.AddDbContextFactory<AppDbContext>(options =>
                PosDatabaseOptionsConfigurator.Configure(
                    options,
                    databaseSettings));
            services.AddSingleton<DatabaseInitializationService>();

            // ==========================================
            // UI SERVICES
            // ==========================================
            services.AddSingleton<IMessageBoxService, MessageBoxService>();
            services.AddSingleton<ExportDialogService>();

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
            services.AddTransient<SupplierReturnAllocationCalculator>();
            services.AddTransient<SupplierDebitNoteTextFormatter>();
            services.AddTransient<SupplierReturnRepository>();
            services.AddTransient<CustomerReturnAllocationCalculator>();
            services.AddTransient<CustomerCreditNoteTextFormatter>();
            services.AddTransient<CustomerReturnRepository>();
            services.AddTransient<CashierCartRepository>();
            services.AddTransient<ExpressItemRepository>();
            services.AddTransient<CustomerRepository>();
            services.AddTransient<CustomerCreditRepository>();
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
            services.AddTransient<VatReportRepository>();
            services.AddTransient<DashboardRepository>();

            // ==========================================
            // CORE SERVICES
            // ==========================================
            services.AddTransient<POS.Core.Services.IBarcodePrintService, WpfBarcodePrintService>();
            services.AddTransient<BackupService>();
            services.AddSingleton<CustomerStatementTextFormatter>();
            services.AddSingleton<CustomerPaymentReceiptTextFormatter>();
            services.AddSingleton<CsvExportService>();
            services.AddSingleton<PdfExportService>();
            services.AddSingleton<OperationalExportBuilder>();
            services.AddTransient<ExportAuthorizationService>();
            services.AddSingleton<GiftVoucherTextFormatter>();
            services.AddSingleton<
                ITerminalHardwareService,
                TerminalHardwareService>();

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
            services.AddTransient<StockAdjustmentHistoryViewModel>();
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
            services.AddTransient<VatReportViewModel>();

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

        private async void Application_Startup(
            object sender,
            StartupEventArgs e)
        {
            if (_serviceConfigurationError != null)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Service configuration",
                    _serviceConfigurationError);

                string message =
                    _serviceConfigurationError is DatabaseConfigurationException
                        ? _serviceConfigurationError.Message +
                          "\n\nOpen Start > Advanced POS > Configure Advanced POS Server " +
                          "to repair the local server profile, then reopen BackOffice."
                        : "BackOffice services could not be configured.";

                MessageBox.Show(
                    message +
                    "\n\nTechnical details were saved in the local POS Logs folder.",
                    "BackOffice Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown();
                return;
            }

            if (Services == null)
            {
                LocalLogService.WriteInformation(
                    "BackOffice",
                    "Startup",
                    "The dependency injection container was unavailable.");

                Shutdown();
                return;
            }

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                var databaseInitialization =
                    Services.GetRequiredService<
                        DatabaseInitializationService>();

                await databaseInitialization
                    .InitializeBackOfficeAsync();
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Database startup",
                    ex);

                string message =
                    ex is DatabaseConfigurationException
                        ? ex.Message +
                          "\n\nOpen Start > Advanced POS > Configure Advanced POS Server " +
                          "to repair the local server profile, then reopen BackOffice."
                        : "The POS database could not be initialized. Close BackOffice and try again.";

                MessageBox.Show(
                    message +
                    "\n\nTechnical details were saved in the local POS Logs folder.",
                    "Database Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown();
                return;
            }

            try
            {
                var userRepository =
                    Services.GetRequiredService<
                        UserRepository>();

                if (!await userRepository.AnyUsersAsync())
                {
                    var setupViewModel =
                        Services.GetRequiredService<
                            FirstRunAdminViewModel>();

                    var setupWindow =
                        new FirstRunAdminWindow(
                            setupViewModel);

                    if (setupWindow.ShowDialog() != true)
                    {
                        Shutdown();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Initial administrator setup",
                    ex);

                MessageBox.Show(
                    "Initial Administrator Setup could not be opened.\n\n" +
                    "Technical details were saved in the local POS Logs folder.",
                    "Administrator Setup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown();
                return;
            }

            var loginViewModel =
                Services.GetRequiredService<
                    LoginViewModel>();

            var loginWindow =
                new LoginWindow(loginViewModel);

            if (loginWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            await ShowLicenseNoticeAsync();

            var mainWindow =
                new ManagementShellView();

            var mainViewModel =
                Services.GetRequiredService<
                    MainViewModel>();

            mainViewModel.RefreshSessionInformation();
            mainWindow.DataContext = mainViewModel;

            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnLastWindowClose;
            mainWindow.Show();
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
                LocalLogService.WriteException(
                    "BackOffice",
                    "License status check",
                    ex);

                MessageBox.Show(
                    "License status could not be checked. " +
                    "BackOffice will remain available. " +
                    "Technical details were saved in the local POS Logs folder.",
                    "License Check",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        public bool TryApproveWindowClose()
        {
            if (_exitApproved)
                return true;

            MessageBoxResult result =
                MessageBox.Show(
                    "Close BackOffice and end the current session?",
                    "Exit BackOffice",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return false;

            ApproveExit();
            return true;
        }

        public void RequestApplicationExit()
        {
            if (!TryApproveWindowClose())
                return;

            Shutdown();
        }

        private void ApproveExit()
        {
            if (_exitApproved)
                return;

            _exitApproved = true;

            try
            {
                Services?
                    .GetService<AuthService>()
                    ?.Logout();
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Session cleanup during exit",
                    ex);
            }
        }

        private void RegisterGlobalExceptionHandlers()
        {
            DispatcherUnhandledException +=
                App_DispatcherUnhandledException;

            AppDomain.CurrentDomain.UnhandledException +=
                CurrentDomain_UnhandledException;

            TaskScheduler.UnobservedTaskException +=
                TaskScheduler_UnobservedTaskException;
        }

        private void App_DispatcherUnhandledException(
            object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            LocalLogService.WriteException(
                "BackOffice",
                "Unhandled WPF UI exception",
                e.Exception);

            e.Handled = true;
            ShowFatalErrorMessage();

            ApproveExit();
            Shutdown(-1);
        }

        private void CurrentDomain_UnhandledException(
            object sender,
            UnhandledExceptionEventArgs e)
        {
            Exception exception =
                e.ExceptionObject as Exception
                ?? new Exception(
                    Convert.ToString(
                        e.ExceptionObject)
                    ?? "Unknown application error.");

            LocalLogService.WriteException(
                "BackOffice",
                "Unhandled application-domain exception",
                exception);

            if (e.IsTerminating)
                ShowFatalErrorMessage();
        }

        private void TaskScheduler_UnobservedTaskException(
            object? sender,
            UnobservedTaskExceptionEventArgs e)
        {
            LocalLogService.WriteException(
                "BackOffice",
                "Unobserved background-task exception",
                e.Exception);

            e.SetObserved();
        }

        private void ShowFatalErrorMessage()
        {
            if (_fatalErrorShown)
                return;

            _fatalErrorShown = true;

            try
            {
                MessageBox.Show(
                    "BackOffice encountered an unexpected error and must close.\n\n" +
                    "Technical details were saved in:\n" +
                    LocalLogService.LogFolderPath,
                    "Unexpected BackOffice Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch
            {
                // The process may already be shutting down.
            }
        }

        protected override void OnExit(
            ExitEventArgs e)
        {
            ApproveExit();

            try
            {
                if (Services is IDisposable disposable)
                    disposable.Dispose();
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Service disposal during exit",
                    ex);
            }

            base.OnExit(e);
        }

    }
}
