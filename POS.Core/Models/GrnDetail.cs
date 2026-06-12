using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class GrnDetail
    {
        [Key]
        public int Id { get; set; }

        // --- Link to the Umbrella (Header) ---
        public int GrnHeaderId { get; set; }
        [ForeignKey("GrnHeaderId")]
        public virtual GrnHeader? GrnHeader { get; set; }

        // --- Link to the Product ---
        public int ItemId { get; set; }
        [ForeignKey("ItemId")]
        public virtual Item? Item { get; set; }

        // --- Receiving Metrics ---
        public int ReceivedQty { get; set; } // Quantities that increase the bill
        public int FocQty { get; set; }      // Free quantities (Increases stock, doesn't increase bill)

        // --- Line Financials ---
        public decimal UnitCost { get; set; }    // Buying price
        public decimal RetailPrice { get; set; } // Updated selling price (optional)

        public bool IsTaxable { get; set; }      // Does this specific item have VAT?
        public decimal LineTaxAmount { get; set; }

        public decimal LineDiscount { get; set; }

        public decimal LineTotal { get; set; }   // (ReceivedQty * UnitCost) - LineDiscount + LineTaxAmount
    }
}