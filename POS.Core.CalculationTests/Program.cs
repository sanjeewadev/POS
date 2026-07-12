using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services.Tax;
using POS.Core.Services.Pricing;
using POS.Core.Services.Documents;

namespace POS.Core.CalculationTests
{
    internal static class Program
    {
        private static readonly PurchasingTaxService Service = new();
        private static readonly SalesTaxService SalesService = new();

        private static int Main()
        {
            var tests = new (string Name, Action Run)[]
            {
                ("Exclusive standard VAT", ExclusiveStandardVat),
                ("Inclusive standard VAT", InclusiveStandardVat),
                ("Inclusive line discount", InclusiveLineDiscount),
                ("Mixed categories with global discount", MixedCategoriesWithGlobalDiscount),
                ("Global discount allocation reconciliation", GlobalDiscountAllocationReconciliation),
                ("Fixed zero-percent treatments", FixedZeroPercentTreatments),
                ("Effective-dated rate resolution", EffectiveDatedRateResolution),
                ("GRN landed-cost markup includes VAT", GrnLandedCostMarkupIncludesVat),
                ("GRN exact selling price", GrnExactSellingPrice),
                ("GRN current-price percentage change", GrnCurrentPricePercentageChange),
                ("GRN selling-price rounding", GrnSellingPriceRounding),
                ("GRN keep-current pricing", GrnKeepCurrentPricing),
                ("Sales inclusive standard VAT", SalesInclusiveStandardVat),
                ("Sales inclusive line discount", SalesInclusiveLineDiscount),
                ("Sales mixed categories with invoice discount", SalesMixedCategoriesWithInvoiceDiscount),
                ("Non-VAT sale is out of scope", NonVatSaleIsOutOfScope),
                ("Service effective tax profile resolution", ServiceEffectiveTaxProfileResolution),
                ("Sales invoice discount allocation reconciliation", SalesInvoiceDiscountAllocationReconciliation),
                ("Sales category totals after invoice discount", SalesCategoryTotalsAfterInvoiceDiscount),
                ("Cashier service search and exact lookup", CashierServiceSearchAndExactLookup),
                ("Cashier retail and wholesale VAT-inclusive pricing", CashierRetailAndWholesaleVatInclusivePricing),
                ("Service checkout saves tax without inventory", ServiceCheckoutSavesTaxWithoutInventory),
                ("Mixed sale preserves Stock Item deduction", MixedSalePreservesStockItemDeduction),
                ("Invoice discount persists exact line allocations", InvoiceDiscountPersistsExactLineAllocations),
                ("Receipt formatter separates receipt from Tax Invoice", ReceiptFormatterSeparatesReceiptFromTaxInvoice),
                ("Tax Invoice formatter uses immutable snapshots", TaxInvoiceFormatterUsesImmutableSnapshots),
                ("Tax Invoice issue is unique and idempotent", TaxInvoiceIssueIsUniqueAndIdempotent),
                ("Legacy and non-VAT Tax Invoice issue is blocked", InvalidTaxInvoiceIssueIsBlocked),
                ("Sales document print audits track original reprint and failure", SalesDocumentPrintAuditsTrackResults),
                ("Last completed receipt reload is terminal scoped", LastCompletedReceiptReloadIsTerminalScoped)
            };

            try
            {
                foreach (var test in tests)
                {
                    test.Run();
                    Console.WriteLine($"PASS: {test.Name}");
                }

                Console.WriteLine();
                Console.WriteLine($"All {tests.Length} purchasing, GRN pricing, sales VAT, repository, and sales document checks passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("FAILED: " + ex.Message);
                return 1;
            }
        }

        private static void ExclusiveStandardVat()
        {
            var result = Service.CalculateDocument(
                new[]
                {
                    Line(1, 1m, 1000m, StandardProfile())
                },
                globalDiscount: 0m,
                isTaxInclusive: false);

            AssertMoney(1000m, result.Lines[0].TaxableAmount, "taxable amount");
            AssertMoney(180m, result.Lines[0].VatAmount, "VAT amount");
            AssertMoney(1180m, result.Lines[0].TaxInclusiveAmount, "inclusive amount");
            AssertMoney(1180m, result.NetPayable, "document payable");
        }

        private static void InclusiveStandardVat()
        {
            var result = Service.CalculateDocument(
                new[]
                {
                    Line(1, 1m, 1180m, StandardProfile())
                },
                globalDiscount: 0m,
                isTaxInclusive: true);

            AssertMoney(1000m, result.Lines[0].TaxableAmount, "taxable amount");
            AssertMoney(180m, result.Lines[0].VatAmount, "VAT amount");
            AssertMoney(1180m, result.Lines[0].TaxInclusiveAmount, "inclusive amount");
        }

        private static void InclusiveLineDiscount()
        {
            var result = Service.CalculateDocument(
                new[]
                {
                    Line(
                        1,
                        quantity: 2m,
                        unitPrice: 1180m,
                        profile: StandardProfile(),
                        discountMode: "Amount",
                        discountValue: 180m)
                },
                globalDiscount: 0m,
                isTaxInclusive: true);

            AssertMoney(2360m, result.Subtotal, "subtotal");
            AssertMoney(180m, result.LineDiscountTotal, "line discount");
            AssertMoney(1847.46m, result.Lines[0].TaxableAmount, "taxable amount");
            AssertMoney(332.54m, result.Lines[0].VatAmount, "VAT amount");
            AssertMoney(2180m, result.Lines[0].TaxInclusiveAmount, "inclusive amount");
        }

        private static void MixedCategoriesWithGlobalDiscount()
        {
            var result = Service.CalculateDocument(
                new[]
                {
                    Line(1, 1m, 1000m, StandardProfile()),
                    Line(2, 1m, 1000m, ExemptProfile())
                },
                globalDiscount: 218m,
                isTaxInclusive: false);

            var standard = result.Lines.Single(l => l.ItemVariantId == 1);
            var exempt = result.Lines.Single(l => l.ItemVariantId == 2);

            AssertMoney(118m, standard.GlobalDiscountAllocation, "standard allocation");
            AssertMoney(100m, exempt.GlobalDiscountAllocation, "exempt allocation");
            AssertMoney(900m, standard.TaxableAmount, "standard taxable value");
            AssertMoney(162m, standard.VatAmount, "standard VAT");
            AssertMoney(900m, exempt.TaxableAmount, "exempt value");
            AssertMoney(1962m, result.NetPayable, "document payable");
            AssertMoney(900m, result.StandardRatedAmount, "standard total");
            AssertMoney(900m, result.ExemptAmount, "exempt total");
        }

        private static void GlobalDiscountAllocationReconciliation()
        {
            var lines = Enumerable.Range(1, 10)
                .Select(index => Line(
                    index,
                    quantity: 1m,
                    unitPrice: 1m,
                    profile: StandardProfile()))
                .ToArray();

            var result = Service.CalculateDocument(
                lines,
                globalDiscount: 0.05m,
                isTaxInclusive: true);

            AssertMoney(
                0.05m,
                result.Lines.Sum(line => line.GlobalDiscountAllocation),
                "allocated global discount");

            if (result.Lines.Any(line => line.GlobalDiscountAllocation < 0m))
                throw new InvalidOperationException("A line received a negative global-discount allocation.");

            AssertMoney(
                result.NetPayable,
                result.Lines.Sum(line => line.TaxInclusiveAmount),
                "line/document reconciliation");
        }

        private static void FixedZeroPercentTreatments()
        {
            var result = Service.CalculateDocument(
                new[]
                {
                    Line(1, 1m, 100m, ZeroProfile()),
                    Line(2, 1m, 200m, ExemptProfile()),
                    Line(3, 1m, 300m, OutOfScopeProfile())
                },
                globalDiscount: 0m,
                isTaxInclusive: false);

            AssertMoney(0m, result.TotalVat, "total VAT");
            AssertMoney(100m, result.ZeroRatedAmount, "zero-rated total");
            AssertMoney(200m, result.ExemptAmount, "exempt total");
            AssertMoney(300m, result.OutOfScopeAmount, "out-of-scope total");
            AssertMoney(600m, result.NetPayable, "document payable");
        }


        private static void EffectiveDatedRateResolution()
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            using var context = new AppDbContext(options);
            context.Database.EnsureCreated();

            var itemCategory = new Category
            {
                CategoryCode = "TEST",
                CategoryName = "Test",
                CreatedBy = "Test",
                UpdatedBy = "Test"
            };

            var taxCategory = new TaxCategory
            {
                CategoryCode = TaxCategoryCodes.Standard,
                CategoryName = "Standard VAT",
                TreatmentType = TaxTreatmentTypes.StandardRated,
                IsRateBased = true,
                IsActive = true,
                DisplayOrder = 10
            };

            var item = new ItemParent
            {
                ItemCode = "TEST-ITEM",
                ItemName = "Test Item",
                Category = itemCategory,
                UnitOfMeasureId = 1,
                BaseUom = "PCS",
                ItemType = ItemTypeCodes.StockItem,
                TaxCategory = taxCategory,
                TaxCode = "VAT-STD",
                IsTaxInclusive = true
            };

            var variant = new ItemVariant
            {
                ItemParent = item,
                SkuCode = "TEST-SKU",
                VariantDescription = "Standard"
            };

            context.ItemVariants.Add(variant);
            context.TaxRates.AddRange(
                new TaxRate
                {
                    TaxCode = "VAT-OLD",
                    TaxName = "Old Standard VAT",
                    TaxCategory = taxCategory,
                    RatePercent = 15m,
                    EffectiveFrom = new DateTime(2023, 1, 1),
                    EffectiveTo = new DateTime(2023, 12, 31),
                    IsActive = true,
                    ChangeReason = "Test old rate",
                    CreatedBy = "Test",
                    UpdatedBy = "Test"
                },
                new TaxRate
                {
                    TaxCode = "VAT-STD",
                    TaxName = "Standard VAT",
                    TaxCategory = taxCategory,
                    RatePercent = 18m,
                    EffectiveFrom = new DateTime(2024, 1, 1),
                    EffectiveTo = null,
                    IsActive = true,
                    ChangeReason = "Test current rate",
                    CreatedBy = "Test",
                    UpdatedBy = "Test"
                });

            context.SaveChanges();

            var oldProfile = Service.ResolveProfilesAsync(
                    context,
                    new[] { variant.Id },
                    new DateTime(2023, 12, 31))
                .GetAwaiter()
                .GetResult()[variant.Id];

            var currentProfile = Service.ResolveProfilesAsync(
                    context,
                    new[] { variant.Id },
                    new DateTime(2024, 1, 1))
                .GetAwaiter()
                .GetResult()[variant.Id];

            AssertMoney(15m, oldProfile.RatePercent, "old effective rate");
            AssertMoney(18m, currentProfile.RatePercent, "current effective rate");

            if (oldProfile.TaxCode != "VAT-OLD" || currentProfile.TaxCode != "VAT-STD")
                throw new InvalidOperationException("Effective tax-code resolution failed.");
        }

