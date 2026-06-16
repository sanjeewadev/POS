using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    public class GrnRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public GrnRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IEnumerable<Supplier>> GetActiveSuppliersAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Suppliers.Where(s => !s.IsDeactivated).AsNoTracking().ToListAsync();
        }

        // --- ATOMIC POSTING ENGINE (HYBRID BATCH UPGRADED) ---
        public async Task PostGrnAsync(GrnHeader header, List<GrnLine> lines)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // 1. CONCURRENCY LOCK: Get safe, sequential GRN Number
                var sequence = await context.DocumentSequences.FirstOrDefaultAsync(s => s.DocumentType == "GRN");
                if (sequence == null)
                    throw new Exception("CRITICAL: GRN Document Sequence not found in database.");

                string grnNumber = $"{sequence.Prefix}{sequence.NextSequenceNumber.ToString().PadLeft(sequence.PaddingLength, '0')}";

                sequence.NextSequenceNumber++;
                sequence.UpdatedAt = DateTime.Now;
                context.DocumentSequences.Update(sequence);

                // 2. Save Header
                header.GrnNumber = grnNumber;
                header.Status = "Posted";
                header.CreatedAt = DateTime.Now;
                await context.GrnHeaders.AddAsync(header);
                await context.SaveChangesAsync();

                // 3. Process Lines, Inventory, MAC, and HYBRID BATCH BUCKETS
                foreach (var line in lines)
                {
                    line.GrnHeaderId = header.Id;
                    await context.GrnLines.AddAsync(line);

                    var variant = await context.ItemVariants.FindAsync(line.ItemVariantId);
                    if (variant != null)
                    {
                        var stockTransactions = await context.InventoryTransactions
                            .Where(t => t.ItemVariantId == variant.Id)
                            .Select(t => t.Quantity)
                            .ToListAsync();

                        decimal currentStock = stockTransactions.Sum();
                        decimal totalPhysicalReceived = line.ReceivedQty + line.FocQty;
                        decimal newTotalQty = currentStock + totalPhysicalReceived;

                        // --- THE RETAIL MATH: MOVING AVERAGE COST (MAC) ---
                        if (newTotalQty > 0)
                        {
                            decimal oldTotalValue = currentStock * variant.AverageCost;
                            decimal newReceivedValue = totalPhysicalReceived * line.LandedCost;

                            variant.AverageCost = Math.Round((oldTotalValue + newReceivedValue) / newTotalQty, 2);
                        }

                        // Update Global Selling Prices for fast retail flow
                        variant.CostPrice = line.LandedCost;
                        variant.RetailPrice = line.RetailPrice;
                        variant.WholesalePrice = line.WholesalePrice;
                        variant.MinimumPrice = line.MinimumPrice;

                        context.ItemVariants.Update(variant);

                        // --- NEW: THE BATCH BUCKET ENGINE ---
                        // If user didn't type a batch, auto-generate one tied to this specific GRN receipt
                        string targetBatchNo = string.IsNullOrWhiteSpace(line.BatchNo)
                            ? $"SYS-{header.GrnNumber}"
                            : line.BatchNo.Trim();

                        var batchBucket = await context.ItemBatches
                            .FirstOrDefaultAsync(b => b.ItemVariantId == variant.Id && b.BatchNo == targetBatchNo);

                        if (batchBucket == null)
                        {
                            // Create a brand new bucket for this distinct batch
                            batchBucket = new ItemBatch
                            {
                                ItemVariantId = variant.Id,
                                BatchNo = targetBatchNo,
                                ExpiryDate = line.ExpiryDate,
                                ReceivedDate = header.ReceivedDate,
                                CostPrice = line.LandedCost,
                                RetailPrice = line.RetailPrice,
                                WholesalePrice = line.WholesalePrice,
                                CurrentStock = totalPhysicalReceived
                            };
                            await context.ItemBatches.AddAsync(batchBucket);
                        }
                        else
                        {
                            // If the supplier sent more of the exact same batch, just pour it in the existing bucket
                            batchBucket.CurrentStock += totalPhysicalReceived;
                            batchBucket.CostPrice = line.LandedCost; // Update to latest landed cost
                            batchBucket.RetailPrice = line.RetailPrice;
                            context.ItemBatches.Update(batchBucket);
                        }

                        // --- WRITE IMMUTABLE STOCK LEDGER ---
                        var inventoryTx = new InventoryTransaction
                        {
                            ItemVariantId = variant.Id,
                            TransactionDate = header.ReceivedDate,
                            TransactionType = "GRN",
                            ReferenceDocument = header.GrnNumber,
                            Quantity = totalPhysicalReceived,
                            UnitCost = line.LandedCost,
                            CreatedBy = "System",
                            Remarks = $"GRN Receipt via Invoice: {header.SupplierInvoiceNo} | Batch: {targetBatchNo}"
                        };
                        await context.InventoryTransactions.AddAsync(inventoryTx);
                    }
                }

                // 4. Hit the Accounts Payable (Supplier Ledger)
                var supplier = await context.Suppliers.FindAsync(header.SupplierId);
                if (supplier != null)
                {
                    var supplierLedger = new SupplierLedger
                    {
                        SupplierId = supplier.Id,
                        TransactionDate = header.ReceivedDate,
                        TransactionType = "GRN",
                        ReferenceDocument = header.GrnNumber,
                        ChargeAmount = header.NetPayable,
                        PaymentAmount = 0m,
                        DueDate = header.DueDate,
                        Remarks = $"Supplier Invoice: {header.SupplierInvoiceNo}"
                    };
                    await context.SupplierLedgers.AddAsync(supplierLedger);
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