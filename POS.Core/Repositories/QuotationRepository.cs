using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models.DTOs;
using POS.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using POS.Core.DTOs;

namespace POS.Core.Repositories
{
    public class QuotationRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public QuotationRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ==============================================================================
        // 1. THE PAGINATED GRID ENGINE (For Searching Old Quotes)
        // ==============================================================================
        public async Task<(List<QuotationGridDto> Records, int TotalCount)> GetPagedQuotationsAsync(
            DateTime startDate, DateTime endDate, string searchText, string statusFilter, int pageIndex, int pageSize)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.QuotationHeaders.AsNoTracking()
                .Where(q => q.DateCreated.Date >= startDate.Date && q.DateCreated.Date <= endDate.Date);

            if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "All")
                query = query.Where(q => q.Status == statusFilter);

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var lowerSearch = searchText.ToLower();
                query = query.Where(q =>
                    q.QuoteNo.ToLower().Contains(lowerSearch) ||
                    q.CustomerName.ToLower().Contains(lowerSearch));
            }

            var totalCount = await query.CountAsync();

            var records = await query
                .OrderByDescending(q => q.DateCreated)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(q => new QuotationGridDto
                {
                    QuotationId = q.Id,
                    QuoteNo = q.QuoteNo,
                    DateCreated = q.DateCreated,
                    ValidUntil = q.ValidUntil,
                    CustomerName = q.CustomerName,
                    CashierName = q.CashierName,
                    NetTotal = q.NetTotal,
                    Status = q.Status
                })
                .ToListAsync();

            return (records, totalCount);
        }

        // ==============================================================================
        // 2. THE QUOTE BUILDER (Saving a New Quote)
        // ==============================================================================
        public async Task<string> SaveQuotationAsync(QuotationDetailDto dto)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Generate a secure Quote Number (e.g., QT-20260618-001)
            var todayStr = DateTime.Today.ToString("yyyyMMdd");
            var countToday = await context.QuotationHeaders.CountAsync(q => q.QuoteNo.Contains(todayStr));
            var newQuoteNo = $"QT-{todayStr}-{(countToday + 1):D3}";

            var header = new QuotationHeader
            {
                QuoteNo = newQuoteNo,
                DateCreated = DateTime.Now,
                ValidUntil = dto.ValidUntil,
                Status = "Draft",
                CustomerName = dto.CustomerName,
                CustomerPhone = dto.CustomerPhone,
                CustomerEmail = dto.CustomerEmail,
                CashierName = dto.CashierName,
                TerminalNo = dto.TerminalNo,
                GrossTotal = dto.GrossTotal,
                TotalDiscount = dto.TotalDiscount,
                NetTotal = dto.NetTotal,
                Notes = dto.Notes
            };

            foreach (var line in dto.Lines)
            {
                header.QuotationLines.Add(new QuotationLine
                {
                    ItemCode = line.ItemCode,
                    ItemDescription = line.ItemDescription,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountAmount = line.DiscountAmount,
                    LineTotal = line.LineTotal,
                    CostPrice = line.CostPrice
                });
            }

            context.QuotationHeaders.Add(header);
            await context.SaveChangesAsync();

            return newQuoteNo;
        }

        // ==============================================================================
        // 3. GET SINGLE QUOTE (For Printing or Converting)
        // ==============================================================================
        public async Task<QuotationDetailDto?> GetQuotationDetailsAsync(int quotationId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var q = await context.QuotationHeaders
                .AsNoTracking()
                .Include(h => h.QuotationLines)
                .FirstOrDefaultAsync(h => h.Id == quotationId);

            if (q == null) return null;

            return new QuotationDetailDto
            {
                QuotationId = q.Id,
                QuoteNo = q.QuoteNo,
                DateCreated = q.DateCreated,
                ValidUntil = q.ValidUntil,
                Status = q.Status,
                CustomerName = q.CustomerName,
                CustomerPhone = q.CustomerPhone,
                CustomerEmail = q.CustomerEmail,
                CashierName = q.CashierName,
                TerminalNo = q.TerminalNo,
                Notes = q.Notes,
                GrossTotal = q.GrossTotal,
                TotalDiscount = q.TotalDiscount,
                NetTotal = q.NetTotal,
                Lines = q.QuotationLines.Select(l => new QuotationLineDto
                {
                    ItemCode = l.ItemCode,
                    ItemDescription = l.ItemDescription,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    DiscountAmount = l.DiscountAmount,
                    CostPrice = l.CostPrice
                }).ToList()
            };
        }

        // ==============================================================================
        // 4. THE ENTERPRISE ENGINE: CONVERT QUOTE TO LIVE SALE
        // ==============================================================================
        public async Task<bool> ConvertQuoteToSaleAsync(int quotationId, int activeShiftSessionId, string paymentMethod)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // SECURITY: Open an SQL Transaction. If this fails, no data is corrupted.
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // 1. Fetch the Quote
                var quote = await context.QuotationHeaders
                    .Include(h => h.QuotationLines)
                    .FirstOrDefaultAsync(h => h.Id == quotationId);

                if (quote == null || quote.Status == "Converted")
                    throw new Exception("Quote not found or already converted.");

                // 2. Generate new live Invoice Number
                var todayStr = DateTime.Today.ToString("yyyyMMdd");
                var countToday = await context.SalesHeaders.CountAsync(s => s.InvoiceNo.Contains(todayStr));
                var newInvoiceNo = $"INV-{todayStr}-{(countToday + 1):D3}";

                // 3. Create the Live Sale Header
                var liveSale = new SalesHeader
                {
                    InvoiceNo = newInvoiceNo,
                    TransactionDate = DateTime.Now,
                    ShiftSessionId = activeShiftSessionId,
                    TerminalNo = quote.TerminalNo,
                    CashierName = quote.CashierName,
                    CustomerName = quote.CustomerName,
                    GrossTotal = quote.GrossTotal,
                    TotalDiscount = quote.TotalDiscount,
                    NetTotal = quote.NetTotal,
                    Status = "Completed"
                };

                // 4. Create the Live Sale Lines AND Deduct Inventory
                foreach (var quoteLine in quote.QuotationLines)
                {
                    liveSale.SalesLines.Add(new SalesLine
                    {
                        ItemDescription = quoteLine.ItemDescription,
                        Quantity = quoteLine.Quantity,
                        UnitPrice = quoteLine.UnitPrice,
                        DiscountAmount = quoteLine.DiscountAmount,
                        LineTotal = quoteLine.LineTotal,
                        CostPrice = quoteLine.CostPrice
                    });

                    // INVENTORY DEDUCTION (Assuming you have an ItemMaster table)
                    // If your table name is different, adjust 'ItemMaster' here!
                    // var inventoryItem = await context.ItemMaster.FirstOrDefaultAsync(i => i.ItemCode == quoteLine.ItemCode);
                    // if (inventoryItem != null)
                    // {
                    //     inventoryItem.CurrentStock -= quoteLine.Quantity;
                    // }
                }

                // 5. Create the Payment Record
                liveSale.SalesPayments.Add(new SalesPayment
                {
                    PaymentType = paymentMethod,
                    Amount = quote.NetTotal
                });

                // 6. Update Quote Status
                quote.Status = "Converted";

                // 7. Save and Commit the Transaction!
                context.SalesHeaders.Add(liveSale);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw; // Push error to the UI
            }
        }
    }
}