        private static void GrnLandedCostMarkupIncludesVat()
        {
            var calculator = new GrnSellingPriceCalculator();

            decimal result = calculator.Calculate(
                currentVatInclusivePrice: 1180m,
                landedCostExcludingVat: 1000m,
                vatRatePercent: 18m,
                method: GrnSellingPriceMethods.MarkupFromLandedCost,
                value: 20m,
                roundingMode: GrnSellingPriceRoundingModes.None);

            AssertMoney(1416m, result, "VAT-inclusive markup price");
        }

        private static void GrnExactSellingPrice()
        {
            var calculator = new GrnSellingPriceCalculator();

            decimal result = calculator.Calculate(
                currentVatInclusivePrice: 1180m,
                landedCostExcludingVat: 1000m,
                vatRatePercent: 18m,
                method: GrnSellingPriceMethods.SetExactPrice,
                value: 1250m,
                roundingMode: GrnSellingPriceRoundingModes.None);

            AssertMoney(1250m, result, "exact selling price");
        }

        private static void GrnCurrentPricePercentageChange()
        {
            var calculator = new GrnSellingPriceCalculator();

            decimal result = calculator.Calculate(
                currentVatInclusivePrice: 1000m,
                landedCostExcludingVat: 800m,
                vatRatePercent: 18m,
                method: GrnSellingPriceMethods.ChangeCurrentByPercent,
                value: 10m,
                roundingMode: GrnSellingPriceRoundingModes.None);

            AssertMoney(1100m, result, "current-price percentage change");
        }

        private static void GrnSellingPriceRounding()
        {
            var calculator = new GrnSellingPriceCalculator();

            decimal result = calculator.Calculate(
                currentVatInclusivePrice: 1180m,
                landedCostExcludingVat: 1000m,
                vatRatePercent: 18m,
                method: GrnSellingPriceMethods.MarkupFromLandedCost,
                value: 20m,
                roundingMode: GrnSellingPriceRoundingModes.NearestFive);

            AssertMoney(1415m, result, "nearest-five selling price");
        }

        private static void GrnKeepCurrentPricing()
        {
            var calculator = new GrnSellingPriceCalculator();

            decimal result = calculator.Calculate(
                currentVatInclusivePrice: 987.65m,
                landedCostExcludingVat: 800m,
                vatRatePercent: 18m,
                method: GrnSellingPriceMethods.KeepCurrent,
                value: 999m,
                roundingMode: GrnSellingPriceRoundingModes.NearestTen);

            AssertMoney(987.65m, result, "keep-current selling price");
        }

        private static void SalesInclusiveStandardVat()
        {
            SalesTaxDocumentResult result =
                SalesService.CalculateDocument(
                    new[]
                    {
                        SalesLine(
                            lineKey: 1,
                            variantId: 1,
                            quantity: 1m,
                            vatInclusivePrice: 1180m,
                            lineDiscount: 0m,
                            profile: SalesStandardProfile())
                    },
                    invoiceDiscount: 0m,
                    isVatRegisteredSale: true);

            AssertMoney(
                1000m,
                result.Lines[0].TaxableAmount,
                "sales taxable amount");

            AssertMoney(
                180m,
                result.Lines[0].VatAmount,
                "sales VAT amount");

            AssertMoney(
                1180m,
                result.NetTotal,
                "sales net total");
        }

        private static void SalesInclusiveLineDiscount()
        {
            SalesTaxDocumentResult result =
                SalesService.CalculateDocument(
                    new[]
                    {
                        SalesLine(
                            lineKey: 1,
                            variantId: 1,
                            quantity: 2m,
                            vatInclusivePrice: 1180m,
                            lineDiscount: 180m,
                            profile: SalesStandardProfile())
                    },
                    invoiceDiscount: 0m,
                    isVatRegisteredSale: true);

            AssertMoney(
                2360m,
                result.GrossTotal,
                "sales gross total");

            AssertMoney(
                180m,
                result.LineDiscountTotal,
                "sales line discount");

            AssertMoney(
                1847.46m,
                result.Lines[0].TaxableAmount,
                "discounted sales taxable amount");

            AssertMoney(
                332.54m,
                result.Lines[0].VatAmount,
                "discounted sales VAT amount");

            AssertMoney(
                2180m,
                result.NetTotal,
                "discounted sales net total");
        }

        private static void SalesMixedCategoriesWithInvoiceDiscount()
        {
            SalesTaxDocumentResult result =
                SalesService.CalculateDocument(
                    new[]
                    {
                        SalesLine(
                            lineKey: 1,
                            variantId: 1,
                            quantity: 1m,
                            vatInclusivePrice: 1180m,
                            lineDiscount: 0m,
                            profile: SalesStandardProfile()),
                        SalesLine(
                            lineKey: 2,
                            variantId: 2,
                            quantity: 1m,
                            vatInclusivePrice: 1000m,
                            lineDiscount: 0m,
                            profile: SalesExemptProfile())
                    },
                    invoiceDiscount: 218m,
                    isVatRegisteredSale: true);

            SalesTaxLineResult standard =
                result.Lines.Single(
                    line => line.LineKey == 1);

            SalesTaxLineResult exempt =
                result.Lines.Single(
                    line => line.LineKey == 2);

            AssertMoney(
                118m,
                standard.InvoiceDiscountAllocation,
                "sales standard invoice-discount allocation");

            AssertMoney(
                100m,
                exempt.InvoiceDiscountAllocation,
                "sales exempt invoice-discount allocation");

            AssertMoney(
                900m,
                standard.TaxableAmount,
                "sales standard taxable amount");

            AssertMoney(
                162m,
                standard.VatAmount,
                "sales standard VAT");

            AssertMoney(
                900m,
                exempt.TaxableAmount,
                "sales exempt amount");

            AssertMoney(
                1962m,
                result.NetTotal,
                "sales mixed net total");
        }

        private static void NonVatSaleIsOutOfScope()
        {
            SalesTaxDocumentResult result =
                SalesService.CalculateDocument(
                    new[]
                    {
                        SalesLine(
                            lineKey: 1,
                            variantId: 1,
                            quantity: 1m,
                            vatInclusivePrice: 1180m,
                            lineDiscount: 0m,
                            profile: SalesStandardProfile())
                    },
                    invoiceDiscount: 0m,
                    isVatRegisteredSale: false);

            AssertMoney(
                0m,
                result.TotalVat,
                "non-VAT store output VAT");

            AssertMoney(
                1180m,
                result.OutOfScopeAmount,
                "non-VAT store out-of-scope value");

            if (result.Lines[0].TaxProfile.TaxCategoryCode !=
                TaxCategoryCodes.OutOfScope)
            {
                throw new InvalidOperationException(
                    "Non-VAT sale did not receive an out-of-scope snapshot.");
            }
        }

        private static void ServiceEffectiveTaxProfileResolution()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");

            connection.Open();

            var options =
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite(connection)
                    .Options;

            using var context =
                new AppDbContext(options);

            context.Database.EnsureCreated();

