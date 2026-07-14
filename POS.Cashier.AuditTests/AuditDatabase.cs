using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Enums;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services.Returns;
using POS.Core.Utilities;

namespace POS.Cashier.AuditTests;

internal sealed class AuditDbContextFactory : IDbContextFactory<AppDbContext>, IDisposable
{
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly string _databasePath;

    public AuditDbContextFactory(bool migrate = false, string? existingDatabasePath = null)
    {
        string root = AuditPaths.GetAuditTempRoot();
        Directory.CreateDirectory(root);

        _databasePath = existingDatabasePath ?? Path.Combine(root, $"cashier-audit-{Guid.NewGuid():N}.db");
        AuditPaths.AssertSafeDatabasePath(_databasePath);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 5,
            Pooling = true
        };

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(builder.ToString())
            .EnableDetailedErrors()
            .Options;

        using AppDbContext context = CreateDbContext();
        if (migrate)
            context.Database.Migrate();
        else
            context.Database.EnsureCreated();

        context.Database.ExecuteSqlRaw("PRAGMA busy_timeout=5000;");
        context.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys=ON;");
    }

    public string DatabasePath => _databasePath;

    public AppDbContext CreateDbContext() => new(_options);

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext());

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        DeleteIfPresent(_databasePath);
        DeleteIfPresent(_databasePath + "-shm");
        DeleteIfPresent(_databasePath + "-wal");
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}

