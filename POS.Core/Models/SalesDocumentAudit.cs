using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class SalesDocumentAudit
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SalesHeaderId { get; set; }

        [Required]
        [MaxLength(20)]
        public string DocumentType { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string DocumentNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string EventType { get; set; } = string.Empty;

        public int CopyNumber { get; set; }

        public bool IsSuccessful { get; set; }

        public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(100)]
        public string PerformedBy { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string TerminalNo { get; set; } = string.Empty;

        [MaxLength(200)]
        public string PrinterName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string ErrorMessage { get; set; } = string.Empty;

        [ForeignKey(nameof(SalesHeaderId))]
        public virtual SalesHeader? SalesHeader { get; set; }
    }
}
