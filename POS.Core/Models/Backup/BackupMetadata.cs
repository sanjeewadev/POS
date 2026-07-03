using System;

namespace POS.Core.Models.Backup
{
    public class BackupMetadata
    {
        public string BackupId { get; set; } = Guid.NewGuid().ToString("N");

        public string StoreId { get; set; } = string.Empty;

        public string StoreName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string CreatedBy { get; set; } = string.Empty;

        public string AppName { get; set; } = "Advance POS System";

        public string AppVersion { get; set; } = string.Empty;

        public string DatabaseProvider { get; set; } = "SQLite";

        public string DatabaseFileName { get; set; } = "pos_local.db";

        public string MachineName { get; set; } = string.Empty;

        public string TerminalNo { get; set; } = string.Empty;

        public string Checksum { get; set; } = string.Empty;

        public string Remarks { get; set; } = string.Empty;

        public string CreatedAtText => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");

        public bool HasStoreInfo =>
            !string.IsNullOrWhiteSpace(StoreId) ||
            !string.IsNullOrWhiteSpace(StoreName);

        public string StoreDisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(StoreName))
                    return StoreName;

                if (!string.IsNullOrWhiteSpace(StoreId))
                    return StoreId;

                return "Unknown Store";
            }
        }
    }
}