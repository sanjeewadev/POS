using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS.Core.Models
{
    // CRITICAL RULE: Ensures no two buttons can overlap on the exact same row/col on the same tab.
    [Index(nameof(TabName), nameof(GridRow), nameof(GridColumn), IsUnique = true)]
    public class ExpressItemMap
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string TabName { get; set; } = string.Empty; // e.g., "Produce", "Bakery"

        [Required]
        public int GridRow { get; set; } // 0 to 4 (Y-Axis)

        [Required]
        public int GridColumn { get; set; } // 0 to 3 (X-Axis)

        [Required]
        public int ItemVariantId { get; set; } // The actual item being sold

        [Required]
        [MaxLength(20)]
        public string ButtonLabel { get; set; } = string.Empty; // The short text on the screen

        [Required]
        [MaxLength(10)]
        public string ButtonColor { get; set; } = "#374151"; // Hex color code

        // Optional: Navigation Property to link to the actual item details
        [ForeignKey("ItemVariantId")]
        public virtual ItemVariant? ItemVariant { get; set; }
    }
}