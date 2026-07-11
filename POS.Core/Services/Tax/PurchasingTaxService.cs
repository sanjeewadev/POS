using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;

namespace POS.Core.Services.Tax
{
    public sealed class PurchasingTaxProfile
    {
        public int ItemVariantId { get; init; }
        public int TaxCategoryId { get; init; }
        public string TaxCategoryCode { get; init; } = string.Empty;
        public string TaxCategoryName { get; init; } = string.Empty;
        public string TaxTreatmentType { get; init; } = string.Empty;
        public int? TaxRateId { get; init; }
        public string TaxCode { get; init; } = string.Empty;
        public string TaxName { get; init; } = string.Empty;
        public decimal RatePercent { get; init; }
    }

    public sealed class PurchasingTaxLineInput
    {
        public int ItemVariantId { get; init; }
        public decimal Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public string DiscountMode { get; init; } = "Amount";
        public decimal DiscountValue { get; init; }
        public PurchasingTaxProfile TaxProfile { get; init; } = null!;
    }

    public sealed class PurchasingTaxLineResult
    {
        public int ItemVariantId { get; init; }
        public PurchasingTaxProfile TaxProfile { get; init; } = null!;
        public decimal GrossAmount { get; init; }
        public decimal LineDiscountAmount { get; init; }
        public decimal GlobalDiscountAllocation { get; init; }
        public decimal TaxableAmount { get; init; }
        public decimal VatAmount { get; init; }
        public decimal TaxInclusiveAmount { get; init; }
    }

    public sealed class PurchasingTaxDocumentResult
    {
        public IReadOnlyList<PurchasingTaxLineResult> Lines { get; init; } = Array.Empty<PurchasingTaxLineResult>();
        public decimal Subtotal { get; init; }
        public decimal LineDiscountTotal { get; init; }
        public decimal GlobalDiscount { get; init; }
        public decimal TotalDiscount { get; init; }
        public decimal TotalVat { get; init; }
        public decimal NetPayable { get; init; }
        public decimal TaxableAmountTotal { get; init; }
        public decimal StandardRatedAmount { get; init; }
        public decimal ZeroRatedAmount { get; init; }
        public decimal ExemptAmount { get; init; }
        public decimal OutOfScopeAmount { get; init; }
    }

    /// <summary>
    /// Authoritative purchasing tax resolver and calculator shared by PO and GRN.
    /// It never derives a percentage from tax-code text.
    /// </summary>
    public sealed class PurchasingTaxService
    {
        public async Task<IReadOnlyDictionary<int, PurchasingTaxProfile>> ResolveProfilesAsync(
            AppDbContext context,
            IReadOnlyCollection<int> itemVariantIds,
            DateTime transactionDate)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var variantIds = itemVariantIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (variantIds.Count == 0)
                return new Dictionary<int, PurchasingTaxProfile>();

            DateTime date = transactionDate.Date;

            var itemRows = await context.ItemVariants
                .AsNoTracking()
                .Where(v => variantIds.Contains(v.Id))
                .Select(v => new
                {
                    ItemVariantId = v.Id,
                    v.SkuCode,
                    ItemType = v.ItemParent.ItemType,
                    v.ItemParent.TaxCategoryId,
                    TaxCategoryCode = v.ItemParent.TaxCategory != null
                        ? v.ItemParent.TaxCategory.CategoryCode
                        : string.Empty,
                    TaxCategoryName = v.ItemParent.TaxCategory != null
                        ? v.ItemParent.TaxCategory.CategoryName
                        : string.Empty,
                    TaxTreatmentType = v.ItemParent.TaxCategory != null
                        ? v.ItemParent.TaxCategory.TreatmentType
                        : string.Empty,
                    TaxCategoryIsActive = v.ItemParent.TaxCategory != null &&
                                          v.ItemParent.TaxCategory.IsActive,
                    TaxCategoryIsRateBased = v.ItemParent.TaxCategory != null &&
                                             v.ItemParent.TaxCategory.IsRateBased
                })
                .ToListAsync();