            var itemCategory =
                new Category
                {
                    CategoryCode = "SERVICE",
                    CategoryName = "Services",
                    CreatedBy = "Test",
                    UpdatedBy = "Test"
                };

            var taxCategory =
                new TaxCategory
                {
                    CategoryCode =
                        TaxCategoryCodes.Standard,
                    CategoryName =
                        "Standard VAT",
                    TreatmentType =
                        TaxTreatmentTypes.StandardRated,
                    IsRateBased = true,
                    IsActive = true,
                    DisplayOrder = 10
                };

            var item =
                new ItemParent
                {
                    ItemCode = "TEST-SERVICE",
                    ItemName = "Test Service",
                    Category = itemCategory,
                    UnitOfMeasureId = 1,
                    BaseUom = "JOB",
                    ItemType = ItemTypeCodes.Service,
                    TaxCategory = taxCategory,
                    TaxCode = "VAT-STD",
                    IsTaxInclusive = true
                };

            var variant =
                new ItemVariant
                {
                    ItemParent = item,
                    SkuCode = "TEST-SERVICE-SKU",
                    VariantDescription = "Standard",
                    RetailPrice = 1180m
                };

            context.ItemVariants.Add(variant);

            context.TaxRates.Add(
                new TaxRate
                {
                    TaxCode = "VAT-STD",
                    TaxName = "Standard VAT",
                    TaxCategory = taxCategory,
                    RatePercent = 18m,
                    EffectiveFrom =
                        new DateTime(2024, 1, 1),
                    EffectiveTo = null,
                    IsActive = true,
                    ChangeReason = "Test service rate",
                    CreatedBy = "Test",
                    UpdatedBy = "Test"
                });

            context.SaveChanges();

            SalesTaxProfile profile =
                SalesService.ResolveProfilesAsync(
                        context,
                        new[] { variant.Id },
                        new DateTime(2026, 7, 11))
                    .GetAwaiter()
                    .GetResult()[variant.Id];

            if (profile.ItemType != ItemTypeCodes.Service)
            {
                throw new InvalidOperationException(
                    "Service item type was not preserved in the sales tax profile.");
            }

            AssertMoney(
                18m,
                profile.RatePercent,
                "service effective VAT rate");
        }

        private static void SalesInvoiceDiscountAllocationReconciliation()
        {
            SalesTaxLineInput[] lines =
                Enumerable.Range(1, 10)
                    .Select(index =>
                        SalesLine(
                            lineKey: index,
                            variantId: index,
                            quantity: 1m,
                            vatInclusivePrice: 1m,
                            lineDiscount: 0m,
                            profile: SalesStandardProfile(
                                variantId: index)))
                    .ToArray();

            SalesTaxDocumentResult result =
                SalesService.CalculateDocument(
                    lines,
                    invoiceDiscount: 0.05m,
                    isVatRegisteredSale: true);

            AssertMoney(
                0.05m,
                result.Lines.Sum(
                    line =>
                        line.InvoiceDiscountAllocation),
                "sales allocated invoice discount");

            AssertMoney(
                result.NetTotal,
                result.Lines.Sum(
                    line =>
                        line.TaxInclusiveAmount),
                "sales line/document reconciliation");
        }

        private static void SalesCategoryTotalsAfterInvoiceDiscount()
        {
            SalesTaxDocumentResult result =
                SalesService.CalculateDocument(
                    new[]
                    {
                        SalesLine(
                            lineKey: 1,
                            variantId: 1,
                            quantity: 1m,
                            vatInclusivePrice: 1180m,
                            lineDiscount: 0m,
                            profile: SalesStandardProfile(
                                variantId: 1)),
                        SalesLine(
                            lineKey: 2,
                            variantId: 2,
                            quantity: 1m,
                            vatInclusivePrice: 100m,
                            lineDiscount: 0m,
                            profile: SalesFixedProfile(
                                variantId: 2,
                                code: TaxCategoryCodes.ZeroRated,
                                name: "Zero Rated",
                                treatment: TaxTreatmentTypes.ZeroRated)),
                        SalesLine(
                            lineKey: 3,
                            variantId: 3,
                            quantity: 1m,
                            vatInclusivePrice: 200m,
                            lineDiscount: 0m,
                            profile: SalesExemptProfile(
                                variantId: 3)),
                        SalesLine(
                            lineKey: 4,
                            variantId: 4,
                            quantity: 1m,
                            vatInclusivePrice: 300m,
                            lineDiscount: 0m,
                            profile: SalesFixedProfile(
                                variantId: 4,
                                code: TaxCategoryCodes.OutOfScope,
                                name: "Out of Scope",
                                treatment: TaxTreatmentTypes.OutOfScope))
                    },
                    invoiceDiscount: 178m,
                    isVatRegisteredSale: true);

            AssertMoney(
                1780m,
                result.GrossTotal,
                "sales category gross total");

            AssertMoney(
                178m,
                result.InvoiceDiscount,
                "sales category invoice discount");

            AssertMoney(
                1602m,
                result.NetTotal,
                "sales category net total");

            AssertMoney(
                900m,
                result.StandardRatedAmount,
                "sales category Standard VAT amount");

            AssertMoney(
                162m,
                result.TotalVat,
                "sales category VAT amount");

            AssertMoney(
                90m,
                result.ZeroRatedAmount,
                "sales category Zero Rated amount");

            AssertMoney(
                180m,
                result.ExemptAmount,
                "sales category Exempt amount");

            AssertMoney(
                270m,
                result.OutOfScopeAmount,
                "sales category Out of Scope amount");

            AssertMoney(
                178m,
                result.Lines.Sum(line =>
                    line.InvoiceDiscountAllocation),
                "sales category allocated invoice discount");
        }

        private static void CashierServiceSearchAndExactLookup()
        {
            using var factory =
                new RepositoryTestDbContextFactory();

            RepositoryTestScenario scenario =
                SeedRepositoryTestScenario(factory);

            var repository =
                new ItemMasterRepository(factory);

            List<ParentSeekDto> parents =
                repository.SearchSeekParentsAsync(
                        "Installation")
                    .GetAwaiter()
                    .GetResult();

            ParentSeekDto parent =
                parents.Single();

            if (!parent.IsService)
            {
                throw new InvalidOperationException(
                    "Cashier parent search did not preserve the Service item type.");
            }

            List<VariantSeekDto> variants =
                repository.GetSeekVariantsAsync(
                        scenario.ServiceParentId)
                    .GetAwaiter()
                    .GetResult();

            VariantSeekDto variant =
                variants.Single();

            if (!variant.IsService ||
                variant.HasBatchTracking ||
                !variant.HasStock)
            {
                throw new InvalidOperationException(
                    "Cashier Service variant was treated as stock or batch controlled.");
            }

            CashierSellableItemDto bySku =
                repository
                    .GetSellableItemByBarcodeOrSkuAsync(
                        scenario.ServiceSku)
                    .GetAwaiter()
                    .GetResult()
                ?? throw new InvalidOperationException(
                    "Service SKU lookup failed.");

            CashierSellableItemDto byBarcode =
                repository
                    .GetSellableItemByBarcodeOrSkuAsync(
                        scenario.ServiceBarcode)
                    .GetAwaiter()
                    .GetResult()
                ?? throw new InvalidOperationException(
                    "Service barcode lookup failed.");

            CashierSellableItemDto byVariant =
                repository
                    .GetSellableItemByVariantIdAsync(
                        scenario.ServiceVariantId)
                    .GetAwaiter()
                    .GetResult()
                ?? throw new InvalidOperationException(
                    "Service variant lookup failed.");

            foreach (CashierSellableItemDto result in
                     new[] { bySku, byBarcode, byVariant })
            {
                if (!result.IsService ||
                    result.HasBatchTracking ||
                    result.StockOnHand != 0m)
                {
                    throw new InvalidOperationException(
                        "Cashier Service lookup returned stock-only behavior.");
                }

                AssertMoney(
                    400m,
                    result.CostPrice,
                    "cashier Service cost");

                if (result.TaxProfile.ItemType !=
                        ItemTypeCodes.Service ||
                    result.TaxProfile.TaxCategoryCode !=
                        TaxCategoryCodes.Standard)
                {
                    throw new InvalidOperationException(
                        "Cashier Service lookup did not load its effective tax profile.");
                }

                AssertMoney(
                    18m,
                    result.TaxProfile.RatePercent,
                    "cashier Service effective VAT rate");
            }
        }

