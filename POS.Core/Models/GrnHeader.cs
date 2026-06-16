using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class GrnHeader
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string GrnNumber { get; set; } = string.Empty; // e.g., GRN-2024-0001

        // Optional Link to a Purchase Order
        public int? PurchaseOrderId { get; set; }

        // Foreign Key to Supplier
        [Required]
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string SupplierInvoiceNo { get; set; } = string.Empty;

        public DateTime InvoiceDate { get; set; } = DateTime.Now;
        public DateTime ReceivedDate { get; set; } = DateTime.Now;
        public DateTime DueDate { get; set; } = DateTime.Now.AddDays(30);

        public int CreditDays { get; set; } = 30;

        [MaxLength(500)]
        public string Remarks { get; set; } = string.Empty;

        // --- GLOBAL FINANCIALS ---
        public bool IsTaxInclusive { get; set; } = false;
        public decimal Subtotal { get; set; } = 0m;
        public decimal GlobalBillDiscount { get; set; } = 0m;
        public decimal FreightAmount { get; set; } = 0m;
        public decimal TotalDiscountAmount { get; set; } = 0m; // Sum of line discounts + global discount
        public decimal NetPayable { get; set; } = 0m; // The actual amount added to Supplier Ledger

        // Document Status (e.g., Draft, Posted, Canceled)
        [MaxLength(20)]
        public string Status { get; set; } = "Draft";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        // Add this directly above or below Subtotal


        [MaxLength(50)]
        public string CreatedBy { get; set; } = string.Empty; // User who processed the GRN

        // Navigation: One GRN contains Many Items
        public ICollection<GrnLine> GrnLines { get; set; } = new List<GrnLine>();
    }
}