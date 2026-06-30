using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Core.Repositories
{
    public class FreeItemClaimSearchDto
    {
        public int Id { get; set; }

        public int SalesHeaderId { get; set; }

        public int SalesLineId { get; set; }

        public string InvoiceNo { get; set; } = string.Empty;

        public DateTime InvoiceDate { get; set; }

        public string CashierName { get; set; } = string.Empty;

        public string TerminalNo { get; set; } = string.Empty;

        public int? FreeIssueRuleId { get; set; }

        public string FreeIssueRuleName { get; set; } = string.Empty;

        public string FreeReasonCode { get; set; } = string.Empty;

        public string FreeReasonText { get; set; } = string.Empty;

        public string FreeIssueType { get; set; } = string.Empty;

        public int? SupplierId { get; set; }

        public string SupplierName { get; set; } = string.Empty;

        public string SupplierPromotionReference { get; set; } = string.Empty;

        public int? ItemVariantId { get; set; }

        public int? ItemBatchId { get; set; }

        public string Barcode { get; set; } = string.Empty;

        public string SkuCode { get; set; } = string.Empty;

        public string ItemDescription { get; set; } = string.Empty;

        public string BatchNo { get; set; } = string.Empty;

        public DateTime? ExpiryDate { get; set; }

        public string Uom { get; set; } = string.Empty;

        public decimal Quantity { get; set; }

        public decimal CostPrice { get; set; }

        public decimal OriginalUnitPrice { get; set; }

        public decimal FreeIssueCostValue { get; set; }

        public decimal FreeIssueSellingValue { get; set; }

        public decimal ClaimValue { get; set; }

        public string ClaimStatus { get; set; } = string.Empty;

        public string ClaimReferenceNo { get; set; } = string.Empty;

        public DateTime? SubmittedAt { get; set; }

        public DateTime? SettledAt { get; set; }

        public DateTime? RejectedAt { get; set; }

        public DateTime? WrittenOffAt { get; set; }

        public DateTime? CancelledAt { get; set; }

        public string SettlementType { get; set; } = string.Empty;

        public string SettlementReferenceNo { get; set; } = string.Empty;

        public string FreeApprovedBy { get; set; } = string.Empty;

        public DateTime? FreeApprovedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public string Remarks { get; set; } = string.Empty;
    }

    public class FreeIssueSummaryDto
    {
        public DateTime DateFrom { get; set; }

        public DateTime DateTo { get; set; }

        public int TotalClaimCount { get; set; }

        public int PendingCount { get; set; }

        public int SubmittedCount { get; set; }

        public int SettledCount { get; set; }

        public int RejectedCount { get; set; }

        public int WrittenOffCount { get; set; }

        public int CancelledCount { get; set; }

        public decimal TotalQuantity { get; set; }

        public decimal TotalCostValue { get; set; }

        public decimal TotalSellingValue { get; set; }

        public decimal TotalClaimValue { get; set; }

        public decimal PendingClaimValue { get; set; }

        public decimal SubmittedClaimValue { get; set; }

        public decimal SettledClaimValue { get; set; }

        public decimal RejectedClaimValue { get; set; }

        public decimal WrittenOffClaimValue { get; set; }
    }

    public class SupplierClaimExportRow
    {
        public string SupplierName { get; set; } = string.Empty;

        public string ClaimStatus { get; set; } = string.Empty;

        public string ClaimReferenceNo { get; set; } = string.Empty;

        public string InvoiceNo { get; set; } = string.Empty;

        public DateTime InvoiceDate { get; set; }

        public string Barcode { get; set; } = string.Empty;

        public string SkuCode { get; set; } = string.Empty;

        public string ItemDescription { get; set; } = string.Empty;

        public string BatchNo { get; set; } = string.Empty;

        public decimal Quantity { get; set; }

        public decimal CostPrice { get; set; }

        public decimal OriginalUnitPrice { get; set; }

        public decimal ClaimValue { get; set; }

        public string FreeReasonText { get; set; } = string.Empty;

        public string SupplierPromotionReference { get; set; } = string.Empty;

        public string CashierName { get; set; } = string.Empty;

        public string TerminalNo { get; set; } = string.Empty;

        public string Remarks { get; set; } = string.Empty;
    }

    public class FreeItemClaimRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public FreeItemClaimRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // =========================================================
        // SEARCH / LOAD
        // =========================================================

        public async Task<List<FreeItemClaimSearchDto>> SearchClaimsAsync(
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string claimStatus = "All",
            int? supplierId = null,
            string searchTerm = "",
            int take = 1000)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            DateTime from = (dateFrom ?? DateTime.Today.AddDays(-30)).Date;
            DateTime toExclusive = (dateTo ?? DateTime.Today).Date.AddDays(1);

            string status = NormalizeText(claimStatus);
            string search = NormalizeText(searchTerm);

            var query = context.FreeItemClaimLogs
                .AsNoTracking()
                .Where(c => c.InvoiceDate >= from && c.InvoiceDate < toExclusive);

            if (!string.IsNullOrWhiteSpace(status) &&
                !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                status = NormalizeClaimStatus(status);
                query = query.Where(c => c.ClaimStatus == status);
            }

            if (supplierId.HasValue && supplierId.Value > 0)
                query = query.Where(c => c.SupplierId == supplierId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                string upper = search.ToUpper();

                query = query.Where(c =>
                    c.InvoiceNo.ToUpper().Contains(upper) ||
                    c.SupplierName.ToUpper().Contains(upper) ||
                    c.ItemDescription.ToUpper().Contains(upper) ||
                    c.Barcode.ToUpper().Contains(upper) ||
                    c.SkuCode.ToUpper().Contains(upper) ||
                    c.BatchNo.ToUpper().Contains(upper) ||
                    c.ClaimReferenceNo.ToUpper().Contains(upper) ||
                    c.SupplierPromotionReference.ToUpper().Contains(upper));
            }

            var rows = await query
                .OrderByDescending(c => c.InvoiceDate)
                .ThenByDescending(c => c.Id)
                .Take(take)
                .ToListAsync();

            return rows.Select(ToSearchDto).ToList();
        }

        public async Task<FreeItemClaimLog?> GetByIdAsync(int id)
        {
            if (id <= 0)
                return null;

            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.FreeItemClaimLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<List<FreeItemClaimSearchDto>> GetClaimsByInvoiceAsync(string invoiceNo)
        {
            string safeInvoiceNo = NormalizeText(invoiceNo);

            if (string.IsNullOrWhiteSpace(safeInvoiceNo))
                return new List<FreeItemClaimSearchDto>();

            using var context = await _contextFactory.CreateDbContextAsync();

            var rows = await context.FreeItemClaimLogs
                .AsNoTracking()
                .Where(c => c.InvoiceNo == safeInvoiceNo)
                .OrderBy(c => c.Id)
                .ToListAsync();

            return rows.Select(ToSearchDto).ToList();
        }

        public async Task<List<SupplierClaimExportRow>> GetSupplierClaimExportRowsAsync(
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            int? supplierId = null,
            string claimStatus = "All")
        {
            var claims = await SearchClaimsAsync(
                dateFrom,
                dateTo,
                claimStatus,
                supplierId,
                searchTerm: "",
                take: 10000);

            return claims.Select(c => new SupplierClaimExportRow
            {
                SupplierName = c.SupplierName,
                ClaimStatus = c.ClaimStatus,
                ClaimReferenceNo = c.ClaimReferenceNo,
                InvoiceNo = c.InvoiceNo,
                InvoiceDate = c.InvoiceDate,
                Barcode = c.Barcode,
                SkuCode = c.SkuCode,
                ItemDescription = c.ItemDescription,
                BatchNo = c.BatchNo,
                Quantity = c.Quantity,
                CostPrice = c.CostPrice,
                OriginalUnitPrice = c.OriginalUnitPrice,
                ClaimValue = c.ClaimValue,
                FreeReasonText = c.FreeReasonText,
                SupplierPromotionReference = c.SupplierPromotionReference,
                CashierName = c.CashierName,
                TerminalNo = c.TerminalNo,
                Remarks = c.Remarks
            }).ToList();
        }

        public async Task<FreeIssueSummaryDto> GetFreeIssueSummaryAsync(
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            int? supplierId = null)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            DateTime from = (dateFrom ?? DateTime.Today.AddDays(-30)).Date;
            DateTime toExclusive = (dateTo ?? DateTime.Today).Date.AddDays(1);

            var query = context.FreeItemClaimLogs
                .AsNoTracking()
                .Where(c => c.InvoiceDate >= from && c.InvoiceDate < toExclusive);

            if (supplierId.HasValue && supplierId.Value > 0)
                query = query.Where(c => c.SupplierId == supplierId.Value);

            var rows = await query.ToListAsync();

            decimal SumByStatus(string status)
            {
                return Math.Round(
                    rows.Where(r => r.ClaimStatus.Equals(status, StringComparison.OrdinalIgnoreCase))
                        .Sum(r => r.ClaimValue),
                    2);
            }

            int CountByStatus(string status)
            {
                return rows.Count(r => r.ClaimStatus.Equals(status, StringComparison.OrdinalIgnoreCase));
            }

            return new FreeIssueSummaryDto
            {
                DateFrom = from,
                DateTo = toExclusive.AddDays(-1),

                TotalClaimCount = rows.Count,
                PendingCount = CountByStatus("Pending"),
                SubmittedCount = CountByStatus("Submitted"),
                SettledCount = CountByStatus("Settled"),
                RejectedCount = CountByStatus("Rejected"),
                WrittenOffCount = CountByStatus("Written Off"),
                CancelledCount = CountByStatus("Cancelled"),

                TotalQuantity = Math.Round(rows.Sum(r => r.Quantity), 3),
                TotalCostValue = Math.Round(rows.Sum(r => r.FreeIssueCostValue), 2),
                TotalSellingValue = Math.Round(rows.Sum(r => r.FreeIssueSellingValue), 2),
                TotalClaimValue = Math.Round(rows.Sum(r => r.ClaimValue), 2),

                PendingClaimValue = SumByStatus("Pending"),
                SubmittedClaimValue = SumByStatus("Submitted"),
                SettledClaimValue = SumByStatus("Settled"),
                RejectedClaimValue = SumByStatus("Rejected"),
                WrittenOffClaimValue = SumByStatus("Written Off")
            };
        }

        // =========================================================
        // CHECKOUT SUPPORT
        // =========================================================

        public static async Task<FreeItemClaimLog?> CreateSupplierClaimFromSaleLineAsync(
            AppDbContext context,
            SalesHeader header,
            SalesLine line)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (header == null)
                throw new ArgumentNullException(nameof(header));

            if (line == null)
                throw new ArgumentNullException(nameof(line));

            if (!line.IsFreeItem)
                return null;

            if (!line.IsSupplierRecoverable)
                return null;

            if (line.SalesHeaderId <= 0 || line.Id <= 0)
                throw new InvalidOperationException("Sales header and sales line must be saved before creating supplier claim.");

            if (!line.SupplierId.HasValue || line.SupplierId.Value <= 0)
                throw new InvalidOperationException("Supplier is required for recoverable free issue claim.");

            if (line.SupplierClaimId.HasValue && line.SupplierClaimId.Value > 0)
                return await context.FreeItemClaimLogs.FirstOrDefaultAsync(c => c.Id == line.SupplierClaimId.Value);

            string claimReference = NormalizeText(line.SupplierClaimReferenceNo);

            if (string.IsNullOrWhiteSpace(claimReference))
                claimReference = $"FI-{header.InvoiceNo}-{line.Id}";

            var claim = new FreeItemClaimLog
            {
                SalesHeaderId = header.Id,
                SalesLineId = line.Id,
                InvoiceNo = header.InvoiceNo,
                InvoiceDate = header.TransactionDate,
                CashierName = header.CashierName,
                TerminalNo = header.TerminalNo,

                FreeIssueRuleId = line.FreeIssueRuleId,
                FreeIssueRuleName = line.FreeIssueRuleName,
                FreeReasonCode = line.FreeReasonCode,
                FreeReasonText = line.FreeReasonText,
                FreeIssueType = line.FreeIssueType,

                SupplierId = line.SupplierId,
                SupplierName = line.SupplierName,
                SupplierPromotionReference = line.SupplierPromotionReference,

                ItemVariantId = line.ItemVariantId,
                ItemBatchId = line.ItemBatchId,
                Barcode = line.Barcode,
                SkuCode = line.SkuCode,
                ItemDescription = line.ItemDescription,
                BatchNo = line.BatchNo,
                ExpiryDate = line.ExpiryDate,
                Uom = line.Uom,

                Quantity = line.Quantity,
                CostPrice = line.CostPrice,
                OriginalUnitPrice = line.OriginalUnitPrice,
                FreeIssueCostValue = line.FreeIssueCostValue,
                FreeIssueSellingValue = line.FreeIssueSellingValue,
                ClaimValue = line.SupplierClaimValue,

                ClaimStatus = "Pending",
                ClaimReferenceNo = claimReference,

                FreeApprovedBy = line.FreeApprovedBy,
                FreeApprovedAt = line.FreeApprovedAt,

                CreatedAt = DateTime.Now,
                CreatedBy = header.CashierName,
                Remarks = line.FreeReasonText
            };

            await context.FreeItemClaimLogs.AddAsync(claim);
            await context.SaveChangesAsync();

            line.SupplierClaimId = claim.Id;
            line.SupplierClaimStatus = "Pending";
            line.SupplierClaimReferenceNo = claim.ClaimReferenceNo;

            context.SalesLines.Update(line);
            await context.SaveChangesAsync();

            return claim;
        }

        // =========================================================
        // STATUS ACTIONS
        // =========================================================

        public async Task MarkSubmittedAsync(
            int claimId,
            string submittedBy,
            string claimReferenceNo = "",
            string remarks = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var claim = await GetClaimForUpdateAsync(context, claimId);

            EnsureClaimCanChange(claim);

            if (!claim.IsPending)
                throw new InvalidOperationException("Only pending claims can be marked as submitted.");

            claim.ClaimStatus = "Submitted";
            claim.SubmittedAt = DateTime.Now;
            claim.SubmittedBy = NormalizeText(submittedBy);

            if (!string.IsNullOrWhiteSpace(claimReferenceNo))
                claim.ClaimReferenceNo = NormalizeText(claimReferenceNo);

            AppendRemarks(claim, remarks);

            await UpdateLinkedSalesLineStatusAsync(context, claim);
            await context.SaveChangesAsync();
        }

        public async Task MarkSettledAsync(
            int claimId,
            string settledBy,
            string settlementType,
            string settlementReferenceNo = "",
            string remarks = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var claim = await GetClaimForUpdateAsync(context, claimId);

            EnsureClaimCanChange(claim);

            if (claim.IsRejected || claim.IsWrittenOff || claim.IsCancelled)
                throw new InvalidOperationException("Rejected, written off, or cancelled claims cannot be settled.");

            claim.ClaimStatus = "Settled";
            claim.SettledAt = DateTime.Now;
            claim.SettledBy = NormalizeText(settledBy);
            claim.SettlementType = NormalizeText(settlementType);
            claim.SettlementReferenceNo = NormalizeText(settlementReferenceNo);

            if (string.IsNullOrWhiteSpace(claim.SettlementType))
                claim.SettlementType = "Other";

            AppendRemarks(claim, remarks);

            await UpdateLinkedSalesLineStatusAsync(context, claim);
            await context.SaveChangesAsync();
        }

        public async Task MarkRejectedAsync(
            int claimId,
            string rejectedBy,
            string rejectReason,
            string remarks = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var claim = await GetClaimForUpdateAsync(context, claimId);

            EnsureClaimCanChange(claim);

            if (claim.IsSettled)
                throw new InvalidOperationException("Settled claims cannot be rejected.");

            claim.ClaimStatus = "Rejected";
            claim.RejectedAt = DateTime.Now;
            claim.RejectedBy = NormalizeText(rejectedBy);
            claim.RejectReason = NormalizeText(rejectReason);

            if (string.IsNullOrWhiteSpace(claim.RejectReason))
                throw new InvalidOperationException("Reject reason is required.");

            AppendRemarks(claim, remarks);

            await UpdateLinkedSalesLineStatusAsync(context, claim);
            await context.SaveChangesAsync();
        }

        public async Task MarkWrittenOffAsync(
            int claimId,
            string writtenOffBy,
            string writeOffReason,
            string remarks = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var claim = await GetClaimForUpdateAsync(context, claimId);

            EnsureClaimCanChange(claim);

            if (claim.IsSettled)
                throw new InvalidOperationException("Settled claims cannot be written off.");

            claim.ClaimStatus = "Written Off";
            claim.WrittenOffAt = DateTime.Now;
            claim.WrittenOffBy = NormalizeText(writtenOffBy);
            claim.WriteOffReason = NormalizeText(writeOffReason);

            if (string.IsNullOrWhiteSpace(claim.WriteOffReason))
                throw new InvalidOperationException("Write off reason is required.");

            AppendRemarks(claim, remarks);

            await UpdateLinkedSalesLineStatusAsync(context, claim);
            await context.SaveChangesAsync();
        }

        public async Task CancelClaimAsync(
            int claimId,
            string cancelledBy,
            string cancelReason,
            string remarks = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var claim = await GetClaimForUpdateAsync(context, claimId);

            EnsureClaimCanChange(claim);

            if (claim.IsSettled)
                throw new InvalidOperationException("Settled claims cannot be cancelled.");

            claim.ClaimStatus = "Cancelled";
            claim.CancelledAt = DateTime.Now;
            claim.CancelledBy = NormalizeText(cancelledBy);
            claim.CancelReason = NormalizeText(cancelReason);

            if (string.IsNullOrWhiteSpace(claim.CancelReason))
                throw new InvalidOperationException("Cancel reason is required.");

            AppendRemarks(claim, remarks);

            await UpdateLinkedSalesLineStatusAsync(context, claim);
            await context.SaveChangesAsync();
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private static async Task<FreeItemClaimLog> GetClaimForUpdateAsync(
            AppDbContext context,
            int claimId)
        {
            if (claimId <= 0)
                throw new InvalidOperationException("Invalid free item claim.");

            var claim = await context.FreeItemClaimLogs
                .FirstOrDefaultAsync(c => c.Id == claimId);

            if (claim == null)
                throw new InvalidOperationException("Free item claim was not found.");

            return claim;
        }

        private static async Task UpdateLinkedSalesLineStatusAsync(
            AppDbContext context,
            FreeItemClaimLog claim)
        {
            var line = await context.SalesLines
                .FirstOrDefaultAsync(l => l.Id == claim.SalesLineId);

            if (line == null)
                return;

            line.SupplierClaimId = claim.Id;
            line.SupplierClaimStatus = claim.ClaimStatus;
            line.SupplierClaimReferenceNo = claim.ClaimReferenceNo;
        }

        private static void EnsureClaimCanChange(FreeItemClaimLog claim)
        {
            if (claim.IsCancelled)
                throw new InvalidOperationException("Cancelled claims cannot be changed.");

            if (claim.IsSettled)
                throw new InvalidOperationException("Settled claims cannot be changed.");
        }

        private static void AppendRemarks(FreeItemClaimLog claim, string remarks)
        {
            string safeRemarks = NormalizeText(remarks);

            if (string.IsNullOrWhiteSpace(safeRemarks))
                return;

            string stamp = $"[{DateTime.Now:yyyy-MM-dd HH:mm}] {safeRemarks}";

            if (string.IsNullOrWhiteSpace(claim.Remarks))
                claim.Remarks = stamp;
            else
                claim.Remarks = $"{claim.Remarks}{Environment.NewLine}{stamp}";

            if (claim.Remarks.Length > 500)
                claim.Remarks = claim.Remarks[^500..];

            claim.UpdatedAt = DateTime.Now;
        }

        private static FreeItemClaimSearchDto ToSearchDto(FreeItemClaimLog claim)
        {
            return new FreeItemClaimSearchDto
            {
                Id = claim.Id,
                SalesHeaderId = claim.SalesHeaderId,
                SalesLineId = claim.SalesLineId,
                InvoiceNo = claim.InvoiceNo,
                InvoiceDate = claim.InvoiceDate,
                CashierName = claim.CashierName,
                TerminalNo = claim.TerminalNo,
                FreeIssueRuleId = claim.FreeIssueRuleId,
                FreeIssueRuleName = claim.FreeIssueRuleName,
                FreeReasonCode = claim.FreeReasonCode,
                FreeReasonText = claim.FreeReasonText,
                FreeIssueType = claim.FreeIssueType,
                SupplierId = claim.SupplierId,
                SupplierName = claim.SupplierName,
                SupplierPromotionReference = claim.SupplierPromotionReference,
                ItemVariantId = claim.ItemVariantId,
                ItemBatchId = claim.ItemBatchId,
                Barcode = claim.Barcode,
                SkuCode = claim.SkuCode,
                ItemDescription = claim.ItemDescription,
                BatchNo = claim.BatchNo,
                ExpiryDate = claim.ExpiryDate,
                Uom = claim.Uom,
                Quantity = claim.Quantity,
                CostPrice = claim.CostPrice,
                OriginalUnitPrice = claim.OriginalUnitPrice,
                FreeIssueCostValue = claim.FreeIssueCostValue,
                FreeIssueSellingValue = claim.FreeIssueSellingValue,
                ClaimValue = claim.ClaimValue,
                ClaimStatus = claim.ClaimStatus,
                ClaimReferenceNo = claim.ClaimReferenceNo,
                SubmittedAt = claim.SubmittedAt,
                SettledAt = claim.SettledAt,
                RejectedAt = claim.RejectedAt,
                WrittenOffAt = claim.WrittenOffAt,
                CancelledAt = claim.CancelledAt,
                SettlementType = claim.SettlementType,
                SettlementReferenceNo = claim.SettlementReferenceNo,
                FreeApprovedBy = claim.FreeApprovedBy,
                FreeApprovedAt = claim.FreeApprovedAt,
                CreatedAt = claim.CreatedAt,
                Remarks = claim.Remarks
            };
        }

        private static string NormalizeClaimStatus(string? value)
        {
            string status = NormalizeText(value);

            if (status.Equals("Submitted", StringComparison.OrdinalIgnoreCase))
                return "Submitted";

            if (status.Equals("Settled", StringComparison.OrdinalIgnoreCase))
                return "Settled";

            if (status.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
                return "Rejected";

            if (status.Equals("WrittenOff", StringComparison.OrdinalIgnoreCase) ||
                status.Equals("Written Off", StringComparison.OrdinalIgnoreCase))
                return "Written Off";

            if (status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) ||
                status.Equals("Canceled", StringComparison.OrdinalIgnoreCase))
                return "Cancelled";

            return "Pending";
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}