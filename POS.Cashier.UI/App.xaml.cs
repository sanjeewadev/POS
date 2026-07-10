using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.Dialogs;
using POS.Cashier.UI.Services;
using POS.Cashier.UI.ViewModels;
using POS.Cashier.UI.Views;
using POS.Core.Data;
using POS.Core.Interfaces;
using POS.Core.Models;
using POS.Core.Models.Licensing;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Services.Licensing;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace POS.Cashier.UI
{
    public partial class App : System.Windows.Application
    {
        public static IServiceProvider? Services { get; private set; }

        private string _terminalNo = "01";
        private TillRepository? _tillRepository;

        public App()
        {
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddDbContextFactory<AppDbContext>(
                options =>
                    options.UseSqlite(
                        DatabasePathProvider.ConnectionString));

            // Core repositories.
            services.AddTransient<UserRepository>();
            services.AddTransient<ItemMasterRepository>();
            services.AddTransient<SalesRepository>();
            services.AddTransient<TillRepository>();
            services.AddTransient<CustomerRepository>();
            services.AddTransient<TerminalSettingsRepository>();

            services.AddSingleton<AuthService>();

            services.AddTransient<
                IReceiptPrintService,
                EscPosReceiptPrintService>();

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

            services.AddTransient<ExpressItemRepository>();

            services.AddSingleton<
                IReceiptPrinterService,
                POS.Hardware.Services.ReceiptPrinterService>();

            services.AddTransient<GiftVoucherRepository>();
            services.AddTransient<SellGiftVoucherDialogViewModel>();
            services.AddTransient<GiftVoucherTenderDialogViewModel>();
            services.AddTransient<FreeIssueRuleRepository>();
            services.AddTransient<FreeItemClaimRepository>();
            services.AddTransient<DiscountRuleDialogViewModel>();
            services.AddTransient<DiscountRuleRepository>();

            return services.BuildServiceProvider();
        }

        protected override async void OnStartup(
            StartupEventArgs e)
        {
            base.OnStartup(e);

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
                MessageBox.Show(
                    ex.Message,
                    "Cashier Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown();
            }
        }

        public async Task ReturnToLoginAsync(
            Window currentWindow)
        {
            if (Services == null)
                return;

            Services
                .GetRequiredService<AuthService>()
                .Logout();

            await ShowLoginWindowAsync(currentWindow);
        }

        private async Task InitializeDatabaseAsync()
        {
            if (Services == null)
                throw new InvalidOperationException(
                    "Cashier services are not available.");

            var dbFactory =
                Services.GetRequiredService<
                    IDbContextFactory<AppDbContext>>();

            try
            {
                await using var context =
                    await dbFactory.CreateDbContextAsync();

                await context.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "The POS database could not be initialized.\n\n" +
                    ex.Message,
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
                    .GetOrCreateForCurrentMachineAsync("01");

            if (!terminalSettings.IsActive)
            {
                throw new InvalidOperationException(
                    "This terminal is disabled. " +
                    "Open BackOffice Terminal Settings and activate it.");
            }

            _terminalNo =
                string.IsNullOrWhiteSpace(
                    terminalSettings.TerminalNo)
                    ? "01"
                    : terminalSettings.TerminalNo.Trim();

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

            salesViewModel.InitializeShiftContext(
                _terminalNo,
                activeShift);

            var salesWindow =
                new SalesView(salesViewModel);

            MainWindow = salesWindow;
            salesWindow.Show();
            oldLoginWindow.Close();
        }
    }
}
