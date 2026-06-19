using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;
using POS.BackOffice.UI.Views;
using POS.BackOffice.UI.Views.Layout;
using POS.BackOffice.UI.Views.Pages.Finance;
using POS.Core.Data;
using POS.Core.Repositories;
using POS.Core.Services;
using System;
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
            // ENTERPRISE FIX: SHARED DATABASE PATH
            // ==========================================
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dbFolder = System.IO.Path.Combine(appData, "POS");
            System.IO.Directory.CreateDirectory(dbFolder);
            string dbPath = System.IO.Path.Combine(dbFolder, "pos_local.db");

            services.AddDbContextFactory<AppDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));

            // 2. Register Security Core
            services.AddSingleton<AuthService>();

            // 3. Register ALL Repositories
            services.AddTransient<UserRepository>();
            services.AddTransient<CategoryRepository>();
            services.AddTransient<SubCategoryRepository>();
            services.AddTransient<AttributeRepository>();
            services.AddTransient<SupplierRepository>();
            services.AddTransient<ItemMasterRepository>();
            services.AddTransient<UnitOfMeasureRepository>();
            services.AddTransient<GrnRepository>();
            services.AddTransient<PoRepository>();
            services.AddTransient<StockAdjustmentRepository>();
            services.AddTransient<StockBalanceRepository>();
            services.AddTransient<ReturnRepository>();
            services.AddTransient<ExpressItemRepository>();

            // CRM & Loyalty Repositories
            services.AddTransient<LoyaltyDiscountRepository>();
            services.AddTransient<CustomerAdminRepository>();

            // Financial & Supplier Repositories
            services.AddTransient<SupplierLedgerRepository>();
            services.AddTransient<SupplierReportRepository>();

            // Sales & Analytics Repositories
            services.AddTransient<FloatCashRepository>();
            services.AddTransient<FinancialAnalyticsRepository>();
            services.AddTransient<SecurityAuditRepository>();
            services.AddTransient<QuotationRepository>();
            services.AddTransient<MasterSalesAnalyticsRepository>();
            services.AddTransient<SalesAnalyticsRepository>();

            // Register the Master Settings Repository
            services.AddTransient<POS.Core.Repositories.SystemSettingsRepository>();



            // 4. Register ALL ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddTransient<LoginViewModel>();

            // Inventory ViewModels
            services.AddTransient<CategoryViewModel>();
            services.AddTransient<SubCategoryViewModel>();
            services.AddTransient<ItemPropertyViewModel>();
            services.AddTransient<SupplierViewModel>();
            services.AddTransient<ItemMasterViewModel>();
            services.AddTransient<UnitOfMeasureViewModel>();
            services.AddTransient<GrnViewModel>();
            services.AddTransient<PurchaseOrderViewModel>();
            services.AddTransient<StockAdjustmentViewModel>();
            services.AddTransient<StockBalanceViewModel>();
            services.AddTransient<SupplierReturnViewModel>();
            services.AddTransient<ExpressItemAdminViewModel>();

            // Inside your ConfigureServices method:
            services.AddTransient<SupplierClaimsViewModel>();
            services.AddTransient<SupplierClaimsView>(); // Optional, depends on how strict your DI is

            // Sales & Analytics ViewModels
            services.AddTransient<SalesExplorerViewModel>();
            services.AddTransient<FloatCashLogViewModel>();
            services.AddTransient<FinancialSummaryViewModel>();
            services.AddTransient<SecurityAuditViewModel>();
            services.AddTransient<QuotationManagerViewModel>();
            services.AddTransient<ItemSalesAnalyticsViewModel>();
            services.AddTransient<CashMovementDashboardViewModel>();

            // CRM ViewModels
            services.AddTransient<CustomerMasterViewModel>();
            services.AddTransient<CustomerLedgerViewModel>();
            services.AddTransient<LoyaltyDiscountAdminViewModel>();

            // Finance ViewModels
            services.AddTransient<SupplierLedgerViewModel>();
            services.AddTransient<SupplierReportViewModel>();

            // Admin ViewModels
            services.AddTransient<UserManagementViewModel>();

            // Register the Store Configuration ViewModel
            services.AddTransient<POS.BackOffice.UI.ViewModels.StoreConfigurationViewModel>();

            return services.BuildServiceProvider();
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (Services == null) return;

            // 1. AUTO-BUILD THE DATABASE
            var dbFactory = Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using (var context = dbFactory.CreateDbContext())
            {
                context.Database.EnsureCreated();
            }

            // ==========================================
            // CRITICAL FIX: PREVENT PREMATURE SHUTDOWN
            // ==========================================
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 2. THE SECURITY GATEWAY
            var loginViewModel = Services.GetRequiredService<LoginViewModel>();
            var loginWindow = new LoginWindow(loginViewModel);

            if (loginWindow.ShowDialog() == true)
            {
                // 3. SECURE LAUNCH
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
    }
}