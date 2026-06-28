using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class GrnHeader
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string GrnNumber { get; set; } = string.Empty;

        // Optional link to Purchase Order.
        public int? PurchaseOrderId { get; set; }

        public PoHeader? PurchaseOrder { get; set; }

        [Required]
        public int SupplierId { get; set; }

        public Supplier Supplier { get; set; } = null!;

        // Supplier/vendor invoice number.
        // Must be unique per supplier in AppDbContext.
        [Required]
        [MaxLength(50)]
        public string SupplierInvoiceNo { get; set; } = string.Empty;

        public DateTime InvoiceDate { get; set; } = DateTime.Now;

        public DateTime ReceivedDate { get; set; } = DateTime.Now;

        public DateTime DueDate { get; set; } = DateTime.Now.AddDays(30);

        public int CreditDays { get; set; } = 30;

        [MaxLength(500)]
        public string Remarks { get; set; } = string.Empty;

        // =========================================================
        // FINANCIAL TOTALS
        // =========================================================

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal GlobalBillDiscount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal FreightAmount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalDiscountAmount { get; set; } = 0m;

        // Final amount posted to supplier ledger.
        [Column(TypeName = "decimal(18,2)")]
        public decimal NetPayable { get; set; } = 0m;

        // Draft, Posted, Cancelled.
        [MaxLength(30)]
        public string Status { get; set; } = "Draft";

        [MaxLength(50)]
        public string CreatedBy { get; set; } = string.Empty;

        [MaxLength(50)]
        public string PostedBy { get; set; } = string.Empty;

        [MaxLength(50)]
        public string CancelledBy { get; set; } = string.Empty;

        [MaxLength(250)]
        public string CancellationReason { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? PostedAt { get; set; }

        public DateTime? CancelledAt { get; set; }

        public ICollection<GrnLine> GrnLines { get; set; } = new List<GrnLine>();

        [NotMapped]
        public bool IsPosted => Status == "Posted";

        [NotMapped]
        public bool IsCancelled => Status == "Cancelled";
    }
}