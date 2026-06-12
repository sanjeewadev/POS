using System;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Core.Data;
using POS.Core.Repositories;
using POS.BackOffice.UI.ViewModels;
using POS.BackOffice.UI.Views.Layout;

namespace POS.BackOffice.UI
{
    public partial class App : System.Windows.Application
    {
        // The global factory for dependency injection
        public static IServiceProvider Services { get; private set; }

        public App()
        {
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // 1. Register the Database FACTORY (The secret to speed and stability)
            services.AddDbContextFactory<AppDbContext>(options =>
                options.UseSqlite("Data Source=pos_local.db"));

            // 2. Register the Repositories (Direct classes, no interfaces!)
            services.AddTransient<CategoryRepository>();
            services.AddTransient<SubCategoryRepository>();
            services.AddTransient<AttributeGroupRepository>();
            services.AddTransient<AttributeValueRepository>();
            services.AddTransient<SupplierRepository>();
            services.AddTransient<ItemRepository>();
            services.AddTransient<GrnRepository>();

            // 3. Register the ViewModels (Singletons for instant navigation memory)
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<LoginViewModel>();
            services.AddSingleton<CategoryViewModel>();
            services.AddSingleton<SubCategoryViewModel>();
            services.AddSingleton<ItemPropertyViewModel>();
            services.AddSingleton<SupplierViewModel>();
            services.AddSingleton<ItemMasterViewModel>();
            services.AddSingleton<GrnViewModel>();

            return services.BuildServiceProvider();
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 1. Create the Main Shell Window
            var mainWindow = new ManagementShellView();

            // 2. Ask the Factory for the MainViewModel (It builds the repos automatically!)
            var mainViewModel = Services.GetRequiredService<MainViewModel>();

            // 3. Connect the Brain to the Window
            mainWindow.DataContext = mainViewModel;

            // 4. Show the Application
            mainWindow.Show();
        }
    }
}