using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class SupplierReturnHeader
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(30)]
        public string ReturnNumber { get; set; } = string.Empty;

        [Required]
        public int SupplierId { get; set; }

        [ForeignKey(nameof(SupplierId))]
        public virtual Supplier? Supplier { get; set; }

        // Optional link to original GRN.
        public int? GrnHeaderId { get; set; }

        [ForeignKey(nameof(GrnHeaderId))]
        public virtual GrnHeader? GrnHeader { get; set; }

        [MaxLength(50)]
        public string OriginalInvoiceNo { get; set; } = string.Empty;

        [Required]
        public DateTime ReturnDate { get; set; } = DateTime.Now;

        [Required]
        [MaxLength(50)]
        public string AuthorizedBy { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Remarks { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossCredit { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal RestockingFee { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetCredit { get; set; } = 0m;

        // Draft, Posted, Cancelled
        [Required]
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

        public virtual ICollection<SupplierReturnLine> ReturnLines { get; set; } = new List<SupplierReturnLine>();

        [NotMapped]
        public bool IsEditable => Status == "Draft";

        [NotMapped]
        public bool IsPosted => Status == "Posted";
    }
}