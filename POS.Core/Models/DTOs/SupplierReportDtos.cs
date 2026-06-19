using System;

namespace POS.Core.Models.DTOs
{
    public class AgedPayableDto
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;

        // Standard Accounting Aging Buckets
        public decimal CurrentTo30Days { get; set; }
        public decimal Days31To60 { get; set; }
        public decimal Days61To90 { get; set; }
        public decimal Over90Days { get; set; }

        // Auto-calculated total
        public decimal TotalOwed => CurrentTo30Days + Days31To60 + Days61To90 + Over90Days;
    }

    public class SupplierVolumeDto
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;

        public decimal TotalGrnValue { get; set; }
        public double PercentageOfTotalStore { get; set; }
    }

    public class SupplierReturnRateDto
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;

        public decimal TotalItemsBought { get; set; }
        public decimal TotalItemsReturned { get; set; }

        // Safe division to prevent DivideByZero exceptions
        public double DefectPercentage => TotalItemsBought == 0
            ? 0
            : Math.Round((double)(TotalItemsReturned / TotalItemsBought) * 100, 2);
    }
}