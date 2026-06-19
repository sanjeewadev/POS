using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class ExpressItemLayout
    {
        [Key]
        public int Id { get; set; }

        // Link directly to the specific sellable item
        [Required]
        public int ItemVariantId { get; set; }

        [ForeignKey("ItemVariantId")]
        public virtual ItemVariant? ItemVariant { get; set; }

        // Grouping for the POS screen (e.g., "Bakery", "Beverages", "Produce")
        [Required]
        [MaxLength(50)]
        public string TabCategory { get; set; } = "General";

        // The text printed on the touch button
        [Required]
        [MaxLength(50)]
        public string DisplayLabel { get; set; } = string.Empty;

        // Visual design for the cashier
        [MaxLength(9)]
        public string ButtonColorHex { get; set; } = "#005555";

        [MaxLength(9)]
        public string TextColorHex { get; set; } = "#FFFFFF";

        // Grid Positioning (X, Y coordinates on the screen)
        [Required]
        public int GridRow { get; set; }

        [Required]
        public int GridColumn { get; set; }

        public bool IsActive { get; set; } = true;
    }
}