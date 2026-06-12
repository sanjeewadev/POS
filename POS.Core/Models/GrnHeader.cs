using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class GrnHeader
    {
        [Key]
        public int Id { get; set; }

        // --- Core Identity ---
        [Required]
        [MaxLength(50)]
        public string GrnNumber { get; set; } = string.Empty; // e.g., GRN-260611-0001

        [Required]
        [MaxLength(50)]
        public string SupplierInvoiceNo { get; set; } = string.Empty; // The physical paper bill number

        [MaxLength(50)]
        public string? ReferencePoNo { get; set; } // Nullable if Direct Intake

        // --- Relationships ---
        public int SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }

        // --- Dates & Rules ---
        public DateTime ReceivedDate { get; set; } = DateTime.Now;
        public DateTime DueDate { get; set; } // ReceivedDate + Supplier.DefaultCreditDays

        [MaxLength(500)]
        public string? Remarks { get; set; }

        public string Status { get; set; } = "DRAFT"; // DRAFT or POSTED

        // --- Financial Totals (The Footer) ---
        public decimal Subtotal { get; set; }
        public decimal BillDiscountAmount { get; set; }
        public decimal FreightAmount { get; set; } // Landed Cost
        public decimal TaxAmount { get; set; }     // VAT/Taxes
        public decimal NetPayable { get; set; }    // The final debt to supplier

        // --- Navigation Property for the Grid ---
        public virtual ICollection<GrnDetail> GrnDetails { get; set; } = new List<GrnDetail>();
    }
}