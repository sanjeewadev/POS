using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace POS.Core.Models.DTOs
{
    public class GrnPoLookupDto
    {
        public int PoHeaderId { get; set; }

        public string PoNumber { get; set; } = string.Empty;

        public int SupplierId { get; set; }

        public string SupplierName { get; set; } = string.Empty;

        public DateTime OrderDate { get; set; }

        public DateTime ExpectedDate { get; set; }

        public decimal NetPayable { get; set; }

        public string Status { get; set; } = string.Empty;

        public decimal TotalOrderedQty { get; set; }

        public decimal TotalReceivedQty { get; set; }

        public decimal TotalOutstandingQty => TotalOrderedQty - TotalReceivedQty < 0
            ? 0
            : TotalOrderedQty - TotalReceivedQty;

        public string DisplayText =>
            $"{PoNumber} | {SupplierName} | Outstanding: {TotalOutstandingQty:N3}";
    }

    public class GrnPoLineDto
    {
        public int PoLineId { get; set; }

        public int ItemVariantId { get; set; }

        public string ItemCode { get; set; } = string.Empty;

        public string SkuCode { get; set; } = string.Empty;

        public string Barcode { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string VariantDescription { get; set; } = string.Empty;

        public string Uom { get; set; } = string.Empty;

        public decimal OrderedQty { get; set; }

        public decimal AlreadyReceivedQty { get; set; }

        public decimal OutstandingQty => OrderedQty - AlreadyReceivedQty < 0
            ? 0
            : OrderedQty - AlreadyReceivedQty;

        public decimal ExpectedCost { get; set; }

        public bool RequiresExpiry { get; set; }

        public string DisplayName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(VariantDescription) ||
                    VariantDescription.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                {
                    return Description;
                }

                return $"{Description} - {VariantDescription}";
            }
        }
    }

    public class GrnVariantLookupDto
    {
        public int ItemVariantId { get; set; }

        public int ItemParentId { get; set; }

        public string ItemCode { get; set; } = string.Empty;

        public string SkuCode { get; set; } = string.Empty;

        public string Barcode { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string VariantDescription { get; set; } = string.Empty;

        public string Uom { get; set; } = string.Empty;

        public decimal LastSupplierCost { get; set; }

        public decimal CurrentCost { get; set; }

        public bool RequiresExpiry { get; set; }

        public string DisplayName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(VariantDescription) ||
                    VariantDescription.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                {
                    return Description;
                }

                return $"{Description} - {VariantDescription}";
            }
        }
    }

    public partial class GrnLineEntryDto : ObservableObject
    {
        public int GrnLineId { get; set; }

        public int? PoLineId { get; set; }

        public int? ItemBatchId { get; set; }

        public int ItemVariantId { get; set; }

        public string ItemCode { get; set; } = string.Empty;

        public string SkuCode { get; set; } = string.Empty;

        public string Barcode { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string VariantDescription { get; set; } = string.Empty;

        public string Uom { get; set; } = string.Empty;

        public decimal OrderedQty { get; set; }

        public decimal OutstandingPoQty { get; set; }

        public bool RequiresExpiry { get; set; }

        [ObservableProperty]
        private string _batchNo = string.Empty;

        [ObservableProperty]
        private DateTime? _expiryDate;

        [ObservableProperty]
        private decimal _receivedQty;

        [ObservableProperty]
        private decimal _unitCost;

        [ObservableProperty]
        private decimal _lineDiscount;

        [ObservableProperty]
        private decimal _landedCost;

        [ObservableProperty]
        private decimal _lineTotal;

        public string DisplayName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(VariantDescription) ||
                    VariantDescription.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                {
                    return Description;
                }

                return $"{Description} - {VariantDescription}";
            }
        }

        public string BatchDisplayText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(BatchNo))
                    return "[AUTO]";

                return BatchNo.Trim();
            }
        }

        public List<string> ValidateForPost(bool isPoLinked)
        {
            var errors = new List<string>();

            if (ItemVariantId <= 0)
                errors.Add("Invalid item variant.");

            if (ReceivedQty <= 0)
                errors.Add($"{DisplayName}: received quantity must be greater than zero.");

            if (UnitCost <= 0)
                errors.Add($"{DisplayName}: unit cost must be greater than zero.");

            if (LineDiscount < 0)
                errors.Add($"{DisplayName}: line discount cannot be negative.");

            decimal gross = ReceivedQty * UnitCost;

            if (LineDiscount > gross)
                errors.Add($"{DisplayName}: line discount cannot be greater than line value.");

            if (BatchNo != null && BatchNo.Trim().Length > 50)
                errors.Add($"{DisplayName}: batch number cannot be longer than 50 characters.");

            if (RequiresExpiry && !ExpiryDate.HasValue)
                errors.Add($"{DisplayName}: expiry date is required.");

            if (isPoLinked && OutstandingPoQty > 0 && ReceivedQty > OutstandingPoQty)
                errors.Add($"{DisplayName}: received quantity cannot exceed outstanding PO quantity.");

            return errors;
        }
    }
}