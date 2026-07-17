using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Enums;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Services.Licensing;

namespace POS.Cashier.AuditTests;

internal static class CashierControlAuditTests
{
    public static async Task CheckoutFailureAfterHeaderStagingRollsBackAsync()
    {
        using var factory = new AuditDbContextFactory();
        AuditScenario scenario = AuditSeed.CreateScenario(factory);
        SalesLine invalidBatchLine = AuditSeed.StockLine(scenario, batchId: int.MaxValue);

        await AuditAssert.ThrowsAsync(
            () => new SalesRepository(factory).ProcessCheckoutAsync(
                AuditSeed.Header(scenario, 1180m),
                new List<SalesLine> { invalidBatchLine },
                new List<SalesPayment> { AuditSeed.Payment(PaymentTypeCodes.Cash, 1180m) }),
            "batch");

        using AppDbContext context = factory.CreateDbContext();
        AuditAssert.Equal(0, context.SalesHeaders.Count(), "rolled-back sale headers");
        AuditAssert.Equal(0, context.SalesLines.Count(), "rolled-back sale lines");
        AuditAssert.Equal(0, context.SalesPayments.Count(), "rolled-back payments");
        AuditAssert.Equal(0, context.InventoryTransactions.Count(), "rolled-back inventory movements");
        AuditAssert.Money(5m, context.ItemBatches.Single(row => row.Id == scenario.StockBatchId).CurrentStock,
            "stock after rollback");
    }

    public static async Task CardSaleRequiresRepositoryReferenceAsync()
    {
        using var factory = new AuditDbContextFactory();
        AuditScenario scenario = AuditSeed.CreateScenario(factory);

        await AuditAssert.ThrowsAsync(
            () => new SalesRepository(factory).ProcessCheckoutAsync(
                AuditSeed.Header(scenario, 0m, PaymentTypeCodes.Card),
                new List<SalesLine> { AuditSeed.ServiceLine(scenario) },
                new List<SalesPayment> { AuditSeed.Payment(PaymentTypeCodes.Card, 1180m) }),
            "reference");

        using AppDbContext context = factory.CreateDbContext();
        AuditAssert.Equal(0, context.SalesHeaders.Count(), "card sale without reference");
    }

    public static async Task ChequeSaleRequiresRepositoryReferenceAsync()
    {
        using var factory = new AuditDbContextFactory();
        AuditScenario scenario = AuditSeed.CreateScenario(factory);

        await AuditAssert.ThrowsAsync(
            () => new SalesRepository(factory).ProcessCheckoutAsync(
                AuditSeed.Header(scenario, 0m, PaymentTypeCodes.Cheque),
                new List<SalesLine> { AuditSeed.ServiceLine(scenario) },
                new List<SalesPayment> { AuditSeed.Payment(PaymentTypeCodes.Cheque, 1180m) }),
            "reference");

        using AppDbContext context = factory.CreateDbContext();
        AuditAssert.Equal(0, context.SalesHeaders.Count(), "cheque sale without reference");
    }

    public static async Task CheckoutRejectsStaleCataloguePriceAsync()
    {
        using var factory = new AuditDbContextFactory();
        AuditScenario scenario = AuditSeed.CreateScenario(factory);

        using (AppDbContext context = factory.CreateDbContext())
        {
            ItemVariant service = context.ItemVariants.Single(row => row.Id == scenario.ServiceVariantId);
            service.RetailPrice = 1500m;
            service.WholesalePrice = 1400m;
            context.SaveChanges();
        }

        await AuditAssert.ThrowsAsync(
            () => new SalesRepository(factory).ProcessCheckoutAsync(
                AuditSeed.Header(scenario, 1180m),
                new List<SalesLine> { AuditSeed.ServiceLine(scenario, unitPrice: 1180m) },
                new List<SalesPayment> { AuditSeed.Payment(PaymentTypeCodes.Cash, 1180m) }),
            "price");

        using AppDbContext verify = factory.CreateDbContext();
        AuditAssert.Equal(0, verify.SalesHeaders.Count(), "stale-price sale count");
    }

