using System;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class DocumentSequence
    {
        // Primary Key (e.g., "GRN", "INV", "PO")
        [Key]
        [MaxLength(10)]
        public string DocumentType { get; set; } = string.Empty;

        // E.g., "GRN-", "INV-"
        [Required]
        [MaxLength(10)]
        public string Prefix { get; set; } = string.Empty;

        // The actual counter
        [Required]
        public int NextSequenceNumber { get; set; } = 1;

        // How many zeros to pad (e.g., length of 4 means "0001")
        public int PaddingLength { get; set; } = 4;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}