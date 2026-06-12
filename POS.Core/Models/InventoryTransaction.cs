using System;
using POS.Core.Enums;

namespace POS.Core.EntitModelsies
{
    public class InventoryTransaction
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int BatchId { get; set; }
        public TransactionType Type { get; set; }
        public decimal Quantity { get; set; }
        public DateTime Date { get; set; }
    }
}