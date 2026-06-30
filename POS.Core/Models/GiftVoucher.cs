using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class GiftVoucher
    {
        [Key]
        public int Id { get; set; }

        // Example: GV-000001
        [Required]
        [MaxLength(50)]
        public string VoucherNo { get; set; } = string.Empty;

        // Barcode printed on physical voucher.
        // Usually same as VoucherNo or a generated barcode number.
        [Required]
        [MaxLength(100)]
        public string Barcode { get; set; } = string.Empty;

        // For one-time voucher, this is the face value.
        // Example: Rs. 1000, Rs. 5000.
        [Column(TypeName = "decimal(18,2)")]
        public decimal VoucherAmount { get; set; } = 0m;

        // Created     = generated/printed but not sold yet
        // Active      = sold and can be redeemed
        // Redeemed    = already used
        // Expired     = expired manually/system
        // Blocked     = blocked because lost/stolen/suspicious
        // Cancelled   = cancelled/admin voided
        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "Created";

        public DateTime? ExpiryDate { get; set; }

        // Optional batch reference for admin-created voucher books.
        // Example: GVB-2026-0001
        [MaxLength(50)]
        public string BatchNo { get; set; } = string.Empty;

        // Optional denomination name.
        // Example: Gift Voucher Rs. 5000
        [MaxLength(100)]
        public string Description { get; set; } = string.Empty;

        // =========================================================
        // CREATED / PRINTED DETAILS
        // =========================================================

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime? PrintedAt { get; set; }

        [MaxLength(100)]
        public string PrintedBy { get; set; } = string.Empty;

        // =========================================================
        // SOLD / ACTIVATED DETAILS
        // =========================================================
        // Voucher becomes Active only after a successful sale.

        public DateTime? ActivatedAt { get; set; }

        public DateTime? SoldDate { get; set; }

        public int? SoldSalesHeaderId { get; set; }

        [MaxLength(50)]
        public string SoldInvoiceNo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string SoldCashierName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string SoldTerminalNo { get; set; } = string.Empty;

        // =========================================================
        // REDEEMED DETAILS
        // =========================================================
        // One-time use. After redeemed, it cannot be reused.

        public DateTime? RedeemedDate { get; set; }

        public int? RedeemedSalesHeaderId { get; set; }

        [MaxLength(50)]
        public string RedeemedInvoiceNo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string RedeemedCashierName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string RedeemedTerminalNo { get; set; } = string.Empty;

        // Amount actually applied to the invoice.
        // Example:
        // Bill = Rs. 4500, Voucher = Rs. 5000
        // RedeemedAmount = Rs. 4500 if manager allows forfeiting balance.
        [Column(TypeName = "decimal(18,2)")]
        public decimal RedeemedAmount { get; set; } = 0m;

        // For one-time voucher, balance is usually not carried.
        // If voucher value is higher than bill, remaining value is forfeited.
        [Column(TypeName = "decimal(18,2)")]
        public decimal ForfeitedAmount { get; set; } = 0m;

        // =========================================================
        // BLOCK / CANCEL DETAILS
        // =========================================================

        public DateTime? BlockedAt { get; set; }

        [MaxLength(100)]
        public string BlockedBy { get; set; } = string.Empty;

        [MaxLength(255)]
        public string BlockReason { get; set; } = string.Empty;

        public DateTime? CancelledAt { get; set; }

        [MaxLength(100)]
        public string CancelledBy { get; set; } = string.Empty;

        [MaxLength(255)]
        public string CancelReason { get; set; } = string.Empty;

        public DateTime? UpdatedAt { get; set; }

        [MaxLength(100)]
        public string UpdatedBy { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Remarks { get; set; } = string.Empty;

        // =========================================================
        // NAVIGATION
        // =========================================================

        [ForeignKey(nameof(SoldSalesHeaderId))]
        public virtual SalesHeader? SoldSalesHeader { get; set; }

        [ForeignKey(nameof(RedeemedSalesHeaderId))]
        public virtual SalesHeader? RedeemedSalesHeader { get; set; }

        public virtual ICollection<GiftVoucherTransaction> Transactions { get; set; } =
            new List<GiftVoucherTransaction>();

        // =========================================================
        // HELPER PROPERTIES
        // =========================================================

        [NotMapped]
        public bool IsCreated =>
            Status.Equals("Created", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsActive =>
            Status.Equals("Active", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsRedeemed =>
            Status.Equals("Redeemed", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsBlocked =>
            Status.Equals("Blocked", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsCancelled =>
            Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsExpiredByDate =>
            ExpiryDate.HasValue &&
            ExpiryDate.Value.Date < DateTime.Today &&
            !IsRedeemed &&
            !IsCancelled &&
            !IsBlocked;

        [NotMapped]
        public bool CanBeSold =>
            IsCreated &&
            VoucherAmount > 0m &&
            !IsExpiredByDate;

        [NotMapped]
        public bool CanBeRedeemed =>
            IsActive &&
            VoucherAmount > 0m &&
            !IsExpiredByDate;

        [NotMapped]
        public string DisplayStatus
        {
            get
            {
                if (IsExpiredByDate)
                    return "Expired";

                return Status;
            }
        }

        // =========================================================
        // TEMPORARY LEGACY COMPATIBILITY
        // =========================================================
        // These help old generated screens compile while we rebuild the new voucher module.

        [NotMapped]
        public decimal InitialAmount
        {
            get => VoucherAmount;
            set => VoucherAmount = value;
        }

        [NotMapped]
        public decimal CurrentBalance
        {
            get
            {
                if (IsRedeemed)
                    return 0m;

                return VoucherAmount;
            }
            set
            {
                // Stored-balance vouchers are not used in the new one-time design.
                // Setter exists only for old code compatibility.
            }
        }

        [NotMapped]
        public DateTime CreatedDate
        {
            get => CreatedAt;
            set => CreatedAt = value;
        }

        [NotMapped]
        public DateTime? ActivationDate
        {
            get => ActivatedAt;
            set => ActivatedAt = value;
        }
    }
}