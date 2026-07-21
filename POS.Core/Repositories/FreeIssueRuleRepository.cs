using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using POS.Core.Utilities;

namespace POS.Core.Repositories
{
    public class FreeIssueRuleSearchDto
    {
        public int Id { get; set; }

        public string RuleName { get; set; } = string.Empty;

        public string FreeIssueType { get; set; } = string.Empty;

        public string DisplayFreeIssueType { get; set; } = string.Empty;

        public string ReasonCode { get; set; } = string.Empty;

        public string ReasonName { get; set; } = string.Empty;

        public string AppliesToType { get; set; } = string.Empty;

        public string DisplayAppliesTo { get; set; } = string.Empty;

        public int? CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int? SubCategoryId { get; set; }
        public string SubCategoryName { get; set; } = string.Empty;
        public int? ItemParentId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int? ItemVariantId { get; set; }
        public string SkuCode { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;

        public int? SupplierId { get; set; }

        public string SupplierName { get; set; } = string.Empty;

        public string SupplierPromotionReference { get; set; } = string.Empty;

        public string ClaimValueMode { get; set; } = string.Empty;

        public decimal FixedClaimValue { get; set; }

        public DateTime ValidFrom { get; set; }

        public DateTime? ValidTo { get; set; }

        public bool IsActive { get; set; }

        public bool IsCurrentlyValid { get; set; }

        public decimal MaxQtyPerInvoice { get; set; }

        public decimal MaxQtyPerDay { get; set; }

        public decimal MaxValuePerInvoice { get; set; }

        public decimal MaxValuePerDay { get; set; }

        public bool RequiresManagerApproval { get; set; }

        public bool RequiresAdminApproval { get; set; }

        public bool AllowCashierWithoutApproval { get; set; }

        public decimal ManagerApprovalThreshold { get; set; }

        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public string Remarks { get; set; } = string.Empty;
    }

    public class FreeIssueRuleValidationResult
    {
        public bool IsAllowed { get; set; }

        public string Message { get; set; } = string.Empty;

        public FreeIssueRule? Rule { get; set; }

        public bool RequiresManagerApproval { get; set; }

        public bool RequiresAdminApproval { get; set; }

        public decimal ClaimValue { get; set; }

        public decimal TodayUsedQty { get; set; }

        public decimal TodayUsedValue { get; set; }
    }

    public class FreeIssueRuleUsageDto
    {
        public int RuleId { get; set; }

        public decimal TodayQty { get; set; }

        public decimal TodayValue { get; set; }
    }


    public class FreeIssueLookupDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayText => string.IsNullOrWhiteSpace(Code) ? Name : $"{Code} - {Name}";
    }

    public class FreeIssueRuleRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public FreeIssueRuleRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // =========================================================
        // SEARCH / LOAD
        // =========================================================

        public async Task<List<FreeIssueRuleSearchDto>> SearchRulesAsync(
            string filter = "All",
            string searchTerm = "",
            int take = 500)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            string safeFilter = NormalizeText(filter);
            string safeSearch = NormalizeText(searchTerm);

            DateTime today = DateTime.Today;

            var query = context.FreeIssueRules
                .AsNoTracking()
                .AsQueryable();

