using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;

namespace POS.Cashier.AuditTests;

internal static class CashierConcurrencyAuditTests
{
    public static async Task ConcurrentSameTokenCheckoutCreatesOneEffectAsync()
    {
        using var factory = new AuditDbContextFactory();
        AuditScenario scenario = AuditSeed.CreateScenario(factory);
        Guid token = Guid.NewGuid();
        await AuditSeed.SaveActiveCartAsync(factory, scenario, token);

        Task<SalesHeader> Checkout() => new SalesRepository(factory).ProcessCheckoutAsync(
            AuditSeed.Header(scenario, 1180m),
            new List<SalesLine> { AuditSeed.StockLine(scenario) },
            new List<SalesPayment> { AuditSeed.Payment(PaymentTypeCodes.Cash, 1180m) },
            token);

        var results = await ConcurrentAudit.RunPairAsync(Checkout, Checkout);
        AuditAssert.True(results.First.Succeeded && results.Second.Succeeded,
            DescribePairFailure("Both same-token calls must return the saved invoice", results.First.Error, results.Second.Error));
        AuditAssert.Equal(results.First.Value!.Id, results.Second.Value!.Id, "same-token sale id");

        using AppDbContext context = factory.CreateDbContext();
        AuditAssert.Equal(1, context.SalesHeaders.Count(), "sale count");
        AuditAssert.Equal(1, context.SalesPayments.Count(), "payment count");
        AuditAssert.Equal(1, context.InventoryTransactions.Count(row => row.TransactionType == "SALE"), "inventory movement count");
        AuditAssert.Money(4m, context.ItemBatches.Single(row => row.Id == scenario.StockBatchId).CurrentStock, "remaining stock");
    }

    public static async Task ConcurrentLastStockCheckoutIsSafeAsync()
    {
        using var factory = new AuditDbContextFactory();
        AuditScenario scenario = AuditSeed.CreateScenario(factory, stock: 1m);
        int secondShiftId;
        using (AppDbContext setupContext = factory.CreateDbContext())
        {
            var secondShift = new ShiftSession
            {
                TerminalNo = "T02",
                CashierName = "Audit Cashier Two",
                StartTime = DateTime.Now,
                Status = ShiftStatusCodes.Open,
                OpeningCash = 0m,
                ExpectedCash = 0m
            };
            setupContext.ShiftSessions.Add(secondShift);
            setupContext.SaveChanges();
            secondShiftId = secondShift.Id;
        }

        Guid firstToken = Guid.NewGuid();
        Guid secondToken = Guid.NewGuid();
        await AuditSeed.SaveActiveCartAsync(factory, scenario, firstToken);
        await AuditSeed.SaveActiveCartAsync(
            factory,
            scenario,
            secondToken,
            shiftSessionId: secondShiftId,
            terminalNo: "T02",
            cashierName: "Audit Cashier Two");

        SalesHeader firstHeader = AuditSeed.Header(scenario, 1180m);
        SalesHeader secondHeader = AuditSeed.Header(scenario, 1180m);
        secondHeader.ShiftSessionId = secondShiftId;
        secondHeader.TerminalNo = "T02";
        secondHeader.CashierName = "Audit Cashier Two";

        Task<SalesHeader> Checkout(SalesHeader header, Guid token) => new SalesRepository(factory).ProcessCheckoutAsync(
            header,
            new List<SalesLine> { AuditSeed.StockLine(scenario) },
            new List<SalesPayment> { AuditSeed.Payment(PaymentTypeCodes.Cash, 1180m) },
            token);

        var results = await ConcurrentAudit.RunPairAsync(
            () => Checkout(firstHeader, firstToken),
            () => Checkout(secondHeader, secondToken));

        int succeeded = CountSucceeded(results.First, results.Second);
        AuditAssert.Equal(1, succeeded,
            DescribePairFailure("Exactly one last-stock checkout must succeed", results.First.Error, results.Second.Error));

        using AppDbContext context = factory.CreateDbContext();
        ItemBatch batch = context.ItemBatches.Single(row => row.Id == scenario.StockBatchId);
        AuditAssert.Money(0m, batch.CurrentStock, "remaining last stock");
        AuditAssert.True(batch.CurrentStock >= 0m, "Stock became negative.");
        AuditAssert.Equal(1, context.SalesHeaders.Count(), "last-stock sale count");
        AuditAssert.Equal(1, context.SalesPayments.Count(), "last-stock payment count");
        AuditAssert.Equal(1, context.InventoryTransactions.Count(row => row.TransactionType == "SALE"), "last-stock movement count");
    }