            var foundIds = itemRows
                .Select(r => r.ItemVariantId)
                .ToHashSet();

            var missingIds = variantIds
                .Where(id => !foundIds.Contains(id))
                .ToList();

            if (missingIds.Count > 0)
            {
                throw new InvalidOperationException(
                    "One or more purchasing item variants were not found.");
            }

            foreach (var row in itemRows)
            {
                if (!string.Equals(row.ItemType, ItemTypeCodes.StockItem, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Service item '{row.SkuCode}' cannot be used in Purchase Orders or GRNs.");
                }

                if (!row.TaxCategoryId.HasValue ||
                    string.IsNullOrWhiteSpace(row.TaxCategoryCode))
                {
                    throw new InvalidOperationException(
                        $"Item '{row.SkuCode}' has no authoritative Tax Category. Open Item Master and assign Standard VAT, Zero Rated, Exempt, or Out of Scope before purchasing it.");
                }

                if (!row.TaxCategoryIsActive)
                {
                    throw new InvalidOperationException(
                        $"Item '{row.SkuCode}' uses an inactive Tax Category.");
                }

                if (!IsApprovedCategoryCode(row.TaxCategoryCode))
                {
                    throw new InvalidOperationException(
                        $"Item '{row.SkuCode}' uses unsupported Tax Category '{row.TaxCategoryCode}'.");
                }
            }

            var rateBasedCategoryIds = itemRows
                .Where(r => r.TaxCategoryIsRateBased)
                .Select(r => r.TaxCategoryId!.Value)
                .Distinct()
                .ToList();

            var effectiveRates = await context.TaxRates
                .AsNoTracking()
                .Where(t =>
                    t.TaxCategoryId.HasValue &&
                    rateBasedCategoryIds.Contains(t.TaxCategoryId.Value) &&
                    t.IsActive &&
                    t.EffectiveFrom.HasValue &&
                    t.EffectiveFrom.Value <= date &&
                    (!t.EffectiveTo.HasValue || t.EffectiveTo.Value >= date))
                .OrderByDescending(t => t.EffectiveFrom)
                .ThenByDescending(t => t.Id)
                .ToListAsync();

            var ratesByCategory = effectiveRates
                .GroupBy(t => t.TaxCategoryId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new Dictionary<int, PurchasingTaxProfile>();

            foreach (var row in itemRows)
            {
                if (!row.TaxCategoryIsRateBased)
                {
                    result[row.ItemVariantId] = new PurchasingTaxProfile
                    {
                        ItemVariantId = row.ItemVariantId,
                        TaxCategoryId = row.TaxCategoryId!.Value,
                        TaxCategoryCode = NormalizeCode(row.TaxCategoryCode),
                        TaxCategoryName = NormalizeText(row.TaxCategoryName),
                        TaxTreatmentType = NormalizeText(row.TaxTreatmentType),
                        TaxRateId = null,
                        TaxCode = NormalizeCode(row.TaxCategoryCode),
                        TaxName = NormalizeText(row.TaxCategoryName),
                        RatePercent = 0m
                    };

                    continue;
                }

                if (!ratesByCategory.TryGetValue(row.TaxCategoryId!.Value, out var rates) ||
                    rates.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"No active tax rate exists for '{row.TaxCategoryName}' on {date:yyyy-MM-dd}. Add the effective rate in Tax Rate Management before saving this document.");
                }

                if (rates.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"More than one active tax rate applies to '{row.TaxCategoryName}' on {date:yyyy-MM-dd}. Correct the overlapping effective periods in Tax Rate Management.");
                }

                var rate = rates[0];

                if (rate.RatePercent <= 0m || rate.RatePercent > 100m)
                {
                    throw new InvalidOperationException(
                        $"Tax rate '{rate.TaxCode}' has an invalid percentage.");
                }

                result[row.ItemVariantId] = new PurchasingTaxProfile
                {
                    ItemVariantId = row.ItemVariantId,
                    TaxCategoryId = row.TaxCategoryId.Value,
                    TaxCategoryCode = NormalizeCode(row.TaxCategoryCode),
                    TaxCategoryName = NormalizeText(row.TaxCategoryName),
                    TaxTreatmentType = NormalizeText(row.TaxTreatmentType),
                    TaxRateId = rate.Id,
                    TaxCode = NormalizeCode(rate.TaxCode),
                    TaxName = NormalizeText(rate.TaxName),
                    RatePercent = rate.RatePercent
                };
            }

