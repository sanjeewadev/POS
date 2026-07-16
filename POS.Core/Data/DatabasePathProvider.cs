using System;
using System.IO;

namespace POS.Core.Data
{
    public static class DatabasePathProvider
    {
        private const string ApplicationFolderName = "POS";
        private const string DatabaseFileName = "pos_local.db";
        public const string DatabasePathEnvironmentVariable =
            "POS_SQLITE_DATABASE_PATH";

        public static string DatabaseFolderPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ApplicationFolderName);

        public static string DatabaseFilePath
        {
            get
            {
                string? overridePath = Environment.GetEnvironmentVariable(
                    DatabasePathEnvironmentVariable);

                return string.IsNullOrWhiteSpace(overridePath)
                    ? Path.Combine(DatabaseFolderPath, DatabaseFileName)
                    : Path.GetFullPath(overridePath.Trim());
            }
        }

        public static string ConnectionString
        {
            get
            {
                string databasePath = DatabaseFilePath;
                string? folder = Path.GetDirectoryName(databasePath);
                if (!string.IsNullOrWhiteSpace(folder))
                    Directory.CreateDirectory(folder);

                return $"Data Source={databasePath}";
            }
        }
    }
}