    public static async Task PriceOverrideValidatesApproverRoleAsync()
    {
        using var factory = new AuditDbContextFactory();
        AuditScenario scenario = AuditSeed.CreateScenario(factory);
        await AuditSeed.AddUserAsync(factory, "auditcashier", "cashier1", UserRole.Cashier, active: true);

        using (AppDbContext context = factory.CreateDbContext())
        {
            ItemVariant service = context.ItemVariants.Single(row => row.Id == scenario.ServiceVariantId);
            service.MinimumPrice = 1000m;
            context.SaveChanges();
        }

        SalesLine line = AuditSeed.ServiceLine(scenario, unitPrice: 900m);
        line.IsPriceOverridden = true;
        line.OriginalUnitPrice = 1180m;
        line.PriceOverrideApprovedBy = "auditcashier";
        line.PriceOverrideApprovedAt = DateTime.Now;

        await AuditAssert.ThrowsAsync(
            () => new SalesRepository(factory).ProcessCheckoutAsync(
                AuditSeed.Header(scenario, 900m),
                new List<SalesLine> { line },
                new List<SalesPayment> { AuditSeed.Payment(PaymentTypeCodes.Cash, 900m) }),
            "manager");

        using AppDbContext verify = factory.CreateDbContext();
        AuditAssert.Equal(0, verify.SalesHeaders.Count(), "unauthorized price-override sale count");
    }

    public static async Task StockInquiryReturnsStockItemsOnlyAsync()
    {
        using var factory = new AuditDbContextFactory();
        AuditScenario scenario = AuditSeed.CreateScenario(factory, stock: 7.25m);

        IReadOnlyList<POS.Core.Models.DTOs.StockInquiryResultDto> results =
            await new StockInquiryRepository(factory).SearchAsync("AUD");

        AuditAssert.Equal(1, results.Count, "Stock Inquiry result count");
        AuditAssert.Equal(scenario.StockVariantId, results[0].ItemVariantId,
            "Stock Inquiry Stock Item variant");
        AuditAssert.Money(7.25m, results[0].CurrentStock,
            "Stock Inquiry quantity");
    }

    public static async Task WalkInWholesaleModePersistsAsync()
    {
        using var factory = new AuditDbContextFactory();
        AuditScenario scenario = AuditSeed.CreateScenario(factory);

        SalesHeader header = AuditSeed.Header(scenario, 1062m);
        header.IsWholesaleSale = true;

        SalesHeader saved = await new SalesRepository(factory).ProcessCheckoutAsync(
            header,
            new List<SalesLine> { AuditSeed.ServiceLine(scenario, unitPrice: 1062m) },
            new List<SalesPayment> { AuditSeed.Payment(PaymentTypeCodes.Cash, 1062m) });

        AuditAssert.True(saved.IsWholesaleSale,
            "Walk-in Wholesale pricing mode was not preserved.");
        AuditAssert.Money(1062m, saved.SalesLines.Single().UnitPrice,
            "Walk-in Wholesale unit price");
    }

    public static async Task FloatBalanceExcludesOperationalCashAsync()
    {
        using var factory = new AuditDbContextFactory();
        AuditScenario scenario = AuditSeed.CreateScenario(factory);
        var till = new TillRepository(factory);

        await till.RegisterCashMovementDetailedAsync(new POS.Core.Models.DTOs.CashMovementRegistrationRequest
        {
            ShiftSessionId = scenario.ShiftSessionId,
            MovementType = CashMovementTypeCodes.PaidIn,
            Amount = 500m,
            ReasonCategory = CashMovementReasonCodes.FloatIn,
            Remarks = "Audit float",
            CashierName = "Audit Cashier",
            AuthorizedBy = "Audit Manager"
        });

        await new SalesRepository(factory).ProcessCheckoutAsync(
            AuditSeed.Header(scenario, 1180m),
            new List<SalesLine> { AuditSeed.ServiceLine(scenario) },
            new List<SalesPayment> { AuditSeed.Payment(PaymentTypeCodes.Cash, 1180m) });

        decimal floatBalance = await till.GetCurrentFloatBalanceAsync(scenario.ShiftSessionId);
        AuditAssert.Money(500m, floatBalance,
            "current float balance must contain only opening/Float In less Float Out, not sales cash");
    }

