using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public class DiscountRuleRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public DiscountRuleRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // =========================================================
        // SEARCH / LOAD
        // =========================================================

        public async Task<List<DiscountRule>> SearchRulesAsync(DiscountRuleSearchDto filter)
        {
            filter ??= new DiscountRuleSearchDto();

            using var context = await _contextFactory.CreateDbContextAsync();

            IQueryable<DiscountRule> query = context.DiscountRules
                .Include(r => r.DiscountReason)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                string term = NormalizeText(filter.SearchText);

                query = query.Where(r =>
                    r.RuleName.Contains(term) ||
                    r.ReasonCode.Contains(term) ||
                    r.ReasonName.Contains(term) ||
                    r.SkuCode.Contains(term) ||
                    r.Barcode.Contains(term) ||
                    r.ItemName.Contains(term) ||
                    r.CategoryName.Contains(term) ||
                    r.SubCategoryName.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(filter.DiscountType) &&
                !filter.DiscountType.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                string type = NormalizeDiscountType(filter.DiscountType);
                query = query.Where(r => r.DiscountType == type);
            }

            if (!string.IsNullOrWhiteSpace(filter.AppliesToType) &&
                !filter.AppliesToType.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                string appliesToType = NormalizeAppliesToType(filter.AppliesToType);
                query = query.Where(r => r.AppliesToType == appliesToType);
            }

            if (filter.IsActiveOnly)
                query = query.Where(r => r.IsActive);

            if (filter.ValidTodayOnly)
            {
                DateTime today = DateTime.Today;

                query = query.Where(r =>
                    r.IsActive &&
                    (!r.ValidFrom.HasValue || r.ValidFrom.Value.Date <= today) &&
                    (!r.ValidTo.HasValue || r.ValidTo.Value.Date >= today));
            }

            return await query
                .OrderByDescending(r => r.IsActive)
                .ThenBy(r => r.RuleName)
                .Take(filter.MaxResults <= 0 ? 300 : filter.MaxResults)
                .ToListAsync();
        }

        public async Task<DiscountRule?> GetByIdAsync(int id)
        {
            if (id <= 0)
                return null;

            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.DiscountRules
                .Include(r => r.DiscountReason)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<List<DiscountReason>> GetActiveReasonsAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.DiscountReasons
                .AsNoTracking()
                .Where(r => r.IsActive)
                .OrderBy(r => r.DisplayOrder)
                .ThenBy(r => r.ReasonName)
                .ToListAsync();
        }

        public async Task<List<DiscountRule>> GetApplicableRulesAsync(
            int itemVariantId,
            string customerType = "Walk-In",
            DateTime? saleDate = null)
        {
            if (itemVariantId <= 0)
                return new List<DiscountRule>();

            using var context = await _contextFactory.CreateDbContextAsync();

            var item = await context.ItemVariants
                .Include(v => v.ItemParent)
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == itemVariantId);

            if (item == null || item.IsDeactivated)
                return new List<DiscountRule>();

            if (item.ItemParent == null || item.ItemParent.IsDeactivated || item.ItemParent.IsSaleLocked)
                return new List<DiscountRule>();

            DateTime effectiveDate = saleDate?.Date ?? DateTime.Today;
            string normalizedCustomerType = NormalizeCustomerType(customerType);

            var rules = await context.DiscountRules
                .Include(r => r.DiscountReason)
                .AsNoTracking()
                .Where(r =>
                    r.IsActive &&
                    (!r.ValidFrom.HasValue || r.ValidFrom.Value.Date <= effectiveDate) &&
                    (!r.ValidTo.HasValue || r.ValidTo.Value.Date >= effectiveDate))
                .OrderBy(r => r.RuleName)
                .ToListAsync();

            return rules
                .Where(rule => DoesRuleApplyToItem(rule, item, normalizedCustomerType))
                .ToList();
        }

        // =========================================================
        // SAVE / UPDATE
        // =========================================================

        public async Task<DiscountRule> SaveRuleAsync(DiscountRule rule, string savedBy = "System")
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            using var context = await _contextFactory.CreateDbContextAsync();

            NormalizeRule(rule);
            ValidateRule(rule);

            await ApplyReasonSnapshotAsync(context, rule);

            DateTime now = DateTime.Now;
            string safeUser = NormalizeText(savedBy);

            if (rule.Id <= 0)
            {
                rule.CreatedAt = now;
                rule.CreatedBy = safeUser;

                await context.DiscountRules.AddAsync(rule);
            }
            else
            {
                var existing = await context.DiscountRules
                    .FirstOrDefaultAsync(r => r.Id == rule.Id);

                if (existing == null)
                    throw new InvalidOperationException("Discount rule was not found.");

                existing.RuleName = rule.RuleName;
                existing.DiscountType = rule.DiscountType;
                existing.DiscountValue = rule.DiscountValue;

                existing.DiscountReasonId = rule.DiscountReasonId;
                existing.ReasonCode = rule.ReasonCode;
                existing.ReasonName = rule.ReasonName;

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
                existing.CustomerType = rule.CustomerType;

                existing.ValidFrom = rule.ValidFrom;
                existing.ValidTo = rule.ValidTo;
                existing.IsActive = rule.IsActive;

                existing.MaxDiscountAmount = rule.MaxDiscountAmount;
                existing.MaxDiscountPercent = rule.MaxDiscountPercent;
                existing.MaxValuePerInvoice = rule.MaxValuePerInvoice;
                existing.MaxValuePerDay = rule.MaxValuePerDay;
                existing.MaxQtyPerInvoice = rule.MaxQtyPerInvoice;
                existing.MaxQtyPerDay = rule.MaxQtyPerDay;

                existing.RequiresManagerApproval = rule.RequiresManagerApproval;
                existing.RequiresAdminApproval = rule.RequiresAdminApproval;
                existing.ManagerApprovalThreshold = rule.ManagerApprovalThreshold;
                existing.AllowBelowMinimumPrice = rule.AllowBelowMinimumPrice;

                existing.UpdatedAt = now;
                existing.UpdatedBy = safeUser;
                existing.Remarks = rule.Remarks;
            }

            await context.SaveChangesAsync();

            return rule;
        }

        public async Task SetRuleActiveAsync(int ruleId, bool isActive, string updatedBy = "System")
        {
            if (ruleId <= 0)
                throw new InvalidOperationException("Invalid discount rule.");

            using var context = await _contextFactory.CreateDbContextAsync();

            var rule = await context.DiscountRules
                .FirstOrDefaultAsync(r => r.Id == ruleId);

            if (rule == null)
                throw new InvalidOperationException("Discount rule was not found.");

            rule.IsActive = isActive;
            rule.UpdatedAt = DateTime.Now;
            rule.UpdatedBy = NormalizeText(updatedBy);

            await context.SaveChangesAsync();
        }

        // =========================================================
        // CASHIER VALIDATION
        // =========================================================

        public async Task<DiscountRuleValidationResult> ValidateRuleUsageAsync(
            int discountRuleId,
            int itemVariantId,
            decimal quantity,
            decimal unitPrice,
            decimal minimumPrice,
            string customerType = "Walk-In",
            bool isGiftVoucherSale = false,
            bool isFreeItem = false,
            bool isPriceOverridden = false,
            bool isManagerModeActive = false,
            DateTime? saleDate = null)
        {
            var result = new DiscountRuleValidationResult();

            if (discountRuleId <= 0)
                return result.Fail("Discount rule is required.");

            if (isGiftVoucherSale)
                return result.Fail("Discount rule cannot be applied to gift voucher sale line.");

            if (isFreeItem)
                return result.Fail("Discount rule cannot be applied to free item line.");

            if (isPriceOverridden)
                return result.Fail("Discount rule cannot be applied after New Price.");

            if (itemVariantId <= 0)
                return result.Fail("Selected item is invalid.");

            if (quantity <= 0m)
                return result.Fail("Quantity must be greater than zero.");

            if (unitPrice < 0m)
                return result.Fail("Unit price cannot be negative.");

            using var context = await _contextFactory.CreateDbContextAsync();

            var rule = await context.DiscountRules
                .Include(r => r.DiscountReason)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == discountRuleId);

            if (rule == null)
                return result.Fail("Discount rule was not found.");

            NormalizeRule(rule);

            if (!rule.IsCurrentlyValid)
                return result.Fail("Discount rule is inactive or outside valid date range.");

            var item = await context.ItemVariants
                .Include(v => v.ItemParent)
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == itemVariantId);

            if (item == null || item.IsDeactivated)
                return result.Fail("Selected item is inactive or missing.");

            if (item.ItemParent == null || item.ItemParent.IsDeactivated || item.ItemParent.IsSaleLocked)
                return result.Fail("Selected item is locked or inactive.");

            string normalizedCustomerType = NormalizeCustomerType(customerType);

            if (!DoesRuleApplyToItem(rule, item, normalizedCustomerType))
                return result.Fail("Discount rule does not apply to the selected item/customer.");

            decimal grossAmount = Math.Round(quantity * unitPrice, 2);
            decimal discountAmount = CalculateDiscountAmount(rule, quantity, unitPrice);

            if (discountAmount <= 0m)
                return result.Fail("Calculated discount amount is zero.");

            if (discountAmount > grossAmount)
                return result.Fail("Discount cannot exceed line gross amount.");

            if (rule.IsPercentDiscount && rule.DiscountValue > 100m)
                return result.Fail("Discount percentage cannot exceed 100%.");

            if (rule.MaxDiscountAmount > 0m && discountAmount > rule.MaxDiscountAmount)
                return result.Fail($"Discount exceeds rule maximum amount Rs. {rule.MaxDiscountAmount:N2}.");

            if (rule.MaxDiscountPercent > 0m && rule.IsPercentDiscount && rule.DiscountValue > rule.MaxDiscountPercent)
                return result.Fail($"Discount exceeds rule maximum percent {rule.MaxDiscountPercent:N2}%.");

            if (rule.MaxValuePerInvoice > 0m && discountAmount > rule.MaxValuePerInvoice)
                return result.Fail($"Discount exceeds max value per invoice Rs. {rule.MaxValuePerInvoice:N2}.");

            if (rule.MaxQtyPerInvoice > 0m && quantity > rule.MaxQtyPerInvoice)
                return result.Fail($"Quantity exceeds rule max quantity per invoice {rule.MaxQtyPerInvoice:N3}.");

            decimal netAfterDiscount = Math.Round(grossAmount - discountAmount, 2);
            decimal netUnitPrice = quantity > 0m
                ? Math.Round(netAfterDiscount / quantity, 2)
                : 0m;

            if (minimumPrice > 0m &&
                netUnitPrice < minimumPrice &&
                !rule.AllowBelowMinimumPrice)
            {
                return result.Fail($"Discount would sell below minimum price Rs. {minimumPrice:N2}.");
            }

            var todayUsage = await GetTodayUsageAsync(context, rule.Id, saleDate ?? DateTime.Today);

            if (rule.MaxValuePerDay > 0m &&
                todayUsage.TotalDiscountAmount + discountAmount > rule.MaxValuePerDay)
            {
                return result.Fail($"Rule daily discount value limit exceeded. Limit Rs. {rule.MaxValuePerDay:N2}.");
            }

            if (rule.MaxQtyPerDay > 0m &&
                todayUsage.TotalQuantity + quantity > rule.MaxQtyPerDay)
            {
                return result.Fail($"Rule daily quantity limit exceeded. Limit {rule.MaxQtyPerDay:N3}.");
            }

            bool approvalRequired = rule.RequiresManagerApproval || rule.RequiresAdminApproval;

            if (rule.ManagerApprovalThreshold > 0m && discountAmount > rule.ManagerApprovalThreshold)
                approvalRequired = true;

            if (minimumPrice > 0m && netUnitPrice < minimumPrice && rule.AllowBelowMinimumPrice)
                approvalRequired = true;

            result.IsValid = true;
            result.Message = "Discount rule is valid.";
            result.Rule = rule;
            result.DiscountRuleId = rule.Id;
            result.DiscountRuleName = rule.RuleName;
            result.DiscountReasonId = rule.DiscountReasonId;
            result.ReasonCode = rule.ReasonCode;
            result.ReasonName = rule.ReasonName;
            result.DiscountType = rule.DiscountType;
            result.DiscountValue = rule.DiscountValue;
            result.DiscountAmount = discountAmount;
            result.GrossAmount = grossAmount;
            result.LineTotalAfterDiscount = netAfterDiscount;
            result.RequiresManagerApproval = rule.RequiresManagerApproval || approvalRequired;
            result.RequiresAdminApproval = rule.RequiresAdminApproval;
            result.ApprovalRequired = approvalRequired;

            if (approvalRequired && !isManagerModeActive)
            {
                result.IsValid = false;
                result.Message = rule.RequiresAdminApproval
                    ? "Admin approval is required for this discount rule."
                    : "Manager approval is required for this discount rule.";
            }

            return result;
        }

        public static decimal CalculateDiscountAmount(
            DiscountRule rule,
            decimal quantity,
            decimal unitPrice)
        {
            if (rule == null)
                return 0m;

            quantity = Math.Round(quantity, 3);
            unitPrice = Math.Round(unitPrice, 2);

            if (quantity <= 0m || unitPrice < 0m)
                return 0m;

            decimal grossAmount = Math.Round(quantity * unitPrice, 2);
            decimal value = Math.Round(rule.DiscountValue, 2);

            if (value <= 0m)
                return 0m;

            if (rule.IsAmountDiscount)
                return Math.Round(Math.Min(value, grossAmount), 2);

            decimal discount = Math.Round(grossAmount * (value / 100m), 2);

            if (discount > grossAmount)
                discount = grossAmount;

            return discount;
        }

        // =========================================================
        // AUDIT SUPPORT
        // =========================================================

        public static async Task<SalesLineDiscountAudit> CreateSalesLineDiscountAuditAsync(
            AppDbContext context,
            SalesLineDiscountAudit audit)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (audit == null)
                throw new ArgumentNullException(nameof(audit));

            NormalizeAudit(audit);
            ValidateAudit(audit);

            await context.SalesLineDiscountAudits.AddAsync(audit);
            await context.SaveChangesAsync();

            return audit;
        }

        // =========================================================
        // SEED DEFAULT REASONS
        // =========================================================

        public async Task SeedDefaultReasonsAsync(string createdBy = "System")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            string safeUser = NormalizeText(createdBy);
            DateTime now = DateTime.Now;

            var defaults = new List<DiscountReason>
            {
                new()
                {
                    ReasonCode = "DAMAGED_ITEM",
                    ReasonName = "Damaged Item Discount",
                    Description = "Discount given because item packaging or item condition is damaged.",
                    RequiresManagerApproval = true,
                    DisplayOrder = 10
                },
                new()
                {
                    ReasonCode = "STAFF_DISCOUNT",
                    ReasonName = "Staff Discount",
                    Description = "Discount given to staff according to shop policy.",
                    RequiresManagerApproval = true,
                    DisplayOrder = 20
                },
                new()
                {
                    ReasonCode = "LOYALTY_DISCOUNT",
                    ReasonName = "Loyalty Customer Discount",
                    Description = "Discount given to registered loyalty customers.",
                    RequiresManagerApproval = false,
                    DisplayOrder = 30
                },
                new()
                {
                    ReasonCode = "PROMOTION",
                    ReasonName = "Promotion Discount",
                    Description = "Discount given under a valid promotion.",
                    RequiresManagerApproval = false,
                    DisplayOrder = 40
                },
                new()
                {
                    ReasonCode = "PRICE_MATCH",
                    ReasonName = "Price Match Discount",
                    Description = "Discount given to match competitor or approved market price.",
                    RequiresManagerApproval = true,
                    DisplayOrder = 50
                },
                new()
                {
                    ReasonCode = "MANAGER_APPROVED",
                    ReasonName = "Manager Approved Discount",
                    Description = "Special discount approved by manager.",
                    RequiresManagerApproval = true,
                    DisplayOrder = 60
                },
                new()
                {
                    ReasonCode = "CUSTOMER_GOODWILL",
                    ReasonName = "Customer Goodwill Discount",
                    Description = "Discount given for customer satisfaction or goodwill.",
                    RequiresManagerApproval = true,
                    DisplayOrder = 70
                }
            };

            foreach (var reason in defaults)
            {
                string code = NormalizeText(reason.ReasonCode).ToUpperInvariant();

                bool exists = await context.DiscountReasons
                    .AnyAsync(r => r.ReasonCode == code);

                if (exists)
                    continue;

                reason.ReasonCode = code;
                reason.CreatedAt = now;
                reason.CreatedBy = safeUser;
                reason.IsActive = true;

                await context.DiscountReasons.AddAsync(reason);
            }

            await context.SaveChangesAsync();
        }

        // =========================================================
        // PRIVATE HELPERS
        // =========================================================

        private static async Task<DiscountRuleUsageDto> GetTodayUsageAsync(
            AppDbContext context,
            int ruleId,
            DateTime date)
        {
            DateTime start = date.Date;
            DateTime end = start.AddDays(1);

            var audits = await context.SalesLineDiscountAudits
                .AsNoTracking()
                .Where(a =>
                    a.DiscountRuleId == ruleId &&
                    a.InvoiceDate >= start &&
                    a.InvoiceDate < end)
                .ToListAsync();

            return new DiscountRuleUsageDto
            {
                TotalDiscountAmount = Math.Round(audits.Sum(a => a.DiscountAmount), 2),
                TotalQuantity = Math.Round(audits.Sum(a => a.Quantity), 3)
            };
        }

        private static bool DoesRuleApplyToItem(
            DiscountRule rule,
            ItemVariant item,
            string customerType)
        {
            if (rule == null || item == null || item.ItemParent == null)
                return false;

            string appliesToType = NormalizeAppliesToType(rule.AppliesToType);

            bool itemMatch = appliesToType switch
            {
                "All" => true,

                "Category" =>
                    rule.CategoryId.HasValue &&
                    item.ItemParent.CategoryId == rule.CategoryId.Value,

                "SubCategory" =>
                    rule.SubCategoryId.HasValue &&
                    item.ItemParent.SubCategoryId == rule.SubCategoryId.Value,

                "ItemParent" =>
                    rule.ItemParentId.HasValue &&
                    item.ItemParentId == rule.ItemParentId.Value,

                "ItemVariant" =>
                    rule.ItemVariantId.HasValue &&
                    item.Id == rule.ItemVariantId.Value,

                "CustomerType" => true,

                _ => false
            };

            if (!itemMatch)
                return false;

            string ruleCustomerType = NormalizeCustomerType(rule.CustomerType);
            string selectedCustomerType = NormalizeCustomerType(customerType);

            if (string.IsNullOrWhiteSpace(ruleCustomerType) ||
                ruleCustomerType.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return ruleCustomerType.Equals(selectedCustomerType, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task ApplyReasonSnapshotAsync(AppDbContext context, DiscountRule rule)
        {
            if (!rule.DiscountReasonId.HasValue || rule.DiscountReasonId.Value <= 0)
            {
                rule.DiscountReasonId = null;
                rule.ReasonCode = NormalizeText(rule.ReasonCode).ToUpperInvariant();
                rule.ReasonName = NormalizeText(rule.ReasonName);
                return;
            }

            var reason = await context.DiscountReasons
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == rule.DiscountReasonId.Value);

            if (reason == null)
                throw new InvalidOperationException("Selected discount reason was not found.");

            if (!reason.IsActive)
                throw new InvalidOperationException("Selected discount reason is inactive.");

            rule.ReasonCode = NormalizeText(reason.ReasonCode).ToUpperInvariant();
            rule.ReasonName = NormalizeText(reason.ReasonName);

            if (reason.RequiresAdminApproval)
                rule.RequiresAdminApproval = true;

            if (reason.RequiresManagerApproval)
                rule.RequiresManagerApproval = true;

            if (reason.ManagerApprovalThreshold > 0m &&
                (rule.ManagerApprovalThreshold <= 0m ||
                 rule.ManagerApprovalThreshold > reason.ManagerApprovalThreshold))
            {
                rule.ManagerApprovalThreshold = reason.ManagerApprovalThreshold;
            }
        }

        private static void NormalizeRule(DiscountRule rule)
        {
            rule.RuleName = NormalizeText(rule.RuleName);
            rule.DiscountType = NormalizeDiscountType(rule.DiscountType);
            rule.DiscountValue = Math.Round(rule.DiscountValue, 2);

            rule.ReasonCode = NormalizeText(rule.ReasonCode).ToUpperInvariant();
            rule.ReasonName = NormalizeText(rule.ReasonName);

            rule.AppliesToType = NormalizeAppliesToType(rule.AppliesToType);

            rule.CategoryName = NormalizeText(rule.CategoryName);
            rule.SubCategoryName = NormalizeText(rule.SubCategoryName);
            rule.ItemName = NormalizeText(rule.ItemName);
            rule.SkuCode = NormalizeText(rule.SkuCode);
            rule.Barcode = NormalizeText(rule.Barcode);
            rule.CustomerType = NormalizeCustomerType(rule.CustomerType);

            rule.MaxDiscountAmount = Math.Round(rule.MaxDiscountAmount, 2);
            rule.MaxDiscountPercent = Math.Round(rule.MaxDiscountPercent, 2);
            rule.MaxValuePerInvoice = Math.Round(rule.MaxValuePerInvoice, 2);
            rule.MaxValuePerDay = Math.Round(rule.MaxValuePerDay, 2);
            rule.MaxQtyPerInvoice = Math.Round(rule.MaxQtyPerInvoice, 3);
            rule.MaxQtyPerDay = Math.Round(rule.MaxQtyPerDay, 3);
            rule.ManagerApprovalThreshold = Math.Round(rule.ManagerApprovalThreshold, 2);

            rule.CreatedBy = NormalizeText(rule.CreatedBy);
            rule.UpdatedBy = NormalizeText(rule.UpdatedBy);
            rule.Remarks = NormalizeText(rule.Remarks);
        }

        private static void ValidateRule(DiscountRule rule)
        {
            if (string.IsNullOrWhiteSpace(rule.RuleName))
                throw new InvalidOperationException("Rule name is required.");

            if (rule.DiscountValue <= 0m)
                throw new InvalidOperationException("Discount value must be greater than zero.");

            if (rule.IsPercentDiscount && rule.DiscountValue > 100m)
                throw new InvalidOperationException("Discount percentage cannot exceed 100%.");

            if (rule.ValidFrom.HasValue &&
                rule.ValidTo.HasValue &&
                rule.ValidFrom.Value.Date > rule.ValidTo.Value.Date)
            {
                throw new InvalidOperationException("Valid From date cannot be later than Valid To date.");
            }

            switch (rule.AppliesToType)
            {
                case "Category":
                    if (!rule.CategoryId.HasValue || rule.CategoryId.Value <= 0)
                        throw new InvalidOperationException("Category is required for category discount rule.");
                    break;

                case "SubCategory":
                    if (!rule.SubCategoryId.HasValue || rule.SubCategoryId.Value <= 0)
                        throw new InvalidOperationException("Sub category is required for sub category discount rule.");
                    break;

                case "ItemParent":
                    if (!rule.ItemParentId.HasValue || rule.ItemParentId.Value <= 0)
                        throw new InvalidOperationException("Item is required for item discount rule.");
                    break;

                case "ItemVariant":
                    if (!rule.ItemVariantId.HasValue || rule.ItemVariantId.Value <= 0)
                        throw new InvalidOperationException("Item variant is required for variant discount rule.");
                    break;

                case "CustomerType":
                    if (string.IsNullOrWhiteSpace(rule.CustomerType) ||
                        rule.CustomerType.Equals("All", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("Specific customer type is required for customer type discount rule.");
                    }
                    break;
            }

            if (rule.MaxDiscountAmount < 0m ||
                rule.MaxDiscountPercent < 0m ||
                rule.MaxValuePerInvoice < 0m ||
                rule.MaxValuePerDay < 0m ||
                rule.MaxQtyPerInvoice < 0m ||
                rule.MaxQtyPerDay < 0m ||
                rule.ManagerApprovalThreshold < 0m)
            {
                throw new InvalidOperationException("Rule limits cannot be negative.");
            }
        }

        private static void NormalizeAudit(SalesLineDiscountAudit audit)
        {
            audit.InvoiceNo = NormalizeText(audit.InvoiceNo);
            audit.CashierName = NormalizeText(audit.CashierName);
            audit.TerminalNo = NormalizeText(audit.TerminalNo);

            audit.DiscountRuleName = NormalizeText(audit.DiscountRuleName);
            audit.ReasonCode = NormalizeText(audit.ReasonCode).ToUpperInvariant();
            audit.ReasonName = NormalizeText(audit.ReasonName);
            audit.DiscountType = NormalizeDiscountType(audit.DiscountType);
            audit.DiscountValue = Math.Round(audit.DiscountValue, 2);
            audit.DiscountAmount = Math.Round(audit.DiscountAmount, 2);

            audit.OriginalUnitPrice = Math.Round(audit.OriginalUnitPrice, 2);
            audit.Quantity = Math.Round(audit.Quantity, 3);
            audit.GrossAmount = Math.Round(audit.GrossAmount, 2);
            audit.LineTotalAfterDiscount = Math.Round(audit.LineTotalAfterDiscount, 2);
            audit.CostPrice = Math.Round(audit.CostPrice, 2);
            audit.ProfitAfterDiscount = Math.Round(audit.ProfitAfterDiscount, 2);

            audit.Barcode = NormalizeText(audit.Barcode);
            audit.SkuCode = NormalizeText(audit.SkuCode);
            audit.ItemDescription = NormalizeText(audit.ItemDescription);
            audit.BatchNo = NormalizeText(audit.BatchNo);
            audit.Uom = string.IsNullOrWhiteSpace(audit.Uom)
                ? "PCS"
                : NormalizeText(audit.Uom);

            audit.ApprovedBy = NormalizeText(audit.ApprovedBy);
            audit.CreatedBy = NormalizeText(audit.CreatedBy);
            audit.Remarks = NormalizeText(audit.Remarks);
        }

        private static void ValidateAudit(SalesLineDiscountAudit audit)
        {
            if (audit.SalesHeaderId <= 0)
                throw new InvalidOperationException("Sales header reference is required for discount audit.");

            if (audit.SalesLineId <= 0)
                throw new InvalidOperationException("Sales line reference is required for discount audit.");

            if (string.IsNullOrWhiteSpace(audit.InvoiceNo))
                throw new InvalidOperationException("Invoice number is required for discount audit.");

            if (audit.DiscountAmount <= 0m)
                throw new InvalidOperationException("Discount audit amount must be greater than zero.");

            if (audit.Quantity <= 0m)
                throw new InvalidOperationException("Discount audit quantity must be greater than zero.");

            if (audit.RequiresManagerApproval && string.IsNullOrWhiteSpace(audit.ApprovedBy))
                throw new InvalidOperationException("Manager approval is required for this discount audit.");

            if (audit.RequiresAdminApproval && string.IsNullOrWhiteSpace(audit.ApprovedBy))
                throw new InvalidOperationException("Admin approval is required for this discount audit.");
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string NormalizeDiscountType(string? value)
        {
            string text = NormalizeText(value);

            if (text.Equals("Amount", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("Rs", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("Rupee", StringComparison.OrdinalIgnoreCase))
            {
                return "Amount";
            }

            return "Percent";
        }

        private static string NormalizeAppliesToType(string? value)
        {
            string text = NormalizeText(value);

            if (text.Equals("Category", StringComparison.OrdinalIgnoreCase))
                return "Category";

            if (text.Equals("SubCategory", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("Sub Category", StringComparison.OrdinalIgnoreCase))
                return "SubCategory";

            if (text.Equals("ItemParent", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("Item", StringComparison.OrdinalIgnoreCase))
                return "ItemParent";

            if (text.Equals("ItemVariant", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("Variant", StringComparison.OrdinalIgnoreCase))
                return "ItemVariant";

            if (text.Equals("CustomerType", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("Customer Type", StringComparison.OrdinalIgnoreCase))
                return "CustomerType";

            return "All";
        }

        private static string NormalizeCustomerType(string? value)
        {
            string text = NormalizeText(value);

            if (text.Equals("Wholesale", StringComparison.OrdinalIgnoreCase))
                return "Wholesale";

            if (text.Equals("Retail", StringComparison.OrdinalIgnoreCase))
                return "Retail";

            if (text.Equals("Loyalty", StringComparison.OrdinalIgnoreCase))
                return "Loyalty";

            if (text.Equals("Walk-In", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("WalkIn", StringComparison.OrdinalIgnoreCase))
                return "Walk-In";

            return string.IsNullOrWhiteSpace(text) ? "All" : text;
        }
    }

    // =========================================================
    // DTOs
    // =========================================================

    public class DiscountRuleSearchDto
    {
        public string SearchText { get; set; } = string.Empty;

        // All / Amount / Percent
        public string DiscountType { get; set; } = "All";

        // All / Category / SubCategory / ItemParent / ItemVariant / CustomerType
        public string AppliesToType { get; set; } = "All";

        public bool IsActiveOnly { get; set; } = false;

        public bool ValidTodayOnly { get; set; } = false;

        public int MaxResults { get; set; } = 300;
    }

    public class DiscountRuleValidationResult
    {
        public bool IsValid { get; set; } = false;

        public string Message { get; set; } = string.Empty;

        public DiscountRule? Rule { get; set; }

        public int DiscountRuleId { get; set; }

        public string DiscountRuleName { get; set; } = string.Empty;

        public int? DiscountReasonId { get; set; }

        public string ReasonCode { get; set; } = string.Empty;

        public string ReasonName { get; set; } = string.Empty;

        public string DiscountType { get; set; } = string.Empty;

        public decimal DiscountValue { get; set; } = 0m;

        public decimal GrossAmount { get; set; } = 0m;

        public decimal DiscountAmount { get; set; } = 0m;

        public decimal LineTotalAfterDiscount { get; set; } = 0m;

        public bool RequiresManagerApproval { get; set; } = false;

        public bool RequiresAdminApproval { get; set; } = false;

        public bool ApprovalRequired { get; set; } = false;

        public DiscountRuleValidationResult Fail(string message)
        {
            IsValid = false;
            Message = message;
            return this;
        }
    }

    public class DiscountRuleUsageDto
    {
        public decimal TotalDiscountAmount { get; set; } = 0m;

        public decimal TotalQuantity { get; set; } = 0m;
    }
}