using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.Dialogs;
using POS.Cashier.UI.Services;
using POS.Cashier.UI.ViewModels;
using POS.Cashier.UI.Views;
using POS.Core.Data;
using POS.Core.Data.Configuration;
using POS.Core.Models;
using POS.Core.Models.Licensing;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Services.Documents;
using POS.Core.Services.Exports;
using POS.Core.Services.Licensing;
using POS.Core.Services.Returns;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace POS.Cashier.UI
{
    public partial class App : System.Windows.Application
    {
        public static IServiceProvider? Services { get; private set; }

        private string _terminalNo = string.Empty;
        private int _autoLockTimeoutMinutes = 10;
        private TerminalSettings? _terminalSettings;
        private StoreSettings? _storeSettings;
        private TillRepository? _tillRepository;
        private bool _fatalErrorShown;
        private Exception? _serviceConfigurationError;

        public App()
        {
            CashierWindowPlacementService.Register();
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

            var databaseSettings =
                new DatabaseConnectionSettingsStore()
                    .LoadOrDefault();

            services.AddSingleton(databaseSettings);
            services.AddDbContextFactory<AppDbContext>(options =>
                PosDatabaseOptionsConfigurator.Configure(
                    options,
                    databaseSettings));
            services.AddSingleton<DatabaseInitializationService>();

            // Core repositories.
            services.AddTransient<UserRepository>();
            services.AddTransient<ItemMasterRepository>();
            services.AddTransient<SalesRepository>();
            services.AddTransient<SalesDocumentRepository>();
            services.AddTransient<CashierCartRepository>();
            services.AddTransient<CustomerReturnRepository>();
            services.AddTransient<TillRepository>();
            services.AddTransient<CustomerRepository>();
            services.AddTransient<CustomerCreditRepository>();
            services.AddTransient<TerminalSettingsRepository>();
            services.AddTransient<StoreSettingsRepository>();

            services.AddSingleton<AuthService>();
            services.AddSingleton<CashierLockService>();

            services.AddSingleton<SalesDocumentTextFormatter>();
            services.AddSingleton<CustomerCreditNoteTextFormatter>();
            services.AddSingleton<ShiftReportTextFormatter>();
            services.AddSingleton<CustomerReturnAllocationCalculator>();
            services.AddSingleton<PdfExportService>();
            services.AddTransient<ExportAuthorizationService>();

            services.AddTransient<
                IReceiptPrintService,
                EscPosReceiptPrintService>();
            services.AddTransient<CashDrawerAuditService>();

            // Offline licensing.
            services.AddTransient<LicenseRepository>();
            services.AddTransient<MachineFingerprintService>();
            services.AddTransient<LicenseSignatureService>();
            services.AddTransient<LicenseFileService>();
            services.AddTransient<LicenseManagerService>();

            // ViewModels.
            services.AddTransient<LoginViewModel>();
            services.AddTransient<SalesViewModel>();
            services.AddTransient<OpenShiftViewModel>();
            services.AddTransient<OpenCloseShiftViewModel>();
            services.AddTransient<CashMovementViewModel>();
            services.AddTransient<B2BCustomerViewModel>();
            services.AddTransient<QuickCustomerCreateViewModel>();
            services.AddTransient<ManagerAuthViewModel>();
            services.AddTransient<FloatCashViewModel>();
            services.AddTransient<ExpressMenuViewModel>();
            services.AddTransient<FreeItemReasonModalViewModel>();
            services.AddTransient<PluSearchViewModel>();
            services.AddTransient<CustomerReturnViewModel>();
            services.AddTransient<StockInquiryViewModel>();

            services.AddTransient<ExpressItemRepository>();
            services.AddTransient<StockInquiryRepository>();

            services.AddTransient<GiftVoucherRepository>();
            services.AddTransient<SellGiftVoucherDialogViewModel>();
            services.AddTransient<GiftVoucherTenderDialogViewModel>();
            services.AddTransient<FreeIssueRuleRepository>();
            services.AddTransient<FreeItemClaimRepository>();

            return services.BuildServiceProvider();
        }

        protected override async void OnStartup(
            StartupEventArgs e)
        {
            base.OnStartup(e);

            if (_serviceConfigurationError != null)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Service configuration",
                    _serviceConfigurationError);

                string message =
                    _serviceConfigurationError is DatabaseConfigurationException
                        ? _serviceConfigurationError.Message
                        : "Cashier services could not be configured.";

                MessageBox.Show(
                    message +
                    "\n\nTechnical details were saved in the local POS Logs folder.",
                    "Cashier Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown();
                return;
            }

            if (Services == null)
            {
                Shutdown();
                return;
            }

            try
            {
                await InitializeDatabaseAsync();
                await InitializeTerminalAndLicenseAsync();

                _tillRepository =
                    Services.GetRequiredService<TillRepository>();

                await ShowLoginWindowAsync();
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Startup",
                    ex);

                string userMessage =
                    ex is DatabaseConfigurationException
                        ? ex.Message
                        : ex is InvalidOperationException &&
                          !string.IsNullOrWhiteSpace(ex.Message)
                            ? ex.Message
                            : "Cashier could not start.";

                MessageBox.Show(
                    userMessage +
                    "\n\nTechnical details were saved in the local POS Logs folder.",
                    "Cashier Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown();
            }
        }

        public async Task<bool> ReturnToLoginAsync(
            Window currentWindow)
        {
            if (Services == null)
                return false;

            if (currentWindow.DataContext is SalesViewModel salesViewModel &&
                !await salesViewModel.FlushCartBeforeLogoffAsync())
            {
                return false;
            }

            Services
                .GetRequiredService<CashierLockService>()
                .Stop();

            Services
                .GetRequiredService<AuthService>()
                .Logout();

            await ShowLoginWindowAsync(currentWindow);
            return true;
        }

        private async Task InitializeDatabaseAsync()
        {
            if (Services == null)
                throw new InvalidOperationException(
                    "Cashier services are not available.");

            var databaseInitialization =
                Services.GetRequiredService<
                    DatabaseInitializationService>();

            try
            {
                await databaseInitialization
                    .InitializeCashierAsync();
            }
            catch (DatabaseConfigurationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "The POS database could not be initialized.",
                    ex);
            }
        }

        private async Task InitializeTerminalAndLicenseAsync()
        {
            if (Services == null)
                throw new InvalidOperationException(
                    "Cashier services are not available.");

            var terminalSettingsRepository =
                Services.GetRequiredService<
                    TerminalSettingsRepository>();

            var terminalSettings =
                await terminalSettingsRepository
                    .GetOrCreateForCurrentMachineAsync();

            ValidateTerminalConfiguration(
                terminalSettings);

            _terminalSettings =
                terminalSettings;

            if (!terminalSettings.IsActive)
            {
                throw new InvalidOperationException(
                    "This terminal is disabled. " +
                    "Open BackOffice Terminal Management and activate it.");
            }

            _terminalNo =
                terminalSettings.TerminalNo.Trim();

            _autoLockTimeoutMinutes =
                Math.Clamp(
                    terminalSettings.AutoLockTimeoutMinutes,
                    0,
                    120);

            var storeSettingsRepository =
                Services.GetRequiredService<
                    StoreSettingsRepository>();

            _storeSettings =
                await storeSettingsRepository
                    .GetOrCreateDefaultAsync();

            var licenseManager =
                Services.GetRequiredService<
                    LicenseManagerService>();

            LicenseSummary summary =
                await licenseManager
                    .GetCurrentLicenseSummaryAsync();

            if (!summary.CanRunCashier)
            {
                throw new InvalidOperationException(
                    summary.StatusMessage);
            }

            if (summary.OverallStatus ==
                LicenseStatus.ExpiringSoon)
            {
                MessageBox.Show(
                    summary.StatusMessage,
                    "License Expiry Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private static void ValidateTerminalConfiguration(
            TerminalSettings settings)
        {
            if (settings == null)
            {
                throw new InvalidOperationException(
                    "Terminal settings are unavailable.");
            }

            if (string.IsNullOrWhiteSpace(
                    settings.TerminalNo))
            {
                throw new InvalidOperationException(
                    "The current terminal number is missing. " +
                    "Open BackOffice Terminal Management and register this computer.");
            }

            if (string.IsNullOrWhiteSpace(
                    settings.TerminalName))
            {
                throw new InvalidOperationException(
                    "The current terminal name is missing. " +
                    "Open BackOffice Terminal Settings and save a terminal name.");
            }

            if (string.IsNullOrWhiteSpace(
                    settings.MachineName))
            {
                throw new InvalidOperationException(
                    "The current terminal is not assigned to a computer.");
            }

            if (settings.AutoLockTimeoutMinutes < 0 ||
                settings.AutoLockTimeoutMinutes > 120)
            {
                throw new InvalidOperationException(
                    "The terminal auto-lock timeout is invalid. " +
                    "Open BackOffice Terminal Settings and save it again.");
            }

            if (settings.ReceiptPaperWidth != 58 &&
                settings.ReceiptPaperWidth != 80)
            {
                throw new InvalidOperationException(
                    "The receipt paper width is invalid. " +
                    "Open BackOffice Terminal Settings and select 58 mm or 80 mm.");
            }

            if (settings.ReceiptCopies < 1 ||
                settings.ReceiptCopies > 3)
            {
                throw new InvalidOperationException(
                    "The receipt copy count is invalid. " +
                    "Open BackOffice Terminal Settings and select 1 to 3 copies.");
            }

            bool printerIsRequired =
                settings.AutoPrintReceipt ||
                settings.EnableCashDrawer;

            if (printerIsRequired &&
                string.IsNullOrWhiteSpace(
                    settings.ReceiptPrinterName))
            {
                throw new InvalidOperationException(
                    "A receipt printer must be selected when automatic printing " +
                    "or the cash drawer is enabled.");
            }
        }

        private async Task ShowLoginWindowAsync(
            Window? previousWindow = null)
        {
            if (Services == null)
                return;

            _tillRepository ??=
                Services.GetRequiredService<TillRepository>();

            ShiftSession? activeShift =
                await _tillRepository
                    .GetActiveShiftAsync(_terminalNo);

            var loginViewModel =
                Services.GetRequiredService<LoginViewModel>();

            loginViewModel.InitializeShiftState(activeShift);

            var loginWindow =
                new LoginView
                {
                    DataContext = loginViewModel
                };

            loginViewModel.LoginCompletedAsync +=
                async () =>
                    await CompleteLoginAsync(loginWindow);

            MainWindow = loginWindow;
            loginWindow.Show();

            if (previousWindow != null &&
                previousWindow != loginWindow)
            {
                previousWindow.Close();
            }
        }

        private async Task<bool> CompleteLoginAsync(
            LoginView loginWindow)
        {
            if (Services == null ||
                _tillRepository == null)
            {
                return false;
            }

            var authService =
                Services.GetRequiredService<AuthService>();

            string cashierName =
                authService.CurrentUser?.Username
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(cashierName))
                return false;

            ShiftSession? activeShift =
                await _tillRepository
                    .GetActiveShiftAsync(_terminalNo);

            if (activeShift != null &&
                !string.Equals(
                    activeShift.CashierName,
                    cashierName,
                    StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    $"Terminal {_terminalNo} already has an open shift " +
                    $"for '{activeShift.CashierName}'.\n\n" +
                    "That cashier must log in, or the shift must be " +
                    "closed through the approved shift-closing workflow.",
                    "Terminal Shift In Use",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            if (activeShift == null)
            {
                var openShiftViewModel =
                    Services.GetRequiredService<
                        OpenShiftViewModel>();

                openShiftViewModel.Initialize(
                    _terminalNo,
                    cashierName);

                var openShiftWindow =
                    new OpenShiftView
                    {
                        Owner = loginWindow,
                        DataContext = openShiftViewModel
                    };

                bool? result =
                    openShiftWindow.ShowDialog();

                if (result != true ||
                    openShiftViewModel.CreatedShift == null)
                {
                    return false;
                }

                activeShift =
                    openShiftViewModel.CreatedShift;
            }

            LaunchMainPos(
                loginWindow,
                activeShift);

            return true;
        }

        private void LaunchMainPos(
            Window oldLoginWindow,
            ShiftSession activeShift)
        {
            if (Services == null)
                return;

            var salesViewModel =
                Services.GetRequiredService<SalesViewModel>();

            if (_terminalSettings == null)
            {
                throw new InvalidOperationException(
                    "Terminal settings are not loaded.");
            }

            if (_storeSettings == null)
            {
                throw new InvalidOperationException(
                    "Store settings are not loaded.");
            }

            salesViewModel.InitializeShiftContext(
                _terminalNo,
                activeShift,
                _terminalSettings,
                _storeSettings);

            var lockService =
                Services.GetRequiredService<
                    CashierLockService>();

            var salesWindow =
                new SalesView(
                    salesViewModel,
                    lockService,
                    _autoLockTimeoutMinutes);

            MainWindow = salesWindow;
            salesWindow.Show();
            oldLoginWindow.Close();
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
                "Cashier",
                "Unhandled WPF UI exception",
                e.Exception);

            e.Handled = true;
            ShowFatalErrorMessage();
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
                "Cashier",
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
                "Cashier",
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
                    "Cashier encountered an unexpected error and must close.\n\n" +
                    "Technical details were saved in:\n" +
                    LocalLogService.LogFolderPath,
                    "Unexpected Cashier Error",
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
            try
            {
                Services?
                    .GetService<CashierLockService>()
                    ?.Stop();

                Services?
                    .GetService<AuthService>()
                    ?.Logout();

                if (Services is IDisposable disposable)
                    disposable.Dispose();
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Cleanup during exit",
                    ex);
            }

            base.OnExit(e);
        }

    }
}
