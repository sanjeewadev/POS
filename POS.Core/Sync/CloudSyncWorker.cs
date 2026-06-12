//using System;
//using System.Diagnostics;
//using System.Threading;
//using System.Threading.Tasks;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Hosting;
//using POS.Application.Interfaces;

//namespace POS.Infrastructure.Sync
//{
//    // BackgroundService keeps this running continuously in the background
//    public class CloudSyncWorker : BackgroundService
//    {
//        private readonly IServiceProvider _serviceProvider;

//        public CloudSyncWorker(IServiceProvider serviceProvider)
//        {
//            _serviceProvider = serviceProvider;
//        }

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            Debug.WriteLine("Cloud Sync Worker has started...");

//            // This loop runs forever until the application is closed
//            while (!stoppingToken.IsCancellationRequested)
//            {
//                try
//                {
//                    // We create a "scope" to safely use our database services in the background
//                    using (var scope = _serviceProvider.CreateScope())
//                    {
//                        var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();

//                        // 1. Upload offline sales and audit logs to the cloud
//                        await syncService.PushOfflineDataToCloudAsync();

//                        // 2. Download new products or price changes from the cloud
//                        await syncService.PullUpdatesFromCloudAsync();
//                    }
//                }
//                catch (Exception ex)
//                {
//                    // If the internet drops completely, it catches the error here.
//                    // The system will not crash; it will simply wait and try again later.
//                    Debug.WriteLine($"Cloud Sync Error: No internet or server down. Details: {ex.Message}");
//                }

//                // Wait for 1 minute before trying to sync again (adjust this time as needed)
//                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
//            }
//        }
//    }
//}