using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    public class StockAdjustmentRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public StockAdjustmentRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // --- ATOMIC ADJUSTMENT SAVING ENGINE ---
        public async Task SaveAdjustmentAsync(StockAdjustmentHeader header, List<StockAdjustmentLine> lines, bool isDraft)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // 1. Finalize Header State
                header.Status = isDraft ? "Draft" : "Posted";

                if (header.Id == 0)
                {
                    header.CreatedAt = DateTime.Now;
                    await context.StockAdjustmentHeaders.AddAsync(header);
                    await context.SaveChangesAsync(); // Generates Header ID
                }
                else
                {
                    context.StockAdjustmentHeaders.Update(header);
                    await context.SaveChangesAsync();
                }

                // 2. Wipe existing lines to rebuild cleanly (if updating a Draft)
                var existingLines = await context.StockAdjustmentLines.Where(l => l.StockAdjustmentHeaderId == header.Id).ToListAsync();
                context.StockAdjustmentLines.RemoveRange(existingLines);
                await context.SaveChangesAsync();

                // 3. Insert New Lines
                foreach (var line in lines)
                {
                    line.StockAdjustmentHeaderId = header.Id;
                    await context.StockAdjustmentLines.AddAsync(line);

                    // 4. IF POSTING: Update the Physical Inventory Ledgers
                    if (!isDraft)
                    {
                        // In a true ERP, you do not update a static number. You insert a ledger record 
                        // representing the variance (+ or -) so the history is perfectly preserved.

                        // Example Ledger Insertion:
                        // var ledgerEntry = new InventoryLedger 
                        // { 
                        //     ItemVariantId = line.ItemVariantId, 
                        //     QuantityChange = line.VarianceQty, 
                        //     TransactionType = "ADJUSTMENT", 
                        //     ReferenceNumber = header.AdjustmentNo,
                        //     Date = DateTime.Now 
                        // };
                        // await context.InventoryLedgers.AddAsync(ledgerEntry);
                    }
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}