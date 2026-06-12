namespace POS.Core.Models
{
    public class Creditor
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public string Phone { get; set; } = string.Empty;
        public decimal CurrentBalance { get; set; }
    }
}