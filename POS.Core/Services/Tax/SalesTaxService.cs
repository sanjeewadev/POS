using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;

namespace POS.Core.Services.Tax
{
    public sealed class SalesTaxProfile
    {
        public int ItemVariantId { get; init; }

        public string ItemType { get; init; } = ItemTypeCodes.StockItem;

        public int? TaxCategoryId { get; init; }

        public string TaxCategoryCode { get; init; } = string.Empty;

        public string TaxCategoryName { get; init; } = string.Empty;

        public string TaxTreatmentType { get; init; } = string.Empty;

        public int? TaxRateId { get; init; }

        public string TaxCode { get; init; } = string.Empty;

        public string TaxName { get; init; } = string.Empty;

        public decimal RatePercent { get; init; }
    }

    public sealed class SalesTaxLineInput
    {
        public int LineKey { get; init; }

        public int ItemVariantId { get; init; }

        public decimal Quantity { get; init; }

        public decimal VatInclusiveUnitPrice { get; init; }

        public decimal LineDiscountAmount { get; init; }

        public SalesTaxProfile TaxProfile { get; init; } = new();
    }

    public sealed class SalesTaxLineResult
    {
        public int LineKey { get; init; }

        public int ItemVariantId { get; init; }

        public SalesTaxProfile TaxProfile { get; init; } = new();

        public decimal GrossAmount { get; init; }

        public decimal LineDiscountAmount { get; init; }

        public decimal InvoiceDiscountAllocation { get; init; }

        public decimal TaxableAmount { get; init; }

        public decimal VatAmount { get; init; }

        public decimal TaxInclusiveAmount { get; init; }
    }

    public sealed class SalesTaxDocumentResult
    {
        public IReadOnlyList<SalesTaxLineResult> Lines { get; init; } =
            Array.Empty<SalesTaxLineResult>();

        public decimal GrossTotal { get; init; }

        public decimal LineDiscountTotal { get; init; }

        public decimal InvoiceDiscount { get; init; }

        public decimal TotalDiscount { get; init; }

        public decimal NetTotal { get; init; }

        public decimal TotalVat { get; init; }

        public decimal TaxableAmountTotal { get; init; }

        public decimal StandardRatedAmount { get; init; }

        public decimal ZeroRatedAmount { get; init; }

        public decimal ExemptAmount { get; init; }

        public decimal OutOfScopeAmount { get; init; }
    }

