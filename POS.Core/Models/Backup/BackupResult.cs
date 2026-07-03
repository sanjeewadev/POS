using System;

namespace POS.Core.Models.Backup
{
    public class BackupResult
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public string BackupFilePath { get; set; } = string.Empty;

        public string BackupFileName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public long FileSizeBytes { get; set; }

        public string Checksum { get; set; } = string.Empty;

        public string DisplayFileSize => FormatFileSize(FileSizeBytes);

        public string CreatedAtText => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");

        public string StatusText => Success ? "Success" : "Failed";

        private static string FormatFileSize(long bytes)
        {
            if (bytes <= 0)
                return "0 KB";

            double size = bytes;

            string[] units =
            {
                "B",
                "KB",
                "MB",
                "GB"
            };

            int unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return $"{size:N2} {units[unitIndex]}";
        }
    }
}