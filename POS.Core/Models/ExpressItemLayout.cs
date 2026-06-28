using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS.Core.Models
{
    // Hidden internal group is kept only for database/index compatibility.
    // The admin and cashier UI will not show categories/tabs.
    [Index(nameof(TabCategory), nameof(GridRow), nameof(GridColumn), IsUnique = true)]
    public class ExpressItemLayout
    {
        public const string MainTabCategory = "MAIN";

        [Key]
        public int Id { get; set; }

        [Required]
        public int ItemVariantId { get; set; }

        [ForeignKey(nameof(ItemVariantId))]
        public virtual ItemVariant? ItemVariant { get; set; }

        [Required]
        [MaxLength(50)]
        public string TabCategory { get; set; } = MainTabCategory;

        [Required]
        [MaxLength(50)]
        public string DisplayLabel { get; set; } = string.Empty;

        [MaxLength(9)]
        public string ButtonColorHex { get; set; } = "#005555";

        [MaxLength(9)]
        public string TextColorHex { get; set; } = "#FFFFFF";

        // User-facing position:
        // Row = Y position, Column = X position.
        // Recommended cashier grid: 5 columns, multiple rows.
        [Required]
        public int GridRow { get; set; } = 1;

        [Required]
        public int GridColumn { get; set; } = 1;

        public bool IsActive { get; set; } = true;

        [NotMapped]
        public string PositionText => $"[{GridRow}, {GridColumn}]";

        [NotMapped]
        public string ItemDisplayName
        {
            get
            {
                if (ItemVariant?.ItemParent == null)
                    return string.Empty;

                if (string.IsNullOrWhiteSpace(ItemVariant.VariantDescription) ||
                    ItemVariant.VariantDescription.Equals("Standard", System.StringComparison.OrdinalIgnoreCase))
                {
                    return ItemVariant.ItemParent.ItemName;
                }

                return $"{ItemVariant.ItemParent.ItemName} - {ItemVariant.VariantDescription}";
            }
        }

        public void ForceMainTab()
        {
            TabCategory = MainTabCategory;
        }
    }
}