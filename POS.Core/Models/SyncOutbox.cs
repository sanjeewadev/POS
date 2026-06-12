namespace POS.Core.Models
{
    public class SyncOutbox
    {
        public int Id { get; set; }
        public string TableName { get; set; } = string.Empty;
        public int RecordId { get; set; }
        public string Action { get; set; } = string.Empty; // INSERT, UPDATE, DELETE
        public bool IsSynced { get; set; } = false;
    }
}