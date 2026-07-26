using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Enums;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.Core.CalculationTests
{
    internal static class StockAdjustmentWorkflowTests
    {
        public static void PostingRequiresActiveManagerOrAdministrator()
        {
            using var fixture = StockAdjustmentFixture.Create(stock: 5m, cost: 100m);
            var repository = new StockAdjustmentRepository(fixture.Factory);

            StockAdjustmentLine line = fixture.CreateLine(systemQty: 5m, actualQty: 4m, unitCost: 100m);

            AssertThrows<InvalidOperationException>(() => repository.SaveAdjustmentAsync(
                    fixture.CreateHeader("Stock Decrease"),
                    new List<StockAdjustmentLine> { line },
                    fixture.CashierActor,
                    isDraft: false)
                .GetAwaiter().GetResult());

            using (AppDbContext suspend = fixture.Factory.CreateDbContext())
            {
                User manager = suspend.Users.Single(u => u.Id == fixture.ManagerActor.UserId);
                manager.IsActive = false;
                suspend.SaveChanges();
            }

            AssertThrows<InvalidOperationException>(() => repository.SaveAdjustmentAsync(
                    fixture.CreateHeader("Stock Decrease"),
                    new List<StockAdjustmentLine>
                    {
                        fixture.CreateLine(systemQty: 5m, actualQty: 4m, unitCost: 100m)
                    },
                    fixture.ManagerActor,
                    isDraft: false)
                .GetAwaiter().GetResult());

            using AppDbContext verify = fixture.Factory.CreateDbContext();
            AssertEqual(5m, verify.ItemBatches.Single().CurrentStock, "unauthorized stock change");
            AssertEqual(0, verify.StockAdjustmentHeaders.Count(), "unauthorized header count");
        }

        public static void ZeroCostIncreaseIsRejectedAtomically()
        {
            using var fixture = StockAdjustmentFixture.Create(stock: 0m, cost: 0m);
            var repository = new StockAdjustmentRepository(fixture.Factory);

            StockAdjustmentLine line = fixture.CreateLine(systemQty: 0m, actualQty: 5m, unitCost: 0m);

            AssertThrows<InvalidOperationException>(() => repository.SaveAdjustmentAsync(
                    fixture.CreateHeader("Stock Increase"),
                    new List<StockAdjustmentLine> { line },
                    fixture.ManagerActor,
                    isDraft: false)
                .GetAwaiter().GetResult());

            using AppDbContext verify = fixture.Factory.CreateDbContext();
            AssertEqual(0m, verify.ItemBatches.Single().CurrentStock, "zero-cost rollback stock");
            AssertEqual(0, verify.StockAdjustmentHeaders.Count(), "zero-cost rollback header");
            AssertEqual(0, verify.InventoryTransactions.Count(), "zero-cost rollback transaction");
        }

        public static void IncreaseUsesEnteredCostAndUpdatesAverageValue()
        {
            using var fixture = StockAdjustmentFixture.Create(stock: 10m, cost: 100m);
            var repository = new StockAdjustmentRepository(fixture.Factory);

            StockAdjustmentHeader header = fixture.CreateHeader("Stock Increase");
            header.AuthorizedBy = "Spoofed User";

            StockAdjustmentHeader saved = repository.SaveAdjustmentAsync(
                    header,
                    new List<StockAdjustmentLine>
                    {
                        fixture.CreateLine(systemQty: 10m, actualQty: 15m, unitCost: 130m)
                    },
                    fixture.ManagerActor,
                    isDraft: false)
                .GetAwaiter().GetResult();

            using AppDbContext verify = fixture.Factory.CreateDbContext();
            ItemBatch batch = verify.ItemBatches.Single();
            ItemVariant variant = verify.ItemVariants.Single();
            StockAdjustmentLine savedLine = verify.StockAdjustmentLines.Single();
            InventoryTransaction transaction = verify.InventoryTransactions.Single(x => x.TransactionType == "ADJUSTMENT");

            AssertEqual(15m, batch.CurrentStock, "increased stock");
            AssertEqual(110m, batch.CostPrice, "weighted GENERAL cost");
            AssertEqual(110m, variant.AverageCost, "variant average cost");
            AssertEqual(130m, savedLine.UnitCost, "increase line cost");
            AssertEqual(650m, savedLine.CostImpact, "increase cost impact");
            AssertEqual(130m, transaction.UnitCost, "increase transaction cost");
            AssertEqual(fixture.ManagerActor.Username, saved.AuthorizedBy, "authenticated authorized user");
        }

        public static void DecreaseUsesAuthoritativeExistingCost()
        {
            using var fixture = StockAdjustmentFixture.Create(stock: 10m, cost: 100m);
            var repository = new StockAdjustmentRepository(fixture.Factory);

            repository.SaveAdjustmentAsync(
                    fixture.CreateHeader("Stock Decrease"),
                    new List<StockAdjustmentLine>
                    {
                        fixture.CreateLine(systemQty: 10m, actualQty: 8m, unitCost: 1m)
                    },
                    fixture.ManagerActor,
                    isDraft: false)
                .GetAwaiter().GetResult();

            using AppDbContext verify = fixture.Factory.CreateDbContext();
            StockAdjustmentLine savedLine = verify.StockAdjustmentLines.Single();
            InventoryTransaction transaction = verify.InventoryTransactions.Single(x => x.TransactionType == "ADJUSTMENT");

            AssertEqual(8m, verify.ItemBatches.Single().CurrentStock, "decreased stock");
            AssertEqual(100m, savedLine.UnitCost, "authoritative decrease cost");
            AssertEqual(-200m, savedLine.CostImpact, "decrease cost impact");
            AssertEqual(100m, transaction.UnitCost, "decrease transaction cost");
        }

        public static void SellingPriceStateIsPreserved()
        {
            using var fixture = StockAdjustmentFixture.Create(stock: 10m, cost: 100m);

            using (AppDbContext setup = fixture.Factory.CreateDbContext())
            {
                ItemBatch batch = setup.ItemBatches.Single();
                batch.HasSellingPriceOverride = true;
                batch.RetailPrice = 175m;
                batch.WholesalePrice = 160m;
                setup.SaveChanges();
            }

            var repository = new StockAdjustmentRepository(fixture.Factory);
            repository.SaveAdjustmentAsync(
                    fixture.CreateHeader("Stock Decrease"),
                    new List<StockAdjustmentLine>
                    {
                        fixture.CreateLine(systemQty: 10m, actualQty: 9m, unitCost: 100m)
                    },
                    fixture.ManagerActor,
                    isDraft: false)
                .GetAwaiter().GetResult();

            using AppDbContext verify = fixture.Factory.CreateDbContext();
            ItemBatch saved = verify.ItemBatches.Single();
            AssertTrue(saved.HasSellingPriceOverride, "stock adjustment override state");
            AssertEqual(175m, saved.RetailPrice, "stock adjustment Retail price");
            AssertEqual(160m, saved.WholesalePrice, "stock adjustment Wholesale price");
        }

        public static void HistoryLoadsAndReversalIsIdempotent()
        {
            using var fixture = StockAdjustmentFixture.Create(stock: 10m, cost: 100m);
            var repository = new StockAdjustmentRepository(fixture.Factory);

            StockAdjustmentHeader saved = repository.SaveAdjustmentAsync(
                    fixture.CreateHeader("Stock Increase"),
                    new List<StockAdjustmentLine>
                    {
                        fixture.CreateLine(systemQty: 10m, actualQty: 12m, unitCost: 100m)
                    },
                    fixture.ManagerActor,
                    isDraft: false)
                .GetAwaiter().GetResult();

            List<StockAdjustmentHistoryRowDto> history = repository.SearchHistoryAsync(
                    DateTime.Today.AddDays(-1),
                    DateTime.Today.AddDays(1),
                    saved.AdjustmentNo,
                    "Posted")
                .GetAwaiter().GetResult();

            AssertEqual(1, history.Count, "history row count");
            StockAdjustmentHistoryDetailDto? detail = repository.GetHistoryDetailAsync(saved.Id)
                .GetAwaiter().GetResult();
            AssertTrue(detail != null && detail.Lines.Count == 1, "history detail line");

            repository.ReverseAdjustmentAsync(saved.Id, "Incorrect opening quantity", fixture.AdminActor)
                .GetAwaiter().GetResult();

            repository.ReverseAdjustmentAsync(saved.Id, "Duplicate request", fixture.AdminActor)
                .GetAwaiter().GetResult();

            using AppDbContext verify = fixture.Factory.CreateDbContext();
            StockAdjustmentHeader reversed = verify.StockAdjustmentHeaders.Single();

            AssertEqual("Cancelled", reversed.Status, "reversed status");
            AssertEqual(fixture.AdminActor.Username, reversed.CancelledBy, "reversed by");
            AssertEqual(10m, verify.ItemBatches.Single().CurrentStock, "reversed stock");
            AssertEqual(1, verify.InventoryTransactions.Count(x => x.TransactionType == "ADJUSTMENT_REVERSAL"), "idempotent reversal transaction count");
            AssertEqual("Reversed", verify.StockAdjustmentLines.Single().LineStatus, "reversed line status");
        }

        public static void ReversalBlocksWhenLaterUsageWouldMakeStockNegative()
        {
            using var fixture = StockAdjustmentFixture.Create(stock: 0m, cost: 100m);
            var repository = new StockAdjustmentRepository(fixture.Factory);

            StockAdjustmentHeader saved = repository.SaveAdjustmentAsync(
                    fixture.CreateHeader("Stock Increase"),
                    new List<StockAdjustmentLine>
                    {
                        fixture.CreateLine(systemQty: 0m, actualQty: 2m, unitCost: 100m)
                    },
                    fixture.ManagerActor,
                    isDraft: false)
                .GetAwaiter().GetResult();

            using (AppDbContext mutate = fixture.Factory.CreateDbContext())
            {
                ItemBatch batch = mutate.ItemBatches.Single();
                batch.CurrentStock = 1m;
                mutate.InventoryTransactions.Add(new InventoryTransaction
                {
                    ItemVariantId = fixture.VariantId,
                    ItemBatchId = fixture.BatchId,
                    TransactionDate = DateTime.Now,
                    TransactionType = "SALE",
                    ReferenceDocument = "TEST-SALE",
                    Quantity = -1m,
                    UnitCost = 100m,
                    CreatedBy = "test",
                    CreatedAt = DateTime.Now
                });
                mutate.SaveChanges();
            }

            AssertThrows<InvalidOperationException>(() => repository.ReverseAdjustmentAsync(
                    saved.Id,
                    "Try unsafe reversal",
                    fixture.AdminActor)
                .GetAwaiter().GetResult());

            using AppDbContext verify = fixture.Factory.CreateDbContext();
            AssertEqual(1m, verify.ItemBatches.Single().CurrentStock, "blocked reversal stock");
            AssertEqual("Posted", verify.StockAdjustmentHeaders.Single().Status, "blocked reversal status");
            AssertEqual(0, verify.InventoryTransactions.Count(x => x.TransactionType == "ADJUSTMENT_REVERSAL"), "blocked reversal transaction");
        }

        public static void ConcurrentSameStockSnapshotNeverDoubleDecrements()
        {
            using var fixture = StockAdjustmentFixture.Create(stock: 1m, cost: 100m);
            var repository1 = new StockAdjustmentRepository(fixture.Factory);
            var repository2 = new StockAdjustmentRepository(fixture.Factory);

            Task<bool> first = AttemptAdjustmentAsync(repository1, fixture, "Concurrent A");
            Task<bool> second = AttemptAdjustmentAsync(repository2, fixture, "Concurrent B");

            bool[] results = Task.WhenAll(first, second).GetAwaiter().GetResult();

            using AppDbContext verify = fixture.Factory.CreateDbContext();
            decimal finalStock = verify.ItemBatches.Single().CurrentStock;
            int postedHeaders = verify.StockAdjustmentHeaders.Count();

            AssertTrue(results.Count(x => x) == 1, "exactly one concurrent adjustment should post");
            AssertEqual(0m, finalStock, "concurrent final stock");
            AssertEqual(1, postedHeaders, "concurrent header count");
            AssertTrue(finalStock >= 0m, "concurrent stock cannot be negative");
        }

        private static async Task<bool> AttemptAdjustmentAsync(
            StockAdjustmentRepository repository,
            StockAdjustmentFixture fixture,
            string reference)
        {
            try
            {
                StockAdjustmentHeader header = fixture.CreateHeader("Stock Decrease");
                header.Reference = reference;

                await repository.SaveAdjustmentAsync(
                    header,
                    new List<StockAdjustmentLine>
                    {
                        fixture.CreateLine(systemQty: 1m, actualQty: 0m, unitCost: 100m)
                    },
                    fixture.ManagerActor,
                    isDraft: false);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void AssertThrows<TException>(Action action)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException($"Expected {typeof(TException).Name} was not thrown.");
        }

        private static void AssertTrue(bool condition, string label)
        {
            if (!condition)
                throw new InvalidOperationException($"Assertion failed: {label}.");
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    $"Assertion failed for {label}. Expected '{expected}', actual '{actual}'.");
            }
        }

        private sealed class StockAdjustmentFixture : IDisposable
        {
            private readonly string _databasePath;

            public TestDbContextFactory Factory { get; }
            public int VariantId { get; private set; }
            public int BatchId { get; private set; }
            public StockAdjustmentActorContext ManagerActor { get; private set; } = new();
            public StockAdjustmentActorContext AdminActor { get; private set; } = new();
            public StockAdjustmentActorContext CashierActor { get; private set; } = new();

            private StockAdjustmentFixture(string databasePath)
            {
                _databasePath = databasePath;
                Factory = new TestDbContextFactory(databasePath);
            }

            public static StockAdjustmentFixture Create(decimal stock, decimal cost)
            {
                string path = Path.Combine(
                    Path.GetTempPath(),
                    $"POS-StockAdjustment-{Guid.NewGuid():N}.db");

                var fixture = new StockAdjustmentFixture(path);
                fixture.Seed(stock, cost);
                return fixture;
            }

            public StockAdjustmentHeader CreateHeader(string mode)
            {
                return new StockAdjustmentHeader
                {
                    AdjustmentDate = DateTime.Now,
                    AdjustmentMode = mode,
                    AuthorizedBy = "UI value must not be trusted",
                    Reference = "Stock adjustment automated test",
                    Remarks = "Automated verification"
                };
            }

            public StockAdjustmentLine CreateLine(decimal systemQty, decimal actualQty, decimal unitCost)
            {
                return new StockAdjustmentLine
                {
                    ItemBatchId = BatchId,
                    ItemVariantId = VariantId,
                    SystemQty = systemQty,
                    ActualQty = actualQty,
                    VarianceQty = actualQty - systemQty,
                    ReasonCode = actualQty >= systemQty ? "Found Stock" : "Audit Correction",
                    LineRemarks = "Automated test",
                    UnitCost = unitCost,
                    CostImpact = Math.Round((actualQty - systemQty) * unitCost, 2)
                };
            }

            private void Seed(decimal stock, decimal cost)
            {
                using AppDbContext context = Factory.CreateDbContext();
                context.Database.EnsureCreated();

                var category = new Category
                {
                    CategoryCode = "TEST",
                    CategoryName = "Test Category",
                    CreatedBy = "test",
                    UpdatedBy = "test"
                };

                UnitOfMeasure uom = context.UnitsOfMeasure
                    .Single(value => value.UomCode == "PCS");

                var manager = new User
                {
                    FirstName = "Test",
                    LastName = "Manager",
                    Username = "stockmanager",
                    PasswordHash = "hash",
                    PasswordSalt = "salt",
                    Role = UserRole.Manager,
                    IsActive = true
                };

                var admin = new User
                {
                    FirstName = "Test",
                    LastName = "Admin",
                    Username = "stockadmin",
                    PasswordHash = "hash",
                    PasswordSalt = "salt",
                    Role = UserRole.Admin,
                    IsActive = true
                };

                var cashier = new User
                {
                    FirstName = "Test",
                    LastName = "Cashier",
                    Username = "stockcashier",
                    PasswordHash = "hash",
                    PasswordSalt = "salt",
                    Role = UserRole.Cashier,
                    IsActive = true
                };

                context.AddRange(category, manager, admin, cashier);
                context.SaveChanges();

                var parent = new ItemParent
                {
                    ItemCode = "STK-001",
                    ItemName = "Stock Adjustment Test Item",
                    PrintName = "Test Item",
                    CategoryId = category.Id,
                    UnitOfMeasureId = uom.Id,
                    BaseUom = "PCS",
                    ItemType = ItemTypeCodes.StockItem,
                    HasBatchTracking = false,
                    IsTaxInclusive = true
                };

                var variant = new ItemVariant
                {
                    ItemParent = parent,
                    SkuCode = "STK-001-STD",
                    VariantDescription = "Standard",
                    Barcode = "9988776655",
                    CostPrice = cost,
                    AverageCost = cost,
                    RetailPrice = 150m,
                    WholesalePrice = 140m
                };

                var batch = new ItemBatch
                {
                    ItemVariant = variant,
                    BatchNo = "GENERAL",
                    CostPrice = cost,
                    RetailPrice = 150m,
                    WholesalePrice = 140m,
                    CurrentStock = stock,
                    ReceivedDate = DateTime.Now
                };

                context.Add(batch);
                context.SaveChanges();

                VariantId = variant.Id;
                BatchId = batch.Id;
                ManagerActor = ToActor(manager);
                AdminActor = ToActor(admin);
                CashierActor = ToActor(cashier);
            }

            private static StockAdjustmentActorContext ToActor(User user)
            {
                return new StockAdjustmentActorContext
                {
                    UserId = user.Id,
                    Username = user.Username,
                    Role = user.Role
                };
            }

            public void Dispose()
            {
                Factory.Dispose();

                try
                {
                    if (File.Exists(_databasePath))
                        File.Delete(_databasePath);
                }
                catch
                {
                    // Test cleanup must not hide the actual assertion result.
                }
            }
        }

        private sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>, IDisposable
        {
            private readonly DbContextOptions<AppDbContext> _options;

            public TestDbContextFactory(string databasePath)
            {
                _options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite($"Data Source={databasePath};Default Timeout=10")
                    .Options;
            }

            public AppDbContext CreateDbContext() => new(_options);

            public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(CreateDbContext());

            public void Dispose()
            {
            }
        }
    }
}
