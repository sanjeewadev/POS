using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class CashDrawerEvent
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ShiftSessionId { get; set; }

        [Required]
        [MaxLength(20)]
        public string TerminalNo { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CashierName { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string EventType { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Note { get; set; } = string.Empty;

        [MaxLength(100)]
        public string AuthorizedBy { get; set; } = string.Empty;

        public int? SalesHeaderId { get; set; }
        public int? CashMovementId { get; set; }

        public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

        public bool Succeeded { get; set; }

        [MaxLength(500)]
        public string FailureMessage { get; set; } = string.Empty;

        [ForeignKey(nameof(ShiftSessionId))]
        public virtual ShiftSession? ShiftSession { get; set; }
    }
}
