using System;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Core.Data;
using POS.Core.Enums;
using POS.Core.Repositories; // Access to the database repositories
using POS.Core.Services;     // Access to the Security State Manager
using POS.Cashier.UI.ViewModels;
using POS.Cashier.UI.Views;

namespace POS.Cashier.UI
{
    public partial class App : System.Windows.Application
    {
        // The Dependency Injection Factory for the Cashier App
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

            // ==========================================
            // 2. Core Repositories & Services
            // ==========================================
            services.AddTransient<UserRepository>();

            // CRITICAL FIX: Add the Item Master Repository so the Cashier can search barcodes
            services.AddTransient<ItemMasterRepository>();

            // CRITICAL ARCHITECTURE: AuthService MUST be a Singleton so it 
            // remembers who is logged in across the entire application lifecycle.
            services.AddSingleton<AuthService>();

            // ==========================================
            // 3. Cashier ViewModels
            // ==========================================
            services.AddTransient<LoginViewModel>();

            // CRITICAL FIX: Uncommented the Sales Logic Engine so DI can inject the ItemMasterRepository!
            services.AddTransient<SalesViewModel>();

            return services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (Services == null) return;

            // ==========================================
            // DATABASE AUTO-BUILDER (Failsafe)
            // ==========================================
            var dbFactory = Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using (var context = dbFactory.CreateDbContext())
            {
                context.Database.EnsureCreated();
            }

            // 1. Ask the Factory to build the Login Brain (It auto-injects AuthService & DbContext!)
            var loginViewModel = Services.GetRequiredService<LoginViewModel>();

            // 2. Create the Login Face (View) and connect the Brain
            var loginWindow = new LoginView
            {
                DataContext = loginViewModel
            };

            // 3. Listen for the "Shout" (The LoginSuccessful Event)
            loginViewModel.LoginSuccessful += (UserRole role) =>
            {
                // Both Admins and Cashiers are allowed to open the register.
                // Later, we will use the 'role' variable to hide/show specific admin buttons on the POS.

                SalesView salesWindow = new SalesView();

                // Make the Sales screen the official main window
                this.MainWindow = salesWindow;
                salesWindow.Show();

                // CRITICAL: Destroy the Login window completely to free up RAM and prevent users from going "back" to it.
                loginWindow.Close();
            };

            // 4. Boot up the application by showing the Login Screen first
            this.MainWindow = loginWindow;
            loginWindow.Show();
        }
    }
}