        private static void CashierRetailAndWholesaleVatInclusivePricing()
        {
            using var factory =
                new RepositoryTestDbContextFactory();

            RepositoryTestScenario scenario =
                SeedRepositoryTestScenario(factory);

            var repository =
                new ItemMasterRepository(factory);

            CashierSellableItemDto stockItem =
                repository
                    .GetSellableItemByVariantIdAsync(
                        scenario.StockVariantId)
                    .GetAwaiter()
                    .GetResult()
                ?? throw new InvalidOperationException(
                    "Stock Item lookup failed for pricing test.");

            CashierSellableItemDto service =
                repository
                    .GetSellableItemByVariantIdAsync(
                        scenario.ServiceVariantId)
                    .GetAwaiter()
                    .GetResult()
                ?? throw new InvalidOperationException(
                    "Service lookup failed for pricing test.");

            foreach (CashierSellableItemDto result in
                     new[] { stockItem, service })
            {
                AssertMoney(
                    1180m,
                    result.RetailPrice,
                    "cashier VAT-inclusive retail price");

                AssertMoney(
                    1062m,
                    result.WholesalePrice,
                    "cashier VAT-inclusive wholesale price");

                SalesTaxDocumentResult retail =
                    SalesService.CalculateDocument(
                        new[]
                        {
                            SalesLine(
                                lineKey: 1,
                                variantId: result.VariantId,
                                quantity: 1m,
                                vatInclusivePrice:
                                    result.RetailPrice,
                                lineDiscount: 0m,
                                profile: result.TaxProfile)
                        },
                        invoiceDiscount: 0m,
                        isVatRegisteredSale: true);

                SalesTaxDocumentResult wholesale =
                    SalesService.CalculateDocument(
                        new[]
                        {
                            SalesLine(
                                lineKey: 1,
                                variantId: result.VariantId,
                                quantity: 1m,
                                vatInclusivePrice:
                                    result.WholesalePrice,
                                lineDiscount: 0m,
                                profile: result.TaxProfile)
                        },
                        invoiceDiscount: 0m,
                        isVatRegisteredSale: true);

                AssertMoney(
                    1000m,
                    retail.StandardRatedAmount,
                    "cashier retail taxable value");

                AssertMoney(
                    180m,
                    retail.TotalVat,
                    "cashier retail VAT value");

                AssertMoney(
                    900m,
                    wholesale.StandardRatedAmount,
                    "cashier wholesale taxable value");

                AssertMoney(
                    162m,
                    wholesale.TotalVat,
                    "cashier wholesale VAT value");
            }
        }

        private static void ServiceCheckoutSavesTaxWithoutInventory()
        {
            using var factory =
                new RepositoryTestDbContextFactory();

            RepositoryTestScenario scenario =
                SeedRepositoryTestScenario(factory);

            var repository =
                new SalesRepository(factory);

            SalesHeader saved =
                repository.ProcessCheckoutAsync(
                        CreateRepositoryTestHeader(
                            scenario.ShiftSessionId,
                            1180m),
                        new List<SalesLine>
                        {
                            CreateRepositoryTestLine(
                                scenario.ServiceVariantId,
                                null,
                                scenario.ServiceSku,
                                "Installation Service",
                                quantity: 1m,
                                unitPrice: 1180m)
                        },
                        new List<SalesPayment>
                        {
                            CreateCashPayment(1180m)
                        })
                    .GetAwaiter()
                    .GetResult();

            using AppDbContext context =
                factory.CreateDbContext();

            SalesHeader storedHeader =
                context.SalesHeaders
                    .AsNoTracking()
                    .Single(header =>
                        header.Id == saved.Id);

            SalesLine storedLine =
                context.SalesLines
                    .AsNoTracking()
                    .Single(line =>
                        line.SalesHeaderId == saved.Id);

            decimal stockAfter =
                context.ItemBatches
                    .AsNoTracking()
                    .Where(batch =>
                        batch.Id == scenario.StockBatchId)
                    .Select(batch =>
                        batch.CurrentStock)
                    .Single();

            int inventoryRows =
                context.InventoryTransactions
                    .AsNoTracking()
                    .Count(transaction =>
                        transaction.ReferenceDocument ==
                        storedHeader.InvoiceNo);

            if (storedLine.ItemBatchId.HasValue)
            {
                throw new InvalidOperationException(
                    "Service checkout persisted a stock batch.");
            }

            if (storedLine.ItemTypeSnapshot !=
                ItemTypeCodes.Service)
            {
                throw new InvalidOperationException(
                    "Service item-type snapshot was not saved.");
            }

            if (storedLine.TaxSnapshotStatus !=
                    TaxSnapshotStatuses.Complete ||
                storedHeader.TaxSnapshotStatus !=
                    TaxSnapshotStatuses.Complete)
            {
                throw new InvalidOperationException(
                    "Service tax snapshots were not completed.");
            }

            AssertMoney(
                400m,
                storedLine.CostPrice,
                "persisted Service cost");

            AssertMoney(
                780m,
                storedLine.ProfitAmount,
                "persisted Service profit");

            AssertMoney(
                1000m,
                storedLine.TaxableAmountSnapshot ?? -1m,
                "Service taxable snapshot");

            AssertMoney(
                180m,
                storedLine.VatAmountSnapshot ?? -1m,
                "Service VAT snapshot");

            AssertMoney(
                1000m,
                storedHeader.StandardRatedAmount ?? -1m,
                "Service header Standard VAT amount");

            AssertMoney(
                180m,
                storedHeader.TotalVatAmount ?? -1m,
                "Service header VAT amount");

            AssertMoney(
                5m,
                stockAfter,
                "stock unchanged after Service sale");

            if (inventoryRows != 0)
            {
                throw new InvalidOperationException(
                    "Service checkout created an inventory transaction.");
            }
        }

        private static void MixedSalePreservesStockItemDeduction()
        {
            using var factory =
                new RepositoryTestDbContextFactory();

            RepositoryTestScenario scenario =
                SeedRepositoryTestScenario(factory);

            var repository =
                new SalesRepository(factory);

            SalesHeader saved =
                repository.ProcessCheckoutAsync(
                        CreateRepositoryTestHeader(
                            scenario.ShiftSessionId,
                            2360m),
                        new List<SalesLine>
                        {
                            CreateRepositoryTestLine(
                                scenario.StockVariantId,
                                scenario.StockBatchId,
                                scenario.StockSku,
                                "Test Stock Item",
                                quantity: 1m,
                                unitPrice: 1180m),
                            CreateRepositoryTestLine(
                                scenario.ServiceVariantId,
                                null,
                                scenario.ServiceSku,
                                "Installation Service",
                                quantity: 1m,
                                unitPrice: 1180m)
                        },
                        new List<SalesPayment>
                        {
                            CreateCashPayment(2360m)
                        })
                    .GetAwaiter()
                    .GetResult();

            using AppDbContext context =
                factory.CreateDbContext();

            List<SalesLine> storedLines =
                context.SalesLines
                    .AsNoTracking()
                    .Where(line =>
                        line.SalesHeaderId == saved.Id)
                    .OrderBy(line =>
                        line.Id)
                    .ToList();

            SalesLine stockLine =
                storedLines.Single(line =>
                    line.ItemVariantId ==
                    scenario.StockVariantId);

            SalesLine serviceLine =
                storedLines.Single(line =>
                    line.ItemVariantId ==
                    scenario.ServiceVariantId);

            decimal stockAfter =
                context.ItemBatches
                    .AsNoTracking()
                    .Where(batch =>
                        batch.Id == scenario.StockBatchId)
                    .Select(batch =>
                        batch.CurrentStock)
                    .Single();

            List<InventoryTransaction> inventoryRows =
                context.InventoryTransactions
                    .AsNoTracking()
                    .Where(transaction =>
                        transaction.ReferenceDocument ==
                        saved.InvoiceNo)
                    .ToList();

            if (stockLine.ItemTypeSnapshot !=
                    ItemTypeCodes.StockItem ||
                serviceLine.ItemTypeSnapshot !=
                    ItemTypeCodes.Service)
            {
                throw new InvalidOperationException(
                    "Mixed sale item-type snapshots are incorrect.");
            }

            if (!stockLine.ItemBatchId.HasValue ||
                serviceLine.ItemBatchId.HasValue)
            {
                throw new InvalidOperationException(
                    "Mixed sale batch references are incorrect.");
            }

            AssertMoney(
                4m,
                stockAfter,
                "Stock Item quantity after mixed sale");

            if (inventoryRows.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Expected one inventory transaction, found {inventoryRows.Count}.");
            }

            InventoryTransaction inventory =
                inventoryRows.Single();

            if (inventory.ItemVariantId !=
                    scenario.StockVariantId ||
                inventory.ItemBatchId !=
                    scenario.StockBatchId)
            {
                throw new InvalidOperationException(
                    "Inventory deduction was not linked to the Stock Item batch.");
            }

            AssertMoney(
                -1m,
                inventory.Quantity,
                "Stock Item inventory deduction");

            AssertMoney(
                600m,
                stockLine.CostPrice,
                "Stock Item persisted batch cost");

            AssertMoney(
                400m,
                serviceLine.CostPrice,
                "mixed-sale Service persisted cost");

            if (storedLines.Any(line =>
                    line.TaxSnapshotStatus !=
                    TaxSnapshotStatuses.Complete))
            {
                throw new InvalidOperationException(
                    "Mixed sale did not save complete line tax snapshots.");
            }
        }

