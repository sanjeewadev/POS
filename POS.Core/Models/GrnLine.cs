using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class GrnLine
    {
        public int Id { get; set; }

        public int GrnHeaderId { get; set; }

        public GrnHeader GrnHeader { get; set; } = null!;

        [Required]
        public int ItemVariantId { get; set; }

        public ItemVariant ItemVariant { get; set; } = null!;

        public int? PoLineId { get; set; }

        public PoLine? PoLine { get; set; }

        public int? ItemBatchId { get; set; }

        public ItemBatch? ItemBatch { get; set; }

        // =========================================================
        // UI HELPERS - NOT SAVED
        // =========================================================

        [NotMapped]
        public string ItemCode { get; set; } = string.Empty;

        [NotMapped]
        public string SkuCode { get; set; } = string.Empty;

        [NotMapped]
        public string VariantDescription { get; set; } = string.Empty;

        [NotMapped]
        public string Description { get; set; } = string.Empty;

        [NotMapped]
        public string PrintName { get; set; } = string.Empty;

        [NotMapped]
        public string Barcode { get; set; } = string.Empty;

        [NotMapped]
        public decimal RemainingPoQty => OrderedQty - ReceivedQty < 0
            ? 0m
            : OrderedQty - ReceivedQty;

        [NotMapped]
        public string FullDisplayName =>
            BuildDisplayName(Description, VariantDescription, fallback: ItemCode);

        [NotMapped]
        public string ReceiptDisplayName =>
            BuildDisplayName(
                string.IsNullOrWhiteSpace(PrintName) ? Description : PrintName,
                VariantDescription,
                fallback: FullDisplayName);

        [NotMapped]
        public string DisplayName => FullDisplayName;

        [NotMapped]
        public string VariantDisplayName =>
            IsStandardVariant(VariantDescription) ? "Standard" : NormalizeText(VariantDescription);

        [NotMapped]
        public string BatchDisplayText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(BatchNo))
                    return "[AUTO]";

                return BatchNo.Trim();
            }
        }

        [NotMapped]
        public string ExpiryDisplayText
        {
            get
            {
                if (!ExpiryDate.HasValue)
                    return "No Expiry";

                return ExpiryDate.Value.ToString("yyyy-MM-dd");
            }
        }

        [NotMapped]
        public decimal GrossAmount => Math.Round(ReceivedQty * UnitCost, 2);

        [NotMapped]
        public string VatDisplayText
        {
            get
            {
                if (VatRatePercent <= 0)
                    return "No VAT";

                return IsVatIncluded
                    ? $"{VatRatePercent:0.##}% Inc."
                    : $"{VatRatePercent:0.##}% Ex.";
            }
        }

        // =========================================================
        // LOGISTICS
        // =========================================================

        [MaxLength(50)]
        public string BatchNo { get; set; } = string.Empty;

        public DateTime? ExpiryDate { get; set; }

        [MaxLength(20)]
        public string Uom { get; set; } = string.Empty;

        // =========================================================
        // QUANTITIES
        // =========================================================

        [Column(TypeName = "decimal(18,3)")]
        public decimal OrderedQty { get; set; } = 0m;

        [Column(TypeName = "decimal(18,3)")]
        public decimal ReceivedQty { get; set; } = 0m;

        // =========================================================
        // COST / DISCOUNT / VAT
        // =========================================================

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; } = 0m;

        [MaxLength(20)]
        public string LineDiscountMode { get; set; } = "Amount";

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineDiscountValue { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineDiscount { get; set; } = 0m;

        [Column(TypeName = "decimal(5,2)")]
        public decimal VatRatePercent { get; set; } = 0m;

        // False = VAT added on top.
        // True = UnitCost already includes VAT.
        public bool IsVatIncluded { get; set; } = false;

        [Column(TypeName = "decimal(18,2)")]
        public decimal VatAmount { get; set; } = 0m;

        // Landed cost after discount, VAT logic, freight, and global discount allocation.
        // VAT is never included in stock value when it is claimable.
        [Column(TypeName = "decimal(18,2)")]
        public decimal LandedCost { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; } = 0m;

        // =========================================================
        // SELLING PRICE UPDATE SNAPSHOT
        // =========================================================

        public bool UpdateSellingPrices { get; set; } = false;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentRetailPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal NewRetailPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentWholesalePrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal NewWholesalePrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentMinimumPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal NewMinimumPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentMaximumPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal NewMaximumPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal RetailMarkupPercent { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal WholesaleMarkupPercent { get; set; } = 0m;

        [MaxLength(30)]
        public string LineStatus { get; set; } = "Posted";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        private static string BuildDisplayName(
            string? baseName,
            string? variantDescription,
            string? fallback)
        {
            string cleanBaseName = NormalizeText(baseName);
            string cleanVariant = NormalizeText(variantDescription);
            string cleanFallback = NormalizeText(fallback);

            if (IsStandardVariant(cleanVariant))
            {
                if (!string.IsNullOrWhiteSpace(cleanBaseName))
                    return cleanBaseName;

                return cleanFallback;
            }

            if (string.IsNullOrWhiteSpace(cleanBaseName))
                return cleanVariant;

            return $"{cleanBaseName} - {cleanVariant}";
        }

        private static bool IsStandardVariant(string? value)
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
