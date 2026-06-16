using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    public class SalesRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public SalesRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<SalesHeader> ProcessCheckoutAsync(SalesHeader header, List<SalesLine> lines)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // 1. CONCURRENCY LOCK: Generate a secure Invoice Number (e.g., INV-00001)
                var sequence = await context.DocumentSequences.FirstOrDefaultAsync(s => s.DocumentType == "INV");
                if (sequence == null)
                {
                    sequence = new DocumentSequence { DocumentType = "INV", Prefix = "INV-", NextSequenceNumber = 1, PaddingLength = 6, UpdatedAt = DateTime.Now };
                    await context.DocumentSequences.AddAsync(sequence);
                }

                header.InvoiceNo = $"{sequence.Prefix}{sequence.NextSequenceNumber.ToString().PadLeft(sequence.PaddingLength, '0')}";
                sequence.NextSequenceNumber++;
                sequence.UpdatedAt = DateTime.Now;
                context.DocumentSequences.Update(sequence);

                // 2. Save the Master Receipt
                header.TransactionDate = DateTime.Now;
                header.Status = "Completed";
                await context.SalesHeaders.AddAsync(header);
                await context.SaveChangesAsync(); // Generates the Header ID

                // 3. Process the Cart Items (Deduct Stock & Write Immutable Ledgers)
                foreach (var line in lines)
                {
                    line.SalesHeaderId = header.Id;

                    // Fetch the exact physical batch bucket
                    var batch = await context.ItemBatches.FirstOrDefaultAsync(b => b.Id == line.ItemBatchId);
                    if (batch == null) throw new Exception($"CRITICAL: Item Batch ID {line.ItemBatchId} not found.");

                    // Lock in the cost price for historical P&L accuracy
                    line.CostPrice = batch.CostPrice;

                    // Deduct the physical stock
                    batch.CurrentStock -= line.Quantity;
                    context.ItemBatches.Update(batch);

                    await context.SalesLines.AddAsync(line);

                    // Write to the global immutable Inventory Ledger
                    var inventoryTx = new InventoryTransaction
                    {
                        ItemVariantId = batch.ItemVariantId,
                        TransactionDate = header.TransactionDate,
                        TransactionType = "SALE",
                        ReferenceDocument = header.InvoiceNo,
                        Quantity = -line.Quantity, // Negative because stock is leaving
                        UnitCost = batch.CostPrice,
                        CreatedBy = header.CashierName,
                        Remarks = $"Batch: {batch.BatchNo}"
                    };
                    await context.InventoryTransactions.AddAsync(inventoryTx);
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return header; // Return the fully saved header so the Printer Service can use it
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}