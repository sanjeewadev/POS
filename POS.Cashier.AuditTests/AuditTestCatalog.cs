namespace POS.Cashier.AuditTests;

internal static class AuditTestCatalog
{
    public static IReadOnlyList<AuditTestCase> Create() => new AuditTestCase[]
    {
        new("Migration from empty creates current Cashier schema", AuditGroups.Migration, MigrationAuditTests.MigrationFromEmptyCreatesCurrentSchemaAsync),
        new("Approved baseline database upgrades safely", AuditGroups.Migration, MigrationAuditTests.ApprovedBaselineUpgradesSafelyAsync),

        new("Concurrent same-token checkout creates one financial effect", AuditGroups.Concurrency, CashierConcurrencyAuditTests.ConcurrentSameTokenCheckoutCreatesOneEffectAsync),
        new("Concurrent last-stock checkout never creates negative stock", AuditGroups.Concurrency, CashierConcurrencyAuditTests.ConcurrentLastStockCheckoutIsSafeAsync),
        new("Concurrent Gift Voucher redemption succeeds only once", AuditGroups.Concurrency, CashierConcurrencyAuditTests.ConcurrentGiftVoucherRedemptionIsOneTimeAsync),
        new("Concurrent customer-credit checkout cannot exceed limit", AuditGroups.Concurrency, CashierConcurrencyAuditTests.ConcurrentCustomerCreditCannotExceedLimitAsync),
        new("Concurrent customer return cannot over-return", AuditGroups.Concurrency, CashierConcurrencyAuditTests.ConcurrentCustomerReturnCannotOverReturnAsync),
        new("Checkout racing shift close has one valid serial outcome", AuditGroups.Concurrency, CashierConcurrencyAuditTests.CheckoutAndShiftCloseRemainConsistentAsync),

        new("Checkout failure after header staging rolls back everything", AuditGroups.Transaction, CashierControlAuditTests.CheckoutFailureAfterHeaderStagingRollsBackAsync),
        new("Card sale requires a final repository reference", AuditGroups.Control, CashierControlAuditTests.CardSaleRequiresRepositoryReferenceAsync),
        new("Cheque sale requires a final repository reference", AuditGroups.Control, CashierControlAuditTests.ChequeSaleRequiresRepositoryReferenceAsync),
        new("Checkout rejects a stale catalogue price", AuditGroups.Control, CashierControlAuditTests.CheckoutRejectsStaleCataloguePriceAsync),
        new("Price override validates active manager or administrator", AuditGroups.Control, CashierControlAuditTests.PriceOverrideValidatesApproverRoleAsync),
        new("Price override accepts an active manager", AuditGroups.Control, CashierControlAuditTests.PriceOverrideAcceptsActiveManagerAsync),
        new("Float balance excludes sales cash and refunds", AuditGroups.Control, CashierControlAuditTests.FloatBalanceExcludesOperationalCashAsync),
        new("Float Out cannot consume sales cash", AuditGroups.Control, CashierControlAuditTests.FloatOutCannotConsumeSalesCashAsync),
        new("Disabled users cannot authenticate", AuditGroups.Control, CashierControlAuditTests.DisabledUserCannotAuthenticateAsync),
        new("Zero-opening shift and one-open-shift rule are enforced", AuditGroups.Control, CashierControlAuditTests.ZeroOpeningShiftAndSingleOpenShiftAsync),

        new("Paid In and Paid Out UI do not require manager password", AuditGroups.SourcePolicy, SourcePolicyAuditTests.PaidInAndPaidOutDoNotRequireManagerPasswordAsync),
        new("Below-minimum New Price captures manager identity", AuditGroups.SourcePolicy, SourcePolicyAuditTests.PriceOverrideUiCapturesManagerIdentityAsync),
        new("Checkout failure path writes a technical log", AuditGroups.SourcePolicy, SourcePolicyAuditTests.CheckoutFailurePathWritesTechnicalLogAsync),
        new("Cashier startup enforces licences before login", AuditGroups.SourcePolicy, SourcePolicyAuditTests.StartupEnforcesLicencesBeforeLoginAsync),
        new("Shift restoration preserves original cashier ownership", AuditGroups.SourcePolicy, SourcePolicyAuditTests.ShiftRestorationPreservesCashierOwnershipAsync),
        new("Sale commits before receipt printer and drawer actions", AuditGroups.SourcePolicy, SourcePolicyAuditTests.SaleCommitsBeforeHardwareActionsAsync),
        new("Live POS database path is never an audit database", AuditGroups.SourcePolicy, SourcePolicyAuditTests.LiveDatabasePathIsExcludedAsync)
    };
}
