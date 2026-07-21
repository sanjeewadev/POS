using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using POS.Core.Utilities;

namespace POS.Core.Repositories
{
    public sealed class FreeItemClaimSearchDto
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
        public decimal ReturnedQuantity { get; set; }
        public decimal NetClaimQuantity => Math.Max(0m, Quantity - ReturnedQuantity);
        public decimal CostPrice { get; set; }
        public decimal OriginalUnitPrice { get; set; }
        public decimal FreeIssueCostValue { get; set; }
        public decimal FreeIssueSellingValue { get; set; }
        public decimal ClaimValue { get; set; }
        public decimal ClaimValueReduction { get; set; }
        public decimal NetClaimValue => Math.Max(0m, ClaimValue - ClaimValueReduction);
        public string ClaimStatus { get; set; } = string.Empty;
        public string ClaimReferenceNo { get; set; } = string.Empty;
        public DateTime? SubmittedAt { get; set; }
        public DateTime? SettledAt { get; set; }
        public DateTime? RejectedAt { get; set; }
        public string SettlementType { get; set; } = string.Empty;
        public string SettlementReferenceNo { get; set; } = string.Empty;
        public string RejectReason { get; set; } = string.Empty;
        public string FreeIssueAppliedBy { get; set; } = string.Empty;
        public DateTime? FreeIssueAppliedAt { get; set; }
        public string FreeApprovedBy { get; set; } = string.Empty;
        public int? FreeApprovedByUserId { get; set; }
        public string FreeApprovedRole { get; set; } = string.Empty;
        public DateTime? FreeApprovedAt { get; set; }
        public string FreeIssueSnapshotStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Remarks { get; set; } = string.Empty;
    }

    public sealed class FreeIssueSummaryDto
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public int TotalClaimCount { get; set; }
        public int DraftCount { get; set; }
        public int SubmittedCount { get; set; }
        public int SettledCount { get; set; }
        public int RejectedCount { get; set; }
        public decimal TotalQuantity { get; set; }
        public decimal ReturnedQuantity { get; set; }
        public decimal NetQuantity { get; set; }
        public decimal TotalCostValue { get; set; }
        public decimal TotalSellingValue { get; set; }
        public decimal TotalClaimValue { get; set; }
        public decimal TotalClaimReduction { get; set; }
        public decimal NetClaimValue { get; set; }
        public decimal DraftClaimValue { get; set; }
        public decimal SubmittedClaimValue { get; set; }
        public decimal SettledClaimValue { get; set; }
        public decimal RejectedClaimValue { get; set; }

        // Kept for old bindings while the page moves from Pending to Draft.
        public int PendingCount => DraftCount;
        public decimal PendingClaimValue => DraftClaimValue;
    }

    public sealed class SupplierClaimGroupSummaryDto
    {
        public int? SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string PromotionReference { get; set; } = string.Empty;
        public int ClaimCount { get; set; }
        public decimal Quantity { get; set; }
        public decimal ClaimValue { get; set; }
    }

    public sealed class SupplierClaimExportRow
    {
        public string SupplierName { get; set; } = string.Empty;
        public string PromotionReference { get; set; } = string.Empty;
        public string ClaimStatus { get; set; } = string.Empty;
        public string ClaimReferenceNo { get; set; } = string.Empty;
        public string InvoiceNo { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public string SkuCode { get; set; } = string.Empty;
        public string ItemDescription { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
        public decimal OriginalQuantity { get; set; }
        public decimal ReturnedQuantity { get; set; }
        public decimal NetQuantity { get; set; }
        public decimal CostPrice { get; set; }
        public decimal OriginalUnitPrice { get; set; }
        public decimal OriginalClaimValue { get; set; }
        public decimal ClaimValueReduction { get; set; }
        public decimal NetClaimValue { get; set; }
        public string FreeReasonText { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;
        public string ApprovedBy { get; set; } = string.Empty;
        public string TerminalNo { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }

    public class FreeItemClaimRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public FreeItemClaimRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<List<FreeItemClaimSearchDto>> SearchClaimsAsync(
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string claimStatus = "All",
            int? supplierId = null,
            string searchTerm = "",
            int take = 1000)
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            DateTime from = (dateFrom ?? DateTime.Today.AddDays(-30)).Date;
            DateTime toExclusive = (dateTo ?? DateTime.Today).Date.AddDays(1);
            string status = NormalizeText(claimStatus);
            string search = NormalizeText(searchTerm);

            IQueryable<FreeItemClaimLog> query = context.FreeItemClaimLogs
                .AsNoTracking()
                .Where(claim => claim.InvoiceDate >= from && claim.InvoiceDate < toExclusive);

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                string normalized = NormalizeClaimStatus(status);
                query = query.Where(claim => claim.ClaimStatus == normalized);
            }

            if (supplierId.GetValueOrDefault() > 0)
                query = query.Where(claim => claim.SupplierId == supplierId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                string upper = search.ToUpperInvariant();
                query = query.Where(claim =>
                    claim.InvoiceNo.ToUpper().Contains(upper) ||
                    claim.SupplierName.ToUpper().Contains(upper) ||
                    claim.ItemDescription.ToUpper().Contains(upper) ||
                    claim.Barcode.ToUpper().Contains(upper) ||
                    claim.SkuCode.ToUpper().Contains(upper) ||
                    claim.BatchNo.ToUpper().Contains(upper) ||
                    claim.ClaimReferenceNo.ToUpper().Contains(upper) ||
                    claim.SupplierPromotionReference.ToUpper().Contains(upper));
            }

            List<FreeItemClaimLog> rows = await query
                .OrderByDescending(claim => claim.InvoiceDate)
                .ThenByDescending(claim => claim.Id)
                .Take(Math.Clamp(take, 1, 10000))
                .ToListAsync();

            Dictionary<int, (decimal Quantity, decimal Value)> adjustments =
                await LoadAdjustmentTotalsAsync(context, rows.Select(row => row.Id));

            return rows.Select(row =>
            {
                (decimal quantity, decimal value) = adjustments.GetValueOrDefault(row.Id);
                return ToSearchDto(row, quantity, value);
            }).ToList();
        }

        public async Task<FreeItemClaimLog?> GetByIdAsync(int id)
        {
            if (id <= 0)
                return null;

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            return await context.FreeItemClaimLogs.AsNoTracking().FirstOrDefaultAsync(claim => claim.Id == id);
        }

        public async Task<List<FreeIssueLookupDto>> GetSupplierLookupsAsync()
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            return await context.FreeItemClaimLogs
                .AsNoTracking()
                .Where(claim => claim.SupplierId.HasValue && claim.SupplierId.Value > 0)
                .GroupBy(claim => new { claim.SupplierId, claim.SupplierName })
                .OrderBy(group => group.Key.SupplierName)
                .Select(group => new FreeIssueLookupDto
                {
                    Id = group.Key.SupplierId!.Value,
                    Name = group.Key.SupplierName,
                    Code = string.Empty
                })
                .ToListAsync();
        }

        public async Task<List<FreeItemClaimSearchDto>> GetClaimsByInvoiceAsync(string invoiceNo)
        {
            string safe = NormalizeText(invoiceNo);
            if (string.IsNullOrWhiteSpace(safe))
                return new List<FreeItemClaimSearchDto>();

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            List<FreeItemClaimLog> claims = await context.FreeItemClaimLogs
                .AsNoTracking()
                .Where(claim => claim.InvoiceNo == safe)
                .OrderBy(claim => claim.Id)
                .ToListAsync();
            Dictionary<int, (decimal Quantity, decimal Value)> adjustments =
                await LoadAdjustmentTotalsAsync(context, claims.Select(claim => claim.Id));

            return claims.Select(claim =>
            {
                (decimal quantity, decimal value) = adjustments.GetValueOrDefault(claim.Id);
                return ToSearchDto(claim, quantity, value);
            }).ToList();
        }

        public async Task<List<SupplierClaimExportRow>> GetSupplierClaimExportRowsAsync(
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            int? supplierId = null,
            string claimStatus = "All",
            string searchTerm = "")
        {
            List<FreeItemClaimSearchDto> claims = await SearchClaimsAsync(
                dateFrom,
                dateTo,
                claimStatus,
                supplierId,
                searchTerm,
                take: 10000);

            return claims.Select(claim => new SupplierClaimExportRow
            {
                SupplierName = claim.SupplierName,
                PromotionReference = claim.SupplierPromotionReference,
                ClaimStatus = claim.ClaimStatus,
                ClaimReferenceNo = claim.ClaimReferenceNo,
                InvoiceNo = claim.InvoiceNo,
                InvoiceDate = claim.InvoiceDate,
                Barcode = claim.Barcode,
                SkuCode = claim.SkuCode,
                ItemDescription = claim.ItemDescription,
                BatchNo = claim.BatchNo,
                OriginalQuantity = claim.Quantity,
                ReturnedQuantity = claim.ReturnedQuantity,
                NetQuantity = claim.NetClaimQuantity,
                CostPrice = claim.CostPrice,
                OriginalUnitPrice = claim.OriginalUnitPrice,
                OriginalClaimValue = claim.ClaimValue,
                ClaimValueReduction = claim.ClaimValueReduction,
                NetClaimValue = claim.NetClaimValue,
                FreeReasonText = claim.FreeReasonText,
                CashierName = claim.CashierName,
                ApprovedBy = claim.FreeApprovedBy,
                TerminalNo = claim.TerminalNo,
                Remarks = claim.Remarks
            }).ToList();
        }

        public async Task<FreeIssueSummaryDto> GetFreeIssueSummaryAsync(
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            int? supplierId = null,
            string claimStatus = "All",
            string searchTerm = "")
        {
            DateTime from = (dateFrom ?? DateTime.Today.AddDays(-30)).Date;
            DateTime to = (dateTo ?? DateTime.Today).Date;
            List<FreeItemClaimSearchDto> claims = await SearchClaimsAsync(
                from,
                to,
                claimStatus,
                supplierId,
                searchTerm,
                take: 10000);

            return new FreeIssueSummaryDto
            {
                DateFrom = from,
                DateTo = to,
                TotalClaimCount = claims.Count,
                DraftCount = claims.Count(claim => claim.ClaimStatus == SupplierClaimStatusCodes.Draft),
                SubmittedCount = claims.Count(claim => claim.ClaimStatus == SupplierClaimStatusCodes.Submitted),
                SettledCount = claims.Count(claim => claim.ClaimStatus == SupplierClaimStatusCodes.Settled),
                RejectedCount = claims.Count(claim => claim.ClaimStatus == SupplierClaimStatusCodes.Rejected),
                TotalQuantity = Math.Round(claims.Sum(claim => claim.Quantity), 3),
                ReturnedQuantity = Math.Round(claims.Sum(claim => claim.ReturnedQuantity), 3),
                NetQuantity = Math.Round(claims.Sum(claim => claim.NetClaimQuantity), 3),
                TotalCostValue = Math.Round(claims.Sum(claim => claim.FreeIssueCostValue), 2),
                TotalSellingValue = Math.Round(claims.Sum(claim => claim.FreeIssueSellingValue), 2),
                TotalClaimValue = Math.Round(claims.Sum(claim => claim.ClaimValue), 2),
                TotalClaimReduction = Math.Round(claims.Sum(claim => claim.ClaimValueReduction), 2),
                NetClaimValue = Math.Round(claims.Sum(claim => claim.NetClaimValue), 2),
                DraftClaimValue = SumStatus(claims, SupplierClaimStatusCodes.Draft),
                SubmittedClaimValue = SumStatus(claims, SupplierClaimStatusCodes.Submitted),
                SettledClaimValue = SumStatus(claims, SupplierClaimStatusCodes.Settled),
                RejectedClaimValue = SumStatus(claims, SupplierClaimStatusCodes.Rejected)
            };
        }

        public async Task<List<SupplierClaimGroupSummaryDto>> GetGroupedSummaryAsync(
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            int? supplierId = null,
            string claimStatus = "All",
            string searchTerm = "")
        {
            List<FreeItemClaimSearchDto> claims = await SearchClaimsAsync(
                dateFrom,
                dateTo,
                claimStatus,
                supplierId,
                searchTerm,
                take: 10000);

            return claims
                .GroupBy(claim => new
                {
                    claim.SupplierId,
                    claim.SupplierName,
                    claim.SupplierPromotionReference
                })
                .Select(group => new SupplierClaimGroupSummaryDto
                {
                    SupplierId = group.Key.SupplierId,
                    SupplierName = group.Key.SupplierName,
                    PromotionReference = group.Key.SupplierPromotionReference,
                    ClaimCount = group.Count(),
                    Quantity = Math.Round(group.Sum(claim => claim.NetClaimQuantity), 3),
                    ClaimValue = Math.Round(group.Sum(claim => claim.NetClaimValue), 2)
                })
                .OrderBy(row => row.SupplierName)
                .ThenBy(row => row.PromotionReference)
                .ToList();
        }

        public static async Task<FreeItemClaimLog?> CreateSupplierClaimFromSaleLineAsync(
            AppDbContext context,
            SalesHeader header,
            SalesLine line)
        {
            if (!line.IsFreeItem || !line.IsSupplierRecoverable)
                return null;
            if (line.Id <= 0 || header.Id <= 0)
                throw new InvalidOperationException("The sale and Free Issue line must be saved before creating a supplier claim.");
            if (!line.SupplierId.HasValue || line.SupplierId.Value <= 0)
                throw new InvalidOperationException("Supplier is required for a supplier-funded Free Issue.");
            if (line.SupplierClaimValue <= 0m)
                throw new InvalidOperationException("Supplier claim value must be greater than zero.");

            FreeItemClaimLog? existing = await context.FreeItemClaimLogs
                .FirstOrDefaultAsync(claim => claim.SalesLineId == line.Id);
            if (existing != null)
            {
                ApplyClaimLink(line, existing);
                return existing;
            }

            string reference = $"FI-{NormalizeText(header.InvoiceNo)}-{line.Id}";
            if (reference.Length > 100)
                reference = reference[..100];

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
                FreeIssueType = FreeIssueTypeCodes.SupplierClaim,
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
                ClaimStatus = SupplierClaimStatusCodes.Draft,
                ClaimReferenceNo = reference,
                FreeIssueAppliedBy = line.FreeIssueAppliedBy,
                FreeIssueAppliedAt = line.FreeIssueAppliedAt,
                FreeApprovedBy = line.FreeApprovedBy,
                FreeApprovedByUserId = line.FreeApprovedByUserId,
                FreeApprovedRole = line.FreeApprovedRole,
                FreeApprovedAt = line.FreeApprovedAt,
                FreeIssueRuleSnapshotJson = line.FreeIssueRuleSnapshotJson,
                FreeIssueSnapshotStatus = line.FreeIssueSnapshotStatus,
                CreatedAt = DateTime.Now,
                CreatedBy = header.CashierName,
                Remarks = "Created automatically from supplier-funded Free Issue sale line."
            };

            await context.FreeItemClaimLogs.AddAsync(claim);
            await context.SaveChangesAsync();
            ApplyClaimLink(line, claim);
            return claim;
        }

        internal static async Task CreateReturnAdjustmentAsync(
            AppDbContext context,
            SalesLine sourceLine,
            CustomerReturnLine returnLine,
            string returnReference,
            string createdBy)
        {
            if (!sourceLine.IsFreeItem || !sourceLine.IsSupplierRecoverable || returnLine.QuantityReturned <= 0m)
                return;

            FreeItemClaimLog? claim = await context.FreeItemClaimLogs
                .FirstOrDefaultAsync(row => row.SalesLineId == sourceLine.Id);
            if (claim == null)
                throw new InvalidOperationException("Supplier claim was not found for the returned Free Issue line.");

            bool exists = await context.FreeItemClaimAdjustments
                .AnyAsync(adjustment => adjustment.CustomerReturnLineId == returnLine.Id);
            if (exists)
                return;

            List<FreeItemClaimAdjustment> priorAdjustments = await context.FreeItemClaimAdjustments
                .AsNoTracking()
                .Where(adjustment => adjustment.FreeItemClaimLogId == claim.Id)
                .ToListAsync();

            // SQLite does not reliably translate decimal SUM. Aggregate monetary and
            // quantity values in memory after retrieving the small claim-specific set.
            decimal priorQuantity = priorAdjustments.Sum(adjustment => adjustment.QuantityReturned);
            decimal priorValue = priorAdjustments.Sum(adjustment => adjustment.ClaimValueReduction);
            decimal remainingQuantity = Math.Max(0m, claim.Quantity - priorQuantity);
            decimal quantity = Math.Min(Math.Round(returnLine.QuantityReturned, 3), remainingQuantity);
            if (quantity <= 0m)
                return;

            decimal remainingValue = Math.Max(0m, claim.ClaimValue - priorValue);
            decimal reduction = quantity >= remainingQuantity
                ? remainingValue
                : Math.Min(remainingValue, Math.Round(claim.ClaimValue * quantity / claim.Quantity, 2));

            var adjustment = new FreeItemClaimAdjustment
            {
                FreeItemClaimLogId = claim.Id,
                CustomerReturnLineId = returnLine.Id,
                QuantityReturned = quantity,
                ClaimValueReduction = reduction,
                CreatedAt = DateTime.Now,
                CreatedBy = NormalizeText(createdBy),
                Remarks = $"Free Issue return {NormalizeText(returnReference)}; supplier claim reduced without changing lifecycle status."
            };
            await context.FreeItemClaimAdjustments.AddAsync(adjustment);
            AppendRemarks(claim, $"Customer return reduced claim by {QuantityDisplayFormatter.Format(quantity)} / Rs. {reduction:N2}.", createdBy);
        }

        public Task MarkSubmittedAsync(int claimId, string submittedBy, string remarks = "") =>
            ChangeStatusAsync(claimId, SupplierClaimStatusCodes.Submitted, submittedBy, remarks: remarks);

        public Task MarkSettledAsync(
            int claimId,
            string settledBy,
            string settlementType,
            string settlementReferenceNo = "",
            string remarks = "") =>
            ChangeStatusAsync(
                claimId,
                SupplierClaimStatusCodes.Settled,
                settledBy,
                settlementType,
                settlementReferenceNo,
                remarks: remarks);

        public Task MarkRejectedAsync(
            int claimId,
            string rejectedBy,
            string rejectReason,
            string remarks = "") =>
            ChangeStatusAsync(
                claimId,
                SupplierClaimStatusCodes.Rejected,
                rejectedBy,
                rejectReason: rejectReason,
                remarks: remarks);

        private async Task ChangeStatusAsync(
            int claimId,
            string targetStatus,
            string changedBy,
            string settlementType = "",
            string settlementReferenceNo = "",
            string rejectReason = "",
            string remarks = "")
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                if (claimId <= 0)
                    throw new InvalidOperationException("Invalid supplier claim.");

                // SQLite transactions are deferred by default. Acquire the write lock
                // before the current status is read so two operators cannot apply
                // conflicting lifecycle transitions to the same claim.
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE FreeItemClaimLogs SET UpdatedAt = UpdatedAt WHERE Id = {claimId}");

                FreeItemClaimLog claim = await GetClaimForUpdateAsync(context, claimId);
                string current = NormalizeClaimStatus(claim.ClaimStatus);
                string target = NormalizeClaimStatus(targetStatus);

                bool valid =
                    (current == SupplierClaimStatusCodes.Draft && target == SupplierClaimStatusCodes.Submitted) ||
                    (current == SupplierClaimStatusCodes.Draft && target == SupplierClaimStatusCodes.Rejected) ||
                    (current == SupplierClaimStatusCodes.Submitted && target == SupplierClaimStatusCodes.Settled) ||
                    (current == SupplierClaimStatusCodes.Submitted && target == SupplierClaimStatusCodes.Rejected);

                if (!valid)
                    throw new InvalidOperationException($"Supplier claim cannot change from {claim.ClaimStatus} to {target}.");

                DateTime now = DateTime.Now;
                string user = NormalizeText(changedBy);
                if (string.IsNullOrWhiteSpace(user))
                    throw new InvalidOperationException("Authenticated user is required for supplier claim action.");

                claim.ClaimStatus = target;
                claim.UpdatedAt = now;
                claim.UpdatedBy = user;

                if (target == SupplierClaimStatusCodes.Submitted)
                {
                    claim.SubmittedAt = now;
                    claim.SubmittedBy = user;
                }
                else if (target == SupplierClaimStatusCodes.Settled)
                {
                    claim.SettledAt = now;
                    claim.SettledBy = user;
                    claim.SettlementType = NormalizeText(settlementType);
                    claim.SettlementReferenceNo = NormalizeText(settlementReferenceNo);
                    if (string.IsNullOrWhiteSpace(claim.SettlementType))
                        throw new InvalidOperationException("Settlement type is required.");
                    if (string.IsNullOrWhiteSpace(claim.SettlementReferenceNo))
                        throw new InvalidOperationException("Settlement reference is required.");
                }
                else if (target == SupplierClaimStatusCodes.Rejected)
                {
                    claim.RejectedAt = now;
                    claim.RejectedBy = user;
                    claim.RejectReason = NormalizeText(rejectReason);
                    if (string.IsNullOrWhiteSpace(claim.RejectReason))
                        throw new InvalidOperationException("Rejection reason is required.");
                }

                AppendRemarks(claim, remarks, user);
                await UpdateLinkedSalesLineStatusAsync(context, claim);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static async Task<FreeItemClaimLog> GetClaimForUpdateAsync(AppDbContext context, int claimId)
        {
            if (claimId <= 0)
                throw new InvalidOperationException("Invalid supplier claim.");
            return await context.FreeItemClaimLogs.FirstOrDefaultAsync(claim => claim.Id == claimId)
                ?? throw new InvalidOperationException("Supplier claim was not found.");
        }

        private static async Task UpdateLinkedSalesLineStatusAsync(AppDbContext context, FreeItemClaimLog claim)
        {
            SalesLine? line = await context.SalesLines.FirstOrDefaultAsync(row => row.Id == claim.SalesLineId);
            if (line != null)
                ApplyClaimLink(line, claim);
        }

        private static void ApplyClaimLink(SalesLine line, FreeItemClaimLog claim)
        {
            line.SupplierClaimId = claim.Id;
            line.SupplierClaimStatus = claim.ClaimStatus;
            line.SupplierClaimReferenceNo = claim.ClaimReferenceNo;
        }

        private static void AppendRemarks(FreeItemClaimLog claim, string remarks, string updatedBy = "")
        {
            string safe = NormalizeText(remarks);
            if (string.IsNullOrWhiteSpace(safe))
                return;

            string stamp = $"[{DateTime.Now:yyyy-MM-dd HH:mm}] {safe}";
            claim.Remarks = string.IsNullOrWhiteSpace(claim.Remarks)
                ? stamp
                : $"{claim.Remarks}{Environment.NewLine}{stamp}";
            if (claim.Remarks.Length > 500)
                claim.Remarks = claim.Remarks[^500..];
            claim.UpdatedAt = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(updatedBy))
                claim.UpdatedBy = NormalizeText(updatedBy);
        }

        private static async Task<Dictionary<int, (decimal Quantity, decimal Value)>> LoadAdjustmentTotalsAsync(
            AppDbContext context,
            IEnumerable<int> claimIds)
        {
            int[] ids = claimIds.Distinct().ToArray();
            if (ids.Length == 0)
                return new Dictionary<int, (decimal Quantity, decimal Value)>();

            List<FreeItemClaimAdjustment> rows = await context.FreeItemClaimAdjustments
                .AsNoTracking()
                .Where(adjustment => ids.Contains(adjustment.FreeItemClaimLogId))
                .ToListAsync();

            // Aggregate decimals in memory for SQLite compatibility.
            return rows
                .GroupBy(adjustment => adjustment.FreeItemClaimLogId)
                .ToDictionary(
                    group => group.Key,
                    group => (
                        group.Sum(adjustment => adjustment.QuantityReturned),
                        group.Sum(adjustment => adjustment.ClaimValueReduction)));
        }

        private static FreeItemClaimSearchDto ToSearchDto(
            FreeItemClaimLog claim,
            decimal returnedQuantity,
            decimal claimValueReduction) =>
            new()
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
                ReturnedQuantity = Math.Round(returnedQuantity, 3),
                CostPrice = claim.CostPrice,
                OriginalUnitPrice = claim.OriginalUnitPrice,
                FreeIssueCostValue = claim.FreeIssueCostValue,
                FreeIssueSellingValue = claim.FreeIssueSellingValue,
                ClaimValue = claim.ClaimValue,
                ClaimValueReduction = Math.Round(claimValueReduction, 2),
                ClaimStatus = claim.ClaimStatus,
                ClaimReferenceNo = claim.ClaimReferenceNo,
                SubmittedAt = claim.SubmittedAt,
                SettledAt = claim.SettledAt,
                RejectedAt = claim.RejectedAt,
                SettlementType = claim.SettlementType,
                SettlementReferenceNo = claim.SettlementReferenceNo,
                RejectReason = claim.RejectReason,
                FreeIssueAppliedBy = claim.FreeIssueAppliedBy,
                FreeIssueAppliedAt = claim.FreeIssueAppliedAt,
                FreeApprovedBy = claim.FreeApprovedBy,
                FreeApprovedByUserId = claim.FreeApprovedByUserId,
                FreeApprovedRole = claim.FreeApprovedRole,
                FreeApprovedAt = claim.FreeApprovedAt,
                FreeIssueSnapshotStatus = claim.FreeIssueSnapshotStatus,
                CreatedAt = claim.CreatedAt,
                Remarks = claim.Remarks
            };

        private static decimal SumStatus(IEnumerable<FreeItemClaimSearchDto> claims, string status) =>
            Math.Round(claims.Where(claim => claim.ClaimStatus == status).Sum(claim => claim.NetClaimValue), 2);

        private static string NormalizeClaimStatus(string? value)
        {
            string status = NormalizeText(value);
            if (status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                return SupplierClaimStatusCodes.Draft;
            if (SupplierClaimStatusCodes.IsApprovedStatus(status))
                return status.Equals(SupplierClaimStatusCodes.Draft, StringComparison.OrdinalIgnoreCase)
                    ? SupplierClaimStatusCodes.Draft
                    : status.Equals(SupplierClaimStatusCodes.Submitted, StringComparison.OrdinalIgnoreCase)
                        ? SupplierClaimStatusCodes.Submitted
                        : status.Equals(SupplierClaimStatusCodes.Settled, StringComparison.OrdinalIgnoreCase)
                            ? SupplierClaimStatusCodes.Settled
                            : SupplierClaimStatusCodes.Rejected;
            return status;
        }

        private static string NormalizeText(string? value) => (value ?? string.Empty).Trim();
    }
}
