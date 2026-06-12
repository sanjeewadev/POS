using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class Item
    {
        [Key]
        public int Id { get; set; }

        // --- 1. Core Identity ---
        [Required]
        [MaxLength(50)]
        public string Barcode { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string ItemCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(30)]
        public string ReceiptDescription { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? SinhalaDescription { get; set; }

        [MaxLength(150)]
        public string? TamilDescription { get; set; }

        // --- 2. Classification (Foreign Keys) ---
        public int CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public virtual Category? Category { get; set; }

        public int? SubCategoryId { get; set; } // Nullable, as not every item needs a sub-category
        [ForeignKey("SubCategoryId")]
        public virtual SubCategory? SubCategory { get; set; }

        public int SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }

        // Note: Assuming you have a ProductUOM model. If not, you can change this to a string for now.
        public int UomId { get; set; }

        // --- 3. Pricing Strategy ---
        public decimal CostPrice { get; set; }
        public decimal RetailPrice { get; set; }
        public decimal WholesalePrice { get; set; }

        // --- 4. Operations & Rules ---
        public int ReorderLevel { get; set; }
        public int ReorderQty { get; set; }

        [MaxLength(50)]
        public string? BinLocation { get; set; }

        public bool IsActive { get; set; } = true;
        public bool AllowDiscounts { get; set; } = true;
        public bool LockPriceAtPos { get; set; } = true;
    }
}