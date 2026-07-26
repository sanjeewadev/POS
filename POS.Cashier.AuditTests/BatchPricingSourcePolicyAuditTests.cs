namespace POS.Cashier.AuditTests;

internal static class BatchPricingSourcePolicyAuditTests
{
    public static Task BatchPricingFoundationIsCentralizedAndTransactionSafeAsync()
    {
        string batchModel = Read("POS.Core", "Models", "ItemBatch.cs");
        string saleLineModel = Read("POS.Core", "Models", "SalesLine.cs");
        string resolver = Read(
            "POS.Core",
            "Services",
            "Pricing",
            "EffectiveSellingPriceResolver.cs");
        string itemRepository = Read(
            "POS.Core",
            "Repositories",
            "ItemMasterRepository.cs");
        string salesRepository = Read(
            "POS.Core",
            "Repositories",
            "SalesRepository.cs");
        string pricingRepository = Read(
            "POS.Core",
            "Repositories",
            "PriceManagementRepository.cs");
        string grnRepository = Read(
            "POS.Core",
            "Repositories",
            "GrnRepository.cs");
        string cart = Read(
            "POS.Cashier.UI",
            "Models",
            "CartItem.cs");
        string cartLifecycle = Read(
            "POS.Cashier.UI",
            "ViewModels",
            "SalesViewModel.CartLifecycle.cs");

        AuditAssert.Contains(
            batchModel,
            "HasSellingPriceOverride",
            "explicit batch override state");
        AuditAssert.Contains(
            saleLineModel,
            "CataloguePriceSourceSnapshot",
            "completed-sale catalogue source snapshot");
        AuditAssert.Contains(
            resolver,
            "public static class EffectiveSellingPriceResolver",
            "central effective-price resolver");
        AuditAssert.Contains(
            resolver,
            "SellingPriceSourceCodes.BatchOverride",
            "batch override source authority");
        AuditAssert.Contains(
            resolver,
            "IsOverrideEligible",
            "batch override eligibility guard");
        AuditAssert.Contains(
            resolver,
            "ValidateActiveOverridesAgainstMasterBounds",
            "master boundary safety for active overrides");
        AuditAssert.Contains(
            itemRepository,
            "BuildCashierBatchDto",
            "Cashier exact-batch mapping");
        AuditAssert.Contains(
            itemRepository,
            "EffectiveSellingPriceResolver.Resolve",
            "Cashier lookup uses central resolution");
        AuditAssert.Contains(
            salesRepository,
            "Refresh the cart before checkout",
            "stale catalogue price rejection");
        AuditAssert.Contains(
            salesRepository,
            "CataloguePriceSourceSnapshot",
            "checkout source persistence");
        AuditAssert.Contains(
            pricingRepository,
            "SynchronizeMasterMirror",
            "automatic nonoverride mirror synchronization");
        AuditAssert.Contains(
            pricingRepository,
            "SetBatchPriceOverrideAsync",
            "explicit batch override save authority");
        AuditAssert.Contains(
            pricingRepository,
            "RemoveBatchPriceOverrideAsync",
            "explicit batch override removal authority");
        AuditAssert.Contains(
            pricingRepository,
            "EffectiveSellingPriceResolver.ValidateOverride",
            "batch override validation remains centralized");
        AuditAssert.False(
            pricingRepository.Contains(
                "Batch selling-price override editing is not available",
                StringComparison.Ordinal),
            "The Patch 1 temporary batch-editing prohibition remains after Patch 2.");
        AuditAssert.Contains(
            grnRepository,
            "SynchronizeMasterMirror",
            "GRN preserves or synchronizes through the central authority");
        AuditAssert.Contains(
            cart,
            "CataloguePriceSource",
            "cart price-source state");
        AuditAssert.Contains(
            cartLifecycle,
            "CataloguePriceSource = item.CataloguePriceSource",
            "cart persistence preserves price source");

        return Task.CompletedTask;
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(
            Path.Combine(
                new[] { AuditPaths.RepositoryRoot }
                    .Concat(segments)
                    .ToArray()));
}