    public sealed class SalesTaxService
    {
        public async Task<Dictionary<int, SalesTaxProfile>> ResolveProfilesAsync(
            AppDbContext context,
            IEnumerable<int> itemVariantIds,
            DateTime transactionDate)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            int[] variantIds = (itemVariantIds ?? Array.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToArray();

            if (variantIds.Length == 0)
                return new Dictionary<int, SalesTaxProfile>();

            DateTime date = transactionDate.Date;

            var rows = await context.ItemVariants
                .AsNoTracking()
                .Where(v =>
                    variantIds.Contains(v.Id) &&
                    !v.IsDeactivated &&
                    !v.ItemParent.IsDeactivated &&
                    !v.ItemParent.IsSaleLocked)
                .Select(v => new
                {
                    v.Id,
                    v.SkuCode,
                    v.ItemParent.ItemType,
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

            if (rows.Count != variantIds.Length)
            {
                int[] foundIds = rows.Select(row => row.Id).ToArray();
                int[] missingIds = variantIds.Except(foundIds).ToArray();

                throw new InvalidOperationException(
                    $"One or more sale items are inactive, locked, or missing. Variant IDs: {string.Join(", ", missingIds)}.");
            }

            foreach (var row in rows)
            {
                if (!ItemTypeCodes.IsValid(row.ItemType))
                {
                    throw new InvalidOperationException(
                        $"Item '{row.SkuCode}' has an unsupported item type '{row.ItemType}'.");
                }

                if (!row.TaxCategoryId.HasValue ||
                    string.IsNullOrWhiteSpace(row.TaxCategoryCode))
                {
                    throw new InvalidOperationException(
                        $"Item '{row.SkuCode}' has no approved Tax Category. Update the item in Item Master before selling it.");
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

            int[] rateBasedCategoryIds = rows
                .Where(row => row.TaxCategoryIsRateBased)
                .Select(row => row.TaxCategoryId!.Value)
                .Distinct()
                .ToArray();

            var effectiveRates = await context.TaxRates
                .AsNoTracking()
                .Where(rate =>
                    rate.IsActive &&
                    rate.TaxCategoryId.HasValue &&
                    rateBasedCategoryIds.Contains(rate.TaxCategoryId.Value) &&
                    rate.EffectiveFrom.HasValue &&
                    rate.EffectiveFrom.Value.Date <= date &&
                    (!rate.EffectiveTo.HasValue ||
                     rate.EffectiveTo.Value.Date >= date))
                .OrderBy(rate => rate.EffectiveFrom)
                .ThenBy(rate => rate.Id)
                .ToListAsync();

            var ratesByCategory = effectiveRates
                .GroupBy(rate => rate.TaxCategoryId!.Value)
                .ToDictionary(group => group.Key, group => group.ToList());

            var result = new Dictionary<int, SalesTaxProfile>();

            foreach (var row in rows)
            {
                if (!row.TaxCategoryIsRateBased)
                {
                    result[row.Id] = new SalesTaxProfile
                    {
                        ItemVariantId = row.Id,
                        ItemType = row.ItemType,
                        TaxCategoryId = row.TaxCategoryId,
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
                        $"No active tax rate exists for '{row.TaxCategoryName}' on {date:yyyy-MM-dd}. Add the effective rate in Tax Rate Management before completing the sale.");
                }

                if (rates.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"More than one active tax rate applies to '{row.TaxCategoryName}' on {date:yyyy-MM-dd}. Correct the overlapping effective periods in Tax Rate Management.");
                }

                var rate = rates[0];

                result[row.Id] = new SalesTaxProfile
                {
                    ItemVariantId = row.Id,
                    ItemType = row.ItemType,
                    TaxCategoryId = row.TaxCategoryId,
                    TaxCategoryCode = NormalizeCode(row.TaxCategoryCode),
                    TaxCategoryName = NormalizeText(row.TaxCategoryName),
                    TaxTreatmentType = NormalizeText(row.TaxTreatmentType),
                    TaxRateId = rate.Id,
                    TaxCode = NormalizeCode(rate.TaxCode),
                    TaxName = NormalizeText(rate.TaxName),
                    RatePercent = Math.Round(rate.RatePercent, 4)
                };
            }

            return result;
        }

        public SalesTaxDocumentResult CalculateDocument(
            IReadOnlyList<SalesTaxLineInput> lines,
            decimal invoiceDiscount,
            bool isVatRegisteredSale)
        {
            if (lines == null || lines.Count == 0)
                throw new InvalidOperationException("A sale must contain at least one normal item or service line.");

            decimal roundedInvoiceDiscount = RoundMoney(invoiceDiscount);

            if (roundedInvoiceDiscount < 0m)
                throw new InvalidOperationException("Invoice discount cannot be negative.");

            var workingLines = new List<WorkingLine>(lines.Count);

            foreach (var line in lines)
            {
                ValidateLine(line);

                SalesTaxProfile effectiveProfile = isVatRegisteredSale
                    ? line.TaxProfile
                    : CreateNonVatProfile(line.TaxProfile);

                decimal gross = RoundMoney(
                    line.Quantity * line.VatInclusiveUnitPrice);

                decimal lineDiscount = RoundMoney(
                    line.LineDiscountAmount);

                if (lineDiscount < 0m)
                    throw new InvalidOperationException("Line discount cannot be negative.");

                if (lineDiscount > gross)
                {
                    throw new InvalidOperationException(
                        "Line discount cannot be greater than line value.");
                }

                decimal amountAfterLineDiscount =
                    RoundMoney(gross - lineDiscount);

                MoneyBreakdown beforeInvoiceDiscount =
                    CalculateFromInclusiveAmount(
                        amountAfterLineDiscount,
                        effectiveProfile.RatePercent);

                workingLines.Add(
                    new WorkingLine(
                        line,
                        effectiveProfile,
                        gross,
                        lineDiscount,
                        beforeInvoiceDiscount));
            }

            decimal totalBeforeInvoiceDiscount = RoundMoney(
                workingLines.Sum(line =>
                    line.BeforeInvoiceDiscount.Inclusive));

            if (roundedInvoiceDiscount > totalBeforeInvoiceDiscount)
            {
                throw new InvalidOperationException(
                    "Invoice discount cannot be greater than sale value.");
            }

            decimal[] allocations = AllocateDiscount(
                workingLines
                    .Select(line =>
                        line.BeforeInvoiceDiscount.Inclusive)
                    .ToArray(),
                roundedInvoiceDiscount);

            var results =
                new List<SalesTaxLineResult>(workingLines.Count);

            decimal standardRated = 0m;
            decimal zeroRated = 0m;
            decimal exempt = 0m;
            decimal outOfScope = 0m;

            for (int index = 0; index < workingLines.Count; index++)
            {
                WorkingLine working = workingLines[index];
                decimal allocation = allocations[index];

                decimal finalInclusive = RoundMoney(
                    working.BeforeInvoiceDiscount.Inclusive -
                    allocation);

                MoneyBreakdown finalBreakdown =
                    allocation == 0m
                        ? working.BeforeInvoiceDiscount
                        : CalculateFromInclusiveAmount(
                            finalInclusive,
                            working.EffectiveProfile.RatePercent);

                string categoryCode = NormalizeCode(
                    working.EffectiveProfile.TaxCategoryCode);

                switch (categoryCode)
                {
                    case TaxCategoryCodes.Standard:
                        standardRated += finalBreakdown.Taxable;
                        break;

                    case TaxCategoryCodes.ZeroRated:
                        zeroRated += finalBreakdown.Taxable;
                        break;

                    case TaxCategoryCodes.Exempt:
                        exempt += finalBreakdown.Taxable;
                        break;

                    case TaxCategoryCodes.OutOfScope:
                        outOfScope += finalBreakdown.Taxable;
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Unsupported Tax Category '{categoryCode}'.");
                }

                results.Add(new SalesTaxLineResult
                {
                    LineKey = working.Input.LineKey,
                    ItemVariantId =
                        working.Input.ItemVariantId,
                    TaxProfile = working.EffectiveProfile,
                    GrossAmount = working.Gross,
                    LineDiscountAmount =
                        working.LineDiscount,
                    InvoiceDiscountAllocation =
                        allocation,
                    TaxableAmount =
                        finalBreakdown.Taxable,
                    VatAmount =
                        finalBreakdown.Vat,
                    TaxInclusiveAmount =
                        finalBreakdown.Inclusive
                });
            }

            decimal grossTotal = RoundMoney(
                workingLines.Sum(line => line.Gross));

            decimal lineDiscountTotal = RoundMoney(
                workingLines.Sum(line => line.LineDiscount));

            decimal netTotal = RoundMoney(
                results.Sum(line => line.TaxInclusiveAmount));

            decimal totalVat = RoundMoney(
                results.Sum(line => line.VatAmount));

            standardRated = RoundMoney(standardRated);
            zeroRated = RoundMoney(zeroRated);
            exempt = RoundMoney(exempt);
            outOfScope = RoundMoney(outOfScope);

            return new SalesTaxDocumentResult
            {
                Lines = results,
                GrossTotal = grossTotal,
                LineDiscountTotal = lineDiscountTotal,
                InvoiceDiscount = roundedInvoiceDiscount,
                TotalDiscount = RoundMoney(
                    lineDiscountTotal +
                    roundedInvoiceDiscount),
                NetTotal = netTotal,
                TotalVat = totalVat,
                TaxableAmountTotal = standardRated,
                StandardRatedAmount = standardRated,
                ZeroRatedAmount = zeroRated,
                ExemptAmount = exempt,
                OutOfScopeAmount = outOfScope
            };
        }

        private static void ValidateLine(
            SalesTaxLineInput line)
        {
            if (line == null)
                throw new InvalidOperationException("Sale line is missing.");

            if (line.ItemVariantId <= 0)
                throw new InvalidOperationException("Sale item variant is required.");

            if (line.Quantity <= 0m)
                throw new InvalidOperationException("Sale quantity must be greater than zero.");

            if (line.VatInclusiveUnitPrice < 0m)
                throw new InvalidOperationException("Selling price cannot be negative.");

            if (line.TaxProfile == null)
                throw new InvalidOperationException("Sale tax profile is missing.");

            if (!ItemTypeCodes.IsValid(line.TaxProfile.ItemType))
            {
                throw new InvalidOperationException(
                    $"Unsupported sale item type '{line.TaxProfile.ItemType}'.");
            }

            if (!IsApprovedCategoryCode(
                    line.TaxProfile.TaxCategoryCode))
            {
                throw new InvalidOperationException(
                    $"Unsupported Tax Category '{line.TaxProfile.TaxCategoryCode}'.");
            }

            if (line.TaxProfile.RatePercent < 0m)
                throw new InvalidOperationException("Tax rate cannot be negative.");
        }

        private static SalesTaxProfile CreateNonVatProfile(
            SalesTaxProfile source)
        {
            return new SalesTaxProfile
            {
                ItemVariantId = source.ItemVariantId,
                ItemType = source.ItemType,
                TaxCategoryId = null,
                TaxCategoryCode =
                    TaxCategoryCodes.OutOfScope,
                TaxCategoryName =
                    "Non-VAT Registered Sale",
                TaxTreatmentType =
                    TaxTreatmentTypes.OutOfScope,
                TaxRateId = null,
                TaxCode = "NON-VAT",
                TaxName =
                    "Non-VAT Registered Sale",
                RatePercent = 0m
            };
        }

        private static MoneyBreakdown
            CalculateFromInclusiveAmount(
                decimal inclusiveAmount,
                decimal ratePercent)
        {
            decimal inclusive = RoundMoney(
                inclusiveAmount);

            if (inclusive <= 0m ||
                ratePercent <= 0m)
            {
                return new MoneyBreakdown(
                    inclusive,
                    0m,
                    inclusive);
            }

            decimal divisor =
                1m + (ratePercent / 100m);

            decimal taxable = RoundMoney(
                inclusive / divisor);

            decimal vat = RoundMoney(
                inclusive - taxable);

            return new MoneyBreakdown(
                taxable,
                vat,
                inclusive);
        }

        private static decimal[] AllocateDiscount(
            IReadOnlyList<decimal> lineValues,
            decimal discount)
        {
            decimal roundedDiscount =
                RoundMoney(discount);

            var result =
                new decimal[lineValues.Count];

            if (lineValues.Count == 0 ||
                roundedDiscount == 0m)
            {
                return result;
            }

            decimal total = RoundMoney(
                lineValues.Sum());

            if (total <= 0m)
                return result;

            decimal allocated = 0m;

            for (int index = 0;
                 index < lineValues.Count;
                 index++)
            {
                if (index == lineValues.Count - 1)
                {
                    result[index] =
                        RoundMoney(
                            roundedDiscount -
                            allocated);

                    break;
                }

                decimal share = RoundMoney(
                    roundedDiscount *
                    lineValues[index] /
                    total);

                if (share < 0m)
                    share = 0m;

                if (share > lineValues[index])
                    share = lineValues[index];

                result[index] = share;
                allocated = RoundMoney(
                    allocated + share);
            }

            decimal difference = RoundMoney(
                roundedDiscount -
                result.Sum());

            if (difference != 0m &&
                result.Length > 0)
            {
                result[^1] = RoundMoney(
                    result[^1] +
                    difference);
            }

            return result;
        }

        private static bool IsApprovedCategoryCode(
            string? categoryCode)
        {
            string code = NormalizeCode(
                categoryCode);

            return code ==
                       TaxCategoryCodes.Standard ||
                   code ==
                       TaxCategoryCodes.ZeroRated ||
                   code ==
                       TaxCategoryCodes.Exempt ||
                   code ==
                       TaxCategoryCodes.OutOfScope;
        }

        private static decimal RoundMoney(
            decimal value)
        {
            return Math.Round(
                value,
                2,
                MidpointRounding.AwayFromZero);
        }

        private static string NormalizeCode(
            string? value)
        {
            return NormalizeText(value)
                .ToUpperInvariant();
        }

        private static string NormalizeText(
            string? value)
        {
            return (value ?? string.Empty)
                .Trim();
        }

        private sealed record WorkingLine(
            SalesTaxLineInput Input,
            SalesTaxProfile EffectiveProfile,
            decimal Gross,
            decimal LineDiscount,
            MoneyBreakdown BeforeInvoiceDiscount);

        private readonly record struct MoneyBreakdown(
            decimal Taxable,
            decimal Vat,
            decimal Inclusive);
    }
}
