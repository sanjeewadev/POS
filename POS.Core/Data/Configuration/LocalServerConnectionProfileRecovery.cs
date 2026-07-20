using POS.Core.Services;
using System;
using System.IO;
using System.Text.Json;

namespace POS.Core.Data.Configuration
{
    public static class LocalServerConnectionProfileRecovery
    {
        private const string DisableEnvironmentVariable =
            "POS_DISABLE_LOCAL_SERVER_PROFILE_RECOVERY";

        public static DatabaseConnectionSettings RepairIfApplicable(
            DatabaseConnectionSettingsStore settingsStore,
            DatabaseConnectionSettings settings,
            string applicationName)
        {
            ArgumentNullException.ThrowIfNull(settingsStore);
            ArgumentNullException.ThrowIfNull(settings);

            string logApplicationName =
                string.IsNullOrWhiteSpace(applicationName)
                    ? "POS"
                    : applicationName.Trim();

            if (!settings.IsCentralSqlServer ||
                IsLoopback(settings.ServerHost) ||
                IsDisabled())
            {
                return settings;
            }

            string deploymentPath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                "Advanced POS",
                "deployment.server.json");

            if (!File.Exists(deploymentPath))
                return settings;

            try
            {
                using JsonDocument document =
                    JsonDocument.Parse(
                        File.ReadAllText(deploymentPath));

                JsonElement root = document.RootElement;

                string recordedHost =
                    GetString(root, "ServerHost");
                string recordedDatabase =
                    GetString(root, "DatabaseName");
                string recordedLogin =
                    GetString(root, "ApplicationLogin");
                string serverComputerName =
                    GetString(root, "ServerComputerName");
                string installRoot =
                    GetString(root, "InstallRoot");
                int recordedPort =
                    GetInt32(root, "Port");

                bool computerMatches =
                    string.IsNullOrWhiteSpace(serverComputerName) ||
                    string.Equals(
                        serverComputerName,
                        Environment.MachineName,
                        StringComparison.OrdinalIgnoreCase);

                bool deploymentMatches =
                    computerMatches &&
                    string.Equals(
                        recordedHost,
                        settings.ServerHost,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        recordedDatabase,
                        settings.DatabaseName,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        recordedLogin,
                        settings.UserName,
                        StringComparison.OrdinalIgnoreCase) &&
                    recordedPort == settings.ServerPort &&
                    IsValidServerInstallation(installRoot);

                if (!deploymentMatches)
                    return settings;

                var repaired =
                    new DatabaseConnectionSettings
                    {
                        FormatVersion = settings.FormatVersion,
                        Provider = settings.Provider,
                        ServerHost = "localhost",
                        ServerPort = settings.ServerPort,
                        DatabaseName = settings.DatabaseName,
                        UserName = settings.UserName,
                        Password = settings.Password,
                        ConnectTimeoutSeconds =
                            settings.ConnectTimeoutSeconds,
                        Encrypt = settings.Encrypt,
                        TrustServerCertificate =
                            settings.TrustServerCertificate
                    };

                settingsStore.Save(repaired);

                LocalLogService.WriteInformation(
                    logApplicationName,
                    "Local server profile recovery",
                    "An existing same-computer Server deployment was verified. " +
                    "The local POS database endpoint was changed from the old " +
                    "LAN address to localhost so router IP changes do not " +
                    "interrupt local operation.");

                return repaired;
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    logApplicationName,
                    "Local server profile recovery",
                    ex);

                return settings;
            }
        }

        private static bool IsDisabled() =>
            string.Equals(
                Environment.GetEnvironmentVariable(
                    DisableEnvironmentVariable),
                "1",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                Environment.GetEnvironmentVariable(
                    DisableEnvironmentVariable),
                "true",
                StringComparison.OrdinalIgnoreCase);

        private static bool IsLoopback(string host) =>
            string.Equals(
                host?.Trim(),
                "localhost",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                host?.Trim(),
                "127.0.0.1",
                StringComparison.OrdinalIgnoreCase);

        private static bool IsValidServerInstallation(
            string installRoot)
        {
            if (string.IsNullOrWhiteSpace(installRoot))
                return false;

            string fullRoot = Path.GetFullPath(installRoot);

            return File.Exists(Path.Combine(
                       fullRoot,
                       "BackOffice",
                       "POS.BackOffice.UI.exe")) &&
                   File.Exists(Path.Combine(
                       fullRoot,
                       "DatabaseSetup",
                       "POS.Database.Setup.exe"));
        }

        private static string GetString(
            JsonElement root,
            string name)
        {
            return root.TryGetProperty(
                       name,
                       out JsonElement value) &&
                   value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }

        private static int GetInt32(
            JsonElement root,
            string name)
        {
            return root.TryGetProperty(
                       name,
                       out JsonElement value) &&
                   value.TryGetInt32(out int result)
                ? result
                : 0;
        }
    }
}
