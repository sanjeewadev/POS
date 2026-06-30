using System;

namespace POS.Core.Models.DTOs
{
    public class CashierBatchDto
    {
        public int ItemBatchId { get; set; }

        public int ItemVariantId { get; set; }

        public string BatchNo { get; set; } = string.Empty;

        public DateTime? ExpiryDate { get; set; }

        public DateTime ReceivedDate { get; set; }

        public decimal CostPrice { get; set; }

        public decimal RetailPrice { get; set; }

        public decimal WholesalePrice { get; set; }

        public decimal AvailableQty { get; set; }

        public bool IsExpired =>
            ExpiryDate.HasValue &&
            ExpiryDate.Value.Date < DateTime.Today;

        public bool IsNearExpiry =>
            ExpiryDate.HasValue &&
            ExpiryDate.Value.Date >= DateTime.Today &&
            ExpiryDate.Value.Date <= DateTime.Today.AddDays(30);

        public bool IsSelectable =>
            AvailableQty > 0 &&
            !IsExpired;

        public string ExpiryDisplayText =>
            ExpiryDate.HasValue
                ? ExpiryDate.Value.ToString("yyyy-MM-dd")
                : "-";

        public string ReceivedDateDisplayText =>
            ReceivedDate.ToString("yyyy-MM-dd");

        public string BatchDisplayText =>
            string.IsNullOrWhiteSpace(BatchNo)
                ? "-"
                : BatchNo;

        public string WarningText
        {
            get
            {
                if (IsExpired)
                    return "EXPIRED";

                if (IsNearExpiry)
                    return "NEAR EXPIRY";

                return string.Empty;
            }
        }
    }
}