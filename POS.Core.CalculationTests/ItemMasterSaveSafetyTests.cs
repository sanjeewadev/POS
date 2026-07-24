using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Core.Utilities;

namespace POS.Core.CalculationTests;

internal static class ItemMasterSaveSafetyTests
{
    public static void NewItemRejectsDuplicateSkuBeforeParentInsert()
    {
        using var fixture = ItemMasterFixture.Create();
        fixture.SeedExistingItem(
            itemCode: "OLD-ITEM-001",
            sku: "NEW-ITEM-001-RED1",
            barcode: "100000000001");

        ItemParent parent = fixture.CreateParent("NEW-ITEM-001");
        ItemVariant variant = fixture.CreateVariant(
            sku: "NEW-ITEM-001-RED1",
            barcode: "100000000002",
            description: "Red");

        InvalidOperationException exception = AssertThrows(() =>
            fixture.Repository.SaveFullMatrixAsync(
                    parent,
                    new List<ItemVariant> { variant },
                    variant.PropertyMappings.ToList())
                .GetAwaiter()
                .GetResult());

        AssertContains(exception.Message, "SKU 'NEW-ITEM-001-RED1' already exists", "duplicate SKU message");
        AssertEqual(0, parent.Id, "duplicate SKU parent identity reset");
        AssertEqual(0, variant.Id, "duplicate SKU variant identity reset");

        using AppDbContext verify = fixture.CreateDbContext();
        AssertEqual(1, verify.ItemParents.Count(), "duplicate SKU parent insert count");
        AssertEqual(1, verify.ItemVariants.Count(), "duplicate SKU variant insert count");
    }

    public static void NewItemRejectsDuplicateBarcodeBeforeParentInsert()
    {
        using var fixture = ItemMasterFixture.Create();
        fixture.SeedExistingItem(
            itemCode: "OLD-ITEM-002",
            sku: "OLD-ITEM-002",
            barcode: "200000000001");

        ItemParent parent = fixture.CreateParent("NEW-ITEM-002");
        ItemVariant variant = fixture.CreateVariant(
            sku: "NEW-ITEM-002",
            barcode: "200000000001",
            description: "Standard");

        InvalidOperationException exception = AssertThrows(() =>
            fixture.Repository.SaveFullMatrixAsync(
                    parent,
                    new List<ItemVariant> { variant },
                    variant.PropertyMappings.ToList())
                .GetAwaiter()
                .GetResult());

        AssertContains(exception.Message, "Barcode '200000000001' already exists", "duplicate barcode message");
        AssertEqual(0, parent.Id, "duplicate barcode parent identity reset");
        AssertEqual(0, variant.Id, "duplicate barcode variant identity reset");

        using AppDbContext verify = fixture.CreateDbContext();
        AssertEqual(1, verify.ItemParents.Count(), "duplicate barcode parent insert count");
        AssertEqual(1, verify.ItemVariants.Count(), "duplicate barcode variant insert count");
    }

    public static void NewItemRejectsBatchBarcodeBeforeParentInsert()
    {
        using var fixture = ItemMasterFixture.Create();
        fixture.SeedExistingItem(
            itemCode: "OLD-ITEM-BATCH",
            sku: "OLD-ITEM-BATCH",
            barcode: "");
        fixture.SeedBatchBarcode("BATCH-BARCODE-001");

        ItemParent parent = fixture.CreateParent("NEW-ITEM-BATCH");
        ItemVariant variant = fixture.CreateVariant(
            sku: "NEW-ITEM-BATCH",
            barcode: "BATCH-BARCODE-001",
            description: "Standard");

        InvalidOperationException exception = AssertThrows(() =>
            fixture.Repository.SaveFullMatrixAsync(
                    parent,
                    new List<ItemVariant> { variant },
                    variant.PropertyMappings.ToList())
                .GetAwaiter()
                .GetResult());

        AssertContains(exception.Message, "GRN batch barcode", "batch barcode collision message");
        AssertEqual(0, parent.Id, "batch barcode parent identity reset");
        AssertEqual(0, variant.Id, "batch barcode variant identity reset");

        using AppDbContext verify = fixture.CreateDbContext();
        AssertEqual(1, verify.ItemParents.Count(), "batch barcode parent insert count");
        AssertEqual(1, verify.ItemVariants.Count(), "batch barcode variant insert count");
    }