            if (safeFilter.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => r.IsActive);
            }
            else if (safeFilter.Equals("Inactive", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => !r.IsActive);
            }
            else if (safeFilter.Equals("Current", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r =>
                    r.IsActive &&
                    r.ValidFrom.Date <= today &&
                    (!r.ValidTo.HasValue || r.ValidTo.Value.Date >= today));
            }
            else if (safeFilter.Equals("Expired", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r =>
                    r.ValidTo.HasValue &&
                    r.ValidTo.Value.Date < today);
            }
            else if (safeFilter.Equals("ShopCost", StringComparison.OrdinalIgnoreCase) ||
                     safeFilter.Equals("Shop Cost", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => r.FreeIssueType == "ShopCost");
            }
            else if (safeFilter.Equals("SupplierClaim", StringComparison.OrdinalIgnoreCase) ||
                     safeFilter.Equals("Supplier Recoverable", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => r.FreeIssueType == "SupplierClaim");
            }

            if (!string.IsNullOrWhiteSpace(safeSearch))
            {
                string upper = safeSearch.ToUpper();

                query = query.Where(r =>
                    r.RuleName.ToUpper().Contains(upper) ||
                    r.ReasonCode.ToUpper().Contains(upper) ||
                    r.ReasonName.ToUpper().Contains(upper) ||
                    r.SupplierName.ToUpper().Contains(upper) ||
                    r.SupplierPromotionReference.ToUpper().Contains(upper) ||
                    r.ItemName.ToUpper().Contains(upper) ||
                    r.SkuCode.ToUpper().Contains(upper) ||
                    r.Barcode.ToUpper().Contains(upper));
            }

            var rules = await query
                .OrderByDescending(r => r.IsActive)
                .ThenByDescending(r => r.CreatedAt)
                .Take(take)
                .ToListAsync();

            return rules.Select(ToSearchDto).ToList();
        }

        public async Task<FreeIssueRule?> GetByIdAsync(int id)
        {
            if (id <= 0)
                return null;

            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.FreeIssueRules
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<List<FreeIssueReason>> GetActiveReasonsAsync(string freeIssueType = "All")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            string type = NormalizeText(freeIssueType);

            var query = context.FreeIssueReasons
                .AsNoTracking()
                .Where(r => r.IsActive);

            if (!string.IsNullOrWhiteSpace(type) &&
                !type.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r =>
                    r.FreeIssueType == type ||
                    r.FreeIssueType == "Both");
            }

            return await query
                .OrderBy(r => r.DisplayOrder)
                .ThenBy(r => r.ReasonName)
                .ToListAsync();
        }

        public async Task<List<FreeIssueLookupDto>> GetSupplierLookupsAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Suppliers
                .AsNoTracking()
                .Where(s => !s.IsDeactivated)
                .OrderBy(s => s.SupplierName)
                .Select(s => new FreeIssueLookupDto
                {
                    Id = s.Id,
                    Code = s.SupplierCode,
                    Name = s.SupplierName
                })
                .ToListAsync();
        }

        public async Task<List<FreeIssueLookupDto>> GetCategoryLookupsAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Categories
                .AsNoTracking()
                .Where(c => !c.IsDeactivated)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.CategoryName)
                .Select(c => new FreeIssueLookupDto
                {
                    Id = c.Id,
                    Code = c.CategoryCode,
                    Name = c.CategoryName
                })
                .ToListAsync();
        }

        public async Task<List<FreeIssueLookupDto>> GetSubCategoryLookupsAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.SubCategories
                .AsNoTracking()
                .Where(c => !c.IsDeactivated)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.SubCategoryName)
                .Select(c => new FreeIssueLookupDto
                {
                    Id = c.Id,
                    Code = c.SubCategoryCode,
                    Name = c.SubCategoryName
                })
                .ToListAsync();
        }

        public async Task<List<FreeIssueLookupDto>> GetItemParentLookupsAsync(string searchTerm = "", int take = 500)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            string search = NormalizeText(searchTerm);

            var query = context.ItemParents
                .AsNoTracking()
                .Where(i => !i.IsDeactivated);

            if (!string.IsNullOrWhiteSpace(search))
            {
                string upper = search.ToUpperInvariant();
                query = query.Where(i =>
                    i.ItemCode.ToUpper().Contains(upper) ||
                    i.ItemName.ToUpper().Contains(upper));
            }

            return await query
                .OrderBy(i => i.ItemName)
                .Take(Math.Max(1, take))
                .Select(i => new FreeIssueLookupDto
                {
                    Id = i.Id,
                    Code = i.ItemCode,
                    Name = i.ItemName
                })
                .ToListAsync();
        }

        public async Task<List<FreeIssueLookupDto>> GetItemVariantLookupsAsync(string searchTerm = "", int take = 500)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            string search = NormalizeText(searchTerm);

            var query = context.ItemVariants
                .Include(v => v.ItemParent)
                .AsNoTracking()
                .Where(v => !v.IsDeactivated && !v.ItemParent.IsDeactivated);

            if (!string.IsNullOrWhiteSpace(search))
            {
                string upper = search.ToUpperInvariant();
                query = query.Where(v =>
                    v.SkuCode.ToUpper().Contains(upper) ||
                    (v.Barcode ?? string.Empty).ToUpper().Contains(upper) ||
                    v.ItemParent.ItemName.ToUpper().Contains(upper));
            }

            return await query
                .OrderBy(v => v.ItemParent.ItemName)
                .ThenBy(v => v.SkuCode)
                .Take(Math.Max(1, take))
                .Select(v => new FreeIssueLookupDto
                {
                    Id = v.Id,
                    Code = v.SkuCode,
                    Name = string.IsNullOrWhiteSpace(v.VariantDescription) || v.VariantDescription == "Standard"
                        ? v.ItemParent.ItemName
                        : v.ItemParent.ItemName + " - " + v.VariantDescription
                })
                .ToListAsync();
        }

        // =========================================================
        // SAVE / STATUS
        // =========================================================

        public async Task<FreeIssueRule> SaveRuleAsync(FreeIssueRule rule, string savedBy)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            NormalizeRule(rule);
            ValidateRule(rule);

            using var context = await _contextFactory.CreateDbContextAsync();

            string upperName = rule.RuleName.ToUpper();

            bool duplicateName = await context.FreeIssueRules
                .AnyAsync(r => r.Id != rule.Id && r.RuleName.ToUpper() == upperName);

            if (duplicateName)
                throw new InvalidOperationException("Another free issue rule already exists with the same name.");

            DateTime now = DateTime.Now;
            string user = NormalizeText(savedBy);

            if (rule.Id == 0)
            {
                rule.CreatedAt = now;
                rule.CreatedBy = user;

                await context.FreeIssueRules.AddAsync(rule);
            }
            else
            {
                var existing = await context.FreeIssueRules
                    .FirstOrDefaultAsync(r => r.Id == rule.Id);

                if (existing == null)
                    throw new InvalidOperationException("Free issue rule was not found.");

                existing.RuleName = rule.RuleName;
                existing.FreeIssueType = rule.FreeIssueType;
                existing.FreeIssueReasonId = rule.FreeIssueReasonId;
                existing.ReasonCode = rule.ReasonCode;
                existing.ReasonName = rule.ReasonName;

                existing.SupplierId = rule.SupplierId;
                existing.SupplierName = rule.SupplierName;
                existing.SupplierPromotionReference = rule.SupplierPromotionReference;
                existing.ClaimValueMode = rule.ClaimValueMode;
                existing.FixedClaimValue = rule.FixedClaimValue;

                existing.AppliesToType = rule.AppliesToType;
                existing.CategoryId = rule.CategoryId;
                existing.CategoryName = rule.CategoryName;
                existing.SubCategoryId = rule.SubCategoryId;
                existing.SubCategoryName = rule.SubCategoryName;
                existing.ItemParentId = rule.ItemParentId;
                existing.ItemName = rule.ItemName;
                existing.ItemVariantId = rule.ItemVariantId;
                existing.SkuCode = rule.SkuCode;
                existing.Barcode = rule.Barcode;

                existing.ValidFrom = rule.ValidFrom;
                existing.ValidTo = rule.ValidTo;
                existing.IsActive = rule.IsActive;

                existing.MaxQtyPerInvoice = rule.MaxQtyPerInvoice;
                existing.MaxQtyPerDay = rule.MaxQtyPerDay;
                existing.MaxValuePerInvoice = rule.MaxValuePerInvoice;
                existing.MaxValuePerDay = rule.MaxValuePerDay;

                existing.RequiresManagerApproval = rule.RequiresManagerApproval;
                existing.RequiresAdminApproval = rule.RequiresAdminApproval;
                existing.AllowCashierWithoutApproval = rule.AllowCashierWithoutApproval;
                existing.ManagerApprovalThreshold = rule.ManagerApprovalThreshold;

                existing.UpdatedAt = now;
                existing.UpdatedBy = user;
                existing.Remarks = rule.Remarks;

                rule = existing;
            }

            await context.SaveChangesAsync();

            return rule;
        }

        public async Task SetRuleActiveAsync(int ruleId, bool isActive, string updatedBy)
        {
            if (ruleId <= 0)
                throw new InvalidOperationException("Invalid free issue rule.");

            using var context = await _contextFactory.CreateDbContextAsync();

            var rule = await context.FreeIssueRules
                .FirstOrDefaultAsync(r => r.Id == ruleId);

            if (rule == null)
                throw new InvalidOperationException("Free issue rule was not found.");

            rule.IsActive = isActive;
            rule.UpdatedAt = DateTime.Now;
            rule.UpdatedBy = NormalizeText(updatedBy);

            await context.SaveChangesAsync();
        }

        // =========================================================
        // CASHIER RULE LOOKUP
        // =========================================================

        public async Task<List<FreeIssueRule>> GetApplicableRulesAsync(
            int? itemVariantId,
            int? itemParentId,
            int? categoryId,
            int? subCategoryId,
            int? supplierId,
            IReadOnlyCollection<int>? supplierIds,
            string skuCode,
            string barcode)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            DateTime today = DateTime.Today;

            string safeSku = NormalizeText(skuCode);
            string safeBarcode = NormalizeText(barcode);

            var activeRules = await context.FreeIssueRules
                .AsNoTracking()
                .Where(r =>
                    r.IsActive &&
                    r.ValidFrom.Date <= today &&
                    (!r.ValidTo.HasValue || r.ValidTo.Value.Date >= today))
                .OrderBy(r => r.FreeIssueType)
                .ThenBy(r => r.RuleName)
                .ToListAsync();

            return activeRules
                .Where(r => DoesRuleApplyToItem(
                        r,
                        itemVariantId,
                        itemParentId,
                        categoryId,
                        subCategoryId,
                        supplierId,
                        safeSku,
                        safeBarcode) ||
                    DoesSupplierTargetApply(r, supplierIds))
                .ToList();
        }

        public async Task<FreeIssueRuleValidationResult> ValidateRuleUsageAsync(
            int ruleId,
            decimal requestedQty,
            decimal originalUnitPrice,
            decimal costPrice,
            int? itemVariantId = null,
            int? itemParentId = null,
            int? categoryId = null,
            int? subCategoryId = null,
            int? supplierId = null,
            IReadOnlyCollection<int>? supplierIds = null,
            string skuCode = "",
            string barcode = "",
            decimal alreadyInInvoiceQty = 0m,
            decimal alreadyInInvoiceValue = 0m,
            DateTime? usageDate = null)
        {
            if (ruleId <= 0)
            {
                return new FreeIssueRuleValidationResult
                {
                    IsAllowed = false,
                    Message = "Free issue rule is required."
                };
            }

            requestedQty = Math.Round(requestedQty, 3);
            originalUnitPrice = Math.Round(originalUnitPrice, 2);
            costPrice = Math.Round(costPrice, 2);

            if (requestedQty <= 0m)
            {
                return new FreeIssueRuleValidationResult
                {
                    IsAllowed = false,
                    Message = "Free issue quantity must be greater than zero."
                };
            }

            using var context = await _contextFactory.CreateDbContextAsync();

            var rule = await context.FreeIssueRules
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == ruleId);

            if (rule == null)
            {
                return new FreeIssueRuleValidationResult
                {
                    IsAllowed = false,
                    Message = "Free issue rule was not found."
                };
            }

            DateTime effectiveDate = (usageDate ?? DateTime.Now).Date;

            if (!rule.IsActive)
                return Denied(rule, "Free issue rule is inactive.");

            if (rule.ValidFrom.Date > effectiveDate)
                return Denied(rule, "Free issue rule is not active yet.");

            if (rule.ValidTo.HasValue && rule.ValidTo.Value.Date < effectiveDate)
                return Denied(rule, "Free issue rule has expired.");

            bool hasItemContext =
                itemVariantId.HasValue ||
                itemParentId.HasValue ||
                categoryId.HasValue ||
                subCategoryId.HasValue ||
                supplierId.HasValue ||
                supplierIds?.Count > 0 ||
                !string.IsNullOrWhiteSpace(skuCode) ||
                !string.IsNullOrWhiteSpace(barcode);

            if (hasItemContext &&
                !DoesRuleApplyToItem(
                    rule,
                    itemVariantId,
                    itemParentId,
                    categoryId,
                    subCategoryId,
                    supplierId,
                    NormalizeText(skuCode),
                    NormalizeText(barcode)) &&
                !DoesSupplierTargetApply(rule, supplierIds))
            {
                return Denied(rule, "The selected free issue rule does not apply to this item.");
            }

            decimal requestedValue = Math.Round(requestedQty * originalUnitPrice, 2);

            if (rule.MaxQtyPerInvoice > 0m &&
                alreadyInInvoiceQty + requestedQty > rule.MaxQtyPerInvoice)
            {
                return Denied(
                    rule,
                    $"Free issue quantity exceeds invoice limit. Limit: {QuantityDisplayFormatter.Format(rule.MaxQtyPerInvoice)}.");
            }

            if (rule.MaxValuePerInvoice > 0m &&
                alreadyInInvoiceValue + requestedValue > rule.MaxValuePerInvoice)
            {
                return Denied(
                    rule,
                    $"Free issue value exceeds invoice limit. Limit: Rs. {rule.MaxValuePerInvoice:N2}.");
            }

            var todayUsage = await GetUsageAsync(context, rule.Id, effectiveDate);

            if (rule.MaxQtyPerDay > 0m &&
                todayUsage.TodayQty + requestedQty > rule.MaxQtyPerDay)
            {
                return new FreeIssueRuleValidationResult
                {
                    IsAllowed = false,
                    Rule = rule,
                    TodayUsedQty = todayUsage.TodayQty,
                    TodayUsedValue = todayUsage.TodayValue,
                    Message = $"Free issue quantity exceeds daily limit. Used: {QuantityDisplayFormatter.Format(todayUsage.TodayQty)}, Limit: {QuantityDisplayFormatter.Format(rule.MaxQtyPerDay)}."
                };
            }

            if (rule.MaxValuePerDay > 0m &&
                todayUsage.TodayValue + requestedValue > rule.MaxValuePerDay)
            {
                return new FreeIssueRuleValidationResult
                {
                    IsAllowed = false,
                    Rule = rule,
                    TodayUsedQty = todayUsage.TodayQty,
                    TodayUsedValue = todayUsage.TodayValue,
                    Message = $"Free issue value exceeds daily limit. Used: Rs. {todayUsage.TodayValue:N2}, Limit: Rs. {rule.MaxValuePerDay:N2}."
                };
            }

            decimal claimValue = CalculateClaimValue(
                rule,
                requestedQty,
                originalUnitPrice,
                costPrice);

            (bool requiresManager, bool requiresAdmin) =
                ResolveApprovalRequirement(rule, requestedValue);

            string message = requiresAdmin
                ? "Free issue rule is valid, but Administrator approval is required."
                : requiresManager
                    ? "Free issue rule is valid, but Manager approval is required."
                    : "Free issue rule is valid.";

            return new FreeIssueRuleValidationResult
            {
                IsAllowed = true,
                Rule = rule,
                RequiresManagerApproval = requiresManager,
                RequiresAdminApproval = requiresAdmin,
                ClaimValue = claimValue,
                TodayUsedQty = todayUsage.TodayQty,
                TodayUsedValue = todayUsage.TodayValue,
                Message = message
            };
        }

        public static (bool RequiresManager, bool RequiresAdmin) ResolveApprovalRequirement(
            FreeIssueRule rule,
            decimal requestedSellingValue)
        {
            if (rule == null)
                return (false, false);

            if (rule.AllowCashierWithoutApproval)
                return (false, false);

            if (rule.RequiresAdminApproval)
                return (false, true);

            bool requiresManager = rule.RequiresManagerApproval ||
                (rule.ManagerApprovalThreshold > 0m &&
                 requestedSellingValue >= rule.ManagerApprovalThreshold);

            return (requiresManager, false);
        }

        private static FreeIssueRuleValidationResult Denied(
            FreeIssueRule rule,
            string message) =>
            new()
            {
                IsAllowed = false,
                Rule = rule,
                Message = message
            };

        public static decimal CalculateClaimValue(
            FreeIssueRule rule,
            decimal quantity,
            decimal originalUnitPrice,
            decimal costPrice)
        {
            if (rule == null)
                return 0m;

            quantity = Math.Round(quantity, 3);
            originalUnitPrice = Math.Round(originalUnitPrice, 2);
            costPrice = Math.Round(costPrice, 2);

            if (!rule.IsSupplierClaim)
                return 0m;

            if (rule.ClaimValueMode.Equals("Fixed", StringComparison.OrdinalIgnoreCase))
                return Math.Round(rule.FixedClaimValue * quantity, 2);

            if (rule.ClaimValueMode.Equals("Retail", StringComparison.OrdinalIgnoreCase))
                return Math.Round(originalUnitPrice * quantity, 2);

            return Math.Round(costPrice * quantity, 2);
        }

        // =========================================================
        // DEFAULT REASONS
        // =========================================================

        public async Task SeedDefaultReasonsAsync(string createdBy = "System")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var defaults = new List<FreeIssueReason>
            {
                new()
                {
                    ReasonCode = "CUSTOMER_GOODWILL",
                    ReasonName = "Customer Goodwill",
                    FreeIssueType = "ShopCost",
                    RequiresManagerApproval = true,
                    RequiresSupplier = false,
                    DisplayOrder = 10,
                    CreatedBy = createdBy
                },
                new()
                {
                    ReasonCode = "DAMAGED_REPLACEMENT",
                    ReasonName = "Damaged Replacement",
                    FreeIssueType = "ShopCost",
                    RequiresManagerApproval = true,
                    RequiresSupplier = false,
                    DisplayOrder = 20,
                    CreatedBy = createdBy
                },
                new()
                {
                    ReasonCode = "STAFF_ERROR",
                    ReasonName = "Staff Error Recovery",
                    FreeIssueType = "ShopCost",
                    RequiresManagerApproval = true,
                    RequiresSupplier = false,
                    DisplayOrder = 30,
                    CreatedBy = createdBy
                },
                new()
                {
                    ReasonCode = "SUPPLIER_PROMO",
                    ReasonName = "Supplier Promotion",
                    FreeIssueType = "SupplierClaim",
                    RequiresManagerApproval = false,
                    RequiresSupplier = true,
                    RequiresClaimReference = true,
                    DisplayOrder = 40,
                    CreatedBy = createdBy
                },
                new()
                {
                    ReasonCode = "BRAND_PROMO",
                    ReasonName = "Brand Promotion",
                    FreeIssueType = "SupplierClaim",
                    RequiresManagerApproval = false,
                    RequiresSupplier = true,
                    RequiresClaimReference = true,
                    DisplayOrder = 50,
                    CreatedBy = createdBy
                },
                new()
                {
                    ReasonCode = "MANAGER_OVERRIDE",
                    ReasonName = "Manager Override",
                    FreeIssueType = "Both",
                    RequiresManagerApproval = true,
                    RequiresSupplier = false,
                    DisplayOrder = 90,
                    CreatedBy = createdBy
                },
                new()
                {
                    ReasonCode = "OTHER",
                    ReasonName = "Other",
                    FreeIssueType = "Both",
                    RequiresManagerApproval = true,
                    RequiresSupplier = false,
                    DisplayOrder = 100,
                    CreatedBy = createdBy
                }
            };

            foreach (var reason in defaults)
            {
                bool exists = await context.FreeIssueReasons
                    .AnyAsync(r => r.ReasonCode == reason.ReasonCode);

                if (!exists)
                    await context.FreeIssueReasons.AddAsync(reason);
            }

            await context.SaveChangesAsync();
        }

        // =========================================================
        // HELPERS
        // =========================================================

        internal static async Task<FreeIssueRuleUsageDto> GetUsageAsync(
            AppDbContext context,
            int ruleId,
            DateTime date)
        {
            DateTime from = date.Date;
            DateTime to = from.AddDays(1);

            var rows = await context.SalesLines
                .AsNoTracking()
                .Where(l =>
                    l.IsFreeItem &&
                    l.FreeIssueRuleId == ruleId &&
                    l.SalesHeader.Status == "Completed" &&
                    !l.SalesHeader.IsVoided &&
                    l.CreatedAt >= from &&
                    l.CreatedAt < to)
                .Select(l => new
                {
                    l.Quantity,
                    l.FreeIssueSellingValue
                })
                .ToListAsync();

            return new FreeIssueRuleUsageDto
            {
                RuleId = ruleId,
                TodayQty = Math.Round(rows.Sum(r => r.Quantity), 3),
                TodayValue = Math.Round(rows.Sum(r => r.FreeIssueSellingValue), 2)
            };
        }

        private static bool DoesSupplierTargetApply(
            FreeIssueRule rule,
            IReadOnlyCollection<int>? supplierIds)
        {
            return rule.AppliesToType.Equals("Supplier", StringComparison.OrdinalIgnoreCase) &&
                   rule.SupplierId.HasValue &&
                   supplierIds != null &&
                   supplierIds.Contains(rule.SupplierId.Value);
        }

        public static bool DoesRuleApplyToItem(
            FreeIssueRule rule,
            int? itemVariantId,
            int? itemParentId,
            int? categoryId,
            int? subCategoryId,
            int? supplierId,
            string skuCode,
            string barcode)
        {
            string appliesTo = NormalizeText(rule.AppliesToType);

            if (appliesTo.Equals("All", StringComparison.OrdinalIgnoreCase))
                return true;

            if (appliesTo.Equals("Category", StringComparison.OrdinalIgnoreCase))
                return rule.CategoryId.HasValue &&
                       categoryId.HasValue &&
                       rule.CategoryId.Value == categoryId.Value;

            if (appliesTo.Equals("SubCategory", StringComparison.OrdinalIgnoreCase))
                return rule.SubCategoryId.HasValue &&
                       subCategoryId.HasValue &&
                       rule.SubCategoryId.Value == subCategoryId.Value;

            if (appliesTo.Equals("ItemParent", StringComparison.OrdinalIgnoreCase))
                return rule.ItemParentId.HasValue &&
                       itemParentId.HasValue &&
                       rule.ItemParentId.Value == itemParentId.Value;

            if (appliesTo.Equals("ItemVariant", StringComparison.OrdinalIgnoreCase))
            {
                if (rule.ItemVariantId.HasValue &&
                    itemVariantId.HasValue &&
                    rule.ItemVariantId.Value == itemVariantId.Value)
                    return true;

                if (!string.IsNullOrWhiteSpace(rule.SkuCode) &&
                    !string.IsNullOrWhiteSpace(skuCode) &&
                    rule.SkuCode.Equals(skuCode, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (!string.IsNullOrWhiteSpace(rule.Barcode) &&
                    !string.IsNullOrWhiteSpace(barcode) &&
                    rule.Barcode.Equals(barcode, StringComparison.OrdinalIgnoreCase))
                    return true;

                return false;
            }

            if (appliesTo.Equals("Supplier", StringComparison.OrdinalIgnoreCase))
            {
                return rule.SupplierId.HasValue &&
                       supplierId.HasValue &&
                       rule.SupplierId.Value == supplierId.Value;
            }

            return false;
        }

        private static FreeIssueRuleSearchDto ToSearchDto(FreeIssueRule rule)
        {
            return new FreeIssueRuleSearchDto
            {
                Id = rule.Id,
                RuleName = rule.RuleName,
                FreeIssueType = rule.FreeIssueType,
                DisplayFreeIssueType = rule.DisplayFreeIssueType,
                ReasonCode = rule.ReasonCode,
                ReasonName = rule.ReasonName,
                AppliesToType = rule.AppliesToType,
                DisplayAppliesTo = rule.DisplayAppliesTo,
                CategoryId = rule.CategoryId,
                CategoryName = rule.CategoryName,
                SubCategoryId = rule.SubCategoryId,
                SubCategoryName = rule.SubCategoryName,
                ItemParentId = rule.ItemParentId,
                ItemName = rule.ItemName,
                ItemVariantId = rule.ItemVariantId,
                SkuCode = rule.SkuCode,
                Barcode = rule.Barcode,
                SupplierId = rule.SupplierId,
                SupplierName = rule.SupplierName,
                SupplierPromotionReference = rule.SupplierPromotionReference,
                ClaimValueMode = rule.ClaimValueMode,
                FixedClaimValue = rule.FixedClaimValue,
                ValidFrom = rule.ValidFrom,
                ValidTo = rule.ValidTo,
                IsActive = rule.IsActive,
                IsCurrentlyValid = rule.IsCurrentlyValid,
                MaxQtyPerInvoice = rule.MaxQtyPerInvoice,
                MaxQtyPerDay = rule.MaxQtyPerDay,
                MaxValuePerInvoice = rule.MaxValuePerInvoice,
                MaxValuePerDay = rule.MaxValuePerDay,
                RequiresManagerApproval = rule.RequiresManagerApproval,
                RequiresAdminApproval = rule.RequiresAdminApproval,
                AllowCashierWithoutApproval = rule.AllowCashierWithoutApproval,
                ManagerApprovalThreshold = rule.ManagerApprovalThreshold,
                CreatedBy = rule.CreatedBy,
                CreatedAt = rule.CreatedAt,
                Remarks = rule.Remarks
            };
        }

        private static void NormalizeRule(FreeIssueRule rule)
        {
            rule.RuleName = NormalizeText(rule.RuleName);
            rule.FreeIssueType = NormalizeFreeIssueType(rule.FreeIssueType);

            rule.ReasonCode = NormalizeText(rule.ReasonCode).ToUpper();
            rule.ReasonName = NormalizeText(rule.ReasonName);

            rule.SupplierName = NormalizeText(rule.SupplierName);
            rule.SupplierPromotionReference = NormalizeText(rule.SupplierPromotionReference);

            rule.ClaimValueMode = NormalizeClaimValueMode(rule.ClaimValueMode);

            rule.AppliesToType = NormalizeAppliesToType(rule.AppliesToType);
            rule.CategoryName = NormalizeText(rule.CategoryName);
            rule.SubCategoryName = NormalizeText(rule.SubCategoryName);
            rule.ItemName = NormalizeText(rule.ItemName);
            rule.SkuCode = NormalizeText(rule.SkuCode);
            rule.Barcode = NormalizeText(rule.Barcode);
            rule.CreatedBy = NormalizeText(rule.CreatedBy);
            rule.UpdatedBy = NormalizeText(rule.UpdatedBy);
            rule.Remarks = NormalizeText(rule.Remarks);

            rule.MaxQtyPerInvoice = Math.Round(rule.MaxQtyPerInvoice, 3);
            rule.MaxQtyPerDay = Math.Round(rule.MaxQtyPerDay, 3);
            rule.MaxValuePerInvoice = Math.Round(rule.MaxValuePerInvoice, 2);
            rule.MaxValuePerDay = Math.Round(rule.MaxValuePerDay, 2);
            rule.FixedClaimValue = Math.Round(rule.FixedClaimValue, 2);
            rule.ManagerApprovalThreshold = Math.Round(rule.ManagerApprovalThreshold, 2);
        }

        private static void ValidateRule(FreeIssueRule rule)
        {
            if (string.IsNullOrWhiteSpace(rule.RuleName))
                throw new InvalidOperationException("Rule name is required.");

            if (string.IsNullOrWhiteSpace(rule.ReasonCode) || string.IsNullOrWhiteSpace(rule.ReasonName))
                throw new InvalidOperationException("Free Issue reason is required.");

            if (rule.ValidTo.HasValue && rule.ValidTo.Value.Date < rule.ValidFrom.Date)
                throw new InvalidOperationException("Valid To date cannot be earlier than Valid From date.");

            if (rule.MaxQtyPerInvoice < 0m ||
                rule.MaxQtyPerDay < 0m ||
                rule.MaxValuePerInvoice < 0m ||
                rule.MaxValuePerDay < 0m ||
                rule.ManagerApprovalThreshold < 0m)
            {
                throw new InvalidOperationException("Free issue limits and approval threshold cannot be negative.");
            }

            if (rule.AllowCashierWithoutApproval &&
                (rule.RequiresManagerApproval || rule.RequiresAdminApproval))
            {
                throw new InvalidOperationException(
                    "Cashier-without-approval cannot be combined with Manager or Administrator approval.");
            }

            if (rule.RequiresAdminApproval)
                rule.RequiresManagerApproval = false;

            if (rule.IsSupplierClaim)
            {
                if (!rule.SupplierId.HasValue || rule.SupplierId.Value <= 0)
                    throw new InvalidOperationException("Supplier is required for supplier recoverable free issue rule.");

                if (string.IsNullOrWhiteSpace(rule.SupplierName))
                    throw new InvalidOperationException("Supplier name is required for supplier recoverable free issue rule.");
            }

            if (rule.AppliesToType.Equals("Category", StringComparison.OrdinalIgnoreCase) &&
                (!rule.CategoryId.HasValue || rule.CategoryId.Value <= 0))
                throw new InvalidOperationException("Category is required for category free issue rule.");

            if (rule.AppliesToType.Equals("SubCategory", StringComparison.OrdinalIgnoreCase) &&
                (!rule.SubCategoryId.HasValue || rule.SubCategoryId.Value <= 0))
                throw new InvalidOperationException("SubCategory is required for subcategory free issue rule.");

            if (rule.AppliesToType.Equals("ItemParent", StringComparison.OrdinalIgnoreCase) &&
                (!rule.ItemParentId.HasValue || rule.ItemParentId.Value <= 0))
                throw new InvalidOperationException("Item is required for item free issue rule.");

            if (rule.AppliesToType.Equals("ItemVariant", StringComparison.OrdinalIgnoreCase) &&
                (!rule.ItemVariantId.HasValue || rule.ItemVariantId.Value <= 0) &&
                string.IsNullOrWhiteSpace(rule.SkuCode) &&
                string.IsNullOrWhiteSpace(rule.Barcode))
                throw new InvalidOperationException("Item variant, SKU, or barcode is required for item variant free issue rule.");

            if (rule.AppliesToType.Equals("Supplier", StringComparison.OrdinalIgnoreCase) &&
                (!rule.SupplierId.HasValue || rule.SupplierId.Value <= 0))
                throw new InvalidOperationException("Supplier is required for supplier free issue rule.");

            if (rule.ClaimValueMode.Equals("Fixed", StringComparison.OrdinalIgnoreCase) &&
                rule.FixedClaimValue <= 0m)
                throw new InvalidOperationException("Fixed claim value must be greater than zero.");
        }

        private static string NormalizeFreeIssueType(string? value)
        {
            string text = NormalizeText(value);

            return FreeIssueTypeCodes.Normalize(text);
        }

        private static string NormalizeClaimValueMode(string? value)
        {
            string text = NormalizeText(value);

            if (text.Equals("Retail", StringComparison.OrdinalIgnoreCase))
                return "Retail";

            if (text.Equals("Fixed", StringComparison.OrdinalIgnoreCase))
                return "Fixed";

            return "Cost";
        }

        private static string NormalizeAppliesToType(string? value)
        {
            string text = NormalizeText(value);

            if (text.Equals("All", StringComparison.OrdinalIgnoreCase))
                return "All";

            if (text.Equals("Category", StringComparison.OrdinalIgnoreCase))
                return "Category";

            if (text.Equals("SubCategory", StringComparison.OrdinalIgnoreCase))
                return "SubCategory";

            if (text.Equals("ItemParent", StringComparison.OrdinalIgnoreCase))
                return "ItemParent";

            if (text.Equals("Supplier", StringComparison.OrdinalIgnoreCase))
                return "Supplier";

            return "ItemVariant";
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}