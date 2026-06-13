using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;
using POS.BackOffice.UI.Views;
using POS.BackOffice.UI.Views.Layout;
using POS.Core.Data;
using POS.Core.Repositories;
using POS.Core.Services; // Needed for AuthService
using System;
using System.Windows;

namespace POS.BackOffice.UI
{
    public partial class App : System.Windows.Application
    {
        // Added the '?' to satisfy the compiler's non-nullable warning
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
            // This creates a permanent folder in Windows: C:\Users\Sanjeewa\AppData\Local\POS
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dbFolder = System.IO.Path.Combine(appData, "POS");
            System.IO.Directory.CreateDirectory(dbFolder); // Build the folder if it doesn't exist
            string dbPath = System.IO.Path.Combine(dbFolder, "pos_local.db");

            // Tell Entity Framework to use this exact file
            services.AddDbContextFactory<AppDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));

            // 2. Register Security Core
            services.AddSingleton<AuthService>(); // MUST be singleton to remember login state

            // 3. Register ALL Repositories
            services.AddTransient<UserRepository>();
            services.AddTransient<CategoryRepository>();
            services.AddTransient<SubCategoryRepository>();
            services.AddTransient<AttributeRepository>(); // Replaced Group/Value with main AttributeRepository
            services.AddTransient<SupplierRepository>();
            services.AddTransient<ItemMasterRepository>();
            services.AddTransient<UnitOfMeasureRepository>();
            services.AddTransient<GrnRepository>();
            services.AddTransient<PoRepository>();
            services.AddTransient<StockAdjustmentRepository>();
            services.AddTransient<StockBalanceRepository>();
            services.AddTransient<ReturnRepository>();

            // 4. Register ALL ViewModels
            // Making Main & Login Singletons, but keeping data pages Transient so they reload fresh data when clicked.
            services.AddSingleton<MainViewModel>();
            services.AddTransient<LoginViewModel>();
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
            services.AddTransient<UserManagementViewModel>();

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
            // Tell WPF not to kill the app when the login window closes
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

                // Restore normal shutdown behavior so the app closes when the Main Window closes
                Application.Current.ShutdownMode = ShutdownMode.OnLastWindowClose;

                mainWindow.Show();
            }
            else
            {
                // User explicitly closed the login window, shut it down completely
                Application.Current.Shutdown();
            }
        }
    }
}