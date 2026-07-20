using POS.Core.Configuration;
using POS.Core.Data.Configuration;
using POS.Core.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace POS.Cashier.UI.Services
{
    internal static class CashierConnectionDiagnosticsService
    {
        public static async Task<string> CreateReportAsync(
            Exception? failure,
            CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(
                LocalLogService.LogFolderPath);

            string reportPath = Path.Combine(
                LocalLogService.LogFolderPath,
                $"Cashier_Connection_Diagnostics_" +
                $"{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            var builder = new StringBuilder();
            builder.AppendLine(
                "ADVANCED POS CASHIER CONNECTION DIAGNOSTICS");
            builder.AppendLine(
                new string('=', 58));
            builder.AppendLine(
                $"Generated: {DateTimeOffset.Now:O}");
            builder.AppendLine(
                $"Product version: {ProductReleaseInfo.ProductVersion}");
            builder.AppendLine(
                $"Computer: {Environment.MachineName}");
            builder.AppendLine(
                $"Windows user: {Environment.UserName}");
            builder.AppendLine(
                $"Application: {Environment.ProcessPath ?? "Unknown"}");
            builder.AppendLine(
                $"Application folder: {AppContext.BaseDirectory}");
            builder.AppendLine();

            DatabaseConnectionSettingsStore settingsStore =
                new();

            builder.AppendLine("DATABASE PROFILE");
            builder.AppendLine(
                $"Path: {settingsStore.SettingsPath}");
            builder.AppendLine(
                $"Exists: {File.Exists(settingsStore.SettingsPath)}");

            DatabaseConnectionSettings? settings = null;

            try
            {
                settings = settingsStore.LoadOrDefault();

                builder.AppendLine(
                    $"Provider: {settings.Provider}");
                builder.AppendLine(
                    $"Server: {Display(settings.ServerHost)}");
                builder.AppendLine(
                    $"Port: {settings.ServerPort}");
                builder.AppendLine(
                    $"Database: {Display(settings.DatabaseName)}");
                builder.AppendLine(
                    $"Application login: {Display(settings.UserName)}");
                builder.AppendLine(
                    $"Connect timeout: {settings.ConnectTimeoutSeconds} seconds");
                builder.AppendLine(
                    "Password: [not written to diagnostics]");
            }
            catch (Exception ex)
            {
                builder.AppendLine(
                    $"Profile read result: FAILED - {ex.Message}");
            }

            builder.AppendLine();
            builder.AppendLine("NETWORK ADAPTERS");

            try
            {
                foreach (NetworkInterface adapter in
                         NetworkInterface.GetAllNetworkInterfaces()
                             .OrderBy(
                                 value => value.Name,
                                 StringComparer.OrdinalIgnoreCase))
                {
                    if (adapter.NetworkInterfaceType is
                        NetworkInterfaceType.Loopback or
                        NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }

                    IPInterfaceProperties properties =
                        adapter.GetIPProperties();

                    string addresses = string.Join(
                        ", ",
                        properties.UnicastAddresses
                            .Select(value => value.Address)
                            .Where(value =>
                                value.AddressFamily ==
                                AddressFamily.InterNetwork)
                            .Select(value => value.ToString()));

                    builder.AppendLine(
                        $"{adapter.Name}: " +
                        $"{adapter.OperationalStatus}; " +
                        $"IPv4={Display(addresses)}");
                }
            }
            catch (Exception ex)
            {
                builder.AppendLine(
                    $"Network adapter inspection failed: {ex.Message}");
            }

            if (settings?.IsCentralSqlServer == true)
            {
                builder.AppendLine();
                builder.AppendLine("SERVER NAME RESOLUTION");

                try
                {
                    IPAddress[] addresses = await Dns
                        .GetHostAddressesAsync(
                            settings.ServerHost,
                            cancellationToken);

                    string resolved = string.Join(
                        ", ",
                        addresses
                            .Where(value =>
                                value.AddressFamily ==
                                AddressFamily.InterNetwork)
                            .Select(value => value.ToString()));

                    builder.AppendLine(
                        $"Resolved IPv4 addresses: {Display(resolved)}");
                }
                catch (Exception ex)
                {
                    builder.AppendLine(
                        $"Name resolution failed: {ex.Message}");
                }

                builder.AppendLine();
                builder.AppendLine("TCP ENDPOINT TEST");

                try
                {
                    using var timeout =
                        CancellationTokenSource
                            .CreateLinkedTokenSource(
                                cancellationToken);

                    timeout.CancelAfter(
                        TimeSpan.FromSeconds(6));

                    using var client = new TcpClient();
                    await client.ConnectAsync(
                        settings.ServerHost,
                        settings.ServerPort,
                        timeout.Token);

                    builder.AppendLine(
                        $"TCP {settings.ServerHost}:" +
                        $"{settings.ServerPort}: REACHABLE");
                }
                catch (Exception ex)
                {
                    builder.AppendLine(
                        $"TCP {settings.ServerHost}:" +
                        $"{settings.ServerPort}: FAILED - " +
                        ex.Message);
                }
            }

            builder.AppendLine();
            builder.AppendLine("STARTUP FAILURE");
            builder.AppendLine(
                failure?.ToString() ??
                "No startup exception was supplied.");

            await File.WriteAllTextAsync(
                reportPath,
                builder.ToString(),
                new UTF8Encoding(false),
                cancellationToken);

            LocalLogService.WriteInformation(
                "Cashier",
                "Connection diagnostics",
                $"Connection diagnostics were saved to {reportPath}");

            return reportPath;
        }

        public static bool TryOpenReportFolder(
            string reportPath,
            out string message)
        {
            try
            {
                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{reportPath}\"",
                        UseShellExecute = true
                    });

                message =
                    "The diagnostic report was saved and selected in File Explorer.";
                return true;
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Open connection diagnostic report",
                    ex);

                message =
                    $"The report was saved to {reportPath}, but File Explorer " +
                    "could not be opened.";
                return false;
            }
        }

        private static string Display(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? "-"
                : value.Trim();
    }
}
