using System;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Core.Data;
using POS.Core.Enums;
using POS.Cashier.UI.ViewModels; // Points to its OWN ViewModels now!
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

            // 1. Shared Database Engine from Core
            services.AddDbContextFactory<AppDbContext>(options =>
                options.UseSqlite("Data Source=pos_local.db"));

            // 2. Repositories (Add whatever repositories the Cashier needs here)
            // services.AddTransient<UserRepository>(); // Example: Needed for Login

            // 3. Cashier ViewModels
            services.AddTransient<LoginViewModel>();
            
            return services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (Services == null) return;

            // 1. Ask the Factory to build the Brain (It automatically injects the database!)
            var loginViewModel = Services.GetRequiredService<LoginViewModel>();

            // 2. Create the Face (View)
            var loginWindow = new LoginView();

            // 3. Connect the Brain to the Face
            loginWindow.DataContext = loginViewModel;

            // 4. Listen for the "Shout" (The LoginSuccessful Event)
            loginViewModel.LoginSuccessful += delegate (UserRole role)
            {
                // Both Admins and Cashiers are allowed to open the register.
                SalesView salesWindow = new SalesView();

                // Make the Sales screen the official main window
                this.MainWindow = salesWindow;
                salesWindow.Show();

                // CRITICAL: Destroy the Login window completely to free up RAM!
                loginWindow.Close();
            };

            // 5. Boot up the application by showing the Login Screen first
            this.MainWindow = loginWindow;
            loginWindow.Show();
        }
    }
}