    public static void FailureAfterParentInsertRestoresIdentityAndAllowsRetry()
    {
        using var fixture = ItemMasterFixture.Create();

        ItemParent parent = fixture.CreateParent("POST-INSERT-RETRY");
        ItemVariant variant = fixture.CreateVariant(
            sku: "POST-INSERT-RETRY-RED1",
            barcode: "350000000001",
            description: "Red");
        variant.PropertyMappings.Add(new ItemPropertyMapping
        {
            AttributeGroupId = 999999,
            AttributeValueId = 999999
        });

        InvalidOperationException exception = AssertThrows(() =>
            fixture.Repository.SaveFullMatrixAsync(
                    parent,
                    new List<ItemVariant> { variant },
                    variant.PropertyMappings.ToList())
                .GetAwaiter()
                .GetResult());

        AssertContains(exception.Message, "property values", "post-insert rollback failure message");
        AssertEqual(0, parent.Id, "post-insert rollback parent identity");
        AssertEqual(0, variant.Id, "post-insert rollback variant identity");
        AssertEqual(0, variant.ItemParentId, "post-insert rollback parent link");

        using (AppDbContext verifyRollback = fixture.CreateDbContext())
        {
            AssertEqual(0, verifyRollback.ItemParents.Count(), "post-insert rollback parent count");
            AssertEqual(0, verifyRollback.ItemVariants.Count(), "post-insert rollback variant count");
        }

        variant.PropertyMappings.Clear();

        fixture.Repository.SaveFullMatrixAsync(
                parent,
                new List<ItemVariant> { variant },
                variant.PropertyMappings.ToList())
            .GetAwaiter()
            .GetResult();

        AssertTrue(parent.Id > 0, "post-insert retry parent identity assigned");
        AssertTrue(variant.Id > 0, "post-insert retry variant identity assigned");

        using AppDbContext verifyRetry = fixture.CreateDbContext();
        AssertEqual(1, verifyRetry.ItemParents.Count(), "post-insert retry parent count");
        AssertEqual(1, verifyRetry.ItemVariants.Count(), "post-insert retry variant count");
    }

    public static void FailedNewItemSaveCanBeCorrectedAndRetried()
    {
        using var fixture = ItemMasterFixture.Create();
        fixture.SeedExistingItem(
            itemCode: "OLD-ITEM-003",
            sku: "RETRY-ITEM-RED1",
            barcode: "300000000001");

        ItemParent parent = fixture.CreateParent("RETRY-ITEM");
        ItemVariant variant = fixture.CreateVariant(
            sku: "RETRY-ITEM-RED1",
            barcode: "300000000002",
            description: "Red");

        _ = AssertThrows(() =>
            fixture.Repository.SaveFullMatrixAsync(
                    parent,
                    new List<ItemVariant> { variant },
                    variant.PropertyMappings.ToList())
                .GetAwaiter()
                .GetResult());

        AssertEqual(0, parent.Id, "failed retry parent identity");
        AssertEqual(0, variant.Id, "failed retry variant identity");

        variant.SkuCode = "RETRY-ITEM-BLUE1";
        variant.Barcode = "300000000003";
        variant.VariantDescription = "Blue";

        fixture.Repository.SaveFullMatrixAsync(
                parent,
                new List<ItemVariant> { variant },
                variant.PropertyMappings.ToList())
            .GetAwaiter()
            .GetResult();

        AssertTrue(parent.Id > 0, "retry parent identity assigned");
        AssertTrue(variant.Id > 0, "retry variant identity assigned");

        using AppDbContext verify = fixture.CreateDbContext();
        AssertEqual(2, verify.ItemParents.Count(), "retry parent count");
        AssertEqual(2, verify.ItemVariants.Count(), "retry variant count");
        AssertTrue(
            verify.ItemVariants.Any(row => row.SkuCode == "RETRY-ITEM-BLUE1"),
            "retry corrected SKU persisted");
    }

