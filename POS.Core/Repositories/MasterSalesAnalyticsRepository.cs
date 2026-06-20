using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Core.Repositories
{
    public class MasterSalesAnalyticsRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public MasterSalesAnalyticsRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ==============================================================================
        // 1. THE PAGINATED GRID ENGINE (Built for 100k+ Records)
        // ==============================================================================
        public async Task<PagedSalesResult> GetPagedSalesAsync(
            DateTime startDate,
            DateTime endDate,
            string searchText,
            string statusFilter, // e.g., "All", "Completed", "Voided", "Returned"
            int pageIndex,       // Starts at 1
            int pageSize)        // Usually 50 or 100
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // 1. Build the Base Query (Deferred - nothing is downloaded yet!)
            var query = context.SalesHeaders.AsNoTracking()
                .Where(h => h.TransactionDate.Date >= startDate.Date && h.TransactionDate.Date <= endDate.Date);

            // Apply Status Filter
            if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "All")
            {
                query = query.Where(h => h.Status == statusFilter);
            }

            // Apply Search Filter (Invoice, Customer, or Cashier)
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var lowerSearch = searchText.ToLower();
                query = query.Where(h =>
                    h.InvoiceNo.ToLower().Contains(lowerSearch) ||
                    h.CustomerName.ToLower().Contains(lowerSearch) ||
                    h.CashierName.ToLower().Contains(lowerSearch));
            }

            // 2. Execute the Macro Math (Count & Grand Totals) DIRECTLY in SQL
            // This is lightning fast because it only returns 3 numbers, not 100,000 rows.
            // 2. Execute the Macro Math (Count & Grand Totals) DIRECTLY in SQL
            // This is lightning fast because it only returns 3 numbers, not 100,000 rows.
            var totalCount = await query.CountAsync();

            // ✅ FIX 1: Double-cast the revenue sum
            var totalRevenue = (decimal)await query.SumAsync(h => (double)h.NetTotal);

            // ✅ FIX 2: Double-cast the cost sum
            var totalCost = (decimal)await context.SalesLines
                .Where(l => query.Select(h => h.Id).Contains(l.SalesHeaderId))
                .SumAsync(l => (double)(l.CostPrice * l.Quantity));

            // 3. Fetch ONLY the specific Page of 50 Records
            var pagedHeaders = await query
                .OrderByDescending(h => h.TransactionDate)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(); // <-- This is where we finally download data (just 50 rows)

            var pagedHeaderIds = pagedHeaders.Select(h => h.Id).ToList();

            // 4. Fetch the specific Lines and Payments ONLY for these 50 rows (N+1 Prevention)
            var associatedLines = await context.SalesLines
                .AsNoTracking()
                .Where(l => pagedHeaderIds.Contains(l.SalesHeaderId))
                .Select(l => new { l.SalesHeaderId, l.CostPrice, l.Quantity })
                .ToListAsync();

            var associatedPayments = await context.SalesPayments
                .AsNoTracking()
                .Where(p => pagedHeaderIds.Contains(p.SalesHeaderId))
                .Select(p => new { p.SalesHeaderId, p.PaymentType })
                .ToListAsync();

            // 5. Assemble the lightweight DTOs for the UI
            var resultRecords = new List<SalesExplorerRecordDto>();

            foreach (var header in pagedHeaders)
            {
                var saleLines = associatedLines.Where(l => l.SalesHeaderId == header.Id);
                var salePayments = associatedPayments.Where(p => p.SalesHeaderId == header.Id);

                // Group the payments into a single clean string (e.g., "Cash, Visa")
                var paymentString = string.Join(", ", salePayments.Select(p => p.PaymentType).Distinct());

                var saleCost = saleLines.Sum(l => l.CostPrice * l.Quantity);

                resultRecords.Add(new SalesExplorerRecordDto
                {
                    SaleId = header.Id,
                    InvoiceNo = header.InvoiceNo,
                    TransactionDate = header.TransactionDate,
                    CustomerName = header.CustomerName,
                    CashierName = header.CashierName,
                    Status = header.Status,
                    GrossAmount = header.GrossTotal,
                    TotalDiscount = header.TotalDiscount,
                    NetAmount = header.NetTotal,
                    TotalCost = saleCost,
                    PaymentMethods = string.IsNullOrWhiteSpace(paymentString) ? "Unknown" : paymentString
                });
            }

            return new PagedSalesResult
            {
                Records = resultRecords,
                TotalCount = totalCount,
                SummaryTotalRevenue = totalRevenue,
                SummaryTotalProfit = totalRevenue - totalCost
            };
        }

        // ==============================================================================
        // 2. THE RECEIPT DRILL-DOWN ENGINE (Deep Dive into a Single Sale)
        // ==============================================================================
        public async Task<SaleReceiptDetailsDto?> GetSaleReceiptDetailsAsync(int saleId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var sale = await context.SalesHeaders
                .AsNoTracking()
                .Include(h => h.SalesLines)
                .Include(h => h.SalesPayments)
                .FirstOrDefaultAsync(h => h.Id == saleId);

            if (sale == null) return null;

            var dto = new SaleReceiptDetailsDto
            {
                SaleId = sale.Id,
                InvoiceNo = sale.InvoiceNo,
                TransactionDate = sale.TransactionDate,
                TerminalNo = sale.TerminalNo,
                CashierName = sale.CashierName,
                CustomerName = sale.CustomerName,
                Status = sale.Status,
                GrossAmount = sale.GrossTotal,
                TotalDiscount = sale.TotalDiscount,
                NetAmount = sale.NetTotal,

                Lines = sale.SalesLines.Select(l => new SaleReceiptLineDto
                {
                    ItemCode = l.ItemDescription, // Fallback, update if you store ItemCode explicitly in SalesLine
                    Description = l.ItemDescription,
                    Qty = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    DiscountAmount = l.DiscountAmount,
                    LineTotal = l.LineTotal
                }).ToList(),

                Payments = sale.SalesPayments.Select(p => new SaleReceiptPaymentDto
                {
                    PaymentType = p.PaymentType,
                    Amount = p.Amount,
                    ReferenceNo = p.ReferenceNo
                }).ToList()
            };

            return dto;
        }
    }
}