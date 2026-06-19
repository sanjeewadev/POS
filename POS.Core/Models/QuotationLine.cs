using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class QuotationLine
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int QuotationHeaderId { get; set; }

        [Required]
        [MaxLength(50)]
        public string ItemCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string ItemDescription { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        // Storing Cost Price helps the manager know if the quoted price will actually make a profit!
        [Column(TypeName = "decimal(18,2)")]
        public decimal CostPrice { get; set; }

        // Navigation Property
        [ForeignKey("QuotationHeaderId")]
        public virtual QuotationHeader? QuotationHeader { get; set; }
    }
}