using System;
using System.IO;

namespace POS.Core.Data
{
    public static class DatabasePathProvider
    {
        private const string ApplicationFolderName = "POS";
        private const string DatabaseFileName = "pos_local.db";

        public static string DatabaseFolderPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ApplicationFolderName);

        public static string DatabaseFilePath =>
            Path.Combine(DatabaseFolderPath, DatabaseFileName);

        public static string ConnectionString
        {
            get
            {
                Directory.CreateDirectory(DatabaseFolderPath);

                return $"Data Source={DatabaseFilePath}";
            }
        }
    }
}