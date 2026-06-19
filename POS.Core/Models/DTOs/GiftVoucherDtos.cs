using System;

namespace POS.Core.Models.DTOs
{
    // Used for the Back-Office Admin DataGrid
    public class GiftVoucherSummaryDto
    {
        public int Id { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public decimal InitialAmount { get; set; }
        public decimal CurrentBalance { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? ActivationDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

    // Used by the Cashier Terminal when a customer tries to pay with a voucher
    public class VoucherValidationResultDto
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public decimal AvailableBalance { get; set; }
        public int VoucherId { get; set; }
    }
}