using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Core.Services.Tax;
using POS.Core.Services.Pricing;

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
                ("Cashier service search and exact lookup", CashierServiceSearchAndExactLookup),
                ("Service checkout saves tax without inventory", ServiceCheckoutSavesTaxWithoutInventory),
                ("Mixed sale preserves Stock Item deduction", MixedSalePreservesStockItemDeduction)
            };

            try
            {
                foreach (var test in tests)
                {
                    test.Run();
                    Console.WriteLine($"PASS: {test.Name}");
                }

                Console.WriteLine();
                Console.WriteLine($"All {tests.Length} purchasing, GRN pricing, sales VAT, and repository integration checks passed.");
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
            decimal amountTendered)
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
                BalanceReturned = 0m
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