        private static void InvoiceDiscountPersistsExactLineAllocations()
        {
            using var factory =
                new RepositoryTestDbContextFactory();

            RepositoryTestScenario scenario =
                SeedRepositoryTestScenario(factory);

            var repository =
                new SalesRepository(factory);

            SalesLine stockInput =
                CreateRepositoryTestLine(
                    scenario.StockVariantId,
                    scenario.StockBatchId,
                    scenario.StockSku,
                    "Test Stock Item",
                    quantity: 1m,
                    unitPrice: 1180m);

            stockInput.ManualDiscountAmount = 118m;
            stockInput.DiscountAmount = 118m;
            stockInput.DiscountMode = "Amount";
            stockInput.IsManualDiscount = true;
            stockInput.LineTotal = 1062m;

            SalesHeader header =
                CreateRepositoryTestHeader(
                    scenario.ShiftSessionId,
                    amountTendered: 2017.80m,
                    invoiceDiscount: 224.20m);

            SalesHeader saved =
                repository.ProcessCheckoutAsync(
                        header,
                        new List<SalesLine>
                        {
                            stockInput,
                            CreateRepositoryTestLine(
                                scenario.ServiceVariantId,
                                null,
                                scenario.ServiceSku,
                                "Installation Service",
                                quantity: 1m,
                                unitPrice: 1180m)
                        },
                        new List<SalesPayment>
                        {
                            CreateCashPayment(2017.80m)
                        })
                    .GetAwaiter()
                    .GetResult();

            using AppDbContext context =
                factory.CreateDbContext();

            SalesHeader storedHeader =
                context.SalesHeaders
                    .AsNoTracking()
                    .Single(candidate =>
                        candidate.Id == saved.Id);

            List<SalesLine> storedLines =
                context.SalesLines
                    .AsNoTracking()
                    .Where(line =>
                        line.SalesHeaderId == saved.Id)
                    .ToList();

            SalesLine stockLine =
                storedLines.Single(line =>
                    line.ItemVariantId ==
                    scenario.StockVariantId);

            SalesLine serviceLine =
                storedLines.Single(line =>
                    line.ItemVariantId ==
                    scenario.ServiceVariantId);

            AssertMoney(
                2360m,
                storedHeader.GrossTotal,
                "invoice-discount header gross total");

            AssertMoney(
                342.20m,
                storedHeader.TotalDiscount,
                "invoice-discount header total discount");

            AssertMoney(
                2017.80m,
                storedHeader.NetTotal,
                "invoice-discount header net total");

            AssertMoney(
                1710m,
                storedHeader.StandardRatedAmount ?? -1m,
                "invoice-discount header taxable amount");

            AssertMoney(
                307.80m,
                storedHeader.TotalVatAmount ?? -1m,
                "invoice-discount header VAT amount");

            AssertMoney(
                224.20m,
                storedLines.Sum(line =>
                    line.DiscountAmount) - 118m,
                "persisted invoice-discount allocation total");

            AssertMoney(
                224.20m,
                stockLine.DiscountAmount,
                "Stock Item combined line discount");

            AssertMoney(
                118m,
                serviceLine.DiscountAmount,
                "Service invoice-discount allocation");

            AssertMoney(
                955.80m,
                stockLine.LineTotal,
                "Stock Item final inclusive line total");

            AssertMoney(
                1062m,
                serviceLine.LineTotal,
                "Service final inclusive line total");

            AssertMoney(
                810m,
                stockLine.TaxableAmountSnapshot ?? -1m,
                "Stock Item final taxable snapshot");

            AssertMoney(
                145.80m,
                stockLine.VatAmountSnapshot ?? -1m,
                "Stock Item final VAT snapshot");

            AssertMoney(
                900m,
                serviceLine.TaxableAmountSnapshot ?? -1m,
                "Service final taxable snapshot");

            AssertMoney(
                162m,
                serviceLine.VatAmountSnapshot ?? -1m,
                "Service final VAT snapshot");

            AssertMoney(
                355.80m,
                stockLine.ProfitAmount,
                "Stock Item final profit after invoice discount");

            AssertMoney(
                662m,
                serviceLine.ProfitAmount,
                "Service final profit after invoice discount");

            decimal stockAfter =
                context.ItemBatches
                    .AsNoTracking()
                    .Where(batch =>
                        batch.Id == scenario.StockBatchId)
                    .Select(batch =>
                        batch.CurrentStock)
                    .Single();

            AssertMoney(
                4m,
                stockAfter,
                "Stock Item quantity after invoice-discount sale");

            int inventoryRows =
                context.InventoryTransactions
                    .AsNoTracking()
                    .Count(transaction =>
                        transaction.ReferenceDocument ==
                        storedHeader.InvoiceNo);

            if (inventoryRows != 1)
            {
                throw new InvalidOperationException(
                    $"Invoice-discount sale expected one inventory transaction, found {inventoryRows}.");
            }

            if (storedLines.Any(line =>
                    line.TaxSnapshotStatus !=
                    TaxSnapshotStatuses.Complete))
            {
                throw new InvalidOperationException(
                    "Invoice-discount sale did not preserve complete line tax snapshots.");
            }
        }