internal static class AuditPaths
{
    public static string RepositoryRoot
    {
        get
        {
            string? configured = Environment.GetEnvironmentVariable("POS_AUDIT_REPO_ROOT");
            if (!string.IsNullOrWhiteSpace(configured))
                return Path.GetFullPath(configured);

            DirectoryInfo? current = new(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "POS.sln")))
                    return current.FullName;
                current = current.Parent;
            }

            throw new InvalidOperationException("POS repository root could not be located.");
        }
    }

    public static string GetAuditTempRoot()
    {
        string? configured = Environment.GetEnvironmentVariable("POS_AUDIT_TEMP_ROOT");
        string root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Path.GetTempPath(), "POS-Cashier-Audit", Guid.NewGuid().ToString("N"))
            : configured;
        return Path.GetFullPath(root);
    }

    public static string GetLiveDatabasePath()
    {
        string? configured = Environment.GetEnvironmentVariable("POS_AUDIT_LIVE_DB_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured);

        return Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "POS",
            "pos_local.db"));
    }

    public static void AssertSafeDatabasePath(string databasePath)
    {
        string candidate = Path.GetFullPath(databasePath);
        string live = GetLiveDatabasePath();
        string liveDirectory = Path.GetDirectoryName(live)
            ?? throw new InvalidOperationException("Live POS database directory could not be resolved.");

        if (string.Equals(candidate, live, StringComparison.OrdinalIgnoreCase) ||
            IsWithin(candidate, liveDirectory))
        {
            throw new InvalidOperationException(
                $"Audit database path is unsafe because it points at the live POS database directory: {candidate}");
        }
    }

    private static bool IsWithin(string path, string directory)
    {
        string normalizedDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
        string normalizedPath = Path.GetFullPath(path);
        return normalizedPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class AuditScenario
{
    public int ShiftSessionId { get; init; }
    public int CategoryId { get; init; }
    public int StockVariantId { get; init; }
    public int StockBatchId { get; init; }
    public string StockSku { get; init; } = string.Empty;
    public int ServiceVariantId { get; init; }
    public string ServiceSku { get; init; } = string.Empty;
    public string ServiceBarcode { get; init; } = string.Empty;
    public int SupplierId { get; init; }
}

internal static class AuditSeed
{
    public static AuditScenario CreateScenario(AuditDbContextFactory factory, decimal stock = 5m)
    {
        using AppDbContext context = factory.CreateDbContext();

        var category = new Category
        {
            CategoryCode = $"AUD-{Guid.NewGuid():N}"[..20],
            CategoryName = $"Audit {Guid.NewGuid():N}"[..30],
            CreatedBy = "Cashier Audit",
            UpdatedBy = "Cashier Audit"
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
        var supplier = new Supplier
        {
            SupplierCode = $"AUDSUP{Guid.NewGuid():N}"[..18],
            SupplierName = "Audit Supplier",
            Phone1 = "0110000000",
            HasVat = true,
            VatNumber = "AUD-VAT"
        };

        context.Categories.Add(category);
        context.TaxCategories.Add(taxCategory);
        context.Suppliers.Add(supplier);
        context.SaveChanges();

        var stockParent = new ItemParent
        {
            ItemCode = $"AUD-STK-{Guid.NewGuid():N}"[..20],
            ItemName = "Audit Stock Item",
            PrintName = "Audit Stock Item",
            CategoryId = category.Id,
            UnitOfMeasureId = 1,
            BaseUom = "PCS",
            ItemType = ItemTypeCodes.StockItem,
            TaxCategoryId = taxCategory.Id,
            TaxCode = "VAT-STD",
            IsTaxInclusive = true,
            HasBatchTracking = true,
            HasExpiryTracking = false,
            HasBatchExpiry = false
        };
        var serviceParent = new ItemParent
        {
            ItemCode = $"AUD-SVC-{Guid.NewGuid():N}"[..20],
            ItemName = "Audit Service",
            PrintName = "Audit Service",
            CategoryId = category.Id,
            UnitOfMeasureId = 1,
            BaseUom = "JOB",
            ItemType = ItemTypeCodes.Service,
            TaxCategoryId = taxCategory.Id,
            TaxCode = "VAT-STD",
            IsTaxInclusive = true,
            HasBatchTracking = false,
            HasExpiryTracking = false,
            HasBatchExpiry = false,
            IsPurchaseLocked = true
        };
        var stockVariant = new ItemVariant
        {
            ItemParent = stockParent,
            SkuCode = $"AUD-STK-{Guid.NewGuid():N}"[..24],
            Barcode = $"AUDSTK{Random.Shared.Next(100000, 999999)}",
            VariantDescription = "Standard",
            AverageCost = 600m,
            CostPrice = 600m,
            RetailPrice = 1180m,
            WholesalePrice = 1062m,
            MinimumPrice = 600m
        };
        var serviceVariant = new ItemVariant
        {
            ItemParent = serviceParent,
            SkuCode = $"AUD-SVC-{Guid.NewGuid():N}"[..24],
            Barcode = $"AUDSVC{Random.Shared.Next(100000, 999999)}",
            VariantDescription = "Standard",
            AverageCost = 350m,
            CostPrice = 400m,
            RetailPrice = 1180m,
            WholesalePrice = 1062m,
            MinimumPrice = 400m
        };

        context.ItemVariants.AddRange(stockVariant, serviceVariant);
        context.SaveChanges();

        context.ItemSuppliers.AddRange(
            new ItemSupplier
            {
                ItemVariantId = stockVariant.Id,
                SupplierId = supplier.Id,
                IsPrimary = true,
                SupplierItemCode = "AUD-STOCK",
                LastCostPrice = 600m
            },
            new ItemSupplier
            {
                ItemVariantId = serviceVariant.Id,
                SupplierId = supplier.Id,
                IsPrimary = true,
                SupplierItemCode = "AUD-SERVICE",
                LastCostPrice = 400m
            });

        var batch = new ItemBatch
        {
            ItemVariantId = stockVariant.Id,
            BatchNo = "AUDIT-BATCH",
            InternalBatchBarcode = $"AUDBATCH{Random.Shared.Next(100000, 999999)}",
            ReceivedDate = DateTime.Today,
            CostPrice = 600m,
            RetailPrice = 1180m,
            WholesalePrice = 1062m,
            CurrentStock = stock,
            IsDeactivated = false
        };
        var rate = new TaxRate
        {
            TaxCode = "VAT-STD",
            TaxName = "Standard VAT",
            TaxCategoryId = taxCategory.Id,
            RatePercent = 18m,
            EffectiveFrom = new DateTime(2024, 1, 1),
            EffectiveTo = null,
            ChangeReason = "Cashier audit",
            CreatedBy = "Cashier Audit",
            UpdatedBy = "Cashier Audit",
            IsActive = true
        };
        var shift = new ShiftSession
        {
            TerminalNo = "T01",
            CashierName = "Audit Cashier",
            StartTime = DateTime.Now,
            Status = ShiftStatusCodes.Open,
            OpeningCash = 0m,
            ExpectedCash = 0m
        };
        var store = new StoreSettings
        {
            LegalName = "Audit Store",
            StoreName = "Audit Store",
            IsVatRegistered = true,
            TaxpayerIdentificationNumber = "AUD-TIN",
            VatRegistrationNumber = "AUD-VAT",
            IsActive = true
        };

        context.ItemBatches.Add(batch);
        context.TaxRates.Add(rate);
        context.ShiftSessions.Add(shift);
        context.StoreSettings.Add(store);
        context.SaveChanges();

        return new AuditScenario
        {
            ShiftSessionId = shift.Id,
            CategoryId = category.Id,
            StockVariantId = stockVariant.Id,
            StockBatchId = batch.Id,
            StockSku = stockVariant.SkuCode,
            ServiceVariantId = serviceVariant.Id,
            ServiceSku = serviceVariant.SkuCode,
            ServiceBarcode = serviceVariant.Barcode,
            SupplierId = supplier.Id
        };
    }

    public static SalesHeader Header(AuditScenario scenario, decimal tendered, string paymentMethod = PaymentTypeCodes.Cash) => new()
    {
        ShiftSessionId = scenario.ShiftSessionId,
        TerminalNo = "T01",
        CashierName = "Audit Cashier",
        CustomerName = "Walk-In",
        CustomerType = "Walk-In",
        PaymentMethod = paymentMethod,
        AmountTendered = tendered,
        BalanceReturned = 0m,
        InvoiceDiscountAmount = 0m
    };

    public static SalesLine StockLine(AuditScenario scenario, decimal quantity = 1m, decimal unitPrice = 1180m, int? batchId = null) =>
        Line(scenario.StockVariantId, batchId ?? scenario.StockBatchId, scenario.StockSku, "Audit Stock Item", quantity, unitPrice, "PCS");

    public static SalesLine ServiceLine(AuditScenario scenario, decimal quantity = 1m, decimal unitPrice = 1180m) =>
        Line(scenario.ServiceVariantId, null, scenario.ServiceSku, "Audit Service", quantity, unitPrice, "JOB");

    private static SalesLine Line(
        int variantId,
        int? batchId,
        string sku,
        string description,
        decimal quantity,
        decimal unitPrice,
        string uom)
    {
        decimal gross = decimal.Round(quantity * unitPrice, 2);
        return new SalesLine
        {
            ItemVariantId = variantId,
            ItemBatchId = batchId,
            SkuCode = sku,
            Barcode = sku,
            ItemDescription = description,
            BatchNo = batchId.HasValue ? "AUDIT-BATCH" : string.Empty,
            Uom = uom,
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

    public static SalesPayment Payment(string type, decimal amount, string reference = "", string bankOrCardType = "") => new()
    {
        PaymentType = type,
        Amount = amount,
        TenderedAmount = amount,
        ChangeAmount = 0m,
        ReferenceNo = reference,
        BankOrCardType = bankOrCardType,
        EnteredBy = "Audit Cashier",
        TerminalNo = "T01"
    };

    public static async Task SaveActiveCartAsync(
        AuditDbContextFactory factory,
        AuditScenario scenario,
        Guid token,
        decimal quantity = 1m,
        int? shiftSessionId = null,
        string terminalNo = "T01",
        string cashierName = "Audit Cashier")
    {
        var repository = new CashierCartRepository(factory);
        CashierCartLineSnapshotDto line = new()
        {
            LineType = CashierCartLineTypeCodes.StockItem,
            ItemVariantId = scenario.StockVariantId,
            ItemBatchId = scenario.StockBatchId,
            ItemCode = "AUDIT-STOCK",
            SkuCode = scenario.StockSku,
            Barcode = scenario.StockSku,
            Description = "Audit Stock Item",
            VariantDescription = "Standard",
            Uom = "PCS",
            ItemType = ItemTypeCodes.StockItem,
            BatchNo = "AUDIT-BATCH",
            AvailableBatchStock = quantity,
            CostPrice = 600m,
            RetailPrice = 1180m,
            WholesalePrice = 1062m,
            MinimumPrice = 600m,
            UnitPrice = 1180m,
            Quantity = quantity,
            OriginalUnitPrice = 1180m,
            TaxableAmount = decimal.Round(1180m * quantity / 1.18m, 2),
            VatAmount = decimal.Round(1180m * quantity - (1180m * quantity / 1.18m), 2),
            TaxInclusiveAmount = decimal.Round(1180m * quantity, 2)
        };

        await repository.SaveActiveAsync(new CashierCartSaveRequest
        {
            CartToken = token,
            Owner = new CashierCartOwnerDto
            {
                ShiftSessionId = shiftSessionId ?? scenario.ShiftSessionId,
                TerminalNo = terminalNo,
                CashierName = cashierName
            },
            GrossTotal = decimal.Round(1180m * quantity, 2),
            TotalDiscount = 0m,
            NetTotal = decimal.Round(1180m * quantity, 2),
            Lines = new[] { line }
        });
    }

    public static GiftVoucher GiftVoucher(AuditDbContextFactory factory, decimal amount)
    {
        using AppDbContext context = factory.CreateDbContext();
        string code = $"GVAUD{Guid.NewGuid():N}"[..28].ToUpperInvariant();
        var voucher = new GiftVoucher
        {
            VoucherNo = code,
            Barcode = code,
            VoucherAmount = amount,
            Status = GiftVoucherStatusCodes.Active,
            ExpiryDate = DateTime.Today.AddYears(1),
            BatchNo = "AUDIT-GV",
            Description = $"Gift Voucher Rs. {amount:N2}",
            CreatedAt = DateTime.Now,
            CreatedBy = "Audit",
            ActivatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            UpdatedBy = "Audit"
        };
        context.GiftVouchers.Add(voucher);
        context.SaveChanges();
        return voucher;
    }

    public static SalesPayment GiftVoucherPayment(GiftVoucher voucher) => new()
    {
        PaymentType = PaymentTypeCodes.GiftVoucher,
        Amount = voucher.VoucherAmount,
        TenderedAmount = voucher.VoucherAmount,
        ChangeAmount = 0m,
        ReferenceNo = voucher.VoucherNo,
        BankOrCardType = "Gift Voucher",
        GiftVoucherId = voucher.Id,
        GiftVoucherNo = voucher.VoucherNo,
        GiftVoucherBarcode = voucher.Barcode,
        GiftVoucherAmount = voucher.VoucherAmount,
        GiftVoucherForfeitedAmount = 0m,
        EnteredBy = "Audit Cashier",
        TerminalNo = "T01"
    };

    public static CustomerMaster CreditCustomer(AuditDbContextFactory factory, decimal limit)
    {
        using AppDbContext context = factory.CreateDbContext();
        var customer = new CustomerMaster
        {
            CustomerCode = $"AUDC{Random.Shared.Next(100000, 999999)}",
            FullName = "Audit Credit Customer",
            Phone = $"07{Random.Shared.Next(10000000, 99999999)}",
            CustomerType = "Retail",
            IsCreditEnabled = true,
            CreditStatus = "Active",
            CreditLimit = limit,
            CreditDays = 30,
            CurrentBalance = 0m,
            IsCreditLocked = false,
            IsActive = true,
            CreatedBy = "Audit"
        };
        context.CustomerMasters.Add(customer);
        context.SaveChanges();
        return customer;
    }

    public static CustomerReturnRepository ReturnRepository(AuditDbContextFactory factory) =>
        new(factory, new CustomerReturnAllocationCalculator());

    public static async Task<User> AddUserAsync(
        AuditDbContextFactory factory,
        string username,
        string password,
        UserRole role,
        bool active)
    {
        string hash = SecurityHelper.HashData(password, out string salt);
        var user = new User
        {
            FirstName = "Audit",
            LastName = role.ToString(),
            Username = username,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = role,
            IsActive = active
        };
        var repository = new UserRepository(factory);
        await repository.AddAsync(user);
        return user;
    }
}

internal static class ConcurrentAudit
{
    internal sealed record Result<T>(bool Succeeded, T? Value, Exception? Error);

    public static async Task<(Result<T> First, Result<T> Second)> RunPairAsync<T>(
        Func<Task<T>> first,
        Func<Task<T>> second)
    {
        using var gate = new ManualResetEventSlim(false);
        Task<Result<T>> firstTask = Task.Run(async () =>
        {
            gate.Wait();
            return await CaptureAsync(first);
        });
        Task<Result<T>> secondTask = Task.Run(async () =>
        {
            gate.Wait();
            return await CaptureAsync(second);
        });
        gate.Set();
        Result<T>[] results = await Task.WhenAll(firstTask, secondTask);
        return (results[0], results[1]);
    }

    private static async Task<Result<T>> CaptureAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return new Result<T>(true, await action(), null);
        }
        catch (Exception ex)
        {
            return new Result<T>(false, default, ex);
        }
    }
}