    public static async Task PriceOverrideAcceptsActiveManagerAsync()
    {
        using var factory = new AuditDbContextFactory();
        AuditScenario scenario = AuditSeed.CreateScenario(factory);
        await AuditSeed.AddUserAsync(factory, "auditmanager", "manager1", UserRole.Manager, active: true);

        using (AppDbContext context = factory.CreateDbContext())
        {
            ItemVariant service = context.ItemVariants.Single(row => row.Id == scenario.ServiceVariantId);
            service.MinimumPrice = 1000m;
            context.SaveChanges();
        }

        SalesLine line = AuditSeed.ServiceLine(scenario, unitPrice: 900m);
        line.IsPriceOverridden = true;
        line.OriginalUnitPrice = 1180m;
        line.PriceOverrideApprovedBy = "auditmanager";
        line.PriceOverrideApprovedAt = DateTime.Now;

        SalesHeader sale = await new SalesRepository(factory).ProcessCheckoutAsync(
            AuditSeed.Header(scenario, 900m),
            new List<SalesLine> { line },
            new List<SalesPayment> { AuditSeed.Payment(PaymentTypeCodes.Cash, 900m) });

        AuditAssert.True(sale.Id > 0, "Manager-approved New Price did not complete.");
        AuditAssert.Equal("auditmanager", sale.SalesLines.Single().PriceOverrideApprovedBy,
            "canonical New Price approver");
    }

    public static async Task FloatOutCannotConsumeSalesCashAsync()
    {
        using var factory = new AuditDbContextFactory();
        AuditScenario scenario = AuditSeed.CreateScenario(factory);
        var till = new TillRepository(factory);

        await new SalesRepository(factory).ProcessCheckoutAsync(
            AuditSeed.Header(scenario, 1180m),
            new List<SalesLine> { AuditSeed.ServiceLine(scenario) },
            new List<SalesPayment> { AuditSeed.Payment(PaymentTypeCodes.Cash, 1180m) });

        await AuditAssert.ThrowsAsync(
            () => till.RegisterCashMovementDetailedAsync(new POS.Core.Models.DTOs.CashMovementRegistrationRequest
            {
                ShiftSessionId = scenario.ShiftSessionId,
                MovementType = CashMovementTypeCodes.PaidOut,
                Amount = 100m,
                ReasonCategory = CashMovementReasonCodes.FloatOut,
                Remarks = "Must not remove sales cash as float",
                CashierName = "Audit Cashier",
                AuthorizedBy = "Audit Manager"
            }),
            "float");

        decimal floatBalance = await till.GetCurrentFloatBalanceAsync(scenario.ShiftSessionId);
        AuditAssert.Money(0m, floatBalance, "float balance after rejected Float Out");
    }

    public static async Task DisabledUserCannotAuthenticateAsync()
    {
        using var factory = new AuditDbContextFactory();
        await AuditSeed.AddUserAsync(factory, "disabledaudit", "audit123", UserRole.Cashier, active: false);
        var auth = new AuthService(new UserRepository(factory));

        var result = await auth.LoginAsync("disabledaudit", "audit123", "CASHIER-AUDIT");
        AuditAssert.False(result.Success, "Disabled user authenticated successfully.");
        AuditAssert.True(auth.CurrentUser is null, "Disabled user replaced the current session identity.");

        using AppDbContext context = factory.CreateDbContext();
        AuditAssert.True(context.LoginAuditEvents.Any(), "Disabled login attempt did not create an audit record.");
    }

    public static async Task ZeroOpeningShiftAndSingleOpenShiftAsync()
    {
        using var factory = new AuditDbContextFactory();
        var till = new TillRepository(factory);

        ShiftSession first = await till.CreateNewShiftAsync("T02", "Second Cashier");
        AuditAssert.Money(0m, first.OpeningCash, "zero-opening shift");
        AuditAssert.Equal(ShiftStatusCodes.Open, first.Status, "new shift status");

        await AuditAssert.ThrowsAsync(
            () => till.CreateNewShiftAsync("T02", "Another Cashier"),
            "already has an open shift");

        using AppDbContext context = factory.CreateDbContext();
        AuditAssert.Equal(1, context.ShiftSessions.Count(row => row.TerminalNo == "T02" && row.Status == ShiftStatusCodes.Open),
            "open shift count for terminal");
    }

