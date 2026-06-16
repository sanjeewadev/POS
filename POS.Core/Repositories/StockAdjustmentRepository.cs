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
                // 1. CONCURRENCY LOCK: Get safe, sequential ADJ Number
                if (header.Id == 0)
                {
                    var sequence = await context.DocumentSequences.FirstOrDefaultAsync(s => s.DocumentType == "ADJ");
                    bool isNewSequence = false;

                    if (sequence == null)
                    {
                        sequence = new DocumentSequence { DocumentType = "ADJ", Prefix = "ADJ-", NextSequenceNumber = 1, PaddingLength = 5, UpdatedAt = DateTime.Now };
                        isNewSequence = true;
                    }

                    header.AdjustmentNo = $"{sequence.Prefix}{sequence.NextSequenceNumber.ToString().PadLeft(sequence.PaddingLength, '0')}";

                    sequence.NextSequenceNumber++;
                    sequence.UpdatedAt = DateTime.Now;

                    if (isNewSequence)
                        await context.DocumentSequences.AddAsync(sequence);
                    else
                        context.DocumentSequences.Update(sequence);

                    header.Status = isDraft ? "Draft" : "Posted";
                    header.CreatedAt = DateTime.Now;
                    await context.StockAdjustmentHeaders.AddAsync(header);
                    await context.SaveChangesAsync();
                }
                else
                {
                    // Existing Draft: Verify it hasn't been posted yet
                    var existingHeader = await context.StockAdjustmentHeaders.AsNoTracking().FirstOrDefaultAsync(h => h.Id == header.Id);
                    if (existingHeader != null && existingHeader.Status == "Posted")
                    {
                        throw new InvalidOperationException("CRITICAL: This Adjustment has already been posted to the P&L and cannot be modified.");
                    }

                    header.Status = isDraft ? "Draft" : "Posted";
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

                    // 4. IF POSTING: Deduct Stock & Write to Immutable Ledger
                    if (!isDraft)
                    {
                        // Fetch the specific Batch Bucket
                        var targetBatch = await context.ItemBatches.FirstOrDefaultAsync(b => b.Id == line.ItemBatchId);
                        if (targetBatch == null)
                            throw new Exception($"CRITICAL: Batch ID {line.ItemBatchId} could not be found in the database.");

                        // Apply the variance to the physical bucket (+ or -)
                        targetBatch.CurrentStock += line.VarianceQty;
                        context.ItemBatches.Update(targetBatch);

                        // Write to the global immutable P&L Ledger
                        var ledgerEntry = new InventoryTransaction
                        {
                            ItemVariantId = targetBatch.ItemVariantId,
                            TransactionDate = header.AdjustmentDate,
                            TransactionType = "ADJUSTMENT",
                            ReferenceDocument = header.AdjustmentNo,
                            Quantity = line.VarianceQty,
                            UnitCost = line.UnitCost,
                            CreatedBy = header.AuthorizedBy,
                            Remarks = $"Reason: {line.ReasonCode} | Batch: {targetBatch.BatchNo} | Ref: {header.Reference}"
                        };

                        await context.InventoryTransactions.AddAsync(ledgerEntry);
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