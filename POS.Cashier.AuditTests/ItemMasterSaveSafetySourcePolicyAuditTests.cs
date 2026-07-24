using POS.Core.Models;
using POS.Core.Utilities;

namespace POS.Cashier.AuditTests;

internal static class ItemMasterSaveSafetySourcePolicyAuditTests
{
    public static Task ItemMasterMatrixAndSaveFailuresAreControlledAsync()
    {
        VerifyItemCodeAndMatrixInvalidation();
        VerifyRepositoryPreflightAndIdentityRecovery();
        VerifyUsefulFailurePresentationAndLogging();
        VerifyIdentityPolicyBehavior();

        return Task.CompletedTask;
    }

    private static void VerifyItemCodeAndMatrixInvalidation()
    {
        string viewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "ItemMasterViewModel.cs");

        AuditAssert.Contains(
            viewModel,
            "private string _generatedVariantItemCode = string.Empty;",
            "generated matrix item-code snapshot");
        AuditAssert.Contains(
            viewModel,
            "partial void OnItemPrefixChanged(string value)",
            "item prefix change hook");
        AuditAssert.Contains(
            viewModel,
            "partial void OnItemSuffixChanged(string value)",
            "item suffix change hook");
        AuditAssert.Contains(
            viewModel,
            "Item code changed. Generated variants were cleared.",
            "item-code matrix invalidation message");
        AuditAssert.Contains(
            viewModel,
            "Matrix properties changed. Generate variants again before saving.",
            "matrix-property invalidation");
        AuditAssert.Contains(
            viewModel,
            "ItemVariantIdentityPolicy.BuildMisalignmentMessage(",
            "new-item stale matrix save guard");
    }

    private static void VerifyRepositoryPreflightAndIdentityRecovery()
    {
        string repository = Read(
            "POS.Core",
            "Repositories",
            "ItemMasterRepository.cs");

        AuditAssert.Contains(
            repository,
            "ValidateItemCodeAgainstDatabaseAsync(",
            "repository item-code preflight");
        AuditAssert.Contains(
            repository,
            "foreach (var variant in variants)",
            "new-item variant preflight loop");
        AuditAssert.Contains(
            repository,
            "ValidateSkuAndBarcodeAgainstDatabaseAsync(",
            "repository SKU and barcode preflight");
        AuditAssert.Contains(
            repository,
            "SubmittedIdentitySnapshot.Capture(parent, variants)",
            "submitted identity snapshot");
        AuditAssert.Contains(
            repository,
            "identitySnapshot.Restore(parent, variants);",
            "rollback identity restoration");
        AuditAssert.Contains(
            repository,
            "CreatePersistedParent(parent, now)",
            "submitted parent graph is not directly tracked");
        AuditAssert.Contains(
            repository,
            "var persistedVariant = new ItemVariant",
            "submitted variant graph is not directly tracked");
        AuditAssert.Contains(
            repository,
            "submittedVariant.Id = persistedVariant.Id;",
            "successful variant identity propagation");
        AuditAssert.Contains(
            repository,
            "ItemMasterSaveFailureFormatter.GetUserMessage(ex)",
            "database exception translation");
    }

    private static void VerifyUsefulFailurePresentationAndLogging()
    {
        string viewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "ItemMasterViewModel.cs");

        AuditAssert.Contains(
            viewModel,
            "ResetTransientNewItemIdentities();",
            "defensive transient identity reset");
        AuditAssert.Contains(
            viewModel,
            "Save Item Master item",
            "Item Master save technical log operation");
        AuditAssert.Contains(
            viewModel,
            "No item data was committed. The form has been kept for correction.",
            "failed-save retry guidance");
        AuditAssert.Contains(
            viewModel,
            "ItemMasterSaveFailureFormatter.GetUserMessage(ex)",
            "friendly failure message formatter");
        AuditAssert.False(
            viewModel.Contains("mapping.AttributeGroup = null!;", StringComparison.Ordinal),
            "failed Item Master save must preserve matrix navigation values");
        AuditAssert.False(
            viewModel.Contains("supplier.Supplier = null!;", StringComparison.Ordinal),
            "failed Item Master save must preserve supplier navigation values");

        string formatter = Read(
            "POS.Core",
            "Utilities",
            "ItemMasterSaveFailureFormatter.cs");

        AuditAssert.Contains(
            formatter,
            "IX_ItemVariants_SkuCode",
            "SQL Server duplicate SKU translation");
        AuditAssert.Contains(
            formatter,
            "IX_ItemVariants_Barcode",
            "SQL Server duplicate barcode translation");
        AuditAssert.Contains(
            formatter,
            "ItemBatches.InternalBatchBarcode",
            "batch barcode collision translation");

        string duplicateSkuMessage =
            ItemMasterSaveFailureFormatter.GetUserMessage(
                new Exception(
                    "An error occurred while saving the entity changes.",
                    new Exception(
                        "Cannot insert duplicate key row with unique index 'IX_ItemVariants_SkuCode'.")));

        AuditAssert.Contains(
            duplicateSkuMessage,
            "generated SKUs already exist",
            "friendly duplicate SKU message");
    }

    private static void VerifyIdentityPolicyBehavior()
    {
        var standard = new ItemVariant
        {
            SkuCode = "ITEM-001",
            VariantDescription = "Standard"
        };

        var matrix = new ItemVariant
        {
            SkuCode = "ITEM-001-RED1",
            VariantDescription = "Red"
        };

        var stale = new ItemVariant
        {
            SkuCode = "ITEM-000-RED1",
            VariantDescription = "Red"
        };

        AuditAssert.True(
            ItemVariantIdentityPolicy.IsSkuAligned("ITEM-001", standard),
            "standard SKU alignment");
        AuditAssert.True(
            ItemVariantIdentityPolicy.IsSkuAligned("ITEM-001", matrix),
            "matrix SKU alignment");
        AuditAssert.False(
            ItemVariantIdentityPolicy.IsSkuAligned("ITEM-001", stale),
            "stale matrix SKU detection");

        string tests = Read(
            "POS.Core.CalculationTests",
            "ItemMasterSaveSafetyTests.cs");

        AuditAssert.Contains(
            tests,
            "FailedNewItemSaveCanBeCorrectedAndRetried",
            "failed-save retry regression test");
        AuditAssert.Contains(
            tests,
            "NewItemRejectsDuplicateSkuBeforeParentInsert",
            "duplicate SKU rollback regression test");
        AuditAssert.Contains(
            tests,
            "NewItemRejectsDuplicateBarcodeBeforeParentInsert",
            "duplicate barcode rollback regression test");
        AuditAssert.Contains(
            tests,
            "NewItemRejectsBatchBarcodeBeforeParentInsert",
            "batch barcode rollback regression test");
        AuditAssert.Contains(
            tests,
            "FailureAfterParentInsertRestoresIdentityAndAllowsRetry",
            "post-parent rollback identity regression test");
        AuditAssert.Contains(
            tests,
            "ExistingItemUpdatePreservesIdentity",
            "existing-item update regression test");
    }

    private static string Read(params string[] parts)
    {
        string path = Path.Combine(
            new[] { AuditPaths.RepositoryRoot }.Concat(parts).ToArray());
        return File.ReadAllText(path);
    }
}