        private static void ReceiptFormatterSeparatesReceiptFromTaxInvoice()
        {
            SalesHeader sale = CreateFormatterTestSale();
            StoreSettings settings = CreateFormatterStoreSettings();
            settings.TaxpayerIdentificationNumber = "CURRENT-TIN";
            settings.VatRegistrationNumber = "CURRENT-VAT";
            var formatter = new SalesDocumentTextFormatter();

            string receipt = formatter.FormatReceipt(
                sale,
                settings,
                paperWidth: 58,
                copyLabel: SalesDocumentCopyLabels.Original);

            AssertContains(receipt, "SALES RECEIPT", "receipt heading");
            AssertContains(receipt, "NOT A TAX INVOICE", "receipt disclaimer");
            AssertContains(receipt, "ORIGINAL", "receipt original label");
            AssertContains(receipt, "TIN-SNAPSHOT", "saved receipt supplier TIN");
            AssertContains(receipt, "VAT-SNAPSHOT", "saved receipt supplier VAT number");
            AssertContains(receipt, "Stock Item With A Very Long", "receipt Stock Item");
            AssertContains(receipt, "Installation Service", "receipt Service");
            AssertContains(receipt, "Discount", "receipt discount");
            AssertContains(receipt, "Rs. 50.00", "receipt discount amount");
            AssertContains(receipt, "Cash", "receipt cash payment");
            AssertContains(receipt, "Card", "receipt card payment");
            AssertContains(receipt, "Zero Rated", "receipt Zero Rated summary");
            AssertContains(receipt, "Exempt", "receipt Exempt summary");
            AssertContains(receipt, "Out of Scope", "receipt Out of Scope summary");

            if (receipt.Contains("CURRENT-TIN", StringComparison.Ordinal) ||
                receipt.Contains("CURRENT-VAT", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Receipt formatter used current tax settings instead of saved snapshots.");
            }

            if (receipt.Split('\n').Any(line => line.TrimEnd('\r').Length > 32))
            {
                throw new InvalidOperationException(
                    "58 mm receipt formatter produced a line wider than 32 characters.");
            }

            sale.TaxSnapshotStatus = TaxSnapshotStatuses.LegacyUnknown;
            string legacyReceipt = formatter.FormatReceipt(
                sale,
                settings,
                paperWidth: 80,
                copyLabel: SalesDocumentCopyLabels.Reprint);

            AssertContains(
                legacyReceipt,
                "Detailed VAT snapshots are unavailable",
                "legacy receipt VAT warning");
        }

        private static void TaxInvoiceFormatterUsesImmutableSnapshots()
        {
            SalesHeader sale = CreateFormatterTestSale();
            sale.DocumentType = SalesDocumentTypes.TaxInvoice;
            sale.TaxInvoiceNo = "TI-000001";
            sale.SupplierTinSnapshot = "SAVED-TIN";
            sale.SupplierVatNoSnapshot = "SAVED-VAT";
            sale.CustomerName = "Snapshot Customer";
            sale.CustomerTinSnapshot = "CUSTOMER-TIN";
            sale.CustomerVatNoSnapshot = "CUSTOMER-VAT";
            sale.CustomerAddressSnapshot = "Saved customer address";

            StoreSettings settings = CreateFormatterStoreSettings();
            settings.TaxpayerIdentificationNumber = "CURRENT-TIN";
            settings.VatRegistrationNumber = "CURRENT-VAT";
            settings.GlobalVatRate = 99m;

            var formatter = new SalesDocumentTextFormatter();

            string invoice = formatter.FormatTaxInvoice(
                sale,
                settings,
                new DateTime(2026, 7, 12, 7, 0, 0, DateTimeKind.Utc),
                paperWidth: 80,
                copyLabel: SalesDocumentCopyLabels.Reprint);

            AssertContains(invoice, "TAX INVOICE", "Tax Invoice heading");
            AssertContains(invoice, "REPRINT", "Tax Invoice reprint label");
            AssertContains(invoice, "TI-000001", "Tax Invoice number");
            AssertContains(invoice, "SAVED-TIN", "saved supplier TIN");
            AssertContains(invoice, "SAVED-VAT", "saved supplier VAT number");
            AssertContains(invoice, "CUSTOMER-TIN", "saved customer TIN");
            AssertContains(invoice, "Rs. 180.00", "saved VAT amount");

            if (invoice.Contains("CURRENT-TIN", StringComparison.Ordinal) ||
                invoice.Contains("CURRENT-VAT", StringComparison.Ordinal) ||
                invoice.Contains("99.0000%", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Tax Invoice formatter used current tax settings instead of saved snapshots.");
            }
        }

        private static void TaxInvoiceIssueIsUniqueAndIdempotent()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SalesHeader sale = CreateCompletedDocumentTestSale(factory, scenario);
            var repository = new SalesDocumentRepository(factory);

            TaxInvoiceIssueRequest request = CreateTaxInvoiceIssueRequest(sale.Id);

            PreparedSalesDocument first =
                repository.IssueOrPrepareTaxInvoiceAsync(request)
                    .GetAwaiter()
                    .GetResult();

            PreparedSalesDocument second =
                repository.IssueOrPrepareTaxInvoiceAsync(request)
                    .GetAwaiter()
                    .GetResult();

            SalesHeader anotherSale =
                CreateCompletedDocumentTestSale(factory, scenario);
            PreparedSalesDocument anotherInvoice =
                repository.IssueOrPrepareTaxInvoiceAsync(
                        CreateTaxInvoiceIssueRequest(anotherSale.Id))
                    .GetAwaiter()
                    .GetResult();

            if (string.IsNullOrWhiteSpace(first.DocumentNumber) ||
                first.DocumentNumber != second.DocumentNumber)
            {
                throw new InvalidOperationException(
                    "Repeated Tax Invoice issue did not return the existing document number.");
            }

            if (string.Equals(
                    first.DocumentNumber,
                    anotherInvoice.DocumentNumber,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Two different sales received the same Tax Invoice number.");
            }

            using AppDbContext context = factory.CreateDbContext();

            int issuedEvents = context.SalesDocumentAudits
                .Count(row =>
                    row.SalesHeaderId == sale.Id &&
                    row.EventType == SalesDocumentEventTypes.TaxInvoiceIssued);

            if (issuedEvents != 1)
            {
                throw new InvalidOperationException(
                    $"Expected one Tax Invoice issue audit, found {issuedEvents}.");
            }

            SalesHeader stored = context.SalesHeaders
                .AsNoTracking()
                .Single(row => row.Id == sale.Id);

            if (stored.DocumentType != SalesDocumentTypes.TaxInvoice ||
                stored.TaxInvoiceNo != first.DocumentNumber ||
                stored.CustomerTinSnapshot != "CUSTOMER-TIN")
            {
                throw new InvalidOperationException(
                    "Tax Invoice immutable issue snapshots were not persisted.");
            }
        }

        private static void InvalidTaxInvoiceIssueIsBlocked()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SalesHeader sale = CreateCompletedDocumentTestSale(factory, scenario);
            var repository = new SalesDocumentRepository(factory);

            using (AppDbContext context = factory.CreateDbContext())
            {
                SalesHeader stored = context.SalesHeaders.Single(row => row.Id == sale.Id);
                stored.TaxSnapshotStatus = TaxSnapshotStatuses.LegacyUnknown;
                context.SaveChanges();
            }

            AssertThrows(
                () => repository.IssueOrPrepareTaxInvoiceAsync(
                        CreateTaxInvoiceIssueRequest(sale.Id))
                    .GetAwaiter()
                    .GetResult(),
                "complete tax snapshots");

            using (AppDbContext context = factory.CreateDbContext())
            {
                SalesHeader stored = context.SalesHeaders.Single(row => row.Id == sale.Id);
                stored.TaxSnapshotStatus = TaxSnapshotStatuses.Complete;
                stored.IsVatRegisteredSale = false;
                context.SaveChanges();
            }

            AssertThrows(
                () => repository.IssueOrPrepareTaxInvoiceAsync(
                        CreateTaxInvoiceIssueRequest(sale.Id))
                    .GetAwaiter()
                    .GetResult(),
                "not VAT registered");
        }

        private static void SalesDocumentPrintAuditsTrackResults()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SalesHeader sale = CreateCompletedDocumentTestSale(factory, scenario);
            var repository = new SalesDocumentRepository(factory);

            PreparedSalesDocument beforePrint =
                repository.PrepareReceiptAsync(sale.Id)
                    .GetAwaiter()
                    .GetResult();

            if (beforePrint.CopyLabel != SalesDocumentCopyLabels.Original ||
                beforePrint.NextCopyNumber != 1)
            {
                throw new InvalidOperationException(
                    "The first receipt print was not identified as ORIGINAL.");
            }

            repository.RecordPrintResultAsync(
                    sale.Id,
                    SalesDocumentTypes.Receipt,
                    sale.InvoiceNo,
                    true,
                    "Test Cashier",
                    "T01",
                    "Test Printer")
                .GetAwaiter()
                .GetResult();

            PreparedSalesDocument afterFirstPrint =
                repository.PrepareReceiptAsync(sale.Id)
                    .GetAwaiter()
                    .GetResult();

            if (afterFirstPrint.CopyLabel != SalesDocumentCopyLabels.Reprint ||
                afterFirstPrint.NextCopyNumber != 2)
            {
                throw new InvalidOperationException(
                    "The next receipt print was not identified as REPRINT.");
            }

            repository.RecordPrintResultAsync(
                    sale.Id,
                    SalesDocumentTypes.Receipt,
                    sale.InvoiceNo,
                    true,
                    "Test Cashier",
                    "T01",
                    "Test Printer")
                .GetAwaiter()
                .GetResult();

            repository.RecordPrintResultAsync(
                    sale.Id,
                    SalesDocumentTypes.Receipt,
                    sale.InvoiceNo,
                    false,
                    "Test Cashier",
                    "T01",
                    "Missing Printer",
                    "Printer unavailable")
                .GetAwaiter()
                .GetResult();

            using AppDbContext context = factory.CreateDbContext();
            List<SalesDocumentAudit> audits = context.SalesDocumentAudits
                .AsNoTracking()
                .Where(row => row.SalesHeaderId == sale.Id)
                .OrderBy(row => row.Id)
                .ToList();

            int saleCount = context.SalesHeaders.Count();

            if (saleCount != 1)
            {
                throw new InvalidOperationException(
                    "A failed print audit created a duplicate sale.");
            }

            if (audits.Count != 3 ||
                audits[0].EventType != SalesDocumentEventTypes.OriginalPrinted ||
                audits[0].CopyNumber != 1 ||
                audits[1].EventType != SalesDocumentEventTypes.Reprinted ||
                audits[1].CopyNumber != 2 ||
                audits[2].EventType != SalesDocumentEventTypes.PrintFailed ||
                audits[2].IsSuccessful ||
                !audits[2].ErrorMessage.Contains("unavailable", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Sales document print audit sequence was not persisted correctly.");
            }
        }

        private static void LastCompletedReceiptReloadIsTerminalScoped()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SalesHeader sale = CreateCompletedDocumentTestSale(factory, scenario);
            var repository = new SalesDocumentRepository(factory);

            SalesHeader? last = repository.GetLastCompletedSaleAsync("T01")
                .GetAwaiter()
                .GetResult();

            SalesHeader? otherTerminal = repository.GetLastCompletedSaleAsync("T99")
                .GetAwaiter()
                .GetResult();

            if (last == null ||
                last.Id != sale.Id ||
                last.SalesLines.Count == 0 ||
                last.SalesPayments.Count == 0 ||
                otherTerminal != null)
            {
                throw new InvalidOperationException(
                    "Last completed receipt lookup was not terminal scoped or fully loaded.");
            }
        }

        private static SalesHeader CreateCompletedDocumentTestSale(
            RepositoryTestDbContextFactory factory,
            RepositoryTestScenario scenario)
        {
            var repository = new SalesRepository(factory);

            return repository.ProcessCheckoutAsync(
                    CreateRepositoryTestHeader(
                        scenario.ShiftSessionId,
                        1180m),
                    new List<SalesLine>
                    {
                        CreateRepositoryTestLine(
                            scenario.ServiceVariantId,
                            null,
                            scenario.ServiceSku,
                            "Installation Service",
                            quantity: 1m,
                            unitPrice: 1180m)
                    },
                    new List<SalesPayment>
                    {
                        CreateCashPayment(1180m)
                    })
                .GetAwaiter()
                .GetResult();
        }

        private static TaxInvoiceIssueRequest CreateTaxInvoiceIssueRequest(
            int salesHeaderId)
        {
            return new TaxInvoiceIssueRequest
            {
                SalesHeaderId = salesHeaderId,
                CustomerName = "Test Business Customer",
                CustomerTin = "CUSTOMER-TIN",
                CustomerVatNo = "CUSTOMER-VAT",
                CustomerAddress = "1 Test Street, Colombo",
                PerformedBy = "Test Cashier",
                TerminalNo = "T01"
            };
        }