            return result;
        }

        public PurchasingTaxDocumentResult CalculateDocument(
            IReadOnlyList<PurchasingTaxLineInput> lines,
            decimal globalDiscount,
            bool isTaxInclusive)
        {
            if (lines == null || lines.Count == 0)
                throw new InvalidOperationException("A purchasing document must contain at least one line.");

            if (globalDiscount < 0m)
                throw new InvalidOperationException("Global bill discount cannot be negative.");

            var workingLines = new List<WorkingLine>(lines.Count);

            foreach (var line in lines)
            {
                ValidateLine(line);

                decimal gross = RoundMoney(line.Quantity * line.UnitPrice);
                decimal lineDiscount = CalculateDiscountAmount(
                    gross,
                    line.DiscountMode,
                    line.DiscountValue);

                if (lineDiscount > gross)
                {
                    throw new InvalidOperationException(
                        "Line discount cannot be greater than line value.");
                }

                decimal amountAfterLineDiscount = RoundMoney(gross - lineDiscount);
                var beforeGlobal = CalculateFromEnteredAmount(
                    amountAfterLineDiscount,
                    line.TaxProfile.RatePercent,
                    isTaxInclusive);

                workingLines.Add(new WorkingLine(
                    line,
                    gross,
                    lineDiscount,
                    beforeGlobal.Taxable,
                    beforeGlobal.Vat,
                    beforeGlobal.Inclusive));
            }

            decimal payableBeforeGlobalDiscount = RoundMoney(
                workingLines.Sum(l => l.PreGlobalInclusive));

            decimal roundedGlobalDiscount = RoundMoney(globalDiscount);

            if (roundedGlobalDiscount > payableBeforeGlobalDiscount)
            {
                throw new InvalidOperationException(
                    "Global bill discount cannot be greater than document value.");
            }

            decimal[] allocations = AllocateDiscount(
                workingLines.Select(l => l.PreGlobalInclusive).ToArray(),
                roundedGlobalDiscount);

            var results = new List<PurchasingTaxLineResult>(workingLines.Count);

            decimal standardRated = 0m;
            decimal zeroRated = 0m;
            decimal exempt = 0m;
            decimal outOfScope = 0m;

            for (int index = 0; index < workingLines.Count; index++)
            {
                var working = workingLines[index];
                decimal allocation = allocations[index];
                decimal finalInclusive = RoundMoney(working.PreGlobalInclusive - allocation);

                MoneyBreakdown finalBreakdown;

                if (allocation == 0m)
                {
                    finalBreakdown = new MoneyBreakdown(
                        working.PreGlobalTaxable,
                        working.PreGlobalVat,
                        working.PreGlobalInclusive);
                }
                else
                {
                    finalBreakdown = CalculateFromInclusiveAmount(
                        finalInclusive,
                        working.Input.TaxProfile.RatePercent);
                }

                decimal categoryValue = finalBreakdown.Taxable;
                string categoryCode = NormalizeCode(
                    working.Input.TaxProfile.TaxCategoryCode);

                switch (categoryCode)
                {
                    case TaxCategoryCodes.Standard:
                        standardRated += categoryValue;
                        break;

                    case TaxCategoryCodes.ZeroRated:
                        zeroRated += categoryValue;
                        break;

                    case TaxCategoryCodes.Exempt:
                        exempt += categoryValue;
                        break;

                    case TaxCategoryCodes.OutOfScope:
                        outOfScope += categoryValue;
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Unsupported Tax Category '{categoryCode}'.");
                }

                results.Add(new PurchasingTaxLineResult
                {
                    ItemVariantId = working.Input.ItemVariantId,
                    TaxProfile = working.Input.TaxProfile,
                    GrossAmount = working.Gross,
                    LineDiscountAmount = working.LineDiscount,
                    GlobalDiscountAllocation = allocation,
                    TaxableAmount = finalBreakdown.Taxable,
                    VatAmount = finalBreakdown.Vat,
                    TaxInclusiveAmount = finalBreakdown.Inclusive
                });
            }

            decimal subtotal = RoundMoney(workingLines.Sum(l => l.Gross));
            decimal lineDiscountTotal = RoundMoney(workingLines.Sum(l => l.LineDiscount));
            decimal vatTotal = RoundMoney(results.Sum(r => r.VatAmount));
            decimal netPayable = RoundMoney(results.Sum(r => r.TaxInclusiveAmount));
            standardRated = RoundMoney(standardRated);
            zeroRated = RoundMoney(zeroRated);
            exempt = RoundMoney(exempt);
            outOfScope = RoundMoney(outOfScope);

            return new PurchasingTaxDocumentResult
            {
                Lines = results,
                Subtotal = subtotal,
                LineDiscountTotal = lineDiscountTotal,
                GlobalDiscount = roundedGlobalDiscount,
                TotalDiscount = RoundMoney(lineDiscountTotal + roundedGlobalDiscount),
                TotalVat = vatTotal,
                NetPayable = netPayable,
                TaxableAmountTotal = standardRated,
                StandardRatedAmount = standardRated,
                ZeroRatedAmount = zeroRated,
                ExemptAmount = exempt,
                OutOfScopeAmount = outOfScope
            };
        }

