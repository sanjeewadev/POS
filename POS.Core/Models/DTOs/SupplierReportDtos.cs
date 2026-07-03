using System;

namespace POS.Core.Models.DTOs
{
    public class SupplierOutstandingSummaryDto
    {
        public int SupplierId { get; set; }

        public string SupplierCode { get; set; } = string.Empty;

        public string SupplierName { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public decimal TotalGrnBilled { get; set; }

        public decimal TotalSupplierReturns { get; set; }

        public decimal TotalPaid { get; set; }

        public decimal NetOutstanding { get; set; }

        public int TransactionCount { get; set; }

        public DateTime? LastTransactionDate { get; set; }

        public string SupplierDisplayName
        {
            get
            {
                string code = NormalizeText(SupplierCode);
                string name = NormalizeText(SupplierName);
                string company = NormalizeText(CompanyName);

                string main = string.IsNullOrWhiteSpace(code)
                    ? name
                    : $"{code} - {name}";

                if (!string.IsNullOrWhiteSpace(company) &&
                    !company.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{main} ({company})";
                }

                return main;
            }
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }

    public class SupplierPurchaseVolumeDto
    {
        public int SupplierId { get; set; }

        public string SupplierCode { get; set; } = string.Empty;

        public string SupplierName { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public int GrnCount { get; set; }

        public decimal TotalGrnValue { get; set; }

        public decimal PercentageOfTotalPurchases { get; set; }

        public DateTime? LastGrnDate { get; set; }

        public string SupplierDisplayName
        {
            get
            {
                string code = NormalizeText(SupplierCode);
                string name = NormalizeText(SupplierName);
                string company = NormalizeText(CompanyName);

                string main = string.IsNullOrWhiteSpace(code)
                    ? name
                    : $"{code} - {name}";

                if (!string.IsNullOrWhiteSpace(company) &&
                    !company.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{main} ({company})";
                }

                return main;
            }
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }

    public class SupplierReturnSummaryDto
    {
        public int SupplierId { get; set; }

        public string SupplierCode { get; set; } = string.Empty;

        public string SupplierName { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public int ReturnDocumentCount { get; set; }

        public decimal TotalReturnedQty { get; set; }

        public decimal GrossReturnValue { get; set; }

        public decimal RestockingFee { get; set; }

        public decimal NetSupplierCredit { get; set; }

        public decimal PurchaseValueInPeriod { get; set; }

        public decimal ReturnValuePercentage { get; set; }

        public DateTime? LastReturnDate { get; set; }

        public string SupplierDisplayName
        {
            get
            {
                string code = NormalizeText(SupplierCode);
                string name = NormalizeText(SupplierName);
                string company = NormalizeText(CompanyName);

                string main = string.IsNullOrWhiteSpace(code)
                    ? name
                    : $"{code} - {name}";

                if (!string.IsNullOrWhiteSpace(company) &&
                    !company.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{main} ({company})";
                }

                return main;
            }
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }

    // =========================================================
    // TEMPORARY COMPATIBILITY DTOs
    // =========================================================
    // Keep these for older SupplierReportViewModel/XAML references until
    // the next files are replaced.

    public class AgedPayableDto
    {
        public int SupplierId { get; set; }

        public string SupplierName { get; set; } = string.Empty;

        public decimal CurrentTo30Days { get; set; }

        public decimal Days31To60 { get; set; }

        public decimal Days61To90 { get; set; }

        public decimal Over90Days { get; set; }

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

        public double DefectPercentage => TotalItemsBought == 0
            ? 0
            : Math.Round((double)(TotalItemsReturned / TotalItemsBought) * 100, 2);
    }
}