        private static SalesHeader CreateFormatterTestSale()
        {
            var sale = new SalesHeader
            {
                Id = 100,
                ShiftSessionId = 1,
                InvoiceNo = "INV-000100",
                TerminalNo = "T01",
                CashierName = "Test Cashier",
                CustomerName = "Test Customer",
                TransactionDate = new DateTime(2026, 7, 12, 10, 30, 0),
                IsVatRegisteredSale = true,
                SupplierTinSnapshot = "TIN-SNAPSHOT",
                SupplierVatNoSnapshot = "VAT-SNAPSHOT",
                GrossTotal = 1830m,
                TotalDiscount = 50m,
                NetTotal = 1780m,
                AmountTendered = 1780m,
                BalanceReturned = 0m,
                PaymentMethod = "Cash",
                TaxableAmountTotal = 1000m,
                TotalVatAmount = 180m,
                StandardRatedAmount = 1000m,
                ZeroRatedAmount = 100m,
                ExemptAmount = 200m,
                OutOfScopeAmount = 300m,
                TaxSnapshotStatus = TaxSnapshotStatuses.Complete
            };

            SalesLine discountedStockLine = CreateFormatterLine(
                1,
                "Stock Item With A Very Long Description For Receipt Wrapping",
                1180m,
                TaxCategoryCodes.Standard,
                "Standard VAT",
                18m,
                1000m,
                180m);

            discountedStockLine.UnitPrice = 1230m;
            discountedStockLine.GrossAmount = 1230m;
            discountedStockLine.DiscountAmount = 50m;
            discountedStockLine.LineTotal = 1180m;
            sale.SalesLines.Add(discountedStockLine);

            sale.SalesLines.Add(CreateFormatterLine(
                2,
                "Installation Service",
                100m,
                TaxCategoryCodes.ZeroRated,
                "Zero Rated",
                0m,
                100m,
                0m));

            sale.SalesLines.Add(CreateFormatterLine(
                3,
                "Exempt Service",
                200m,
                TaxCategoryCodes.Exempt,
                "Exempt",
                0m,
                200m,
                0m));

            sale.SalesLines.Add(CreateFormatterLine(
                4,
                "Out of Scope Service",
                300m,
                TaxCategoryCodes.OutOfScope,
                "Out of Scope",
                0m,
                300m,
                0m));

            sale.PaymentMethod = "Split";
            sale.SalesPayments.Add(new SalesPayment
            {
                Id = 1,
                PaymentType = "Cash",
                Amount = 1280m
            });
            sale.SalesPayments.Add(new SalesPayment
            {
                Id = 2,
                PaymentType = "Card",
                Amount = 500m,
                ReferenceNo = "CARD-REF"
            });

            return sale;
        }

        private static SalesLine CreateFormatterLine(
            int id,
            string description,
            decimal inclusiveAmount,
            string categoryCode,
            string taxName,
            decimal rate,
            decimal taxableAmount,
            decimal vatAmount)
        {
            return new SalesLine
            {
                Id = id,
                ItemDescription = description,
                Uom = "PCS",
                Quantity = 1m,
                UnitPrice = inclusiveAmount,
                GrossAmount = inclusiveAmount,
                LineTotal = inclusiveAmount,
                TaxCategoryCodeSnapshot = categoryCode,
                TaxNameSnapshot = taxName,
                TaxRatePercentSnapshot = rate,
                TaxableAmountSnapshot = taxableAmount,
                VatAmountSnapshot = vatAmount,
                TaxInclusiveAmountSnapshot = inclusiveAmount,
                TaxSnapshotStatus = TaxSnapshotStatuses.Complete
            };
        }

        private static StoreSettings CreateFormatterStoreSettings()
        {
            return new StoreSettings
            {
                StoreName = "Test Store",
                LegalName = "Test Store (Pvt) Ltd",
                AddressLine1 = "1 Main Street",
                City = "Colombo",
                Country = "Sri Lanka",
                Phone = "0112345678",
                CurrencySymbol = "Rs.",
                ReceiptFooter = "Thank You"
            };
        }

        private static void AssertContains(
            string text,
            string expected,
            string label)
        {
            if (!text.Contains(expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{label}: expected text '{expected}' was not found.");
            }
        }

        private static void AssertThrows(
            Action action,
            string expectedMessagePart)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Exception actual = ex is AggregateException aggregate &&
                                   aggregate.InnerException != null
                    ? aggregate.InnerException
                    : ex;

                if (actual.Message.Contains(
                        expectedMessagePart,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"Expected error containing '{expectedMessagePart}', actual: {actual.Message}");
            }

            throw new InvalidOperationException(
                $"Expected an error containing '{expectedMessagePart}'.");
        }

        private static RepositoryTestScenario SeedRepositoryTestScenario(
            RepositoryTestDbContextFactory factory)
        {
            using AppDbContext context =
                factory.CreateDbContext();

            var category =
                new Category
                {
                    CategoryCode = "TEST-SALES",
                    CategoryName = "Test Sales",
                    CreatedBy = "Test",
                    UpdatedBy = "Test"
                };

            var standardTax =
                new TaxCategory
                {
                    CategoryCode =
                        TaxCategoryCodes.Standard,
                    CategoryName =
                        "Standard VAT",
                    TreatmentType =
                        TaxTreatmentTypes.StandardRated,
                    IsRateBased = true,
                    IsActive = true,
                    DisplayOrder = 10
                };

            context.Categories.Add(category);
            context.TaxCategories.Add(standardTax);
            context.SaveChanges();

            var stockParent =
                new ItemParent
                {
                    ItemCode = "TEST-STOCK",
                    ItemName = "Test Stock Item",
                    PrintName = "Test Stock Item",
                    CategoryId = category.Id,
                    UnitOfMeasureId = 1,
                    BaseUom = "PCS",
                    ItemType = ItemTypeCodes.StockItem,
                    TaxCategoryId = standardTax.Id,
                    TaxCode = "VAT-STD",
                    IsTaxInclusive = true,
                    HasBatchTracking = true,
                    HasExpiryTracking = false,
                    HasBatchExpiry = false
                };

            var serviceParent =
                new ItemParent
                {
                    ItemCode = "TEST-SERVICE",
                    ItemName = "Installation Service",
                    PrintName = "Installation Service",
                    CategoryId = category.Id,
                    UnitOfMeasureId = 1,
                    BaseUom = "JOB",
                    ItemType = ItemTypeCodes.Service,
                    TaxCategoryId = standardTax.Id,
                    TaxCode = "VAT-STD",
                    IsTaxInclusive = true,
                    HasBatchTracking = false,
                    HasExpiryTracking = false,
                    HasBatchExpiry = false,
                    IsPurchaseLocked = true
                };

            var stockVariant =
                new ItemVariant
                {
                    ItemParent = stockParent,
                    SkuCode = "TEST-STOCK-SKU",
                    Barcode = "TEST-STOCK-BARCODE",
                    VariantDescription = "Standard",
                    AverageCost = 600m,
                    CostPrice = 600m,
                    RetailPrice = 1180m,
                    WholesalePrice = 1062m,
                    MinimumPrice = 600m
                };

            var serviceVariant =
                new ItemVariant
                {
                    ItemParent = serviceParent,
                    SkuCode = "TEST-SERVICE-SKU",
                    Barcode = "TEST-SERVICE-BARCODE",
                    VariantDescription = "Standard",
                    AverageCost = 350m,
                    CostPrice = 400m,
                    RetailPrice = 1180m,
                    WholesalePrice = 1062m,
                    MinimumPrice = 400m
                };

            context.ItemVariants.AddRange(
                stockVariant,
                serviceVariant);

            context.SaveChanges();

            var stockBatch =
                new ItemBatch
                {
                    ItemVariantId = stockVariant.Id,
                    BatchNo = "TEST-BATCH",
                    InternalBatchBarcode =
                        "TEST-BATCH-BARCODE",
                    ReceivedDate =
                        new DateTime(2026, 7, 1),
                    CostPrice = 600m,
                    RetailPrice = 1180m,
                    WholesalePrice = 1062m,
                    CurrentStock = 5m
                };

            var rate =
                new TaxRate
                {
                    TaxCode = "VAT-STD",
                    TaxName = "Standard VAT",
                    TaxCategoryId = standardTax.Id,
                    RatePercent = 18m,
                    EffectiveFrom =
                        new DateTime(2024, 1, 1),
                    EffectiveTo = null,
                    ChangeReason =
                        "Repository integration test",
                    CreatedBy = "Test",
                    UpdatedBy = "Test",
                    IsActive = true
                };

            var shift =
                new ShiftSession
                {
                    TerminalNo = "T01",
                    CashierName = "Test Cashier",
                    StartTime =
                        new DateTime(2026, 7, 12, 8, 0, 0),
                    Status = "Open"
                };

            var store =
                new StoreSettings
                {
                    LegalName = "Test Store",
                    StoreName = "Test Store",
                    IsVatRegistered = true,
                    TaxpayerIdentificationNumber =
                        "TIN-TEST",
                    VatRegistrationNumber =
                        "VAT-TEST",
                    IsActive = true
                };

            context.ItemBatches.Add(stockBatch);
            context.TaxRates.Add(rate);
            context.ShiftSessions.Add(shift);
            context.StoreSettings.Add(store);
            context.SaveChanges();

            return new RepositoryTestScenario
            {
                ShiftSessionId = shift.Id,
                StockParentId = stockParent.Id,
                StockVariantId = stockVariant.Id,
                StockBatchId = stockBatch.Id,
                StockSku = stockVariant.SkuCode,
                ServiceParentId = serviceParent.Id,
                ServiceVariantId = serviceVariant.Id,
                ServiceSku = serviceVariant.SkuCode,
                ServiceBarcode = serviceVariant.Barcode
            };
        }