    public static async Task TerminalReleaseBlocksActiveWorkAndPreservesIdentityAsync()
    {
        using var factory = new AuditDbContextFactory();

        int terminalId;
        int shiftId;
        int cartId;

        using (AppDbContext context = factory.CreateDbContext())
        {
            var settings = new TerminalSettings
            {
                TerminalNo = "01",
                TerminalName = "Main Cashier",
                MachineName = "OLD-CASHIER-PC",
                Location = "Main Store",
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            var terminal = new POS.Core.Models.Terminals.RegisteredTerminal
            {
                TerminalNo = "01",
                TerminalName = "Main Cashier",
                MachineName = "OLD-CASHIER-PC",
                MachineCode = "OLD-MACHINE-CODE",
                Location = "Main Store",
                IsCashierTerminal = true,
                IsBackOfficeAllowed = true,
                IsActive = true,
                LicenseId = "LIC-OLD-01",
                LicenseExpiryDate = DateTime.Today.AddYears(1),
                CreatedAt = DateTime.Now
            };

            var shift = new ShiftSession
            {
                TerminalNo = "01",
                CashierName = "auditcashier",
                StartTime = DateTime.Now,
                Status = "Open"
            };

            context.TerminalSettings.Add(settings);
            context.RegisteredTerminals.Add(terminal);
            context.ShiftSessions.Add(shift);
            context.SaveChanges();

            terminalId = terminal.Id;
            shiftId = shift.Id;
        }

        var repository = new TerminalManagementRepository(
            factory,
            new MachineFingerprintService(),
            new TerminalSettingsRepository(factory));

        await AuditAssert.ThrowsAsync(
            () => repository.ReleaseMachineAssignmentAsync(
                terminalId,
                "auditadmin"),
            "shift");

        using (AppDbContext context = factory.CreateDbContext())
        {
            ShiftSession shift = context.ShiftSessions.Single(row => row.Id == shiftId);
            shift.Status = "Closed";
            shift.EndTime = DateTime.Now;

            var cart = new CashierCartSession
            {
                CartToken = Guid.NewGuid(),
                ReferenceNo = "AUDIT-RELEASE-01",
                ShiftSessionId = shiftId,
                TerminalNo = "01",
                CashierName = "auditcashier",
                Status = CashierCartStatusCodes.Held,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            context.CashierCartSessions.Add(cart);
            context.SaveChanges();
            cartId = cart.Id;
        }

        await AuditAssert.ThrowsAsync(
            () => repository.ReleaseMachineAssignmentAsync(
                terminalId,
                "auditadmin"),
            "cart");

        using (AppDbContext context = factory.CreateDbContext())
        {
            CashierCartSession cart = context.CashierCartSessions.Single(row => row.Id == cartId);
            cart.Status = CashierCartStatusCodes.Cancelled;
            cart.CancelledAtUtc = DateTime.UtcNow;
            cart.CancelledBy = "auditadmin";
            context.SaveChanges();
        }

        await repository.ReleaseMachineAssignmentAsync(
            terminalId,
            "auditadmin");

        using AppDbContext verify = factory.CreateDbContext();

        var released = verify.RegisteredTerminals.Single(row => row.Id == terminalId);
        TerminalSettings persistedSettings = verify.TerminalSettings.Single(row => row.TerminalNo == "01");

        AuditAssert.Equal("01", released.TerminalNo, "released terminal number");
        AuditAssert.Equal("Main Cashier", released.TerminalName, "released terminal name");
        AuditAssert.Equal(string.Empty, released.MachineName, "released machine name");
        AuditAssert.Equal(string.Empty, released.MachineCode, "released machine code");
        AuditAssert.Equal(string.Empty, released.LicenseId, "released licence snapshot");
        AuditAssert.Equal(string.Empty, persistedSettings.MachineName, "released settings machine name");
        AuditAssert.Equal(1, verify.ShiftSessions.Count(), "historical shift count");
        AuditAssert.Equal(1, verify.CashierCartSessions.Count(), "historical cart count");
    }

}
