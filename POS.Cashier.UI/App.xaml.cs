using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.Services;
using POS.Cashier.UI.ViewModels;
using POS.Cashier.UI.Views;
using POS.Core.Data;
using POS.Core.Interfaces;
using POS.Core.Models.Licensing;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Services.Licensing;
using System;
using System.Windows;

namespace POS.Cashier.UI
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

            services.AddDbContextFactory<AppDbContext>(
                options =>
                    options.UseSqlite(
                        DatabasePathProvider
                            .ConnectionString));

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
                POS.Hardware.Services
                    .ReceiptPrinterService>();

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

            var dbFactory =
                Services.GetRequiredService<
                    IDbContextFactory<AppDbContext>>();

            try
            {
                await using var context =
                    await dbFactory
                        .CreateDbContextAsync();

                await context.Database
                    .MigrateAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"The POS database could not be initialized.\n\n" +
                    ex.Message,
                    "Database Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown();
                return;
            }

            string terminalNo;

            try
            {
                var terminalSettingsRepository =
                    Services.GetRequiredService<
                        TerminalSettingsRepository>();

                var terminalSettings =
                    await terminalSettingsRepository
                        .GetOrCreateForCurrentMachineAsync(
                            "01");

                terminalNo =
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
                    MessageBox.Show(
                        summary.StatusMessage,
                        "Cashier License Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    Shutdown();
                    return;
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
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The Cashier license could not be verified.\n\n" +
                    ex.Message,
                    "License Check Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown();
                return;
            }

            var tillRepository =
                Services.GetRequiredService<
                    TillRepository>();

            var activeShift =
                await tillRepository
                    .GetActiveShiftAsync(
                        terminalNo);

            var loginViewModel =
                Services.GetRequiredService<
                    LoginViewModel>();

            loginViewModel
                .InitializeShiftState(
                    activeShift);

            var loginWindow =
                new LoginView
                {
                    DataContext = loginViewModel
                };

            loginViewModel.LoginSuccessful +=
                async () =>
                {
                    if (!loginViewModel
                            .HasOpenShift)
                    {
                        var authService =
                            Services
                                .GetRequiredService<
                                    AuthService>();

                        string cashierName =
                            authService.CurrentUser
                                ?.Username ??
                            "Unknown";

                        await tillRepository
                            .CreateNewShiftAsync(
                                terminalNo,
                                cashierName);
                    }

                    LaunchMainPos(loginWindow);
                };

            MainWindow = loginWindow;
            loginWindow.Show();
        }

        private void LaunchMainPos(
            Window oldLoginWindow)
        {
            var salesWindow =
                new SalesView();

            MainWindow = salesWindow;
            salesWindow.Show();
            oldLoginWindow.Close();
        }
    }
}