        private static SalesHeader CreateRepositoryTestHeader(
            int shiftSessionId,
            decimal amountTendered,
            decimal invoiceDiscount = 0m)
        {
            return new SalesHeader
            {
                ShiftSessionId = shiftSessionId,
                TerminalNo = "T01",
                CashierName = "Test Cashier",
                CustomerName = "Walk-In",
                CustomerType = "Walk-In",
                PaymentMethod = "Cash",
                AmountTendered = amountTendered,
                BalanceReturned = 0m,
                InvoiceDiscountAmount = invoiceDiscount
            };
        }

        private static SalesLine CreateRepositoryTestLine(
            int itemVariantId,
            int? itemBatchId,
            string sku,
            string description,
            decimal quantity,
            decimal unitPrice)
        {
            decimal gross =
                Math.Round(
                    quantity * unitPrice,
                    2);

            return new SalesLine
            {
                ItemVariantId = itemVariantId,
                ItemBatchId = itemBatchId,
                SkuCode = sku,
                Barcode = sku,
                ItemDescription = description,
                BatchNo =
                    itemBatchId.HasValue
                        ? "TEST-BATCH"
                        : string.Empty,
                Uom =
                    itemBatchId.HasValue
                        ? "PCS"
                        : "JOB",
                Quantity = quantity,
                UnitPrice = unitPrice,
                GrossAmount = gross,
                DiscountPercentage = 0m,
                DiscountAmount = 0m,
                ManualDiscountAmount = 0m,
                DiscountMode = "None",
                IsManualDiscount = false,
                OriginalUnitPrice = unitPrice,
                LineTotal = gross
            };
        }

        private static SalesPayment CreateCashPayment(
            decimal amount)
        {
            return new SalesPayment
            {
                PaymentType = "Cash",
                Amount = amount,
                ReferenceNo = string.Empty,
                BankOrCardType = string.Empty
            };
        }

        private sealed class RepositoryTestScenario
        {
            public int ShiftSessionId { get; init; }

            public int StockParentId { get; init; }

            public int StockVariantId { get; init; }

            public int StockBatchId { get; init; }

            public string StockSku { get; init; } =
                string.Empty;

            public int ServiceParentId { get; init; }

            public int ServiceVariantId { get; init; }

            public string ServiceSku { get; init; } =
                string.Empty;

            public string ServiceBarcode { get; init; } =
                string.Empty;
        }

        private sealed class RepositoryTestDbContextFactory :
            IDbContextFactory<AppDbContext>,
            IDisposable
        {
            private readonly string _databasePath;

            private readonly DbContextOptions<AppDbContext>
                _options;

            public RepositoryTestDbContextFactory()
            {
                _databasePath =
                    Path.Combine(
                        Path.GetTempPath(),
                        $"pos-phase7e2-{Guid.NewGuid():N}.db");

                _options =
                    new DbContextOptionsBuilder<AppDbContext>()
                        .UseSqlite(
                            $"Data Source={_databasePath}")
                        .Options;

                using AppDbContext context =
                    CreateDbContext();

                context.Database.EnsureCreated();
            }

            public AppDbContext CreateDbContext()
            {
                return new AppDbContext(_options);
            }

            public Task<AppDbContext> CreateDbContextAsync(
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(
                    CreateDbContext());
            }

            public void Dispose()
            {
                SqliteConnection.ClearAllPools();

                DeleteIfPresent(_databasePath);
                DeleteIfPresent(_databasePath + "-shm");
                DeleteIfPresent(_databasePath + "-wal");
            }

            private static void DeleteIfPresent(
                string path)
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private static SalesTaxLineInput SalesLine(
            int lineKey,
            int variantId,
            decimal quantity,
            decimal vatInclusivePrice,
            decimal lineDiscount,
            SalesTaxProfile profile)
        {
            return new SalesTaxLineInput
            {
                LineKey = lineKey,
                ItemVariantId = variantId,
                Quantity = quantity,
                VatInclusiveUnitPrice =
                    vatInclusivePrice,
                LineDiscountAmount =
                    lineDiscount,
                TaxProfile = profile
            };
        }

        private static SalesTaxProfile SalesStandardProfile(
            int variantId = 1,
            string itemType = ItemTypeCodes.StockItem)
        {
            return new SalesTaxProfile
            {
                ItemVariantId = variantId,
                ItemType = itemType,
                TaxCategoryId = 1,
                TaxCategoryCode =
                    TaxCategoryCodes.Standard,
                TaxCategoryName =
                    "Standard VAT",
                TaxTreatmentType =
                    TaxTreatmentTypes.StandardRated,
                TaxRateId = 1,
                TaxCode = "VAT-STD",
                TaxName = "Standard VAT",
                RatePercent = 18m
            };
        }

        private static SalesTaxProfile SalesExemptProfile(
            int variantId = 2,
            string itemType = ItemTypeCodes.StockItem)
        {
            return new SalesTaxProfile
            {
                ItemVariantId = variantId,
                ItemType = itemType,
                TaxCategoryId = 3,
                TaxCategoryCode =
                    TaxCategoryCodes.Exempt,
                TaxCategoryName = "Exempt",
                TaxTreatmentType =
                    TaxTreatmentTypes.Exempt,
                TaxRateId = null,
                TaxCode =
                    TaxCategoryCodes.Exempt,
                TaxName = "Exempt",
                RatePercent = 0m
            };
        }

        private static SalesTaxProfile SalesFixedProfile(
            int variantId,
            string code,
            string name,
            string treatment,
            string itemType = ItemTypeCodes.StockItem)
        {
            return new SalesTaxProfile
            {
                ItemVariantId = variantId,
                ItemType = itemType,
                TaxCategoryId = variantId,
                TaxCategoryCode = code,
                TaxCategoryName = name,
                TaxTreatmentType = treatment,
                TaxRateId = null,
                TaxCode = code,
                TaxName = name,
                RatePercent = 0m
            };
        }

        private static PurchasingTaxLineInput Line(
            int variantId,
            decimal quantity,
            decimal unitPrice,
            PurchasingTaxProfile profile,
            string discountMode = "Amount",
            decimal discountValue = 0m)
        {
            return new PurchasingTaxLineInput
            {
                ItemVariantId = variantId,
                Quantity = quantity,
                UnitPrice = unitPrice,
                DiscountMode = discountMode,
                DiscountValue = discountValue,
                TaxProfile = profile
            };
        }

        private static PurchasingTaxProfile StandardProfile()
        {
            return new PurchasingTaxProfile
            {
                ItemVariantId = 1,
                TaxCategoryId = 1,
                TaxCategoryCode = TaxCategoryCodes.Standard,
                TaxCategoryName = "Standard VAT",
                TaxTreatmentType = TaxTreatmentTypes.StandardRated,
                TaxRateId = 1,
                TaxCode = "VAT-STD",
                TaxName = "Standard VAT",
                RatePercent = 18m
            };
        }

        private static PurchasingTaxProfile ZeroProfile()
        {
            return FixedProfile(
                TaxCategoryCodes.ZeroRated,
                "Zero Rated",
                TaxTreatmentTypes.ZeroRated,
                categoryId: 2);
        }

        private static PurchasingTaxProfile ExemptProfile()
        {
            return FixedProfile(
                TaxCategoryCodes.Exempt,
                "Exempt",
                TaxTreatmentTypes.Exempt,
                categoryId: 3);
        }

        private static PurchasingTaxProfile OutOfScopeProfile()
        {
            return FixedProfile(
                TaxCategoryCodes.OutOfScope,
                "Out of Scope",
                TaxTreatmentTypes.OutOfScope,
                categoryId: 4);
        }

        private static PurchasingTaxProfile FixedProfile(
            string code,
            string name,
            string treatment,
            int categoryId)
        {
            return new PurchasingTaxProfile
            {
                ItemVariantId = categoryId,
                TaxCategoryId = categoryId,
                TaxCategoryCode = code,
                TaxCategoryName = name,
                TaxTreatmentType = treatment,
                TaxRateId = null,
                TaxCode = code,
                TaxName = name,
                RatePercent = 0m
            };
        }

        private static void AssertMoney(
            decimal expected,
            decimal actual,
            string label)
        {
            if (expected != actual)
            {
                throw new InvalidOperationException(
                    $"{label}: expected {expected:N2}, actual {actual:N2}.");
            }
        }
    }
}