        private static MoneyBreakdown CalculateFromEnteredAmount(
            decimal amount,
            decimal ratePercent,
            bool isTaxInclusive)
        {
            amount = RoundMoney(amount);

            if (amount <= 0m || ratePercent <= 0m)
                return new MoneyBreakdown(amount, 0m, amount);

            decimal rate = ratePercent / 100m;

            if (isTaxInclusive)
                return CalculateFromInclusiveAmount(amount, ratePercent);

            decimal taxable = amount;
            decimal vat = RoundMoney(taxable * rate);
            decimal inclusive = RoundMoney(taxable + vat);

            return new MoneyBreakdown(taxable, vat, inclusive);
        }

        private static MoneyBreakdown CalculateFromInclusiveAmount(
            decimal inclusiveAmount,
            decimal ratePercent)
        {
            inclusiveAmount = RoundMoney(inclusiveAmount);

            if (inclusiveAmount <= 0m || ratePercent <= 0m)
                return new MoneyBreakdown(inclusiveAmount, 0m, inclusiveAmount);

            decimal rate = ratePercent / 100m;
            decimal taxable = RoundMoney(inclusiveAmount / (1m + rate));
            decimal vat = RoundMoney(inclusiveAmount - taxable);

            return new MoneyBreakdown(taxable, vat, inclusiveAmount);
        }

        private static decimal CalculateDiscountAmount(
            decimal gross,
            string? discountMode,
            decimal discountValue)
        {
            if (discountValue < 0m)
                throw new InvalidOperationException("Discount value cannot be negative.");

            if (gross <= 0m || discountValue == 0m)
                return 0m;

            string mode = NormalizeDiscountMode(discountMode);

            if (mode == "Percent")
            {
                if (discountValue > 100m)
                    throw new InvalidOperationException("Discount percentage cannot be greater than 100.");

                return RoundMoney(gross * discountValue / 100m);
            }

            return RoundMoney(discountValue);
        }