    public static void NewItemRejectsStaleMatrixSku()
    {
        using var fixture = ItemMasterFixture.Create();

        ItemParent parent = fixture.CreateParent("CAT002-SC007-017");
        ItemVariant variant = fixture.CreateVariant(
            sku: "CAT002-SC007-016-ISLA1",
            barcode: "400000000001",
            description: "Island Brew");

        string policyMessage = ItemVariantIdentityPolicy.BuildMisalignmentMessage(
            parent.ItemCode,
            new[] { variant });

        AssertContains(policyMessage, "Generate the matrix again", "stale matrix policy message");

        InvalidOperationException exception = AssertThrows(() =>
            fixture.Repository.SaveFullMatrixAsync(
                    parent,
                    new List<ItemVariant> { variant },
                    variant.PropertyMappings.ToList())
                .GetAwaiter()
                .GetResult());

        AssertContains(exception.Message, "do not match the current item code", "stale matrix repository message");
        AssertEqual(0, parent.Id, "stale matrix parent identity");
        AssertEqual(0, variant.Id, "stale matrix variant identity");

        using AppDbContext verify = fixture.CreateDbContext();
        AssertEqual(0, verify.ItemParents.Count(), "stale matrix parent count");
        AssertEqual(0, verify.ItemVariants.Count(), "stale matrix variant count");
    }

    public static void ExistingItemUpdatePreservesIdentity()
    {
        using var fixture = ItemMasterFixture.Create();

        ItemParent parent = fixture.CreateParent("UPDATE-ITEM-001");
        ItemVariant variant = fixture.CreateVariant(
            sku: "UPDATE-ITEM-001",
            barcode: "500000000001",
            description: "Standard");

        fixture.Repository.SaveFullMatrixAsync(
                parent,
                new List<ItemVariant> { variant },
                variant.PropertyMappings.ToList())
            .GetAwaiter()
            .GetResult();

        int parentId = parent.Id;
        int variantId = variant.Id;

        parent.ItemName = "Updated Item Name";
        parent.PrintName = "Updated Item";
        variant.RetailPrice = 250m;

        fixture.Repository.SaveFullMatrixAsync(
                parent,
                new List<ItemVariant> { variant },
                variant.PropertyMappings.ToList())
            .GetAwaiter()
            .GetResult();

        AssertEqual(parentId, parent.Id, "existing update parent identity");
        AssertEqual(variantId, variant.Id, "existing update variant identity");

        using AppDbContext verify = fixture.CreateDbContext();
        AssertEqual(1, verify.ItemParents.Count(), "existing update parent count");
        AssertEqual(1, verify.ItemVariants.Count(), "existing update variant count");
        AssertEqual(
            "Updated Item Name",
            verify.ItemParents.Single().ItemName,
            "existing update item name");
        AssertEqual(
            250m,
            verify.ItemVariants.Single().RetailPrice,
            "existing update retail price");
    }

    private static InvalidOperationException AssertThrows(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }

        throw new InvalidOperationException(
            "Expected InvalidOperationException was not thrown.");
    }

    private static void AssertContains(
        string source,
        string expected,
        string label)
    {
        if (!source.Contains(expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{label}: expected '{expected}' in '{source}'.");
        }
    }

    private static void AssertTrue(bool condition, string label)
    {
        if (!condition)
            throw new InvalidOperationException($"{label}: expected true.");
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

    private sealed class ItemMasterFixture :
        IDbContextFactory<AppDbContext>,
        IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        private ItemMasterFixture()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .EnableDetailedErrors()
                .Options;

            using AppDbContext context = CreateDbContext();
            context.Database.EnsureCreated();
            SeedReferences(context);

            Repository = new ItemMasterRepository(this);
        }

        public ItemMasterRepository Repository { get; }

        public int CategoryId { get; private set; }

        public int UomId { get; private set; }

        public int TaxCategoryId { get; private set; }

        public static ItemMasterFixture Create() => new();

        public AppDbContext CreateDbContext() => new(_options);

        public Task<AppDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());

        public ItemParent CreateParent(string itemCode) => new()
        {
            ItemCode = itemCode,
            ItemName = $"Test {itemCode}",
            PrintName = $"Test {itemCode}",
            CategoryId = CategoryId,
            UnitOfMeasureId = UomId,
            BaseUom = "PCS",
            ItemType = ItemTypeCodes.StockItem,
            TaxCategoryId = TaxCategoryId,
            TaxCode = "VAT-STD",
            IsTaxInclusive = true,
            HasBatchTracking = true,
            HasExpiryTracking = false,
            HasBatchExpiry = false,
            AllowCashierDiscount = true
        };

        public ItemVariant CreateVariant(
            string sku,
            string barcode,
            string description) => new()
        {
            SkuCode = sku,
            Barcode = barcode,
            VariantDescription = description,
            AverageCost = 100m,
            CostPrice = 100m,
            RetailPrice = 200m,
            WholesalePrice = 180m,
            MinimumPrice = 100m,
            MaximumPrice = 250m,
            ReorderLevel = 1,
            PropertyMappings = new List<ItemPropertyMapping>(),
            ItemSuppliers = new List<ItemSupplier>()
        };

        public void SeedExistingItem(
            string itemCode,
            string sku,
            string barcode)
        {
            using AppDbContext context = CreateDbContext();
            var parent = CreateParent(itemCode);
            context.ItemParents.Add(parent);
            context.SaveChanges();

            context.ItemVariants.Add(new ItemVariant
            {
                ItemParentId = parent.Id,
                SkuCode = sku,
                Barcode = barcode,
                VariantDescription = sku == itemCode ? "Standard" : "Legacy Variant",
                AverageCost = 100m,
                CostPrice = 100m,
                RetailPrice = 200m,
                WholesalePrice = 180m,
                MinimumPrice = 100m,
                MaximumPrice = 250m,
                ReorderLevel = 1
            });
            context.SaveChanges();
        }

        public void SeedBatchBarcode(string barcode)
        {
            using AppDbContext context = CreateDbContext();
            int variantId = context.ItemVariants
                .OrderBy(variant => variant.Id)
                .Select(variant => variant.Id)
                .First();

            context.ItemBatches.Add(new ItemBatch
            {
                ItemVariantId = variantId,
                BatchNo = "TEST-BATCH",
                InternalBatchBarcode = barcode,
                ReceivedDate = DateTime.Today,
                CostPrice = 100m,
                RetailPrice = 200m,
                WholesalePrice = 180m,
                CurrentStock = 1m
            });
            context.SaveChanges();
        }

        public void Dispose() => _connection.Dispose();

        private void SeedReferences(AppDbContext context)
        {
            var category = new Category
            {
                CategoryCode = "TEST",
                CategoryName = "Test Category",
                Description = "Item Master safety tests"
            };

            UnitOfMeasure uom = context.UnitsOfMeasure
                .Single(row => row.UomCode == "PCS");

            var taxCategory = new TaxCategory
            {
                CategoryCode = TaxCategoryCodes.Standard,
                CategoryName = "Standard VAT",
                TreatmentType = "StandardRated",
                IsRateBased = true,
                IsActive = true
            };

            context.Categories.Add(category);
            context.TaxCategories.Add(taxCategory);
            context.SaveChanges();

            context.TaxRates.Add(new TaxRate
            {
                TaxCode = "VAT-STD",
                TaxName = "Standard VAT",
                TaxCategoryId = taxCategory.Id,
                RatePercent = 18m,
                EffectiveFrom = DateTime.Today.AddYears(-1),
                IsActive = true
            });
            context.SaveChanges();

            CategoryId = category.Id;
            UomId = uom.Id;
            TaxCategoryId = taxCategory.Id;
        }
    }
}