    public static async Task ConcurrentGiftVoucherRedemptionIsOneTimeAsync()
    {
        using var factory = new AuditDbContextFactory();
        AuditScenario scenario = AuditSeed.CreateScenario(factory);
        GiftVoucher voucher = AuditSeed.GiftVoucher(factory, 1180m);

        Task<SalesHeader> Redeem() => new SalesRepository(factory).ProcessCheckoutAsync(
            AuditSeed.Header(scenario, 0m, PaymentTypeCodes.GiftVoucher),
            new List<SalesLine> { AuditSeed.ServiceLine(scenario) },
            new List<SalesPayment> { AuditSeed.GiftVoucherPayment(voucher) });

        var results = await ConcurrentAudit.RunPairAsync(Redeem, Redeem);
        AuditAssert.Equal(1, CountSucceeded(results.First, results.Second),
            DescribePairFailure("Exactly one voucher redemption must succeed", results.First.Error, results.Second.Error));

        using AppDbContext context = factory.CreateDbContext();
        GiftVoucher saved = context.GiftVouchers.Single(row => row.Id == voucher.Id);
        AuditAssert.Equal(GiftVoucherStatusCodes.Redeemed, saved.Status, "voucher status");
        AuditAssert.Equal(1, context.GiftVoucherTransactions.Count(row => row.ReferenceKey == $"REDEEM:{voucher.Id}"),
            "voucher redemption transaction count");
        AuditAssert.Equal(1, context.SalesHeaders.Count(), "voucher-funded sale count");
    }

    public static async Task ConcurrentCustomerCreditCannotExceedLimitAsync()
    {
        using var factory = new AuditDbContextFactory();
        AuditScenario scenario = AuditSeed.CreateScenario(factory);
        CustomerMaster customer = AuditSeed.CreditCustomer(factory, 1180m);

        Task<SalesHeader> CreditSale() => new SalesRepository(factory).ProcessCheckoutAsync(
            new SalesHeader
            {
                ShiftSessionId = scenario.ShiftSessionId,
                TerminalNo = "T01",
                CashierName = "Audit Cashier",
                CustomerMasterId = customer.Id,
                CustomerName = customer.FullName,
                CustomerType = customer.CustomerType,
                PaymentMethod = CustomerCreditCodes.PaymentType,
                AmountTendered = 0m
            },
            new List<SalesLine> { AuditSeed.ServiceLine(scenario) },
            new List<SalesPayment>
            {
                AuditSeed.Payment(CustomerCreditCodes.PaymentType, 1180m, customer.CustomerCode, "Customer Account")
            });

        var results = await ConcurrentAudit.RunPairAsync(CreditSale, CreditSale);
        AuditAssert.Equal(1, CountSucceeded(results.First, results.Second),
            DescribePairFailure("Exactly one credit sale may consume the remaining limit", results.First.Error, results.Second.Error));

        using AppDbContext context = factory.CreateDbContext();
        CustomerMaster saved = context.CustomerMasters.Single(row => row.Id == customer.Id);
        AuditAssert.Money(1180m, saved.CurrentBalance, "customer balance");
        AuditAssert.Equal(1, context.CustomerLedgers.Count(row => row.CustomerMasterId == customer.Id), "credit ledger count");
        AuditAssert.Equal(1, context.SalesHeaders.Count(), "credit sale count");
    }