        private static decimal[] AllocateDiscount(
            IReadOnlyList<decimal> lineAmounts,
            decimal discount)
        {
            var allocations = new decimal[lineAmounts.Count];

            long discountCents = ToCents(discount);

            if (discountCents <= 0)
                return allocations;

            var lineCents = lineAmounts
                .Select(ToCents)
                .ToArray();

            long totalCents = lineCents.Sum();

            if (totalCents <= 0)
            {
                throw new InvalidOperationException(
                    "Global bill discount cannot be allocated to a zero-value document.");
            }

            if (discountCents > totalCents)
            {
                throw new InvalidOperationException(
                    "Global bill discount cannot be greater than document value.");
            }

            var eligible = Enumerable.Range(0, lineCents.Length)
                .Where(index => lineCents[index] > 0)
                .Select(index =>
                {
                    decimal exactShare = (decimal)discountCents * lineCents[index] / totalCents;
                    long baseCents = decimal.ToInt64(decimal.Floor(exactShare));

                    return new AllocationShare(
                        index,
                        baseCents,
                        exactShare - baseCents);
                })
                .ToList();

            long allocatedCents = eligible.Sum(share => share.BaseCents);
            long remainingCents = discountCents - allocatedCents;

            foreach (var share in eligible)
                allocations[share.Index] = share.BaseCents / 100m;

            foreach (var share in eligible
                         .OrderByDescending(item => item.Fraction)
                         .ThenBy(item => item.Index))
            {
                if (remainingCents <= 0)
                    break;

                if (ToCents(allocations[share.Index]) >= lineCents[share.Index])
                    continue;

                allocations[share.Index] += 0.01m;
                remainingCents--;
            }

            if (remainingCents != 0 || ToCents(allocations.Sum()) != discountCents)
            {
                throw new InvalidOperationException(
                    "Global bill discount could not be allocated exactly across document lines.");
            }

            if (allocations.Any(value => value < 0m))
            {
                throw new InvalidOperationException(
                    "Global bill discount allocation produced an invalid negative line value.");
            }

            return allocations;
        }

        private static long ToCents(decimal value)
        {
            return checked(decimal.ToInt64(RoundMoney(value) * 100m));
        }

        private static void ValidateLine(PurchasingTaxLineInput line)
        {
            if (line == null)
                throw new InvalidOperationException("Purchasing line is required.");

            if (line.ItemVariantId <= 0)
                throw new InvalidOperationException("Purchasing line has an invalid item variant.");

            if (line.Quantity <= 0m)
                throw new InvalidOperationException("Purchasing quantity must be greater than zero.");

            if (line.UnitPrice <= 0m)
                throw new InvalidOperationException("Purchasing unit price must be greater than zero.");

            if (line.TaxProfile == null)
                throw new InvalidOperationException("Purchasing Tax Category profile is required.");

            if (line.TaxProfile.RatePercent < 0m ||
                line.TaxProfile.RatePercent > 100m)
            {
                throw new InvalidOperationException("VAT rate must be between 0 and 100.");
            }
        }

        private static bool IsApprovedCategoryCode(string? value)
        {
            string code = NormalizeCode(value);

            return code == TaxCategoryCodes.Standard ||
                   code == TaxCategoryCodes.ZeroRated ||
                   code == TaxCategoryCodes.Exempt ||
                   code == TaxCategoryCodes.OutOfScope;
        }

        private static string NormalizeDiscountMode(string? value)
        {
            string mode = NormalizeText(value);

            if (mode.Equals("Percent", StringComparison.OrdinalIgnoreCase) ||
                mode.Equals("%", StringComparison.OrdinalIgnoreCase))
            {
                return "Percent";
            }

            return "Amount";
        }

        private static decimal RoundMoney(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private static string NormalizeCode(string? value)
        {
            return NormalizeText(value).ToUpperInvariant();
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private sealed record WorkingLine(
            PurchasingTaxLineInput Input,
            decimal Gross,
            decimal LineDiscount,
            decimal PreGlobalTaxable,
            decimal PreGlobalVat,
            decimal PreGlobalInclusive);

        private readonly record struct AllocationShare(
            int Index,
            long BaseCents,
            decimal Fraction);

        private readonly record struct MoneyBreakdown(
            decimal Taxable,
            decimal Vat,
            decimal Inclusive);
    }
}
