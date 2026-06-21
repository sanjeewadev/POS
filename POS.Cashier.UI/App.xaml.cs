using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.Services;
using POS.Cashier.UI.ViewModels;
using POS.Cashier.UI.Views;
using POS.Core.Data;
using POS.Core.Enums;
using POS.Core.Interfaces;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Core.Services;
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

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dbFolder = System.IO.Path.Combine(appData, "POS");
            System.IO.Directory.CreateDirectory(dbFolder);
            string dbPath = System.IO.Path.Combine(dbFolder, "pos_local.db");

            services.AddDbContextFactory<AppDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));


            services.AddSingleton<FreeItemClaimRepository>();
            // Core
            services.AddTransient<UserRepository>();
            services.AddTransient<ItemMasterRepository>();
            services.AddTransient<SalesRepository>();
            services.AddTransient<TillRepository>();
            services.AddSingleton<AuthService>();
            services.AddTransient<IReceiptPrintService, EscPosReceiptPrintService>();


            // ViewModels
            services.AddTransient<LoginViewModel>();
            services.AddTransient<SalesViewModel>();
            services.AddTransient<OpenCloseShiftViewModel>();

            // Tells the app: When a UI asks for ICashMovementService, give it the CashMovementService logic.
            // FIXED NAMESPACE HERE
            services.AddTransient<ICashMovementService, POS.Core.Services.CashMovementService>();

            // NEW: Express Items Repository (Required for the popup to load buttons)
            services.AddTransient<POS.Core.Repositories.ExpressItemRepository>();

            // Tells the app: When a UI asks for IReceiptPrinterService, use the real hardware.
            services.AddSingleton<IReceiptPrinterService, POS.Hardware.Services.ReceiptPrinterService>();

            services.AddTransient<POS.Core.Repositories.LoyaltyCustomerRepository>();

            return services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            if (Services == null) return;

            // 1. Ensure DB exists
            var dbFactory = Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using (var context = dbFactory.CreateDbContext())
            {
                context.Database.EnsureCreated();
            }

            // 2. CHECK DATABASE FOR OPEN SHIFTS (The Alt+F4 Protection)
            var tillRepo = Services.GetRequiredService<TillRepository>();
            var activeShift = tillRepo.GetActiveShiftAsync("01").GetAwaiter().GetResult();

            // 3. Setup Login ViewModel and pass the lock state
            var loginViewModel = Services.GetRequiredService<LoginViewModel>();
            loginViewModel.InitializeShiftState(activeShift);

            var loginWindow = new LoginView { DataContext = loginViewModel };

            // 4. The New Seamless Routing Logic
            loginViewModel.LoginSuccessful += async () =>
            {
                if (!loginViewModel.HasOpenShift)
                {
                    // Silently create the new shift in the background with Rs. 0
                    var tillRepository = Services.GetRequiredService<TillRepository>();
                    var authService = Services.GetRequiredService<AuthService>();
                    string cashierName = authService.CurrentUser?.Username ?? "Unknown";

                    await tillRepository.CreateNewShiftAsync("01", cashierName);
                }

                // Route directly to the Sales Screen without bothering the cashier!
                LaunchMainPos(loginWindow);
            };

            this.MainWindow = loginWindow;
            loginWindow.Show();
        }

        private void LaunchMainPos(Window oldLoginWindow)
        {
            SalesView salesWindow = new SalesView();
            this.MainWindow = salesWindow;
            salesWindow.Show();
            oldLoginWindow.Close(); // Destroy the login window
        }
    }
}