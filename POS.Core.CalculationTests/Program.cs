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
using POS.Core.Services.Returns;
using POS.Core.Enums;
using POS.Core.Services;
using POS.Core.Utilities;


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
                ("Last completed receipt reload is terminal scoped", LastCompletedReceiptReloadIsTerminalScoped),
                ("Customer return final residual reconciles", CustomerReturnFinalResidualReconciles),
                ("Full Stock Item return restores original batch", FullStockItemReturnRestoresOriginalBatch),
                ("Partial returns prevent over-return", PartialReturnsPreventOverReturn),
                ("Service return creates no inventory", ServiceReturnCreatesNoInventory),
                ("Mixed return reverses stock and service safely", MixedReturnReversesStockAndServiceSafely),
                ("Customer return reverses invoice discount", CustomerReturnReversesInvoiceDiscount),
                ("Customer return preserves all tax categories", CustomerReturnPreservesAllTaxCategories),
                ("Historical VAT return uses saved snapshot", HistoricalVatReturnUsesSavedSnapshot),
                ("Legacy return invents no VAT", LegacyReturnInventsNoVat),
                ("Invalid multi-line return changes nothing", InvalidMultiLineReturnChangesNothing),
                ("Return lookup reports remaining quantity", ReturnLookupReportsRemainingQuantity),
                ("Credit Note formatter uses saved return snapshots", CreditNoteFormatterUsesSavedReturnSnapshots),
                ("Supplier return final residual reconciles", SupplierReturnFinalResidualReconciles),
                ("Full supplier return deducts batch and supplier balance", FullSupplierReturnDeductsBatchAndLedger),
                ("Partial supplier returns prevent over-return", PartialSupplierReturnsPreventOverReturn),
                ("Supplier return blocks insufficient stock", SupplierReturnBlocksInsufficientStock),
                ("Supplier return preserves inclusive and exclusive snapshots", SupplierReturnPreservesInclusiveAndExclusiveSnapshots),
                ("Supplier return preserves all tax categories", SupplierReturnPreservesAllTaxCategories),
                ("Supplier return preserves historical VAT rate", SupplierReturnPreservesHistoricalVatRate),
                ("Legacy supplier return invents no VAT", LegacySupplierReturnInventsNoVat),
                ("Supplier credit excludes landed-cost freight", SupplierCreditExcludesLandedCostFreight),
                ("Invalid supplier return rolls back everything", InvalidSupplierReturnRollsBackEverything),
                ("Deactivated historical supplier return remains available", DeactivatedHistoricalSupplierReturnRemainsAvailable),
                ("Supplier debit note formatter uses saved snapshots", SupplierDebitNoteFormatterUsesSavedSnapshots),
                ("Supplier return lookup uses financial snapshots", SupplierReturnLookupUsesFinancialSnapshots),
                ("Prior legacy supplier return prevents invented VAT", PriorLegacySupplierReturnPreventsInventedVat),
                ("VAT report includes completed sales", VatReportIncludesCompletedSales),
                ("VAT report excludes voided sales", VatReportExcludesVoidedSales),
                ("Customer-return VAT is deducted", VatReportDeductsCustomerReturnVat),
                ("Posted GRN product VAT is included", VatReportIncludesPostedGrnVat),
                ("Cancelled GRNs are excluded", VatReportExcludesCancelledGrns),
                ("Posted supplier-return VAT is deducted", VatReportDeductsPostedSupplierReturnVat),
                ("Cancelled supplier returns are excluded", VatReportExcludesCancelledSupplierReturns),
                ("VAT report Standard totals are correct", VatReportStandardTotalsAreCorrect),
                ("VAT report Zero Rated totals are correct", VatReportZeroRatedTotalsAreCorrect),
                ("VAT report Exempt totals are correct", VatReportExemptTotalsAreCorrect),
                ("VAT report Out of Scope totals are correct", VatReportOutOfScopeTotalsAreCorrect),
                ("VAT-inclusive and exclusive GRNs aggregate", VatReportAggregatesInclusiveAndExclusiveGrns),
                ("Historical VAT rates remain separate", VatReportGroupsHistoricalRates),
                ("Current Tax Rate changes do not alter VAT reports", VatReportIgnoresCurrentTaxRateChanges),
                ("Non-VAT sales remain Out of Scope", VatReportKeepsNonVatSalesOutOfScope),
                ("Sales snapshot discrepancy is detected", VatReportDetectsSalesDiscrepancy),
                ("GRN snapshot discrepancy is detected", VatReportDetectsGrnDiscrepancy),
                ("Customer-return discrepancy is detected", VatReportDetectsCustomerReturnDiscrepancy),
                ("Supplier-return discrepancy is detected", VatReportDetectsSupplierReturnDiscrepancy),
                ("LegacyUnknown documents are separated", VatReportSeparatesLegacyUnknown),
                ("Unknown freight VAT is separated", VatReportSeparatesUnknownFreight),
                ("VAT date boundaries and position reconcile", VatReportDateBoundariesAndPositionReconcile),
                ("Active Stock Item cart saves and restores", ActiveStockItemCartSavesAndRestores),
                ("Service cart saves and restores", ServiceCartSavesAndRestores),
                ("Mixed cart saves and restores", MixedCartSavesAndRestores),
                ("Customer and Wholesale mode restore", CustomerAndWholesaleModeRestore),
                ("Batch and selected pricing restore", BatchAndSelectedPricingRestore),
                ("Manual line discount restores", ManualLineDiscountRestores),
                ("Invoice discount restores", InvoiceDiscountRestores),
                ("Price override audit restores", PriceOverrideAuditRestores),
                ("Gift Voucher and Free Issue state restores", GiftVoucherAndFreeIssueStateRestores),
                ("Cart persistence excludes payment drafts", CartPersistenceExcludesPaymentDrafts),
                ("Active cart survives context restart", ActiveCartSurvivesContextRestart),
                ("Suspend and Recall lifecycle works", SuspendAndRecallLifecycleWorks),
                ("Cart ownership is isolated", CartOwnershipIsIsolated),
                ("Held cart can be recalled only once", HeldCartCanBeRecalledOnlyOnce),
                ("Cancelled cart audits without financial posting", CancelledCartAuditsWithoutFinancialPosting),
                ("Completed and cancelled carts cannot be recalled", CompletedAndCancelledCartsCannotBeRecalled),
                ("Checkout token is idempotent", CheckoutTokenIsIdempotent),
                ("Duplicate checkout creates one stock and payment effect", DuplicateCheckoutCreatesOneStockAndPaymentEffect),
                ("Failed checkout leaves cart recoverable", FailedCheckoutLeavesCartRecoverable),
                ("Successful checkout completes cart", SuccessfulCheckoutCompletesCart),
                ("Manager approval preserves cashier session", ManagerApprovalPreservesCashierSession),
                ("Completed payment audit details persist", CompletedPaymentAuditDetailsPersist)
            };

            try
            {
                foreach (var test in tests)
                {
                    test.Run();
                    Console.WriteLine($"PASS: {test.Name}");
                }

                Console.WriteLine();
                Console.WriteLine($"All {tests.Length} purchasing, GRN pricing, sales VAT, repository, sales document, customer return, supplier return, VAT report, and cashier cart safety checks passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("FAILED:");
                Console.Error.WriteLine(ex.ToString());
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


        private static void CustomerReturnFinalResidualReconciles()
        {
            var calculator = new CustomerReturnAllocationCalculator();

            CustomerReturnAllocationResult first = calculator.Calculate(
                new CustomerReturnAllocationInput
                {
                    SoldQuantity = 3m,
                    PreviouslyReturnedQuantity = 0m,
                    RequestedQuantity = 1m,
                    OriginalGrossAmount = 1000m,
                    OriginalDiscountAmount = 100m,
                    OriginalRefundAmount = 900m,
                    PreviouslyRefundedAmount = 0m,
                    OriginalTaxableAmount = 762.71m,
                    OriginalVatAmount = 137.29m,
                    OriginalTaxInclusiveAmount = 900m,
                    PreviouslyReturnedTaxableAmount = 0m,
                    PreviouslyReturnedVatAmount = 0m,
                    PreviouslyReturnedTaxInclusiveAmount = 0m,
                    TaxSnapshotStatus = TaxSnapshotStatuses.Complete
                });

            CustomerReturnAllocationResult second = calculator.Calculate(
                new CustomerReturnAllocationInput
                {
                    SoldQuantity = 3m,
                    PreviouslyReturnedQuantity = 1m,
                    RequestedQuantity = 1m,
                    OriginalGrossAmount = 1000m,
                    OriginalDiscountAmount = 100m,
                    OriginalRefundAmount = 900m,
                    PreviouslyRefundedAmount = first.RefundAmount,
                    OriginalTaxableAmount = 762.71m,
                    OriginalVatAmount = 137.29m,
                    OriginalTaxInclusiveAmount = 900m,
                    PreviouslyReturnedTaxableAmount = first.TaxableAmount ?? 0m,
                    PreviouslyReturnedVatAmount = first.VatAmount ?? 0m,
                    PreviouslyReturnedTaxInclusiveAmount = first.TaxInclusiveAmount ?? 0m,
                    TaxSnapshotStatus = TaxSnapshotStatuses.Complete
                });

            CustomerReturnAllocationResult final = calculator.Calculate(
                new CustomerReturnAllocationInput
                {
                    SoldQuantity = 3m,
                    PreviouslyReturnedQuantity = 2m,
                    RequestedQuantity = 1m,
                    OriginalGrossAmount = 1000m,
                    OriginalDiscountAmount = 100m,
                    OriginalRefundAmount = 900m,
                    PreviouslyRefundedAmount = first.RefundAmount + second.RefundAmount,
                    OriginalTaxableAmount = 762.71m,
                    OriginalVatAmount = 137.29m,
                    OriginalTaxInclusiveAmount = 900m,
                    PreviouslyReturnedTaxableAmount = (first.TaxableAmount ?? 0m) + (second.TaxableAmount ?? 0m),
                    PreviouslyReturnedVatAmount = (first.VatAmount ?? 0m) + (second.VatAmount ?? 0m),
                    PreviouslyReturnedTaxInclusiveAmount = (first.TaxInclusiveAmount ?? 0m) + (second.TaxInclusiveAmount ?? 0m),
                    TaxSnapshotStatus = TaxSnapshotStatuses.Complete
                });

            AssertMoney(900m, first.RefundAmount + second.RefundAmount + final.RefundAmount, "return refund residual");
            AssertMoney(762.71m, (first.TaxableAmount ?? 0m) + (second.TaxableAmount ?? 0m) + (final.TaxableAmount ?? 0m), "return taxable residual");
            AssertMoney(137.29m, (first.VatAmount ?? 0m) + (second.VatAmount ?? 0m) + (final.VatAmount ?? 0m), "return VAT residual");
        }

        private static void FullStockItemReturnRestoresOriginalBatch()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SalesHeader sale = CreateReturnTestSale(factory, scenario, stockQuantity: 2m, serviceQuantity: 0m);
            var repository = CreateCustomerReturnRepository(factory);

            CustomerReturnProcessResult result = repository.ProcessReturnAsync(
                    CreateReturnRequest(sale, scenario, (sale.SalesLines.Single().Id, 2m)))
                .GetAwaiter().GetResult();

            using AppDbContext context = factory.CreateDbContext();
            ItemBatch batch = context.ItemBatches.Single(row => row.Id == scenario.StockBatchId);
            SalesLine source = context.SalesLines.Single(row => row.Id == sale.SalesLines.Single().Id);

            AssertMoney(5m, batch.CurrentStock, "restored original batch stock");
            AssertMoney(2360m, result.TotalRefundAmount, "full Stock Item refund");

            if (!source.IsReturned ||
                context.InventoryTransactions.Count(row => row.TransactionType == "RETURN") != 1 ||
                context.CashMovements.Count(row => row.ReasonCategory == CustomerReturnCashMovementCodes.ReasonCategory) != 1)
            {
                throw new InvalidOperationException("Full Stock Item return did not persist stock, cash, and completion state.");
            }
        }

        private static void PartialReturnsPreventOverReturn()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SalesHeader sale = CreateReturnTestSale(factory, scenario, stockQuantity: 3m, serviceQuantity: 0m);
            SalesLine line = sale.SalesLines.Single();
            var repository = CreateCustomerReturnRepository(factory);

            repository.ProcessReturnAsync(CreateReturnRequest(sale, scenario, (line.Id, 1m))).GetAwaiter().GetResult();
            repository.ProcessReturnAsync(CreateReturnRequest(sale, scenario, (line.Id, 1m))).GetAwaiter().GetResult();

            AssertThrows(
                () => repository.ProcessReturnAsync(CreateReturnRequest(sale, scenario, (line.Id, 2m))).GetAwaiter().GetResult(),
                "remaining quantity");

            repository.ProcessReturnAsync(CreateReturnRequest(sale, scenario, (line.Id, 1m))).GetAwaiter().GetResult();

            using AppDbContext context = factory.CreateDbContext();
            decimal returned = context.CustomerReturnLines.Where(row => row.SalesLineId == line.Id).AsEnumerable().Sum(row => row.QuantityReturned);
            decimal refunded = context.CustomerReturnLines.Where(row => row.SalesLineId == line.Id).AsEnumerable().Sum(row => row.LineTotalRefund);

            AssertMoney(3m, returned, "partial returned quantity");
            AssertMoney(3540m, refunded, "partial refund reconciliation");
        }

        private static void ServiceReturnCreatesNoInventory()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SalesHeader sale = CreateReturnTestSale(factory, scenario, stockQuantity: 0m, serviceQuantity: 2m);
            SalesLine serviceLine = sale.SalesLines.Single();
            var repository = CreateCustomerReturnRepository(factory);

            CustomerReturnProcessResult result = repository.ProcessReturnAsync(
                    CreateReturnRequest(sale, scenario, (serviceLine.Id, 1m)))
                .GetAwaiter().GetResult();

            using AppDbContext context = factory.CreateDbContext();
            CustomerReturnLine saved = context.CustomerReturnLines.Single();

            if (saved.ItemBatchId.HasValue ||
                saved.InventoryAction != CustomerReturnInventoryActions.NoInventory ||
                context.InventoryTransactions.Any(row => row.TransactionType == "RETURN"))
            {
                throw new InvalidOperationException("Service return incorrectly created inventory activity.");
            }

            AssertMoney(1180m, result.TotalRefundAmount, "Service refund");
        }

        private static void MixedReturnReversesStockAndServiceSafely()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SalesHeader sale = CreateReturnTestSale(factory, scenario, stockQuantity: 1m, serviceQuantity: 1m);
            SalesLine stock = sale.SalesLines.Single(row => row.ItemBatchId.HasValue);
            SalesLine service = sale.SalesLines.Single(row => !row.ItemBatchId.HasValue);
            var repository = CreateCustomerReturnRepository(factory);

            CustomerReturnProcessResult result = repository.ProcessReturnAsync(
                    CreateReturnRequest(sale, scenario, (stock.Id, 1m), (service.Id, 1m)))
                .GetAwaiter().GetResult();

            using AppDbContext context = factory.CreateDbContext();
            AssertMoney(5m, context.ItemBatches.Single(row => row.Id == scenario.StockBatchId).CurrentStock, "mixed return batch stock");
            AssertMoney(2360m, result.TotalRefundAmount, "mixed return refund");

            if (context.CustomerReturnLines.Count() != 2 ||
                context.InventoryTransactions.Count(row => row.TransactionType == "RETURN") != 1)
            {
                throw new InvalidOperationException("Mixed Stock Item and Service return created incorrect inventory rows.");
            }
        }

        private static void CustomerReturnReversesInvoiceDiscount()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SalesHeader sale = CreateReturnTestSale(factory, scenario, stockQuantity: 1m, serviceQuantity: 1m, invoiceDiscount: 180m);
            SalesLine stock = sale.SalesLines.Single(row => row.ItemBatchId.HasValue);
            var repository = CreateCustomerReturnRepository(factory);

            CustomerReturnProcessResult result = repository.ProcessReturnAsync(
                    CreateReturnRequest(sale, scenario, (stock.Id, 1m)))
                .GetAwaiter().GetResult();

            AssertMoney(stock.LineTotal, result.TotalRefundAmount, "invoice-discount return amount");

            using AppDbContext context = factory.CreateDbContext();
            CustomerReturnLine returned = context.CustomerReturnLines.Single();
            AssertMoney(stock.TaxableAmountSnapshot ?? 0m, returned.TaxableAmountSnapshot ?? 0m, "invoice-discount returned taxable");
            AssertMoney(stock.VatAmountSnapshot ?? 0m, returned.VatAmountSnapshot ?? 0m, "invoice-discount returned VAT");
        }

        private static void CustomerReturnPreservesAllTaxCategories()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SalesHeader sale = CreateFourCategoryReturnSale(factory, scenario);
            var repository = CreateCustomerReturnRepository(factory);

            CustomerReturnProcessResult result = repository.ProcessReturnAsync(
                    CreateReturnRequest(
                        sale,
                        scenario,
                        sale.SalesLines.Select(line => (line.Id, 1m)).ToArray()))
                .GetAwaiter().GetResult();

            CustomerReturnHeader header = result.ReturnHeader;
            AssertMoney(1000m, header.StandardRatedAmount ?? 0m, "return Standard VAT total");
            AssertMoney(100m, header.ZeroRatedAmount ?? 0m, "return Zero Rated total");
            AssertMoney(200m, header.ExemptAmount ?? 0m, "return Exempt total");
            AssertMoney(300m, header.OutOfScopeAmount ?? 0m, "return Out of Scope total");
            AssertMoney(180m, header.TotalVatAmount ?? 0m, "return VAT total");
        }

        private static void HistoricalVatReturnUsesSavedSnapshot()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SalesHeader sale = CreateReturnTestSale(factory, scenario, stockQuantity: 0m, serviceQuantity: 1m);

            using (AppDbContext context = factory.CreateDbContext())
            {
                TaxRate rate = context.TaxRates.Single();
                rate.RatePercent = 99m;
                context.SaveChanges();
            }

            var repository = CreateCustomerReturnRepository(factory);
            CustomerReturnProcessResult result = repository.ProcessReturnAsync(
                    CreateReturnRequest(sale, scenario, (sale.SalesLines.Single().Id, 1m)))
                .GetAwaiter().GetResult();

            AssertMoney(180m, result.ReturnHeader.TotalVatAmount ?? 0m, "historical VAT reversal");
        }

        private static void LegacyReturnInventsNoVat()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SalesHeader sale = CreateLegacyReturnSale(factory, scenario);
            var repository = CreateCustomerReturnRepository(factory);

            CustomerReturnProcessResult result = repository.ProcessReturnAsync(
                    CreateReturnRequest(sale, scenario, (sale.SalesLines.Single().Id, 1m)))
                .GetAwaiter().GetResult();

            if (result.ReturnHeader.TaxSnapshotStatus != TaxSnapshotStatuses.LegacyUnknown ||
                result.ReturnHeader.TotalVatAmount.HasValue ||
                result.ReturnHeader.Lines.Single().VatAmountSnapshot.HasValue)
            {
                throw new InvalidOperationException("Legacy customer return invented VAT values.");
            }

            AssertMoney(500m, result.TotalRefundAmount, "legacy financial refund");
        }

        private static void InvalidMultiLineReturnChangesNothing()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SalesHeader sale = CreateReturnTestSale(factory, scenario, stockQuantity: 1m, serviceQuantity: 1m);
            SalesLine valid = sale.SalesLines.First();
            var repository = CreateCustomerReturnRepository(factory);

            using (AppDbContext before = factory.CreateDbContext())
            {
                AssertMoney(4m, before.ItemBatches.Single(row => row.Id == scenario.StockBatchId).CurrentStock, "pre-failure stock");
            }

            AssertThrows(
                () => repository.ProcessReturnAsync(
                        CreateReturnRequest(sale, scenario, (valid.Id, 1m), (999999, 1m)))
                    .GetAwaiter().GetResult(),
                "do not belong");

            using AppDbContext after = factory.CreateDbContext();
            AssertMoney(4m, after.ItemBatches.Single(row => row.Id == scenario.StockBatchId).CurrentStock, "post-failure stock");

            if (after.CustomerReturnHeaders.Any() ||
                after.CustomerReturnLines.Any() ||
                after.CashMovements.Any(row => row.ReasonCategory == CustomerReturnCashMovementCodes.ReasonCategory))
            {
                throw new InvalidOperationException("Invalid multi-line return left partial records.");
            }
        }

        private static void ReturnLookupReportsRemainingQuantity()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SalesHeader sale = CreateReturnTestSale(factory, scenario, stockQuantity: 2m, serviceQuantity: 0m);
            SalesLine line = sale.SalesLines.Single();
            var repository = CreateCustomerReturnRepository(factory);

            repository.ProcessReturnAsync(CreateReturnRequest(sale, scenario, (line.Id, 1m))).GetAwaiter().GetResult();

            CustomerReturnInvoiceDto invoice = repository.FindCompletedSaleAsync(sale.InvoiceNo).GetAwaiter().GetResult()
                ?? throw new InvalidOperationException("Return lookup failed.");

            CustomerReturnableLineDto loaded = invoice.Lines.Single();
            AssertMoney(1m, loaded.PreviouslyReturnedQuantity, "lookup previous returned quantity");
            AssertMoney(1m, loaded.RemainingQuantity, "lookup remaining quantity");
        }

        private static void CreditNoteFormatterUsesSavedReturnSnapshots()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            SalesHeader sale = CreateReturnTestSale(factory, scenario, stockQuantity: 0m, serviceQuantity: 1m);
            var repository = CreateCustomerReturnRepository(factory);
            CustomerReturnHeader returned = repository.ProcessReturnAsync(
                    CreateReturnRequest(sale, scenario, (sale.SalesLines.Single().Id, 1m)))
                .GetAwaiter().GetResult().ReturnHeader;

            var formatter = new CustomerCreditNoteTextFormatter();
            string document = formatter.FormatCreditNote(returned, CreateFormatterStoreSettings(), 80);

            AssertContains(document, "CREDIT NOTE", "credit note title");
            AssertContains(document, returned.ReturnNo, "credit note number");
            AssertContains(document, sale.InvoiceNo, "original invoice reference");
            AssertContains(document, "VAT reversed", "credit note VAT summary");
            AssertContains(document, "Installation Service", "credit note Service line");
        }


        private static void SupplierReturnFinalResidualReconciles()
        {
            var calculator = new SupplierReturnAllocationCalculator();

            SupplierReturnAllocationResult first = calculator.Calculate(
                new SupplierReturnAllocationInput
                {
                    OriginalQuantity = 3m,
                    PreviouslyReturnedQuantity = 0m,
                    RequestedQuantity = 1m,
                    OriginalCreditAmount = 3540m,
                    PreviouslyReturnedCreditAmount = 0m,
                    OriginalTaxableAmount = 3000m,
                    OriginalVatAmount = 540m,
                    OriginalTaxInclusiveAmount = 3540m,
                    TaxSnapshotStatus = TaxSnapshotStatuses.Complete
                });

            SupplierReturnAllocationResult second = calculator.Calculate(
                new SupplierReturnAllocationInput
                {
                    OriginalQuantity = 3m,
                    PreviouslyReturnedQuantity = 1m,
                    RequestedQuantity = 1m,
                    OriginalCreditAmount = 3540m,
                    PreviouslyReturnedCreditAmount = first.CreditAmount,
                    OriginalTaxableAmount = 3000m,
                    OriginalVatAmount = 540m,
                    OriginalTaxInclusiveAmount = 3540m,
                    PreviouslyReturnedTaxableAmount = first.TaxableAmount ?? 0m,
                    PreviouslyReturnedVatAmount = first.VatAmount ?? 0m,
                    PreviouslyReturnedTaxInclusiveAmount = first.TaxInclusiveAmount ?? 0m,
                    TaxSnapshotStatus = TaxSnapshotStatuses.Complete
                });

            SupplierReturnAllocationResult final = calculator.Calculate(
                new SupplierReturnAllocationInput
                {
                    OriginalQuantity = 3m,
                    PreviouslyReturnedQuantity = 2m,
                    RequestedQuantity = 1m,
                    OriginalCreditAmount = 3540m,
                    PreviouslyReturnedCreditAmount = first.CreditAmount + second.CreditAmount,
                    OriginalTaxableAmount = 3000m,
                    OriginalVatAmount = 540m,
                    OriginalTaxInclusiveAmount = 3540m,
                    PreviouslyReturnedTaxableAmount = (first.TaxableAmount ?? 0m) + (second.TaxableAmount ?? 0m),
                    PreviouslyReturnedVatAmount = (first.VatAmount ?? 0m) + (second.VatAmount ?? 0m),
                    PreviouslyReturnedTaxInclusiveAmount = (first.TaxInclusiveAmount ?? 0m) + (second.TaxInclusiveAmount ?? 0m),
                    TaxSnapshotStatus = TaxSnapshotStatuses.Complete
                });

            AssertMoney(3540m, first.CreditAmount + second.CreditAmount + final.CreditAmount, "supplier credit residual");
            AssertMoney(3000m, (first.TaxableAmount ?? 0m) + (second.TaxableAmount ?? 0m) + (final.TaxableAmount ?? 0m), "supplier taxable residual");
            AssertMoney(540m, (first.VatAmount ?? 0m) + (second.VatAmount ?? 0m) + (final.VatAmount ?? 0m), "supplier VAT residual");
        }

        private static void FullSupplierReturnDeductsBatchAndLedger()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SupplierReturnTestScenario scenario = SeedSupplierReturnTestScenario(factory);
            SupplierReturnPostResult result = PostSupplierReturn(
                factory,
                scenario,
                scenario.MainGrnId,
                "return.manager",
                (scenario.StandardLineId, 3m));

            using AppDbContext context = factory.CreateDbContext();
            ItemBatch batch = context.ItemBatches.Single(row => row.Id == scenario.StandardBatchId);
            Supplier supplier = context.Suppliers.Single(row => row.Id == scenario.SupplierId);
            InventoryTransaction inventory = context.InventoryTransactions.Single(row => row.TransactionType == SupplierReturnCodes.InventoryTransactionType);
            SupplierLedger ledger = context.SupplierLedgers.Single(row => row.TransactionType == SupplierReturnCodes.DebitNoteTransactionType);

            AssertMoney(7m, batch.CurrentStock, "supplier return batch stock");
            AssertMoney(3540m, result.ReturnHeader.NetCredit, "supplier return credit");
            AssertMoney(6460m, supplier.CurrentBalance, "supplier balance after debit note");
            AssertMoney(-3m, inventory.Quantity, "supplier return inventory quantity");
            AssertMoney(900m, inventory.UnitCost, "supplier return inventory landed cost");
            AssertMoney(3540m, ledger.PaymentAmount, "supplier ledger debit note amount");

            if (!result.ReturnHeader.ReturnNumber.StartsWith("SDN-", StringComparison.Ordinal) ||
                ledger.CreatedBy != "return.manager")
            {
                throw new InvalidOperationException("Supplier return document numbering or user audit is incorrect.");
            }
        }

        private static void PartialSupplierReturnsPreventOverReturn()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SupplierReturnTestScenario scenario = SeedSupplierReturnTestScenario(factory);

            PostSupplierReturn(factory, scenario, scenario.MainGrnId, "manager", (scenario.StandardLineId, 1m));
            PostSupplierReturn(factory, scenario, scenario.MainGrnId, "manager", (scenario.StandardLineId, 1m));

            AssertThrows(
                () => PostSupplierReturn(factory, scenario, scenario.MainGrnId, "manager", (scenario.StandardLineId, 2m)),
                "Remaining returnable GRN quantity");

            PostSupplierReturn(factory, scenario, scenario.MainGrnId, "manager", (scenario.StandardLineId, 1m));

            using AppDbContext context = factory.CreateDbContext();
            List<SupplierReturnLine> rows = context.SupplierReturnLines.Where(row => row.GrnLineId == scenario.StandardLineId).ToList();
            AssertMoney(3m, rows.Sum(row => row.ReturnQty), "supplier returned quantity");
            AssertMoney(3540m, rows.Sum(row => row.CreditValue), "supplier returned credit");
            AssertMoney(540m, rows.Sum(row => row.VatAmountSnapshot ?? 0m), "supplier returned VAT");
        }

        private static void SupplierReturnBlocksInsufficientStock()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SupplierReturnTestScenario scenario = SeedSupplierReturnTestScenario(factory);

            using (AppDbContext context = factory.CreateDbContext())
            {
                context.ItemBatches.Single(row => row.Id == scenario.StandardBatchId).CurrentStock = 0.5m;
                context.SaveChanges();
            }

            AssertThrows(
                () => PostSupplierReturn(factory, scenario, scenario.MainGrnId, "manager", (scenario.StandardLineId, 1m)),
                "Current stock is only");

            using AppDbContext after = factory.CreateDbContext();
            if (after.SupplierReturnHeaders.Any() || after.InventoryTransactions.Any(row => row.TransactionType == SupplierReturnCodes.InventoryTransactionType))
                throw new InvalidOperationException("Insufficient-stock supplier return persisted partial data.");
        }

        private static void SupplierReturnPreservesInclusiveAndExclusiveSnapshots()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SupplierReturnTestScenario scenario = SeedSupplierReturnTestScenario(factory);

            SupplierReturnPostResult inclusive = PostSupplierReturn(
                factory, scenario, scenario.MainGrnId, "manager", (scenario.StandardLineId, 1m));
            SupplierReturnPostResult exclusive = PostSupplierReturn(
                factory, scenario, scenario.ExclusiveGrnId, "manager", (scenario.ExclusiveLineId, 1m));

            using AppDbContext context = factory.CreateDbContext();
            SupplierReturnLine inclusiveLine = context.SupplierReturnLines.Single(row => row.ReturnHeaderId == inclusive.ReturnHeader.Id);
            SupplierReturnLine exclusiveLine = context.SupplierReturnLines.Single(row => row.ReturnHeaderId == exclusive.ReturnHeader.Id);

            if (inclusiveLine.IsTaxInclusiveSnapshot != true || exclusiveLine.IsTaxInclusiveSnapshot != false)
                throw new InvalidOperationException("Supplier return did not preserve original VAT entry mode.");

            AssertMoney(1180m, inclusiveLine.CreditValue, "inclusive supplier return credit");
            AssertMoney(1180m, exclusiveLine.CreditValue, "exclusive supplier return credit");
            AssertMoney(1000m, exclusiveLine.TaxableAmountSnapshot ?? 0m, "exclusive supplier return taxable");
            AssertMoney(180m, exclusiveLine.VatAmountSnapshot ?? 0m, "exclusive supplier return VAT");
        }

        private static void SupplierReturnPreservesAllTaxCategories()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SupplierReturnTestScenario scenario = SeedSupplierReturnTestScenario(factory);

            SupplierReturnPostResult result = PostSupplierReturn(
                factory,
                scenario,
                scenario.MainGrnId,
                "manager",
                (scenario.StandardLineId, 1m),
                (scenario.ZeroLineId, 1m),
                (scenario.ExemptLineId, 1m),
                (scenario.OutOfScopeLineId, 1m));

            SupplierReturnHeader header = result.ReturnHeader;
            AssertMoney(1000m, header.StandardRatedAmount ?? 0m, "supplier Standard VAT amount");
            AssertMoney(200m, header.ZeroRatedAmount ?? 0m, "supplier Zero Rated amount");
            AssertMoney(300m, header.ExemptAmount ?? 0m, "supplier Exempt amount");
            AssertMoney(500m, header.OutOfScopeAmount ?? 0m, "supplier Out of Scope amount");
            AssertMoney(180m, header.TotalVatAmount ?? 0m, "supplier VAT reversal total");
        }

        private static void SupplierReturnPreservesHistoricalVatRate()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SupplierReturnTestScenario scenario = SeedSupplierReturnTestScenario(factory);

            using (AppDbContext setupContext = factory.CreateDbContext())
            {
                TaxRate rate = setupContext.TaxRates.Single(row => row.Id == scenario.StandardTaxRateId);
                rate.RatePercent = 25m;
                setupContext.SaveChanges();
            }

            SupplierReturnPostResult result = PostSupplierReturn(
                factory, scenario, scenario.MainGrnId, "manager", (scenario.StandardLineId, 1m));

            using AppDbContext context = factory.CreateDbContext();
            SupplierReturnLine saved = context.SupplierReturnLines.Single(row => row.ReturnHeaderId == result.ReturnHeader.Id);
            AssertMoney(18m, saved.TaxRatePercentSnapshot ?? 0m, "historical supplier VAT rate");
            AssertMoney(180m, saved.VatAmountSnapshot ?? 0m, "historical supplier VAT amount");
        }

        private static void LegacySupplierReturnInventsNoVat()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SupplierReturnTestScenario scenario = SeedSupplierReturnTestScenario(factory);

            SupplierReturnPostResult result = PostSupplierReturn(
                factory, scenario, scenario.LegacyGrnId, "manager", (scenario.LegacyLineId, 1m));

            using AppDbContext context = factory.CreateDbContext();
            SupplierReturnLine saved = context.SupplierReturnLines.Single(row => row.ReturnHeaderId == result.ReturnHeader.Id);

            if (result.ReturnHeader.TaxSnapshotStatus != TaxSnapshotStatuses.LegacyUnknown ||
                result.ReturnHeader.TotalVatAmount.HasValue ||
                saved.VatAmountSnapshot.HasValue ||
                saved.TaxableAmountSnapshot.HasValue)
            {
                throw new InvalidOperationException("Legacy supplier return invented VAT values.");
            }

            AssertMoney(700m, saved.CreditValue, "legacy supplier credit");
        }

        private static void SupplierCreditExcludesLandedCostFreight()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SupplierReturnTestScenario scenario = SeedSupplierReturnTestScenario(factory);

            using AppDbContext before = factory.CreateDbContext();
            ItemVariant variantBefore = before.ItemVariants.Single(row => row.Id == scenario.StandardVariantId);
            decimal retailBefore = variantBefore.RetailPrice;
            decimal wholesaleBefore = variantBefore.WholesalePrice;

            SupplierReturnPostResult result = PostSupplierReturn(
                factory, scenario, scenario.MainGrnId, "manager", (scenario.StandardLineId, 1m));

            using AppDbContext after = factory.CreateDbContext();
            SupplierReturnLine saved = after.SupplierReturnLines.Single(row => row.ReturnHeaderId == result.ReturnHeader.Id);
            InventoryTransaction inventory = after.InventoryTransactions.Single(row => row.ReferenceDocument == result.ReturnHeader.ReturnNumber);
            ItemVariant variantAfter = after.ItemVariants.Single(row => row.Id == scenario.StandardVariantId);
            GrnLine source = after.GrnLines.Single(row => row.Id == scenario.StandardLineId);

            AssertMoney(1180m, saved.CreditValue, "supplier product payable credit");
            AssertMoney(900m, saved.HistoricalCost, "supplier inventory landed cost");
            AssertMoney(900m, inventory.UnitCost, "supplier inventory transaction cost");
            AssertMoney(900m, source.LandedCost, "original GRN landed cost unchanged");
            AssertMoney(retailBefore, variantAfter.RetailPrice, "retail price unchanged");
            AssertMoney(wholesaleBefore, variantAfter.WholesalePrice, "wholesale price unchanged");
        }

        private static void InvalidSupplierReturnRollsBackEverything()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SupplierReturnTestScenario scenario = SeedSupplierReturnTestScenario(factory);

            using AppDbContext before = factory.CreateDbContext();
            decimal stockBefore = before.ItemBatches.Single(row => row.Id == scenario.StandardBatchId).CurrentStock;
            decimal balanceBefore = before.Suppliers.Single(row => row.Id == scenario.SupplierId).CurrentBalance;

            AssertThrows(
                () => PostSupplierReturn(
                    factory,
                    scenario,
                    scenario.MainGrnId,
                    "manager",
                    (scenario.StandardLineId, 1m),
                    (scenario.ZeroLineId, 999m)),
                "Current stock is only");

            using AppDbContext after = factory.CreateDbContext();
            AssertMoney(stockBefore, after.ItemBatches.Single(row => row.Id == scenario.StandardBatchId).CurrentStock, "rollback batch stock");
            AssertMoney(balanceBefore, after.Suppliers.Single(row => row.Id == scenario.SupplierId).CurrentBalance, "rollback supplier balance");

            if (after.SupplierReturnHeaders.Any() ||
                after.SupplierReturnLines.Any() ||
                after.SupplierLedgers.Any(row => row.TransactionType == SupplierReturnCodes.DebitNoteTransactionType) ||
                after.InventoryTransactions.Any(row => row.TransactionType == SupplierReturnCodes.InventoryTransactionType))
            {
                throw new InvalidOperationException("Invalid supplier return persisted partial financial or inventory data.");
            }
        }

        private static void DeactivatedHistoricalSupplierReturnRemainsAvailable()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SupplierReturnTestScenario scenario = SeedSupplierReturnTestScenario(factory);

            using (AppDbContext context = factory.CreateDbContext())
            {
                context.Suppliers.Single(row => row.Id == scenario.SupplierId).IsDeactivated = true;
                context.ItemParents.Single(row => row.Id == scenario.StandardParentId).IsDeactivated = true;
                context.ItemVariants.Single(row => row.Id == scenario.StandardVariantId).IsDeactivated = true;
                context.ItemBatches.Single(row => row.Id == scenario.StandardBatchId).IsDeactivated = true;
                context.SaveChanges();
            }

            var repository = CreateSupplierReturnRepository(factory);
            List<SupplierLookupDto> suppliers = repository.GetActiveSuppliersAsync().GetAwaiter().GetResult();
            List<SupplierReturnSourceDto> rows = repository.GetReturnableBatchesForGrnAsync(scenario.MainGrnId).GetAwaiter().GetResult();

            if (suppliers.All(row => row.Id != scenario.SupplierId) || rows.All(row => row.GrnLineId != scenario.StandardLineId))
                throw new InvalidOperationException("Historical deactivation incorrectly hid a valid supplier return source.");

            SupplierReturnPostResult result = PostSupplierReturn(
                factory, scenario, scenario.MainGrnId, "audit.user", (scenario.StandardLineId, 1m));

            using AppDbContext after = factory.CreateDbContext();
            SupplierLedger ledger = after.SupplierLedgers.Single(row => row.ReferenceDocument == result.ReturnHeader.ReturnNumber);
            InventoryTransaction inventory = after.InventoryTransactions.Single(row => row.ReferenceDocument == result.ReturnHeader.ReturnNumber);

            if (result.ReturnHeader.AuthorizedBy != "audit.user" ||
                result.ReturnHeader.CreatedBy != "audit.user" ||
                ledger.CreatedBy != "audit.user" ||
                inventory.CreatedBy != "audit.user")
            {
                throw new InvalidOperationException("Supplier return authenticated user audit was not preserved.");
            }
        }

        private static void SupplierDebitNoteFormatterUsesSavedSnapshots()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SupplierReturnTestScenario scenario = SeedSupplierReturnTestScenario(factory);
            SupplierReturnPostResult result = PostSupplierReturn(
                factory, scenario, scenario.MainGrnId, "manager", (scenario.StandardLineId, 1m));

            var formatter = new SupplierDebitNoteTextFormatter();
            string document = formatter.Format(result.DebitNote, 80);

            AssertContains(document, "SUPPLIER DEBIT NOTE", "supplier debit note title");
            AssertContains(document, result.ReturnHeader.ReturnNumber, "supplier debit note number");
            AssertContains(document, "GRN-SR-MAIN", "supplier debit note GRN reference");
            AssertContains(document, "18%", "supplier debit note saved VAT rate");
            AssertContains(document, "Rs. 1,180.00", "supplier debit note saved credit");
            AssertContains(document, "Freight and restocking fees are not credited", "supplier debit note freight rule");
        }

        private static void SupplierReturnLookupUsesFinancialSnapshots()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SupplierReturnTestScenario scenario = SeedSupplierReturnTestScenario(factory);
            var repository = CreateSupplierReturnRepository(factory);

            SupplierReturnSourceDto source = repository.GetReturnableBatchesForGrnAsync(scenario.MainGrnId)
                .GetAwaiter()
                .GetResult()
                .Single(row => row.GrnLineId == scenario.StandardLineId);

            AssertMoney(3540m, source.OriginalCreditAmount, "lookup original supplier credit");
            AssertMoney(3540m, source.MaxReturnCredit, "lookup maximum supplier credit");
            AssertMoney(900m, source.HistoricalCost, "lookup landed cost");

            if (source.TaxSnapshotStatus != TaxSnapshotStatuses.Complete || source.TaxCategoryCode != TaxCategoryCodes.Standard)
                throw new InvalidOperationException("Supplier return lookup did not expose saved tax snapshot identity.");
        }

        private static void PriorLegacySupplierReturnPreventsInventedVat()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SupplierReturnTestScenario scenario = SeedSupplierReturnTestScenario(factory);

            using (AppDbContext context = factory.CreateDbContext())
            {
                var legacyHeader = new SupplierReturnHeader
                {
                    ReturnNumber = "SDN-LEGACY-PRIOR",
                    SupplierId = scenario.SupplierId,
                    GrnHeaderId = scenario.MainGrnId,
                    OriginalInvoiceNo = "SUP-INV-MAIN",
                    ReturnDate = new DateTime(2026, 7, 11),
                    AuthorizedBy = "legacy.user",
                    GrossCredit = 900m,
                    NetCredit = 900m,
                    TaxSnapshotStatus = TaxSnapshotStatuses.LegacyUnknown,
                    Status = SupplierReturnCodes.PostedStatus,
                    CreatedBy = "legacy.user",
                    PostedBy = "legacy.user",
                    PostedAt = new DateTime(2026, 7, 11)
                };

                context.SupplierReturnHeaders.Add(legacyHeader);
                context.SaveChanges();

                context.SupplierReturnLines.Add(new SupplierReturnLine
                {
                    ReturnHeaderId = legacyHeader.Id,
                    GrnLineId = scenario.StandardLineId,
                    ItemVariantId = scenario.StandardVariantId,
                    ItemBatchId = scenario.StandardBatchId,
                    BatchNo = "SR-BATCH-1",
                    ReturnQty = 1m,
                    HistoricalCost = 900m,
                    CreditValue = 900m,
                    ReasonCode = "Legacy Return",
                    LineStatus = SupplierReturnCodes.PostedStatus,
                    TaxSnapshotStatus = TaxSnapshotStatuses.LegacyUnknown
                });
                context.SaveChanges();
            }

            SupplierReturnPostResult result = PostSupplierReturn(
                factory,
                scenario,
                scenario.MainGrnId,
                "manager",
                (scenario.StandardLineId, 1m));

            using AppDbContext after = factory.CreateDbContext();
            SupplierReturnLine line = after.SupplierReturnLines
                .Single(row => row.ReturnHeaderId == result.ReturnHeader.Id);

            if (line.TaxSnapshotStatus != TaxSnapshotStatuses.LegacyUnknown ||
                line.TaxableAmountSnapshot.HasValue ||
                line.VatAmountSnapshot.HasValue ||
                line.TaxInclusiveAmountSnapshot.HasValue)
            {
                throw new InvalidOperationException("A later supplier return invented VAT after a legacy prior return.");
            }
        }

        private static SupplierReturnRepository CreateSupplierReturnRepository(
            RepositoryTestDbContextFactory factory)
        {
            return new SupplierReturnRepository(
                factory,
                new SupplierReturnAllocationCalculator());
        }

        private static SupplierReturnPostResult PostSupplierReturn(
            RepositoryTestDbContextFactory factory,
            SupplierReturnTestScenario scenario,
            int grnHeaderId,
            string authorizedBy,
            params (int GrnLineId, decimal Quantity)[] requestedLines)
        {
            using AppDbContext context = factory.CreateDbContext();
            int[] requestedIds = requestedLines
                .Select(request => request.GrnLineId)
                .Distinct()
                .ToArray();

            Dictionary<int, GrnLine> sources = context.GrnLines
                .Where(row => requestedIds.Contains(row.Id))
                .ToDictionary(row => row.Id);

            var header = new SupplierReturnHeader
            {
                SupplierId = scenario.SupplierId,
                GrnHeaderId = grnHeaderId,
                ReturnDate = new DateTime(2026, 7, 12, 12, 0, 0),
                AuthorizedBy = authorizedBy,
                CreatedBy = authorizedBy,
                PostedBy = authorizedBy,
                Remarks = "Repository supplier return test"
            };

            List<SupplierReturnLine> lines = requestedLines.Select(request =>
            {
                GrnLine source = sources[request.GrnLineId];
                return new SupplierReturnLine
                {
                    GrnLineId = source.Id,
                    ItemVariantId = source.ItemVariantId,
                    ItemBatchId = source.ItemBatchId ?? 0,
                    ReturnQty = request.Quantity,
                    ReasonCode = "Supplier Recall",
                    LineRemarks = "Test"
                };
            }).ToList();

            return CreateSupplierReturnRepository(factory)
                .PostSupplierReturnAsync(header, lines)
                .GetAwaiter()
                .GetResult();
        }

        private static SupplierReturnTestScenario SeedSupplierReturnTestScenario(
            RepositoryTestDbContextFactory factory)
        {
            using AppDbContext context = factory.CreateDbContext();

            var category = new Category
            {
                CategoryCode = "SUP-RET",
                CategoryName = "Supplier Return Tests",
                CreatedBy = "Test",
                UpdatedBy = "Test"
            };

            UnitOfMeasure uom = context.UnitsOfMeasure
                .Single(row => row.UomCode == "PCS");

            var standardTax = CreateSupplierReturnTaxCategory(TaxCategoryCodes.Standard, "Standard VAT", TaxTreatmentTypes.StandardRated, true, 1);
            var zeroTax = CreateSupplierReturnTaxCategory(TaxCategoryCodes.ZeroRated, "Zero Rated", TaxTreatmentTypes.ZeroRated, false, 2);
            var exemptTax = CreateSupplierReturnTaxCategory(TaxCategoryCodes.Exempt, "Exempt", TaxTreatmentTypes.Exempt, false, 3);
            var outTax = CreateSupplierReturnTaxCategory(TaxCategoryCodes.OutOfScope, "Out of Scope", TaxTreatmentTypes.OutOfScope, false, 4);

            var supplier = new Supplier
            {
                SupplierCode = "SUP-RET-01",
                SupplierName = "Supplier Return Test Vendor",
                CompanyName = "Supplier Return Test Vendor (Pvt) Ltd",
                Phone1 = "0110000000",
                Address = "Supplier Test Address",
                HasVat = true,
                VatNumber = "SUP-VAT-18",
                CurrentBalance = 10000m
            };

            context.AddRange(category, standardTax, zeroTax, exemptTax, outTax, supplier);
            context.SaveChanges();

            var standardRate = new TaxRate
            {
                TaxCode = "VAT-18",
                TaxName = "Standard VAT 18%",
                TaxCategoryId = standardTax.Id,
                RatePercent = 18m,
                EffectiveFrom = new DateTime(2024, 1, 1),
                IsActive = true,
                CreatedBy = "Test",
                UpdatedBy = "Test"
            };
            context.TaxRates.Add(standardRate);

            var parents = new[]
            {
                CreateSupplierReturnParent("SR-STANDARD", "Standard VAT Item", category.Id, uom.Id, standardTax.Id),
                CreateSupplierReturnParent("SR-ZERO", "Zero Rated Item", category.Id, uom.Id, zeroTax.Id),
                CreateSupplierReturnParent("SR-EXEMPT", "Exempt Item", category.Id, uom.Id, exemptTax.Id),
                CreateSupplierReturnParent("SR-OUT", "Out of Scope Item", category.Id, uom.Id, outTax.Id),
                CreateSupplierReturnParent("SR-EXCLUSIVE", "VAT Exclusive Item", category.Id, uom.Id, standardTax.Id),
                CreateSupplierReturnParent("SR-LEGACY", "Legacy Item", category.Id, uom.Id, null)
            };
            context.ItemParents.AddRange(parents);
            context.SaveChanges();

            ItemVariant[] variants = parents.Select((parent, index) => new ItemVariant
            {
                ItemParentId = parent.Id,
                SkuCode = $"SR-SKU-{index + 1}",
                VariantDescription = "Standard",
                AverageCost = 500m + index * 10m,
                CostPrice = 500m + index * 10m,
                RetailPrice = 1500m + index * 100m,
                WholesalePrice = 1400m + index * 100m,
                MinimumPrice = 500m
            }).ToArray();
            context.ItemVariants.AddRange(variants);
            context.SaveChanges();

            ItemBatch[] batches = variants.Select((variant, index) => new ItemBatch
            {
                ItemVariantId = variant.Id,
                BatchNo = $"SR-BATCH-{index + 1}",
                InternalBatchBarcode = $"SR-BC-{index + 1}",
                ReceivedDate = new DateTime(2026, 7, 1),
                CostPrice = 900m - index * 50m,
                RetailPrice = variant.RetailPrice,
                WholesalePrice = variant.WholesalePrice,
                CurrentStock = 10m
            }).ToArray();
            context.ItemBatches.AddRange(batches);

            var mainGrn = CreateSupplierReturnGrn(supplier.Id, "GRN-SR-MAIN", "SUP-INV-MAIN", true);
            var exclusiveGrn = CreateSupplierReturnGrn(supplier.Id, "GRN-SR-EX", "SUP-INV-EX", false);
            var legacyGrn = CreateSupplierReturnGrn(supplier.Id, "GRN-SR-LEG", "SUP-INV-LEG", null);
            context.GrnHeaders.AddRange(mainGrn, exclusiveGrn, legacyGrn);
            context.SaveChanges();

            GrnLine standardLine = CreateSupplierReturnGrnLine(
                mainGrn.Id, variants[0].Id, batches[0].Id, 3m, 1180m, 900m,
                standardTax.Id, standardRate.Id, TaxCategoryCodes.Standard, "Standard VAT", 18m, true,
                3000m, 540m, 3540m, TaxSnapshotStatuses.Complete);
            GrnLine zeroLine = CreateSupplierReturnGrnLine(
                mainGrn.Id, variants[1].Id, batches[1].Id, 2m, 200m, 180m,
                zeroTax.Id, null, TaxCategoryCodes.ZeroRated, "Zero Rated", 0m, true,
                400m, 0m, 400m, TaxSnapshotStatuses.Complete);
            GrnLine exemptLine = CreateSupplierReturnGrnLine(
                mainGrn.Id, variants[2].Id, batches[2].Id, 2m, 300m, 270m,
                exemptTax.Id, null, TaxCategoryCodes.Exempt, "Exempt", 0m, true,
                600m, 0m, 600m, TaxSnapshotStatuses.Complete);
            GrnLine outLine = CreateSupplierReturnGrnLine(
                mainGrn.Id, variants[3].Id, batches[3].Id, 1m, 500m, 450m,
                outTax.Id, null, TaxCategoryCodes.OutOfScope, "Out of Scope", 0m, true,
                500m, 0m, 500m, TaxSnapshotStatuses.Complete);
            GrnLine exclusiveLine = CreateSupplierReturnGrnLine(
                exclusiveGrn.Id, variants[4].Id, batches[4].Id, 1m, 1000m, 1000m,
                standardTax.Id, standardRate.Id, TaxCategoryCodes.Standard, "Standard VAT", 18m, false,
                1000m, 180m, 1180m, TaxSnapshotStatuses.Complete);
            GrnLine legacyLine = CreateSupplierReturnGrnLine(
                legacyGrn.Id, variants[5].Id, batches[5].Id, 1m, 700m, 650m,
                null, null, null, null, null, null,
                null, null, null, TaxSnapshotStatuses.LegacyUnknown);
            legacyLine.LineTotal = 700m;

            context.GrnLines.AddRange(standardLine, zeroLine, exemptLine, outLine, exclusiveLine, legacyLine);
            context.StoreSettings.Add(new StoreSettings
            {
                StoreName = "Supplier Return Test Store",
                LegalName = "Supplier Return Test Store (Pvt) Ltd",
                AddressLine1 = "1 Test Road",
                City = "Colombo",
                Country = "Sri Lanka",
                Phone = "0111111111",
                TaxpayerIdentificationNumber = "STORE-TIN",
                VatRegistrationNumber = "STORE-VAT",
                IsVatRegistered = true,
                IsActive = true
            });
            context.SaveChanges();

            return new SupplierReturnTestScenario
            {
                SupplierId = supplier.Id,
                MainGrnId = mainGrn.Id,
                ExclusiveGrnId = exclusiveGrn.Id,
                LegacyGrnId = legacyGrn.Id,
                StandardParentId = parents[0].Id,
                StandardVariantId = variants[0].Id,
                StandardBatchId = batches[0].Id,
                StandardLineId = standardLine.Id,
                ZeroLineId = zeroLine.Id,
                ExemptLineId = exemptLine.Id,
                OutOfScopeLineId = outLine.Id,
                ExclusiveLineId = exclusiveLine.Id,
                LegacyLineId = legacyLine.Id,
                StandardTaxRateId = standardRate.Id
            };
        }

        private static TaxCategory CreateSupplierReturnTaxCategory(
            string code,
            string name,
            string treatment,
            bool rateBased,
            int order)
        {
            return new TaxCategory
            {
                CategoryCode = code,
                CategoryName = name,
                TreatmentType = treatment,
                IsRateBased = rateBased,
                IsActive = true,
                DisplayOrder = order
            };
        }

        private static ItemParent CreateSupplierReturnParent(
            string code,
            string name,
            int categoryId,
            int uomId,
            int? taxCategoryId)
        {
            return new ItemParent
            {
                ItemCode = code,
                ItemName = name,
                PrintName = name,
                CategoryId = categoryId,
                UnitOfMeasureId = uomId,
                BaseUom = "PCS",
                ItemType = ItemTypeCodes.StockItem,
                TaxCategoryId = taxCategoryId,
                TaxCode = taxCategoryId.HasValue ? "VAT-18" : "TAX-FREE",
                IsTaxInclusive = true,
                HasBatchTracking = true,
                HasExpiryTracking = false,
                HasBatchExpiry = false
            };
        }

        private static GrnHeader CreateSupplierReturnGrn(
            int supplierId,
            string grnNumber,
            string invoiceNumber,
            bool? isTaxInclusive)
        {
            return new GrnHeader
            {
                GrnNumber = grnNumber,
                SupplierId = supplierId,
                SupplierInvoiceNo = invoiceNumber,
                InvoiceDate = new DateTime(2026, 7, 1),
                ReceivedDate = new DateTime(2026, 7, 2),
                DueDate = new DateTime(2026, 8, 1),
                Status = "Posted",
                IsTaxInclusive = isTaxInclusive,
                TaxSnapshotStatus = isTaxInclusive.HasValue
                    ? TaxSnapshotStatuses.Complete
                    : TaxSnapshotStatuses.LegacyUnknown,
                CreatedBy = "Test",
                PostedBy = "Test",
                PostedAt = new DateTime(2026, 7, 2)
            };
        }

        private static GrnLine CreateSupplierReturnGrnLine(
            int grnHeaderId,
            int variantId,
            int batchId,
            decimal quantity,
            decimal unitCost,
            decimal landedCost,
            int? taxCategoryId,
            int? taxRateId,
            string? categoryCode,
            string? taxName,
            decimal? taxRate,
            bool? inclusive,
            decimal? taxable,
            decimal? vat,
            decimal? taxInclusive,
            string snapshotStatus)
        {
            return new GrnLine
            {
                GrnHeaderId = grnHeaderId,
                ItemVariantId = variantId,
                ItemBatchId = batchId,
                BatchNo = $"BATCH-{batchId}",
                Uom = "PCS",
                ReceivedQty = quantity,
                UnitCost = unitCost,
                LandedCost = landedCost,
                LineTotal = taxInclusive ?? quantity * unitCost,
                TaxCategoryId = taxCategoryId,
                TaxRateId = taxRateId,
                TaxCategoryCodeSnapshot = categoryCode,
                TaxCodeSnapshot = categoryCode,
                TaxNameSnapshot = taxName,
                TaxRatePercentSnapshot = taxRate,
                IsTaxInclusiveSnapshot = inclusive,
                TaxableAmountSnapshot = taxable,
                VatAmountSnapshot = vat,
                TaxInclusiveAmountSnapshot = taxInclusive,
                TaxSnapshotStatus = snapshotStatus,
                LineStatus = "Posted"
            };
        }

        private sealed class SupplierReturnTestScenario
        {
            public int SupplierId { get; init; }
            public int MainGrnId { get; init; }
            public int ExclusiveGrnId { get; init; }
            public int LegacyGrnId { get; init; }
            public int StandardParentId { get; init; }
            public int StandardVariantId { get; init; }
            public int StandardBatchId { get; init; }
            public int StandardLineId { get; init; }
            public int ZeroLineId { get; init; }
            public int ExemptLineId { get; init; }
            public int OutOfScopeLineId { get; init; }
            public int ExclusiveLineId { get; init; }
            public int LegacyLineId { get; init; }
            public int StandardTaxRateId { get; init; }
        }

        private static CustomerReturnRepository CreateCustomerReturnRepository(
            RepositoryTestDbContextFactory factory)
        {
            return new CustomerReturnRepository(
                factory,
                new CustomerReturnAllocationCalculator());
        }

        private static SalesHeader CreateReturnTestSale(
            RepositoryTestDbContextFactory factory,
            RepositoryTestScenario scenario,
            decimal stockQuantity,
            decimal serviceQuantity,
            decimal invoiceDiscount = 0m)
        {
            var lines = new List<SalesLine>();
            decimal gross = 0m;

            if (stockQuantity > 0m)
            {
                lines.Add(CreateRepositoryTestLine(
                    scenario.StockVariantId,
                    scenario.StockBatchId,
                    scenario.StockSku,
                    "Test Stock Item",
                    stockQuantity,
                    1180m));
                gross += stockQuantity * 1180m;
            }

            if (serviceQuantity > 0m)
            {
                lines.Add(CreateRepositoryTestLine(
                    scenario.ServiceVariantId,
                    null,
                    scenario.ServiceSku,
                    "Installation Service",
                    serviceQuantity,
                    1180m));
                gross += serviceQuantity * 1180m;
            }

            decimal payable = gross - invoiceDiscount;
            var repository = new SalesRepository(factory);

            return repository.ProcessCheckoutAsync(
                    CreateRepositoryTestHeader(
                        scenario.ShiftSessionId,
                        payable,
                        invoiceDiscount),
                    lines,
                    new List<SalesPayment> { CreateCashPayment(payable) })
                .GetAwaiter().GetResult();
        }

        private static CustomerReturnRequest CreateReturnRequest(
            SalesHeader sale,
            RepositoryTestScenario scenario,
            params (int SalesLineId, decimal Quantity)[] lines)
        {
            return new CustomerReturnRequest
            {
                SalesHeaderId = sale.Id,
                ShiftSessionId = scenario.ShiftSessionId,
                TerminalNo = "T01",
                CashierName = "Test Cashier",
                AuthorizedBy = "Test Manager",
                ReturnReason = "Test customer return",
                Lines = lines.Select(line => new CustomerReturnRequestLine
                {
                    SalesLineId = line.SalesLineId,
                    Quantity = line.Quantity
                }).ToList()
            };
        }

        private static SalesHeader CreateFourCategoryReturnSale(
            RepositoryTestDbContextFactory factory,
            RepositoryTestScenario scenario)
        {
            using AppDbContext context = factory.CreateDbContext();

            var sale = new SalesHeader
            {
                ShiftSessionId = scenario.ShiftSessionId,
                InvoiceNo = ("INV-RET-" + Guid.NewGuid().ToString("N"))[..20],
                TerminalNo = "T01",
                CashierName = "Test Cashier",
                CustomerName = "Walk-In",
                TransactionDate = DateTime.Now,
                DocumentType = SalesDocumentTypes.Receipt,
                IsVatRegisteredSale = true,
                GrossTotal = 1780m,
                TotalDiscount = 0m,
                NetTotal = 1780m,
                AmountTendered = 1780m,
                PaymentMethod = "Cash",
                Status = "Completed",
                TaxableAmountTotal = 1600m,
                TotalVatAmount = 180m,
                StandardRatedAmount = 1000m,
                ZeroRatedAmount = 100m,
                ExemptAmount = 200m,
                OutOfScopeAmount = 300m,
                TaxSnapshotStatus = TaxSnapshotStatuses.Complete
            };

            sale.SalesLines.Add(CreateDirectReturnLine(scenario.ServiceVariantId, "Standard Service", TaxCategoryCodes.Standard, 1180m, 1000m, 180m, 18m));
            sale.SalesLines.Add(CreateDirectReturnLine(scenario.ServiceVariantId, "Zero Service", TaxCategoryCodes.ZeroRated, 100m, 100m, 0m, 0m));
            sale.SalesLines.Add(CreateDirectReturnLine(scenario.ServiceVariantId, "Exempt Service", TaxCategoryCodes.Exempt, 200m, 200m, 0m, 0m));
            sale.SalesLines.Add(CreateDirectReturnLine(scenario.ServiceVariantId, "Out Service", TaxCategoryCodes.OutOfScope, 300m, 300m, 0m, 0m));

            context.SalesHeaders.Add(sale);
            context.SaveChanges();
            return sale;
        }

        private static SalesLine CreateDirectReturnLine(
            int variantId,
            string description,
            string categoryCode,
            decimal inclusive,
            decimal taxable,
            decimal vat,
            decimal rate)
        {
            return new SalesLine
            {
                ItemVariantId = variantId,
                ItemBatchId = null,
                SkuCode = description.Replace(" ", "-").ToUpperInvariant(),
                ItemDescription = description,
                Uom = "JOB",
                ItemTypeSnapshot = ItemTypeCodes.Service,
                Quantity = 1m,
                UnitPrice = inclusive,
                OriginalUnitPrice = inclusive,
                GrossAmount = inclusive,
                DiscountAmount = 0m,
                LineTotal = inclusive,
                TaxCategoryCodeSnapshot = categoryCode,
                TaxCodeSnapshot = categoryCode,
                TaxNameSnapshot = categoryCode,
                TaxRatePercentSnapshot = rate,
                IsTaxInclusiveSnapshot = true,
                TaxableAmountSnapshot = taxable,
                VatAmountSnapshot = vat,
                TaxInclusiveAmountSnapshot = inclusive,
                TaxSnapshotStatus = TaxSnapshotStatuses.Complete
            };
        }

        private static SalesHeader CreateLegacyReturnSale(
            RepositoryTestDbContextFactory factory,
            RepositoryTestScenario scenario)
        {
            using AppDbContext context = factory.CreateDbContext();

            var sale = new SalesHeader
            {
                ShiftSessionId = scenario.ShiftSessionId,
                InvoiceNo = ("INV-LEG-" + Guid.NewGuid().ToString("N"))[..20],
                TerminalNo = "T01",
                CashierName = "Test Cashier",
                CustomerName = "Walk-In",
                TransactionDate = DateTime.Now,
                GrossTotal = 500m,
                NetTotal = 500m,
                AmountTendered = 500m,
                PaymentMethod = "Cash",
                Status = "Completed",
                TaxSnapshotStatus = TaxSnapshotStatuses.LegacyUnknown
            };

            sale.SalesLines.Add(new SalesLine
            {
                ItemVariantId = scenario.ServiceVariantId,
                ItemBatchId = null,
                SkuCode = scenario.ServiceSku,
                ItemDescription = "Legacy Service",
                Uom = "JOB",
                ItemTypeSnapshot = ItemTypeCodes.Service,
                Quantity = 1m,
                UnitPrice = 500m,
                OriginalUnitPrice = 500m,
                GrossAmount = 500m,
                LineTotal = 500m,
                TaxSnapshotStatus = TaxSnapshotStatuses.LegacyUnknown
            });

            context.SalesHeaders.Add(sale);
            context.SaveChanges();
            return sale;
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

        private static void VatReportIncludesCompletedSales()
        {
            using var factory = new RepositoryTestDbContextFactory();
            VatReportTestScenario scenario = SeedVatReportScenario(factory);
            VatReportResultDto report = GetVatReport(factory);

            if (!report.Documents.Any(row => row.DocumentNumber == scenario.StandardSaleNumber) ||
                report.Summary.CompleteDocumentCount != 11)
            {
                throw new InvalidOperationException("Completed VAT sales were not included in the report.");
            }
        }

        private static void VatReportExcludesVoidedSales()
        {
            using var factory = new RepositoryTestDbContextFactory();
            VatReportTestScenario scenario = SeedVatReportScenario(factory);
            VatReportResultDto report = GetVatReport(factory);

            if (report.Documents.Any(row => row.DocumentNumber == scenario.VoidedSaleNumber))
                throw new InvalidOperationException("Voided sale was included in VAT reporting.");
        }

        private static void VatReportDeductsCustomerReturnVat()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SeedVatReportScenario(factory);
            VatReportResultDto report = GetVatReport(factory);

            AssertMoney(246m, report.Summary.GrossOutputVat, "gross output VAT");
            AssertMoney(36m, report.Summary.CustomerReturnVat, "customer-return VAT");
            AssertMoney(210m, report.Summary.NetOutputVat, "net output VAT");
        }

        private static void VatReportIncludesPostedGrnVat()
        {
            using var factory = new RepositoryTestDbContextFactory();
            VatReportTestScenario scenario = SeedVatReportScenario(factory);
            VatReportResultDto report = GetVatReport(factory);

            AssertMoney(300m, report.Summary.GrossInputVat, "gross input VAT");
            if (!report.Documents.Any(row => row.DocumentNumber == scenario.MainGrnNumber))
                throw new InvalidOperationException("Posted GRN was not included in VAT reporting.");
        }

        private static void VatReportExcludesCancelledGrns()
        {
            using var factory = new RepositoryTestDbContextFactory();
            VatReportTestScenario scenario = SeedVatReportScenario(factory);
            VatReportResultDto report = GetVatReport(factory);

            if (report.Documents.Any(row => row.DocumentNumber == scenario.CancelledGrnNumber))
                throw new InvalidOperationException("Cancelled GRN was included in VAT reporting.");
        }

        private static void VatReportDeductsPostedSupplierReturnVat()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SeedVatReportScenario(factory);
            VatReportResultDto report = GetVatReport(factory);

            AssertMoney(18m, report.Summary.SupplierReturnVat, "supplier-return VAT");
            AssertMoney(282m, report.Summary.NetInputVat, "net input VAT");
        }

        private static void VatReportExcludesCancelledSupplierReturns()
        {
            using var factory = new RepositoryTestDbContextFactory();
            VatReportTestScenario scenario = SeedVatReportScenario(factory);
            VatReportResultDto report = GetVatReport(factory);

            if (report.Documents.Any(row => row.DocumentNumber == scenario.CancelledSupplierReturnNumber))
                throw new InvalidOperationException("Cancelled supplier return was included in VAT reporting.");
        }

        private static void VatReportStandardTotalsAreCorrect()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SeedVatReportScenario(factory);
            VatReportResultDto report = GetVatReport(factory);

            decimal salesStandard = report.CategoryRateRows
                .Where(row => row.SourceType == "Sale" && row.TaxCategoryCode == TaxCategoryCodes.Standard)
                .Sum(row => row.TaxableAmount);
            decimal returnedStandard = report.CategoryRateRows
                .Where(row => row.SourceType == "Customer Credit Note" && row.TaxCategoryCode == TaxCategoryCodes.Standard)
                .Sum(row => row.TaxableAmount);

            AssertMoney(1400m, salesStandard, "sales Standard taxable");
            AssertMoney(200m, returnedStandard, "customer-return Standard taxable");
        }

        private static void VatReportZeroRatedTotalsAreCorrect()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SeedVatReportScenario(factory);
            VatReportResultDto report = GetVatReport(factory);

            AssertMoney(100m, report.Summary.SalesZeroRatedAmount, "net sales Zero Rated");
            AssertMoney(200m, report.Summary.PurchaseZeroRatedAmount, "net purchase Zero Rated");
        }

        private static void VatReportExemptTotalsAreCorrect()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SeedVatReportScenario(factory);
            VatReportResultDto report = GetVatReport(factory);

            AssertMoney(200m, report.Summary.SalesExemptAmount, "net sales Exempt");
            AssertMoney(300m, report.Summary.PurchaseExemptAmount, "net purchase Exempt");
        }

        private static void VatReportOutOfScopeTotalsAreCorrect()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SeedVatReportScenario(factory);
            VatReportResultDto report = GetVatReport(factory);

            AssertMoney(800m, report.Summary.SalesOutOfScopeAmount, "net sales Out of Scope");
            AssertMoney(400m, report.Summary.PurchaseOutOfScopeAmount, "net purchase Out of Scope");
        }

        private static void VatReportAggregatesInclusiveAndExclusiveGrns()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SeedVatReportScenario(factory);
            VatReportResultDto report = GetVatReport(factory);

            VatCategoryRateRowDto standard18 = report.CategoryRateRows.Single(row =>
                row.SourceType == "GRN Purchase" &&
                row.TaxCategoryCode == TaxCategoryCodes.Standard &&
                row.VatRatePercent == 18m);

            AssertMoney(1500m, standard18.TaxableAmount, "inclusive and exclusive GRN taxable");
            AssertMoney(270m, standard18.VatAmount, "inclusive and exclusive GRN VAT");
        }

        private static void VatReportGroupsHistoricalRates()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SeedVatReportScenario(factory);
            VatReportResultDto report = GetVatReport(factory);

            if (!report.CategoryRateRows.Any(row =>
                    row.SourceType == "Sale" &&
                    row.TaxCategoryCode == TaxCategoryCodes.Standard &&
                    row.VatRatePercent == 15m) ||
                !report.CategoryRateRows.Any(row =>
                    row.SourceType == "GRN Purchase" &&
                    row.TaxCategoryCode == TaxCategoryCodes.Standard &&
                    row.VatRatePercent == 15m))
            {
                throw new InvalidOperationException("Historical VAT rates were not grouped separately.");
            }
        }

        private static void VatReportIgnoresCurrentTaxRateChanges()
        {
            using var factory = new RepositoryTestDbContextFactory();
            SeedVatReportScenario(factory);
            VatReportResultDto report = GetVatReport(factory);

            if (report.CategoryRateRows.Any(row => row.VatRatePercent == 25m))
                throw new InvalidOperationException("VAT report used the current Tax Rate instead of saved snapshots.");

            AssertMoney(246m, report.Summary.GrossOutputVat, "historical output VAT after rate change");
            AssertMoney(300m, report.Summary.GrossInputVat, "historical input VAT after rate change");
        }

        private static void VatReportKeepsNonVatSalesOutOfScope()
        {
            using var factory = new RepositoryTestDbContextFactory();
            VatReportTestScenario scenario = SeedVatReportScenario(factory);
            VatReportResultDto report = GetVatReport(factory);

            VatReportDocumentRowDto row = report.Documents.Single(document =>
                document.DocumentNumber == scenario.NonVatSaleNumber);
            AssertMoney(0m, row.VatAmount, "non-VAT sale VAT");
            AssertMoney(500m, row.OutOfScopeAmount, "non-VAT sale Out of Scope");
        }

        private static void VatReportDetectsSalesDiscrepancy()
        {
            using var factory = new RepositoryTestDbContextFactory();
            VatReportTestScenario scenario = SeedVatReportScenario(factory);
            using (AppDbContext context = factory.CreateDbContext())
            {
                context.SalesHeaders.Single(row => row.Id == scenario.StandardSaleId).TotalVatAmount = 181m;
                context.SaveChanges();
            }

            VatReportResultDto report = GetVatReport(factory);
            AssertHasDiscrepancy(report, "Sale", scenario.StandardSaleNumber, "VAT total");
        }

        private static void VatReportDetectsGrnDiscrepancy()
        {
            using var factory = new RepositoryTestDbContextFactory();
            VatReportTestScenario scenario = SeedVatReportScenario(factory);
            using (AppDbContext context = factory.CreateDbContext())
            {
                context.GrnHeaders.Single(row => row.Id == scenario.MainGrnId).TotalVatAmount = 181m;
                context.SaveChanges();
            }

            VatReportResultDto report = GetVatReport(factory);
            AssertHasDiscrepancy(report, "GRN Purchase", scenario.MainGrnNumber, "VAT total");
        }

        private static void VatReportDetectsCustomerReturnDiscrepancy()
        {
            using var factory = new RepositoryTestDbContextFactory();
            VatReportTestScenario scenario = SeedVatReportScenario(factory);
            using (AppDbContext context = factory.CreateDbContext())
            {
                context.CustomerReturnHeaders.Single(row => row.Id == scenario.CustomerReturnId).TotalVatAmount = 37m;
                context.SaveChanges();
            }

            VatReportResultDto report = GetVatReport(factory);
            AssertHasDiscrepancy(report, "Customer Credit Note", scenario.CustomerReturnNumber, "VAT total");
        }

        private static void VatReportDetectsSupplierReturnDiscrepancy()
        {
            using var factory = new RepositoryTestDbContextFactory();
            VatReportTestScenario scenario = SeedVatReportScenario(factory);
            using (AppDbContext context = factory.CreateDbContext())
            {
                context.SupplierReturnHeaders.Single(row => row.Id == scenario.SupplierReturnId).TotalVatAmount = 19m;
                context.SaveChanges();
            }

            VatReportResultDto report = GetVatReport(factory);
            AssertHasDiscrepancy(report, "Supplier Debit Note", scenario.SupplierReturnNumber, "VAT total");
        }

        private static void VatReportSeparatesLegacyUnknown()
        {
            using var factory = new RepositoryTestDbContextFactory();
            VatReportTestScenario scenario = SeedVatReportScenario(factory);
            VatReportResultDto report = GetVatReport(factory);

            if (report.Documents.Any(row =>
                    row.DocumentNumber == scenario.LegacySaleNumber ||
                    row.DocumentNumber == scenario.LegacyGrnNumber ||
                    row.DocumentNumber == scenario.LegacyCustomerReturnNumber ||
                    row.DocumentNumber == scenario.LegacySupplierReturnNumber) ||
                report.LegacyUnknownRows.Count != 5)
            {
                throw new InvalidOperationException("LegacyUnknown documents were not fully separated from VAT totals.");
            }
        }

        private static void VatReportSeparatesUnknownFreight()
        {
            using var factory = new RepositoryTestDbContextFactory();
            VatReportTestScenario scenario = SeedVatReportScenario(factory);
            VatReportResultDto report = GetVatReport(factory);

            VatLegacyUnknownRowDto freight = report.LegacyUnknownRows.Single(row =>
                row.SourceType == "GRN Freight" &&
                row.DocumentNumber == scenario.MainGrnNumber);
            AssertMoney(50m, freight.FinancialAmount, "unknown GRN freight");
            AssertMoney(300m, report.Summary.GrossInputVat, "input VAT excluding freight");
        }

        private static void VatReportDateBoundariesAndPositionReconcile()
        {
            using var factory = new RepositoryTestDbContextFactory();
            VatReportTestScenario scenario = SeedVatReportScenario(factory);
            VatReportResultDto full = GetVatReport(factory);

            if (!full.Documents.Any(row => row.DocumentNumber == scenario.StartBoundarySaleNumber) ||
                !full.Documents.Any(row => row.DocumentNumber == scenario.EndBoundarySaleNumber))
            {
                throw new InvalidOperationException("Inclusive VAT report date boundaries were not applied.");
            }

            AssertMoney(-72m, full.Summary.OperationalVatPosition, "operational VAT position");

            VatReportResultDto middle = new VatReportRepository(factory)
                .GetReportAsync(new DateTime(2026, 7, 2), new DateTime(2026, 7, 30))
                .GetAwaiter()
                .GetResult();

            if (middle.Documents.Any(row =>
                row.DocumentNumber == scenario.StartBoundarySaleNumber ||
                row.DocumentNumber == scenario.EndBoundarySaleNumber))
            {
                throw new InvalidOperationException("VAT date filter included documents outside the requested period.");
            }
        }

        private static VatReportResultDto GetVatReport(
            RepositoryTestDbContextFactory factory) =>
            new VatReportRepository(factory)
                .GetReportAsync(
                    new DateTime(2026, 7, 1),
                    new DateTime(2026, 7, 31))
                .GetAwaiter()
                .GetResult();

        private static void AssertHasDiscrepancy(
            VatReportResultDto report,
            string sourceType,
            string documentNumber,
            string field)
        {
            if (!report.ReconciliationRows.Any(row =>
                row.SourceType == sourceType &&
                row.DocumentNumber == documentNumber &&
                row.FieldChecked == field))
            {
                throw new InvalidOperationException(
                    $"Expected {sourceType} discrepancy '{field}' was not detected.");
            }
        }

        private static VatReportTestScenario SeedVatReportScenario(
            RepositoryTestDbContextFactory factory)
        {
            RepositoryTestScenario baseScenario = SeedRepositoryTestScenario(factory);
            using AppDbContext context = factory.CreateDbContext();

            TaxCategory standard = context.TaxCategories.Single(row =>
                row.CategoryCode == TaxCategoryCodes.Standard);
            TaxRate standardRate = context.TaxRates.Single(row =>
                row.TaxCategoryId == standard.Id);

            TaxCategory zero = CreateVatReportTaxCategory(
                TaxCategoryCodes.ZeroRated, "Zero Rated", TaxTreatmentTypes.ZeroRated, false, 20);
            TaxCategory exempt = CreateVatReportTaxCategory(
                TaxCategoryCodes.Exempt, "Exempt", TaxTreatmentTypes.Exempt, false, 30);
            TaxCategory outOfScope = CreateVatReportTaxCategory(
                TaxCategoryCodes.OutOfScope, "Out of Scope", TaxTreatmentTypes.OutOfScope, false, 40);
            var supplier = new Supplier
            {
                SupplierCode = "VAT-REPORT-SUP",
                SupplierName = "VAT Report Supplier",
                CompanyName = "VAT Report Supplier (Pvt) Ltd",
                Phone1 = "0110000000",
                Address = "Colombo",
                HasVat = true,
                VatNumber = "SUP-VAT-REPORT",
                CurrentBalance = 5000m
            };

            context.AddRange(zero, exempt, outOfScope, supplier);
            context.SaveChanges();

            SalesHeader standardSale = CreateVatReportSale(
                baseScenario, "VAT-SALE-STD", new DateTime(2026, 7, 10, 10, 0, 0), true,
                taxable: 1000m, vat: 180m, inclusive: 1180m,
                standard: 1000m, zero: 0m, exempt: 0m, outOfScope: 0m,
                CreateVatReportSalesLine(baseScenario.ServiceVariantId, standard.Id, standardRate.Id,
                    TaxCategoryCodes.Standard, 18m, 1000m, 180m, 1180m));

            SalesHeader mixedSale = CreateVatReportSale(
                baseScenario, "VAT-SALE-MIX", new DateTime(2026, 7, 11, 11, 0, 0), true,
                taxable: 600m, vat: 0m, inclusive: 600m,
                standard: 0m, zero: 100m, exempt: 200m, outOfScope: 300m,
                CreateVatReportSalesLine(baseScenario.ServiceVariantId, zero.Id, null,
                    TaxCategoryCodes.ZeroRated, 0m, 100m, 0m, 100m),
                CreateVatReportSalesLine(baseScenario.ServiceVariantId, exempt.Id, null,
                    TaxCategoryCodes.Exempt, 0m, 200m, 0m, 200m),
                CreateVatReportSalesLine(baseScenario.ServiceVariantId, outOfScope.Id, null,
                    TaxCategoryCodes.OutOfScope, 0m, 300m, 0m, 300m));

            SalesHeader historicalSale = CreateVatReportSale(
                baseScenario, "VAT-SALE-HIST", new DateTime(2026, 7, 12, 12, 0, 0), true,
                taxable: 200m, vat: 30m, inclusive: 230m,
                standard: 200m, zero: 0m, exempt: 0m, outOfScope: 0m,
                CreateVatReportSalesLine(baseScenario.ServiceVariantId, standard.Id, standardRate.Id,
                    TaxCategoryCodes.Standard, 15m, 200m, 30m, 230m));

            SalesHeader nonVatSale = CreateVatReportSale(
                baseScenario, "VAT-SALE-NONVAT", new DateTime(2026, 7, 13, 13, 0, 0), false,
                taxable: 500m, vat: 0m, inclusive: 500m,
                standard: 0m, zero: 0m, exempt: 0m, outOfScope: 500m,
                CreateVatReportSalesLine(baseScenario.ServiceVariantId, outOfScope.Id, null,
                    TaxCategoryCodes.OutOfScope, 0m, 500m, 0m, 500m));

            SalesHeader startBoundarySale = CreateVatReportSale(
                baseScenario, "VAT-SALE-START", new DateTime(2026, 7, 1, 0, 0, 0), true,
                taxable: 100m, vat: 18m, inclusive: 118m,
                standard: 100m, zero: 0m, exempt: 0m, outOfScope: 0m,
                CreateVatReportSalesLine(baseScenario.ServiceVariantId, standard.Id, standardRate.Id,
                    TaxCategoryCodes.Standard, 18m, 100m, 18m, 118m));

            SalesHeader endBoundarySale = CreateVatReportSale(
                baseScenario, "VAT-SALE-END", new DateTime(2026, 7, 31, 23, 59, 59), true,
                taxable: 100m, vat: 18m, inclusive: 118m,
                standard: 100m, zero: 0m, exempt: 0m, outOfScope: 0m,
                CreateVatReportSalesLine(baseScenario.ServiceVariantId, standard.Id, standardRate.Id,
                    TaxCategoryCodes.Standard, 18m, 100m, 18m, 118m));

            SalesHeader voidedSale = CreateVatReportSale(
                baseScenario, "VAT-SALE-VOID", new DateTime(2026, 7, 14), true,
                taxable: 1000m, vat: 180m, inclusive: 1180m,
                standard: 1000m, zero: 0m, exempt: 0m, outOfScope: 0m,
                CreateVatReportSalesLine(baseScenario.ServiceVariantId, standard.Id, standardRate.Id,
                    TaxCategoryCodes.Standard, 18m, 1000m, 180m, 1180m));
            voidedSale.IsVoided = true;

            SalesHeader legacySale = new()
            {
                ShiftSessionId = baseScenario.ShiftSessionId,
                InvoiceNo = "VAT-SALE-LEGACY",
                TerminalNo = "T01",
                CashierName = "Test Cashier",
                CustomerName = "Walk-In",
                TransactionDate = new DateTime(2026, 7, 15),
                NetTotal = 400m,
                GrossTotal = 400m,
                Status = "Completed",
                TaxSnapshotStatus = TaxSnapshotStatuses.LegacyUnknown
            };
            legacySale.SalesLines.Add(new SalesLine
            {
                ItemVariantId = baseScenario.ServiceVariantId,
                ItemDescription = "Legacy report line",
                Quantity = 1m,
                UnitPrice = 400m,
                GrossAmount = 400m,
                LineTotal = 400m,
                TaxSnapshotStatus = TaxSnapshotStatuses.LegacyUnknown
            });

            context.SalesHeaders.AddRange(
                standardSale, mixedSale, historicalSale, nonVatSale,
                startBoundarySale, endBoundarySale, voidedSale, legacySale);
            context.SaveChanges();

            SalesLine originalStandardLine = standardSale.SalesLines.Single();
            var customerReturn = new CustomerReturnHeader
            {
                ReturnNo = "VAT-CR-001",
                CreditNoteNo = "VAT-CR-001",
                OriginalInvoiceNo = standardSale.InvoiceNo,
                OriginalSalesHeaderId = standardSale.Id,
                ShiftSessionId = baseScenario.ShiftSessionId,
                TerminalNo = "T01",
                CashierName = "Test Cashier",
                AuthorizedBy = "Test Manager",
                ReturnDate = new DateTime(2026, 7, 16),
                TotalRefundAmount = 236m,
                RefundMethod = "Cash",
                DocumentType = "CreditNote",
                TaxableAmountTotal = 200m,
                TotalVatAmount = 36m,
                StandardRatedAmount = 200m,
                ZeroRatedAmount = 0m,
                ExemptAmount = 0m,
                OutOfScopeAmount = 0m,
                TaxSnapshotStatus = TaxSnapshotStatuses.Complete
            };
            customerReturn.Lines.Add(new CustomerReturnLine
            {
                SalesLineId = originalStandardLine.Id,
                ItemVariantId = baseScenario.ServiceVariantId,
                ItemDescription = "Returned standard service",
                QuantityReturned = 0.2m,
                RefundValue = 1180m,
                LineTotalRefund = 236m,
                ReturnReason = "Test",
                InventoryAction = "None",
                ItemTypeSnapshot = ItemTypeCodes.Service,
                TaxCategoryId = standard.Id,
                TaxRateId = standardRate.Id,
                TaxCategoryCodeSnapshot = TaxCategoryCodes.Standard,
                TaxCodeSnapshot = "VAT-18",
                TaxNameSnapshot = "Standard VAT",
                TaxRatePercentSnapshot = 18m,
                IsTaxInclusiveSnapshot = true,
                TaxableAmountSnapshot = 200m,
                VatAmountSnapshot = 36m,
                TaxInclusiveAmountSnapshot = 236m,
                OriginalTaxableAmount = 1000m,
                OriginalVatAmount = 180m,
                OriginalTaxInclusiveAmount = 1180m,
                TaxSnapshotStatus = TaxSnapshotStatuses.Complete
            });

            var legacyCustomerReturn = new CustomerReturnHeader
            {
                ReturnNo = "VAT-CR-LEGACY",
                CreditNoteNo = "VAT-CR-LEGACY",
                OriginalInvoiceNo = legacySale.InvoiceNo,
                OriginalSalesHeaderId = legacySale.Id,
                ShiftSessionId = baseScenario.ShiftSessionId,
                TerminalNo = "T01",
                CashierName = "Test Cashier",
                AuthorizedBy = "Test Manager",
                ReturnDate = new DateTime(2026, 7, 17),
                TotalRefundAmount = 50m,
                RefundMethod = "Cash",
                DocumentType = "CreditNote",
                TaxSnapshotStatus = TaxSnapshotStatuses.LegacyUnknown
            };
            legacyCustomerReturn.Lines.Add(new CustomerReturnLine
            {
                SalesLineId = legacySale.SalesLines.Single().Id,
                ItemVariantId = baseScenario.ServiceVariantId,
                ItemDescription = "Legacy return",
                QuantityReturned = 0.125m,
                RefundValue = 400m,
                LineTotalRefund = 50m,
                ReturnReason = "Test",
                InventoryAction = "None",
                ItemTypeSnapshot = ItemTypeCodes.Service,
                TaxSnapshotStatus = TaxSnapshotStatuses.LegacyUnknown
            });

            context.CustomerReturnHeaders.AddRange(customerReturn, legacyCustomerReturn);
            context.SaveChanges();

            GrnHeader mainGrn = CreateVatReportGrn(
                supplier.Id, "VAT-GRN-MAIN", "VAT-SUP-INV-1", new DateTime(2026, 7, 18),
                taxable: 1900m, vat: 180m, productInclusive: 2080m, freight: 50m,
                standard: 1000m, zero: 200m, exempt: 300m, outOfScope: 400m);
            mainGrn.GrnLines.Add(CreateVatReportGrnLine(baseScenario.StockVariantId, baseScenario.StockBatchId,
                standard.Id, standardRate.Id, TaxCategoryCodes.Standard, 18m, true, 1m, 1000m, 180m, 1180m));
            mainGrn.GrnLines.Add(CreateVatReportGrnLine(baseScenario.StockVariantId, baseScenario.StockBatchId,
                zero.Id, null, TaxCategoryCodes.ZeroRated, 0m, true, 1m, 200m, 0m, 200m));
            mainGrn.GrnLines.Add(CreateVatReportGrnLine(baseScenario.StockVariantId, baseScenario.StockBatchId,
                exempt.Id, null, TaxCategoryCodes.Exempt, 0m, true, 1m, 300m, 0m, 300m));
            mainGrn.GrnLines.Add(CreateVatReportGrnLine(baseScenario.StockVariantId, baseScenario.StockBatchId,
                outOfScope.Id, null, TaxCategoryCodes.OutOfScope, 0m, true, 1m, 400m, 0m, 400m));

            GrnHeader exclusiveGrn = CreateVatReportGrn(
                supplier.Id, "VAT-GRN-EX", "VAT-SUP-INV-2", new DateTime(2026, 7, 19),
                taxable: 500m, vat: 90m, productInclusive: 590m, freight: 0m,
                standard: 500m, zero: 0m, exempt: 0m, outOfScope: 0m);
            exclusiveGrn.IsTaxInclusive = false;
            exclusiveGrn.GrnLines.Add(CreateVatReportGrnLine(baseScenario.StockVariantId, baseScenario.StockBatchId,
                standard.Id, standardRate.Id, TaxCategoryCodes.Standard, 18m, false, 1m, 500m, 90m, 590m));

            GrnHeader historicalGrn = CreateVatReportGrn(
                supplier.Id, "VAT-GRN-HIST", "VAT-SUP-INV-3", new DateTime(2026, 7, 20),
                taxable: 200m, vat: 30m, productInclusive: 230m, freight: 0m,
                standard: 200m, zero: 0m, exempt: 0m, outOfScope: 0m);
            historicalGrn.GrnLines.Add(CreateVatReportGrnLine(baseScenario.StockVariantId, baseScenario.StockBatchId,
                standard.Id, standardRate.Id, TaxCategoryCodes.Standard, 15m, true, 1m, 200m, 30m, 230m));

            GrnHeader cancelledGrn = CreateVatReportGrn(
                supplier.Id, "VAT-GRN-CANCEL", "VAT-SUP-INV-C", new DateTime(2026, 7, 21),
                taxable: 1000m, vat: 180m, productInclusive: 1180m, freight: 0m,
                standard: 1000m, zero: 0m, exempt: 0m, outOfScope: 0m);
            cancelledGrn.Status = "Cancelled";
            cancelledGrn.GrnLines.Add(CreateVatReportGrnLine(baseScenario.StockVariantId, baseScenario.StockBatchId,
                standard.Id, standardRate.Id, TaxCategoryCodes.Standard, 18m, true, 1m, 1000m, 180m, 1180m));

            GrnHeader legacyGrn = new()
            {
                GrnNumber = "VAT-GRN-LEGACY",
                SupplierId = supplier.Id,
                SupplierInvoiceNo = "VAT-SUP-INV-LEG",
                InvoiceDate = new DateTime(2026, 7, 22),
                ReceivedDate = new DateTime(2026, 7, 22),
                NetPayable = 700m,
                Status = "Posted",
                TaxSnapshotStatus = TaxSnapshotStatuses.LegacyUnknown,
                FreightTaxSnapshotStatus = TaxSnapshotStatuses.LegacyUnknown
            };
            legacyGrn.GrnLines.Add(new GrnLine
            {
                ItemVariantId = baseScenario.StockVariantId,
                ItemBatchId = baseScenario.StockBatchId,
                BatchNo = "TEST-BATCH",
                Uom = "PCS",
                ReceivedQty = 1m,
                UnitCost = 700m,
                LandedCost = 700m,
                LineTotal = 700m,
                TaxSnapshotStatus = TaxSnapshotStatuses.LegacyUnknown,
                LineStatus = "Posted"
            });

            context.GrnHeaders.AddRange(mainGrn, exclusiveGrn, historicalGrn, cancelledGrn, legacyGrn);
            context.SaveChanges();

            GrnLine mainStandardLine = mainGrn.GrnLines.Single(line =>
                line.TaxCategoryCodeSnapshot == TaxCategoryCodes.Standard);
            var supplierReturn = CreateVatReportSupplierReturn(
                supplier.Id, mainGrn.Id, "VAT-SDN-001", mainGrn.SupplierInvoiceNo,
                new DateTime(2026, 7, 23), 100m, 18m, 118m);
            supplierReturn.ReturnLines.Add(CreateVatReportSupplierReturnLine(
                mainStandardLine, baseScenario.StockVariantId, baseScenario.StockBatchId,
                standard.Id, standardRate.Id, TaxCategoryCodes.Standard, 18m,
                0.1m, 100m, 18m, 118m, TaxSnapshotStatuses.Complete));

            var cancelledSupplierReturn = CreateVatReportSupplierReturn(
                supplier.Id, mainGrn.Id, "VAT-SDN-CANCEL", mainGrn.SupplierInvoiceNo,
                new DateTime(2026, 7, 24), 500m, 90m, 590m);
            cancelledSupplierReturn.Status = "Cancelled";
            cancelledSupplierReturn.ReturnLines.Add(CreateVatReportSupplierReturnLine(
                mainStandardLine, baseScenario.StockVariantId, baseScenario.StockBatchId,
                standard.Id, standardRate.Id, TaxCategoryCodes.Standard, 18m,
                0.5m, 500m, 90m, 590m, TaxSnapshotStatuses.Complete));

            var legacySupplierReturn = new SupplierReturnHeader
            {
                ReturnNumber = "VAT-SDN-LEGACY",
                SupplierId = supplier.Id,
                GrnHeaderId = legacyGrn.Id,
                OriginalInvoiceNo = legacyGrn.SupplierInvoiceNo,
                ReturnDate = new DateTime(2026, 7, 25),
                GrossCredit = 60m,
                NetCredit = 60m,
                Status = "Posted",
                TaxSnapshotStatus = TaxSnapshotStatuses.LegacyUnknown,
                CreatedBy = "Test",
                PostedBy = "Test"
            };
            legacySupplierReturn.ReturnLines.Add(CreateVatReportSupplierReturnLine(
                legacyGrn.GrnLines.Single(), baseScenario.StockVariantId, baseScenario.StockBatchId,
                null, null, null, null, 0.1m, null, null, null,
                TaxSnapshotStatuses.LegacyUnknown, creditValue: 60m));

            context.SupplierReturnHeaders.AddRange(
                supplierReturn, cancelledSupplierReturn, legacySupplierReturn);

            standardRate.RatePercent = 25m;
            context.SaveChanges();

            return new VatReportTestScenario
            {
                StandardSaleId = standardSale.Id,
                StandardSaleNumber = standardSale.InvoiceNo,
                VoidedSaleNumber = voidedSale.InvoiceNo,
                NonVatSaleNumber = nonVatSale.InvoiceNo,
                StartBoundarySaleNumber = startBoundarySale.InvoiceNo,
                EndBoundarySaleNumber = endBoundarySale.InvoiceNo,
                LegacySaleNumber = legacySale.InvoiceNo,
                MainGrnId = mainGrn.Id,
                MainGrnNumber = mainGrn.GrnNumber,
                CancelledGrnNumber = cancelledGrn.GrnNumber,
                LegacyGrnNumber = legacyGrn.GrnNumber,
                CustomerReturnId = customerReturn.Id,
                CustomerReturnNumber = customerReturn.CreditNoteNo!,
                LegacyCustomerReturnNumber = legacyCustomerReturn.CreditNoteNo!,
                SupplierReturnId = supplierReturn.Id,
                SupplierReturnNumber = supplierReturn.ReturnNumber,
                CancelledSupplierReturnNumber = cancelledSupplierReturn.ReturnNumber,
                LegacySupplierReturnNumber = legacySupplierReturn.ReturnNumber
            };
        }

        private static TaxCategory CreateVatReportTaxCategory(
            string code,
            string name,
            string treatment,
            bool isRateBased,
            int order) => new()
        {
            CategoryCode = code,
            CategoryName = name,
            TreatmentType = treatment,
            IsRateBased = isRateBased,
            IsActive = true,
            DisplayOrder = order
        };

        private static SalesHeader CreateVatReportSale(
            RepositoryTestScenario scenario,
            string invoiceNumber,
            DateTime date,
            bool isVatRegistered,
            decimal taxable,
            decimal vat,
            decimal inclusive,
            decimal standard,
            decimal zero,
            decimal exempt,
            decimal outOfScope,
            params SalesLine[] lines)
        {
            var header = new SalesHeader
            {
                ShiftSessionId = scenario.ShiftSessionId,
                InvoiceNo = invoiceNumber,
                TerminalNo = "T01",
                CashierName = "Test Cashier",
                CustomerName = "Walk-In",
                TransactionDate = date,
                DocumentType = SalesDocumentTypes.Receipt,
                IsVatRegisteredSale = isVatRegistered,
                GrossTotal = inclusive,
                NetTotal = inclusive,
                AmountTendered = inclusive,
                PaymentMethod = "Cash",
                Status = "Completed",
                TaxableAmountTotal = taxable,
                TotalVatAmount = vat,
                StandardRatedAmount = standard,
                ZeroRatedAmount = zero,
                ExemptAmount = exempt,
                OutOfScopeAmount = outOfScope,
                TaxSnapshotStatus = TaxSnapshotStatuses.Complete
            };
            foreach (SalesLine line in lines)
                header.SalesLines.Add(line);
            return header;
        }

        private static SalesLine CreateVatReportSalesLine(
            int variantId,
            int? taxCategoryId,
            int? taxRateId,
            string categoryCode,
            decimal rate,
            decimal taxable,
            decimal vat,
            decimal inclusive) => new()
        {
            ItemVariantId = variantId,
            ItemDescription = $"VAT report {categoryCode}",
            Uom = "JOB",
            ItemTypeSnapshot = ItemTypeCodes.Service,
            Quantity = 1m,
            UnitPrice = inclusive,
            OriginalUnitPrice = inclusive,
            GrossAmount = inclusive,
            LineTotal = inclusive,
            TaxCategoryId = taxCategoryId,
            TaxRateId = taxRateId,
            TaxCategoryCodeSnapshot = categoryCode,
            TaxCodeSnapshot = categoryCode,
            TaxNameSnapshot = categoryCode,
            TaxRatePercentSnapshot = rate,
            IsTaxInclusiveSnapshot = true,
            TaxableAmountSnapshot = taxable,
            VatAmountSnapshot = vat,
            TaxInclusiveAmountSnapshot = inclusive,
            TaxSnapshotStatus = TaxSnapshotStatuses.Complete
        };

        private static GrnHeader CreateVatReportGrn(
            int supplierId,
            string grnNumber,
            string supplierInvoice,
            DateTime date,
            decimal taxable,
            decimal vat,
            decimal productInclusive,
            decimal freight,
            decimal standard,
            decimal zero,
            decimal exempt,
            decimal outOfScope) => new()
        {
            GrnNumber = grnNumber,
            SupplierId = supplierId,
            SupplierInvoiceNo = supplierInvoice,
            InvoiceDate = date,
            ReceivedDate = date,
            DueDate = date.AddDays(30),
            Subtotal = productInclusive,
            FreightAmount = freight,
            TotalVatAmount = vat,
            NetPayable = productInclusive + freight,
            IsTaxInclusive = true,
            TaxableAmountTotal = taxable,
            StandardRatedAmount = standard,
            ZeroRatedAmount = zero,
            ExemptAmount = exempt,
            OutOfScopeAmount = outOfScope,
            FreightTaxSnapshotStatus = TaxSnapshotStatuses.LegacyUnknown,
            TaxSnapshotStatus = TaxSnapshotStatuses.Complete,
            Status = "Posted",
            CreatedBy = "Test",
            PostedBy = "Test"
        };

        private static GrnLine CreateVatReportGrnLine(
            int variantId,
            int batchId,
            int? taxCategoryId,
            int? taxRateId,
            string? categoryCode,
            decimal? rate,
            bool? inclusive,
            decimal quantity,
            decimal taxable,
            decimal vat,
            decimal taxInclusive) => new()
        {
            ItemVariantId = variantId,
            ItemBatchId = batchId,
            ItemCode = "VAT-REPORT-ITEM",
            SkuCode = "VAT-REPORT-SKU",
            Description = "VAT Report Item",
            BatchNo = $"TEST-{categoryCode ?? "LEGACY"}",
            Uom = "PCS",
            ReceivedQty = quantity,
            UnitCost = taxInclusive / quantity,
            LandedCost = taxInclusive / quantity,
            LineTotal = taxInclusive,
            TaxCategoryId = taxCategoryId,
            TaxRateId = taxRateId,
            TaxCategoryCodeSnapshot = categoryCode,
            TaxCodeSnapshot = categoryCode,
            TaxNameSnapshot = categoryCode,
            TaxRatePercentSnapshot = rate,
            IsTaxInclusiveSnapshot = inclusive,
            TaxableAmountSnapshot = taxable,
            VatAmountSnapshot = vat,
            TaxInclusiveAmountSnapshot = taxInclusive,
            TaxSnapshotStatus = TaxSnapshotStatuses.Complete,
            LineStatus = "Posted"
        };

        private static SupplierReturnHeader CreateVatReportSupplierReturn(
            int supplierId,
            int grnHeaderId,
            string number,
            string originalInvoice,
            DateTime date,
            decimal taxable,
            decimal vat,
            decimal inclusive) => new()
        {
            ReturnNumber = number,
            SupplierId = supplierId,
            GrnHeaderId = grnHeaderId,
            OriginalInvoiceNo = originalInvoice,
            ReturnDate = date,
            AuthorizedBy = "Test Manager",
            GrossCredit = inclusive,
            NetCredit = inclusive,
            TaxableAmountTotal = taxable,
            TotalVatAmount = vat,
            StandardRatedAmount = taxable,
            ZeroRatedAmount = 0m,
            ExemptAmount = 0m,
            OutOfScopeAmount = 0m,
            TaxSnapshotStatus = TaxSnapshotStatuses.Complete,
            Status = "Posted",
            CreatedBy = "Test",
            PostedBy = "Test"
        };

        private static SupplierReturnLine CreateVatReportSupplierReturnLine(
            GrnLine source,
            int variantId,
            int batchId,
            int? taxCategoryId,
            int? taxRateId,
            string? categoryCode,
            decimal? rate,
            decimal quantity,
            decimal? taxable,
            decimal? vat,
            decimal? inclusive,
            string status,
            decimal? creditValue = null) => new()
        {
            GrnLineId = source.Id,
            ItemVariantId = variantId,
            ItemBatchId = batchId,
            BatchNo = "TEST-BATCH",
            ReturnQty = quantity,
            HistoricalCost = source.LandedCost,
            CreditValue = creditValue ?? inclusive ?? 0m,
            TaxCategoryId = taxCategoryId,
            TaxRateId = taxRateId,
            TaxCategoryCodeSnapshot = categoryCode,
            TaxCodeSnapshot = categoryCode,
            TaxNameSnapshot = categoryCode,
            TaxRatePercentSnapshot = rate,
            IsTaxInclusiveSnapshot = source.IsTaxInclusiveSnapshot,
            TaxableAmountSnapshot = taxable,
            VatAmountSnapshot = vat,
            TaxInclusiveAmountSnapshot = inclusive,
            OriginalTaxableAmount = source.TaxableAmountSnapshot,
            OriginalVatAmount = source.VatAmountSnapshot,
            OriginalTaxInclusiveAmount = source.TaxInclusiveAmountSnapshot,
            TaxSnapshotStatus = status,
            ReasonCode = "TEST",
            LineStatus = "Posted"
        };

        private sealed class VatReportTestScenario
        {
            public int StandardSaleId { get; init; }
            public string StandardSaleNumber { get; init; } = string.Empty;
            public string VoidedSaleNumber { get; init; } = string.Empty;
            public string NonVatSaleNumber { get; init; } = string.Empty;
            public string StartBoundarySaleNumber { get; init; } = string.Empty;
            public string EndBoundarySaleNumber { get; init; } = string.Empty;
            public string LegacySaleNumber { get; init; } = string.Empty;
            public int MainGrnId { get; init; }
            public string MainGrnNumber { get; init; } = string.Empty;
            public string CancelledGrnNumber { get; init; } = string.Empty;
            public string LegacyGrnNumber { get; init; } = string.Empty;
            public int CustomerReturnId { get; init; }
            public string CustomerReturnNumber { get; init; } = string.Empty;
            public string LegacyCustomerReturnNumber { get; init; } = string.Empty;
            public int SupplierReturnId { get; init; }
            public string SupplierReturnNumber { get; init; } = string.Empty;
            public string CancelledSupplierReturnNumber { get; init; } = string.Empty;
            public string LegacySupplierReturnNumber { get; init; } = string.Empty;
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


        private static void ActiveStockItemCartSavesAndRestores()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var repository = new CashierCartRepository(factory);
            Guid token = Guid.NewGuid();

            repository.SaveActiveAsync(CreateCartRequest(
                    scenario,
                    token,
                    CreateStockCartSnapshot(scenario)))
                .GetAwaiter().GetResult();

            CashierCartSessionDto restored = repository.GetByTokenAsync(token)
                .GetAwaiter().GetResult()
                ?? throw new InvalidOperationException("Saved Stock Item cart was not restored.");

            AssertEqual(CashierCartStatusCodes.Active, restored.Status, "active cart status");
            AssertEqual(1, restored.Lines.Count, "active cart line count");
            AssertEqual(CashierCartLineTypeCodes.StockItem, restored.Lines[0].LineType, "Stock Item cart line type");
            AssertMoney(2m, restored.Lines[0].Quantity, "Stock Item restored quantity");
        }

        private static void ServiceCartSavesAndRestores()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var repository = new CashierCartRepository(factory);
            Guid token = Guid.NewGuid();

            repository.SaveActiveAsync(CreateCartRequest(
                    scenario,
                    token,
                    CreateServiceCartSnapshot(scenario)))
                .GetAwaiter().GetResult();

            CashierCartSessionDto restored = repository.GetByTokenAsync(token)
                .GetAwaiter().GetResult()
                ?? throw new InvalidOperationException("Saved Service cart was not restored.");

            AssertEqual(CashierCartLineTypeCodes.Service, restored.Lines.Single().LineType, "Service cart line type");
            AssertEqual(0, restored.Lines.Single().ItemBatchId, "Service has no batch");
            AssertEqual(ItemTypeCodes.Service, restored.Lines.Single().ItemType, "Service Item Type");
        }

        private static void MixedCartSavesAndRestores()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var repository = new CashierCartRepository(factory);
            Guid token = Guid.NewGuid();

            repository.SaveActiveAsync(CreateCartRequest(
                    scenario,
                    token,
                    CreateStockCartSnapshot(scenario),
                    CreateServiceCartSnapshot(scenario)))
                .GetAwaiter().GetResult();

            CashierCartSessionDto restored = repository.GetByTokenAsync(token)
                .GetAwaiter().GetResult()
                ?? throw new InvalidOperationException("Mixed cart was not restored.");

            AssertEqual(2, restored.Lines.Count, "mixed cart line count");
            AssertEqual(2, restored.ItemCount, "mixed cart item count");
            AssertMoney(3m, restored.TotalQuantity, "mixed cart total quantity");
        }

        private static void CustomerAndWholesaleModeRestore()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var repository = new CashierCartRepository(factory);
            Guid token = Guid.NewGuid();
            CustomerSearchDto customer = new()
            {
                CustomerCode = "WHO-001",
                FullName = "Nimal Perera",
                CompanyName = "Nimal Stores",
                CustomerType = "Wholesale",
                Phone = "0770000000",
                IsDiscountEligible = true
            };

            CashierCartSaveRequest request = CreateCartRequest(
                scenario,
                token,
                CreateStockCartSnapshot(scenario));
            request = new CashierCartSaveRequest
            {
                CartToken = request.CartToken,
                Owner = request.Owner,
                Customer = customer,
                IsWholesaleMode = true,
                InvoiceDiscountAmount = request.InvoiceDiscountAmount,
                GrossTotal = request.GrossTotal,
                TotalDiscount = request.TotalDiscount,
                NetTotal = request.NetTotal,
                Lines = request.Lines
            };

            repository.SaveActiveAsync(request).GetAwaiter().GetResult();
            CashierCartSessionDto restored = repository.GetByTokenAsync(token)
                .GetAwaiter().GetResult()
                ?? throw new InvalidOperationException("Customer cart was not restored.");

            AssertTrue(restored.IsWholesaleMode, "Wholesale mode should restore");
            AssertEqual("WHO-001", restored.Customer?.CustomerCode ?? string.Empty, "customer code snapshot");
            AssertEqual("Nimal Stores", restored.Customer?.DisplayName ?? string.Empty, "customer display snapshot");
        }

        private static void BatchAndSelectedPricingRestore()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var repository = new CashierCartRepository(factory);
            Guid token = Guid.NewGuid();
            CashierCartLineSnapshotDto line = CreateStockCartSnapshot(scenario);
            line.UnitPrice = 1062m;
            line.WholesalePrice = 1062m;
            line.BatchNo = "TEST-BATCH";

            repository.SaveActiveAsync(CreateCartRequest(scenario, token, line))
                .GetAwaiter().GetResult();

            CashierCartLineSnapshotDto restored = repository.GetByTokenAsync(token)
                .GetAwaiter().GetResult()!.Lines.Single();
            AssertEqual(scenario.StockBatchId, restored.ItemBatchId, "restored batch ID");
            AssertEqual("TEST-BATCH", restored.BatchNo, "restored batch number");
            AssertMoney(1062m, restored.UnitPrice, "restored selected price");
        }

        private static void ManualLineDiscountRestores()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var repository = new CashierCartRepository(factory);
            Guid token = Guid.NewGuid();
            CashierCartLineSnapshotDto line = CreateStockCartSnapshot(scenario);
            line.IsManualDiscount = true;
            line.DiscountMode = "Amount";
            line.ManualDiscountAmount = 80m;
            line.DiscountPercentage = 0m;
            line.DiscountReasonCode = "CUSTOMER";
            line.DiscountReasonName = "Customer Discount";

            repository.SaveActiveAsync(CreateCartRequest(scenario, token, line))
                .GetAwaiter().GetResult();

            CashierCartLineSnapshotDto restored = repository.GetByTokenAsync(token)
                .GetAwaiter().GetResult()!.Lines.Single();
            AssertTrue(restored.IsManualDiscount, "manual discount flag");
            AssertEqual("Amount", restored.DiscountMode, "manual discount mode");
            AssertMoney(80m, restored.ManualDiscountAmount, "manual discount amount");
            AssertEqual("CUSTOMER", restored.DiscountReasonCode, "discount reason snapshot");
        }

        private static void InvoiceDiscountRestores()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var repository = new CashierCartRepository(factory);
            Guid token = Guid.NewGuid();
            CashierCartSaveRequest original = CreateCartRequest(
                scenario,
                token,
                CreateStockCartSnapshot(scenario));
            CashierCartSaveRequest request = new()
            {
                CartToken = original.CartToken,
                Owner = original.Owner,
                InvoiceDiscountAmount = 125.50m,
                GrossTotal = original.GrossTotal,
                TotalDiscount = 125.50m,
                NetTotal = original.GrossTotal - 125.50m,
                Lines = original.Lines
            };

            repository.SaveActiveAsync(request).GetAwaiter().GetResult();
            CashierCartSessionDto restored = repository.GetByTokenAsync(token)
                .GetAwaiter().GetResult()!;
            AssertMoney(125.50m, restored.InvoiceDiscountAmount, "restored invoice discount");
            AssertMoney(request.NetTotal, restored.NetTotal, "restored invoice net total");
        }

        private static void PriceOverrideAuditRestores()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var repository = new CashierCartRepository(factory);
            Guid token = Guid.NewGuid();
            DateTime approvedAt = new(2026, 7, 12, 10, 30, 0, DateTimeKind.Utc);
            CashierCartLineSnapshotDto line = CreateStockCartSnapshot(scenario);
            line.IsPriceOverridden = true;
            line.PriceOverrideAmount = 980m;
            line.PriceOverrideApprovedBy = "Manager One";
            line.PriceOverrideApprovedAt = approvedAt;

            repository.SaveActiveAsync(CreateCartRequest(scenario, token, line))
                .GetAwaiter().GetResult();

            CashierCartLineSnapshotDto restored = repository.GetByTokenAsync(token)
                .GetAwaiter().GetResult()!.Lines.Single();
            AssertTrue(restored.IsPriceOverridden, "price override flag");
            AssertMoney(980m, restored.PriceOverrideAmount, "price override amount");
            AssertEqual("Manager One", restored.PriceOverrideApprovedBy, "price override approver");
            AssertEqual<DateTime?>(approvedAt, restored.PriceOverrideApprovedAt, "price override approval time");
        }

        private static void GiftVoucherAndFreeIssueStateRestores()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var repository = new CashierCartRepository(factory);
            Guid token = Guid.NewGuid();
            CashierCartLineSnapshotDto freeLine = CreateStockCartSnapshot(scenario);
            freeLine.IsFreeItem = true;
            freeLine.FreeIssueRuleId = 25;
            freeLine.FreeIssueRuleName = "Buy One Get One";
            freeLine.FreeApprovedBy = "Manager One";
            freeLine.IsSupplierRecoverable = true;
            freeLine.SupplierPromotionReference = "PROMO-2026";

            CashierCartLineSnapshotDto voucherLine = new()
            {
                LineType = CashierCartLineTypeCodes.GiftVoucherSale,
                Description = "Gift Voucher GV-001",
                Quantity = 1m,
                UnitPrice = 5000m,
                TaxInclusiveAmount = 5000m,
                IsGiftVoucherSale = true,
                GiftVoucherId = 77,
                GiftVoucherNo = "GV-001",
                GiftVoucherBarcode = "GV-BAR-001"
            };

            repository.SaveActiveAsync(CreateCartRequest(scenario, token, freeLine, voucherLine))
                .GetAwaiter().GetResult();

            CashierCartSessionDto restored = repository.GetByTokenAsync(token)
                .GetAwaiter().GetResult()!;
            CashierCartLineSnapshotDto restoredFree = restored.Lines.Single(l => l.IsFreeItem);
            CashierCartLineSnapshotDto restoredVoucher = restored.Lines.Single(l => l.IsGiftVoucherSale);
            AssertEqual(25, restoredFree.FreeIssueRuleId, "Free Issue rule snapshot");
            AssertEqual("PROMO-2026", restoredFree.SupplierPromotionReference, "supplier promotion snapshot");
            AssertEqual(77, restoredVoucher.GiftVoucherId, "Gift Voucher ID snapshot");
            AssertEqual("GV-BAR-001", restoredVoucher.GiftVoucherBarcode, "Gift Voucher barcode snapshot");
        }

        private static void CartPersistenceExcludesPaymentDrafts()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var repository = new CashierCartRepository(factory);
            Guid token = Guid.NewGuid();

            repository.SaveActiveAsync(CreateCartRequest(
                    scenario,
                    token,
                    CreateStockCartSnapshot(scenario)))
                .GetAwaiter().GetResult();

            using AppDbContext context = factory.CreateDbContext();
            CashierCartLine storedLine = context.CashierCartLines.AsNoTracking().Single();
            AssertFalse(storedLine.SnapshotJson.Contains("CardLastDigits", StringComparison.OrdinalIgnoreCase), "cart snapshot must not store card data");
            AssertFalse(storedLine.SnapshotJson.Contains("Cheque", StringComparison.OrdinalIgnoreCase), "cart snapshot must not store cheque data");
            AssertEqual(0, context.SalesPayments.Count(), "draft cart must not create SalesPayment rows");
        }

        private static void ActiveCartSurvivesContextRestart()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            Guid token = Guid.NewGuid();
            new CashierCartRepository(factory)
                .SaveActiveAsync(CreateCartRequest(scenario, token, CreateStockCartSnapshot(scenario)))
                .GetAwaiter().GetResult();

            var reopenedRepository = new CashierCartRepository(factory);
            CashierCartSessionDto restored = reopenedRepository.GetActiveAsync(CreateCartOwner(scenario))
                .GetAwaiter().GetResult()
                ?? throw new InvalidOperationException("Active cart did not survive context restart.");

            AssertEqual(token, restored.CartToken, "restarted active cart token");
            AssertEqual(1, restored.Lines.Count, "restarted active cart lines");
        }

        private static void SuspendAndRecallLifecycleWorks()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var repository = new CashierCartRepository(factory);
            Guid token = Guid.NewGuid();
            CashierCartOwnerDto owner = CreateCartOwner(scenario);

            repository.SaveActiveAsync(CreateCartRequest(scenario, token, CreateStockCartSnapshot(scenario)))
                .GetAwaiter().GetResult();
            CashierCartSessionDto held = repository.SuspendAsync(token, owner)
                .GetAwaiter().GetResult();
            AssertEqual(CashierCartStatusCodes.Held, held.Status, "held cart status");
            AssertEqual(1, repository.ListHeldAsync(owner).GetAwaiter().GetResult().Count, "held cart list count");

            CashierCartSessionDto recalled = repository.RecallAsync(held.Id, owner)
                .GetAwaiter().GetResult();
            AssertEqual(CashierCartStatusCodes.Active, recalled.Status, "recalled cart status");
            AssertEqual(1, recalled.RecallCount, "recall count");
        }

        private static void CartOwnershipIsIsolated()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var repository = new CashierCartRepository(factory);
            Guid token = Guid.NewGuid();
            repository.SaveActiveAsync(CreateCartRequest(scenario, token, CreateStockCartSnapshot(scenario)))
                .GetAwaiter().GetResult();

            CashierCartOwnerDto otherOwner = new()
            {
                ShiftSessionId = scenario.ShiftSessionId,
                TerminalNo = "T02",
                CashierName = "Other Cashier"
            };

            AssertEqual(0, repository.ListHeldAsync(otherOwner).GetAwaiter().GetResult().Count, "other owner held carts");
            AssertThrows(
                () => repository.SuspendAsync(token, otherOwner).GetAwaiter().GetResult(),
                "different terminal, shift or cashier");
        }

        private static void HeldCartCanBeRecalledOnlyOnce()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var repository = new CashierCartRepository(factory);
            Guid token = Guid.NewGuid();
            CashierCartOwnerDto owner = CreateCartOwner(scenario);
            repository.SaveActiveAsync(CreateCartRequest(scenario, token, CreateStockCartSnapshot(scenario)))
                .GetAwaiter().GetResult();
            CashierCartSessionDto held = repository.SuspendAsync(token, owner).GetAwaiter().GetResult();
            repository.RecallAsync(held.Id, owner).GetAwaiter().GetResult();

            AssertThrows(
                () => repository.RecallAsync(held.Id, owner).GetAwaiter().GetResult(),
                "current active cart");
        }

        private static void CancelledCartAuditsWithoutFinancialPosting()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var repository = new CashierCartRepository(factory);
            Guid token = Guid.NewGuid();
            repository.SaveActiveAsync(CreateCartRequest(scenario, token, CreateStockCartSnapshot(scenario)))
                .GetAwaiter().GetResult();

            CashierCartSessionDto cancelled = repository.CancelAsync(
                    token,
                    CreateCartOwner(scenario),
                    CashierCartCancellationReasons.CustomerChangedMind,
                    "Customer cancelled before payment.")
                .GetAwaiter().GetResult();

            AssertEqual(CashierCartStatusCodes.Cancelled, cancelled.Status, "cancelled cart status");
            AssertEqual(CashierCartCancellationReasons.CustomerChangedMind, cancelled.CancellationReasonCode, "cancellation reason code");
            AssertContains(cancelled.CancellationReasonText, "before payment", "cancellation note");

            using AppDbContext context = factory.CreateDbContext();
            AssertEqual(0, context.SalesHeaders.Count(), "cancelled cart invoices");
            AssertEqual(0, context.SalesPayments.Count(), "cancelled cart payments");
            AssertEqual(0, context.InventoryTransactions.Count(), "cancelled cart inventory movements");
        }

        private static void CompletedAndCancelledCartsCannotBeRecalled()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var cartRepository = new CashierCartRepository(factory);
            var salesRepository = new SalesRepository(factory);
            CashierCartOwnerDto owner = CreateCartOwner(scenario);

            Guid cancelledToken = Guid.NewGuid();
            CashierCartSessionDto cancelledSaved = cartRepository.SaveActiveAsync(
                    CreateCartRequest(scenario, cancelledToken, CreateStockCartSnapshot(scenario)))
                .GetAwaiter().GetResult();
            cartRepository.SuspendAsync(cancelledToken, owner).GetAwaiter().GetResult();
            cartRepository.CancelHeldAsync(
                    cancelledSaved.Id,
                    owner,
                    CashierCartCancellationReasons.WrongItems,
                    "Wrong item selected.")
                .GetAwaiter().GetResult();
            AssertThrows(
                () => cartRepository.RecallAsync(cancelledSaved.Id, owner).GetAwaiter().GetResult(),
                "no longer held");

            Guid completedToken = Guid.NewGuid();
            CashierCartSessionDto completedSaved = cartRepository.SaveActiveAsync(
                    CreateCartRequest(scenario, completedToken, CreateStockCartSnapshot(scenario, 1m)))
                .GetAwaiter().GetResult();
            CompleteStockSale(factory, salesRepository, scenario, completedToken, 1m);
            AssertThrows(
                () => cartRepository.RecallAsync(completedSaved.Id, owner).GetAwaiter().GetResult(),
                "no longer held");
        }

        private static void CheckoutTokenIsIdempotent()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var cartRepository = new CashierCartRepository(factory);
            var salesRepository = new SalesRepository(factory);
            Guid token = Guid.NewGuid();
            cartRepository.SaveActiveAsync(CreateCartRequest(
                    scenario,
                    token,
                    CreateStockCartSnapshot(scenario, 1m)))
                .GetAwaiter().GetResult();

            SalesHeader first = CompleteStockSale(factory, salesRepository, scenario, token, 1m);
            SalesHeader second = CompleteStockSale(factory, salesRepository, scenario, token, 1m);

            AssertEqual(first.Id, second.Id, "idempotent SalesHeader ID");
            AssertEqual(first.InvoiceNo, second.InvoiceNo, "idempotent invoice number");
            using AppDbContext context = factory.CreateDbContext();
            AssertEqual(1, context.SalesHeaders.Count(), "idempotent sale count");
        }

        private static void DuplicateCheckoutCreatesOneStockAndPaymentEffect()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var cartRepository = new CashierCartRepository(factory);
            var salesRepository = new SalesRepository(factory);
            Guid token = Guid.NewGuid();
            cartRepository.SaveActiveAsync(CreateCartRequest(
                    scenario,
                    token,
                    CreateStockCartSnapshot(scenario, 1m)))
                .GetAwaiter().GetResult();

            CompleteStockSale(factory, salesRepository, scenario, token, 1m);
            CompleteStockSale(factory, salesRepository, scenario, token, 1m);

            using AppDbContext context = factory.CreateDbContext();
            ItemBatch batch = context.ItemBatches.Single(b => b.Id == scenario.StockBatchId);
            AssertMoney(4m, batch.CurrentStock, "single stock deduction after duplicate checkout");
            AssertEqual(1, context.SalesPayments.Count(), "single payment after duplicate checkout");
            AssertEqual(1, context.InventoryTransactions.Count(t => t.TransactionType == "SALE"), "single SALE inventory transaction");
        }

        private static void FailedCheckoutLeavesCartRecoverable()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var cartRepository = new CashierCartRepository(factory);
            var salesRepository = new SalesRepository(factory);
            Guid token = Guid.NewGuid();
            cartRepository.SaveActiveAsync(CreateCartRequest(
                    scenario,
                    token,
                    CreateStockCartSnapshot(scenario, 2m)))
                .GetAwaiter().GetResult();

            using (AppDbContext context = factory.CreateDbContext())
            {
                ItemBatch batch = context.ItemBatches.Single(b => b.Id == scenario.StockBatchId);
                batch.CurrentStock = 1m;
                context.SaveChanges();
            }

            AssertThrows(
                () => CompleteStockSale(factory, salesRepository, scenario, token, 2m),
                "stock");

            CashierCartSessionDto restored = cartRepository.GetByTokenAsync(token)
                .GetAwaiter().GetResult()!;
            AssertEqual(CashierCartStatusCodes.Active, restored.Status, "failed checkout cart remains active");
            AssertEqual(2m, restored.Lines.Single().Quantity, "failed checkout cart quantity remains recoverable");
            using AppDbContext verify = factory.CreateDbContext();
            AssertEqual(0, verify.SalesHeaders.Count(), "failed checkout creates no sale");
        }

        private static void SuccessfulCheckoutCompletesCart()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var cartRepository = new CashierCartRepository(factory);
            var salesRepository = new SalesRepository(factory);
            Guid token = Guid.NewGuid();
            cartRepository.SaveActiveAsync(CreateCartRequest(
                    scenario,
                    token,
                    CreateStockCartSnapshot(scenario, 1m)))
                .GetAwaiter().GetResult();

            SalesHeader sale = CompleteStockSale(factory, salesRepository, scenario, token, 1m);
            CashierCartSessionDto completed = cartRepository.GetByTokenAsync(token)
                .GetAwaiter().GetResult()!;
            AssertEqual(CashierCartStatusCodes.Completed, completed.Status, "completed cart status");
            AssertEqual(sale.Id, completed.SalesHeaderId.GetValueOrDefault(), "completed cart sale link");
            AssertTrue(completed.CompletedAtUtc.HasValue, "completed cart timestamp");
        }

        private static void ManagerApprovalPreservesCashierSession()
        {
            using var factory = new RepositoryTestDbContextFactory();
            var users = new UserRepository(factory);
            string cashierHash = SecurityHelper.HashData("cashier-pass", out string cashierSalt);
            string managerHash = SecurityHelper.HashData("manager-pass", out string managerSalt);
            users.AddAsync(new User
            {
                FirstName = "Test",
                LastName = "Cashier",
                Username = "cashier1",
                PasswordHash = cashierHash,
                PasswordSalt = cashierSalt,
                Role = UserRole.Cashier,
                IsActive = true
            }).GetAwaiter().GetResult();
            users.AddAsync(new User
            {
                FirstName = "Test",
                LastName = "Manager",
                Username = "manager1",
                PasswordHash = managerHash,
                PasswordSalt = managerSalt,
                Role = UserRole.Manager,
                IsActive = true
            }).GetAwaiter().GetResult();

            var auth = new AuthService(users);
            var login = auth.LoginAsync("cashier1", "cashier-pass", "TEST")
                .GetAwaiter().GetResult();
            AssertTrue(login.Success, "cashier login");

            ManagerAuthorizationResult approval = auth.ValidateManagerCredentialsAsync(
                    "manager1",
                    "manager-pass",
                    "TEST-APPROVAL")
                .GetAwaiter().GetResult();

            AssertTrue(approval.Success, "manager approval result");
            AssertEqual("manager1", approval.Username, "approving manager username");
            AssertEqual("cashier1", auth.CurrentUser?.Username ?? string.Empty, "active cashier remains unchanged");
        }

        private static void CompletedPaymentAuditDetailsPersist()
        {
            using var factory = new RepositoryTestDbContextFactory();
            RepositoryTestScenario scenario = SeedRepositoryTestScenario(factory);
            var cartRepository = new CashierCartRepository(factory);
            var salesRepository = new SalesRepository(factory);
            Guid token = Guid.NewGuid();
            cartRepository.SaveActiveAsync(CreateCartRequest(
                    scenario,
                    token,
                    CreateStockCartSnapshot(scenario, 1m)))
                .GetAwaiter().GetResult();

            SalesHeader header = CreateRepositoryTestHeader(scenario.ShiftSessionId, 0m);
            List<SalesLine> lines = new()
            {
                CreateRepositoryTestLine(
                    scenario.StockVariantId,
                    scenario.StockBatchId,
                    scenario.StockSku,
                    "Test Stock Item",
                    1m,
                    1180m)
            };
            List<SalesPayment> payments = new()
            {
                new SalesPayment
                {
                    PaymentType = "Card",
                    Amount = 1180m,
                    TenderedAmount = 1180m,
                    ChangeAmount = 0m,
                    CardLastDigits = "123456",
                    BankOrCardType = "Visa",
                    ReferenceNo = "AUTH-778899",
                    EnteredBy = "Test Cashier",
                    TerminalNo = "T01"
                }
            };

            salesRepository.ProcessCheckoutAsync(header, lines, payments, token)
                .GetAwaiter().GetResult();

            using AppDbContext context = factory.CreateDbContext();
            SalesPayment saved = context.SalesPayments.AsNoTracking().Single();
            AssertEqual("Card", saved.PaymentType, "saved card payment type");
            AssertMoney(1180m, saved.TenderedAmount, "saved card tendered amount");
            AssertMoney(0m, saved.ChangeAmount, "saved card change");
            AssertEqual("123456", saved.CardLastDigits, "saved card last six digits");
            AssertEqual("AUTH-778899", saved.ReferenceNo, "saved card reference");
            AssertEqual("Visa", saved.BankOrCardType, "saved card provider");
            AssertEqual("Test Cashier", saved.EnteredBy, "saved payment cashier");
            AssertEqual("T01", saved.TerminalNo, "saved payment terminal");

            var explorer = new MasterSalesAnalyticsRepository(factory);
            SaleReceiptDetailsDto detail = explorer.GetSaleReceiptDetailsAsync(saved.SalesHeaderId)
                .GetAwaiter().GetResult()
                ?? throw new InvalidOperationException("Sales Explorer payment detail was not loaded.");
            SaleReceiptPaymentDto visiblePayment = detail.Payments.Single();
            AssertEqual("123456", visiblePayment.CardLastDigits, "Sales Explorer card digits");
            AssertContains(visiblePayment.Details, "AUTH-778899", "Sales Explorer payment reference");
        }

        private static CashierCartOwnerDto CreateCartOwner(RepositoryTestScenario scenario)
        {
            return new CashierCartOwnerDto
            {
                ShiftSessionId = scenario.ShiftSessionId,
                TerminalNo = "T01",
                CashierName = "Test Cashier"
            };
        }

        private static CashierCartSaveRequest CreateCartRequest(
            RepositoryTestScenario scenario,
            Guid token,
            params CashierCartLineSnapshotDto[] lines)
        {
            decimal gross = Math.Round(lines.Sum(l => l.UnitPrice * l.Quantity), 2);
            decimal net = Math.Round(lines.Sum(l => l.TaxInclusiveAmount > 0m
                ? l.TaxInclusiveAmount
                : l.UnitPrice * l.Quantity), 2);

            return new CashierCartSaveRequest
            {
                CartToken = token,
                Owner = CreateCartOwner(scenario),
                GrossTotal = gross,
                TotalDiscount = Math.Max(0m, gross - net),
                NetTotal = net,
                Lines = lines
            };
        }

        private static CashierCartLineSnapshotDto CreateStockCartSnapshot(
            RepositoryTestScenario scenario,
            decimal quantity = 2m)
        {
            return new CashierCartLineSnapshotDto
            {
                LineType = CashierCartLineTypeCodes.StockItem,
                ItemVariantId = scenario.StockVariantId,
                ItemBatchId = scenario.StockBatchId,
                ItemCode = "TEST-STOCK",
                SkuCode = scenario.StockSku,
                Barcode = "TEST-STOCK-BARCODE",
                Description = "Test Stock Item",
                VariantDescription = "Standard",
                Uom = "PCS",
                ItemType = ItemTypeCodes.StockItem,
                TaxProfile = SalesStandardProfile(),
                BatchNo = "TEST-BATCH",
                AvailableBatchStock = 5m,
                CostPrice = 600m,
                RetailPrice = 1180m,
                WholesalePrice = 1062m,
                MinimumPrice = 600m,
                UnitPrice = 1180m,
                Quantity = quantity,
                OriginalUnitPrice = 1180m,
                TaxableAmount = Math.Round((1180m * quantity) / 1.18m, 2),
                VatAmount = Math.Round((1180m * quantity) - ((1180m * quantity) / 1.18m), 2),
                TaxInclusiveAmount = Math.Round(1180m * quantity, 2)
            };
        }

        private static CashierCartLineSnapshotDto CreateServiceCartSnapshot(
            RepositoryTestScenario scenario)
        {
            return new CashierCartLineSnapshotDto
            {
                LineType = CashierCartLineTypeCodes.Service,
                ItemVariantId = scenario.ServiceVariantId,
                ItemBatchId = 0,
                ItemCode = "TEST-SERVICE",
                SkuCode = scenario.ServiceSku,
                Barcode = scenario.ServiceBarcode,
                Description = "Installation Service",
                VariantDescription = "Standard",
                Uom = "JOB",
                ItemType = ItemTypeCodes.Service,
                TaxProfile = SalesStandardProfile(),
                CostPrice = 400m,
                RetailPrice = 1180m,
                WholesalePrice = 1062m,
                MinimumPrice = 400m,
                UnitPrice = 1180m,
                Quantity = 1m,
                OriginalUnitPrice = 1180m,
                TaxableAmount = 1000m,
                VatAmount = 180m,
                TaxInclusiveAmount = 1180m
            };
        }

        private static SalesHeader CompleteStockSale(
            RepositoryTestDbContextFactory factory,
            SalesRepository repository,
            RepositoryTestScenario scenario,
            Guid token,
            decimal quantity)
        {
            decimal payable = Math.Round(quantity * 1180m, 2);
            SalesHeader header = CreateRepositoryTestHeader(
                scenario.ShiftSessionId,
                payable);
            List<SalesLine> lines = new()
            {
                CreateRepositoryTestLine(
                    scenario.StockVariantId,
                    scenario.StockBatchId,
                    scenario.StockSku,
                    "Test Stock Item",
                    quantity,
                    1180m)
            };
            List<SalesPayment> payments = new()
            {
                CreateCashPayment(payable)
            };

            return repository.ProcessCheckoutAsync(header, lines, payments, token)
                .GetAwaiter().GetResult();
        }

        private static void AssertEqual<T>(
            T expected,
            T actual,
            string label)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    $"{label}: expected {expected}, actual {actual}.");
            }
        }

        private static void AssertTrue(bool condition, string label)
        {
            if (!condition)
                throw new InvalidOperationException($"{label}: expected true.");
        }

        private static void AssertFalse(bool condition, string label)
        {
            if (condition)
                throw new InvalidOperationException($"{label}: expected false.");
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
