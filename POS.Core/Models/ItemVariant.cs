using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class ItemVariant
    {
        public int Id { get; set; }

        // =========================================================
        // PARENT LINK
        // =========================================================

        public int ItemParentId { get; set; }

        public ItemParent ItemParent { get; set; } = null!;

        // =========================================================
        // VARIANT IDENTITY
        // =========================================================

        [Required]
        [MaxLength(100)]
        public string SkuCode { get; set; } = string.Empty;

        // Example:
        // Red / Medium
        // 256GB / Black
        // Standard
        //
        // Important:
        // "Standard" is the internal value for no-property / single-variant items.
        // It should not be printed as part of the final product name.
        [MaxLength(250)]
        public string VariantDescription { get; set; } = string.Empty;

        // Internal barcode or supplier/manufacturer barcode.
        // Must be unique when not empty.
        [MaxLength(100)]
        public string Barcode { get; set; } = string.Empty;

        // =========================================================
        // PRICING
        // =========================================================

        // For batch-tracked items:
        // AverageCost can be updated from real batch purchases.
        //
        // For non-batch items:
        // System can still use a hidden internal stock bucket/batch,
        // and AverageCost can be used for cashier profit/reporting.
        [Column(TypeName = "decimal(18,2)")]
        public decimal AverageCost { get; set; } = 0m;

        // Default/latest cost shown in Item Master.
        // GRN/Price pages may update this later depending on your workflow.
        [Column(TypeName = "decimal(18,2)")]
        public decimal CostPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal RetailPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal WholesalePrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MinimumPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MaximumPrice { get; set; } = 0m;

        public int ReorderLevel { get; set; } = 0;

        // Used instead of hard delete after the variant has history.
        public bool IsDeactivated { get; set; } = false;

        // Display-only for UI. Actual stock should be calculated from batches/transactions.
        [NotMapped]
        public decimal TotalStockOnHand { get; set; } = 0m;

        // Used only by Item Master UI for bulk supplier assignment.
        // This is not saved to the database.
        [NotMapped]
        public bool IsSelectedForSupplierAssignment { get; set; } = false;

        // =========================================================
        // DISPLAY HELPERS - NOT SAVED
        // =========================================================
        // These are used by Item Master, GRN, PO, cashier, barcode labels,
        // and reports to show the real product name.
        //
        // They do not create database columns.

        // Used when the variant is generated in the UI before it is saved.
        // For saved records, ItemParent.ItemName is normally available.
        [NotMapped]
        public string ParentItemName { get; set; } = string.Empty;

        // Used when the variant is generated in the UI before it is saved.
        // For saved records, ItemParent.PrintName is normally available.
        [NotMapped]
        public string ParentPrintName { get; set; } = string.Empty;

        [NotMapped]
        public bool IsStandardVariant =>
            IsStandardVariantDescription(VariantDescription);

        [NotMapped]
        public string FullDisplayName
        {
            get
            {
                string baseName = GetBestParentItemName();

                return BuildDisplayName(
                    baseName,
                    VariantDescription,
                    fallback: SkuCode);
            }
        }

        [NotMapped]
        public string ReceiptDisplayName
        {
            get
            {
                string basePrintName = GetBestParentPrintName();

                return BuildDisplayName(
                    basePrintName,
                    VariantDescription,
                    fallback: FullDisplayName);
            }
        }

        [NotMapped]
        public string VariantDisplayName
        {
            get
            {
                if (IsStandardVariant)
                    return "Standard";

                return NormalizeText(VariantDescription);
            }
        }

        [NotMapped]
        public string StatusText => IsDeactivated ? "Inactive" : "Active";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? DeactivatedAt { get; set; }

        // =========================================================
        // NAVIGATION
        // =========================================================

        // Example:
        // Color -> Red
        // Size  -> Medium
        public ICollection<ItemPropertyMapping> PropertyMappings { get; set; } = new List<ItemPropertyMapping>();

        // One variant can have many physical stock batches.
        //
        // For non-batch items, the system can still store stock in one hidden
        // internal batch/bucket such as "GENERAL" or "DEFAULT".
        // Cashier should not show batch selection for non-batch items.
        public ICollection<ItemBatch> ItemBatches { get; set; } = new List<ItemBatch>();

        // Approved suppliers for this specific variant.
        public ICollection<ItemSupplier> ItemSuppliers { get; set; } = new List<ItemSupplier>();

        // =========================================================
        // DISPLAY HELPER METHODS
        // =========================================================

        private string GetBestParentItemName()
        {
            if (!string.IsNullOrWhiteSpace(ParentItemName))
                return NormalizeText(ParentItemName);

            if (ItemParent != null && !string.IsNullOrWhiteSpace(ItemParent.ItemName))
                return NormalizeText(ItemParent.ItemName);

            return string.Empty;
        }

        private string GetBestParentPrintName()
        {
            if (!string.IsNullOrWhiteSpace(ParentPrintName))
                return NormalizeText(ParentPrintName);

            if (ItemParent != null && !string.IsNullOrWhiteSpace(ItemParent.PrintName))
                return NormalizeText(ItemParent.PrintName);

            return GetBestParentItemName();
        }

        private static string BuildDisplayName(
            string baseName,
            string variantDescription,
            string fallback)
        {
            string cleanBaseName = NormalizeText(baseName);
            string cleanVariant = NormalizeText(variantDescription);
            string cleanFallback = NormalizeText(fallback);

            if (IsStandardVariantDescription(cleanVariant))
            {
                if (!string.IsNullOrWhiteSpace(cleanBaseName))
                    return cleanBaseName;

                return cleanFallback;
            }

            if (string.IsNullOrWhiteSpace(cleanBaseName))
                return cleanVariant;

            return $"{cleanBaseName} - {cleanVariant}";
        }

        private static bool IsStandardVariantDescription(string value)
        {
            string cleanValue = NormalizeText(value);

            return string.IsNullOrWhiteSpace(cleanValue) ||
                   cleanValue.Equals("Standard", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}