    public static async Task ConcurrentCustomerReturnCannotOverReturnAsync()
    {
        using var factory = new AuditDbContextFactory();
        AuditScenario scenario = AuditSeed.CreateScenario(factory);
        SalesHeader sale = await new SalesRepository(factory).ProcessCheckoutAsync(
            AuditSeed.Header(scenario, 1180m),
            new List<SalesLine> { AuditSeed.ServiceLine(scenario) },
            new List<SalesPayment> { AuditSeed.Payment(PaymentTypeCodes.Cash, 1180m) });
        int salesLineId = sale.SalesLines.Single().Id;

        CustomerReturnRequest Request() => new()
        {
            SalesHeaderId = sale.Id,
            ShiftSessionId = scenario.ShiftSessionId,
            TerminalNo = "T01",
            CashierName = "Audit Cashier",
            AuthorizedBy = "Audit Manager",
            ReturnReason = "Concurrent audit",
            Lines = new List<CustomerReturnRequestLine>
            {
                new() { SalesLineId = salesLineId, Quantity = 1m }
            }
        };

        var results = await ConcurrentAudit.RunPairAsync(
            () => AuditSeed.ReturnRepository(factory).ProcessReturnAsync(Request()),
            () => AuditSeed.ReturnRepository(factory).ProcessReturnAsync(Request()));

        AuditAssert.Equal(1, CountSucceeded(results.First, results.Second),
            DescribePairFailure("Exactly one return of the remaining quantity must succeed", results.First.Error, results.Second.Error));

        using AppDbContext context = factory.CreateDbContext();
        AuditAssert.Equal(1, context.CustomerReturnHeaders.Count(), "return header count");
        decimal returnedQuantity = context.CustomerReturnLines
            .AsNoTracking()
            .Select(row => row.QuantityReturned)
            .ToList()
            .Sum();
        decimal refundTotal = context.CustomerReturnHeaders
            .AsNoTracking()
            .Select(row => row.TotalRefundAmount)
            .ToList()
            .Sum();
        AuditAssert.Money(1m, returnedQuantity, "returned quantity");
        AuditAssert.Money(1180m, refundTotal, "refund total");
    }

    public static async Task CheckoutAndShiftCloseRemainConsistentAsync()
    {
        using var factory = new AuditDbContextFactory();
        AuditScenario scenario = AuditSeed.CreateScenario(factory);
        Guid token = Guid.NewGuid();
        await AuditSeed.SaveActiveCartAsync(factory, scenario, token);

        async Task<string> Checkout()
        {
            await new SalesRepository(factory).ProcessCheckoutAsync(
                AuditSeed.Header(scenario, 1180m),
                new List<SalesLine> { AuditSeed.StockLine(scenario) },
                new List<SalesPayment> { AuditSeed.Payment(PaymentTypeCodes.Cash, 1180m) },
                token);
            return "checkout";
        }

        async Task<string> Close()
        {
            await new TillRepository(factory).CloseShiftSafelyAsync(new ShiftCloseRequest
            {
                ShiftSessionId = scenario.ShiftSessionId,
                TerminalNo = "T01",
                CashierName = "Audit Cashier",
                CountedCash = 0m,
                ClosedBy = "Audit Cashier",
                AuthorizedBy = "Audit Manager",
                VarianceNote = "Concurrent audit",
                CloseToken = Guid.NewGuid()
            });
            return "close";
        }

        var results = await ConcurrentAudit.RunPairAsync(Checkout, Close);

        using AppDbContext context = factory.CreateDbContext();
        ShiftSession shift = context.ShiftSessions.Single(row => row.Id == scenario.ShiftSessionId);
        int saleCount = context.SalesHeaders.Count();
        int closeCount = context.ShiftCloseSnapshots.Count();

        AuditAssert.True(saleCount is 0 or 1, "More than one sale was created.");
        AuditAssert.True(closeCount is 0 or 1, "More than one close snapshot was created.");
        AuditAssert.False(shift.Status == ShiftStatusCodes.Closed && saleCount == 0 && context.CashierCartSessions.Any(row => row.Status == CashierCartStatusCodes.Completed),
            "A cart was marked completed without a sale.");
        if (shift.Status == ShiftStatusCodes.Closed)
            AuditAssert.Equal(1, closeCount, "closed shift snapshot count");
        if (saleCount == 1)
            AuditAssert.Equal(1, context.SalesPayments.Count(), "checkout payment count");

        AuditAssert.True(results.First.Succeeded || results.Second.Succeeded,
            DescribePairFailure("At least one concurrent operation should complete", results.First.Error, results.Second.Error));
    }

    private static int CountSucceeded<T>(ConcurrentAudit.Result<T> first, ConcurrentAudit.Result<T> second) =>
        (first.Succeeded ? 1 : 0) + (second.Succeeded ? 1 : 0);

    private static string DescribePairFailure(string message, Exception? first, Exception? second) =>
        $"{message}. First: {first?.Message ?? "success"}. Second: {second?.Message ?? "success"}.";
}
