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

        // ==============================================================================
        // --- PO LOOKUP ENGINE ---
        // ==============================================================================
        public async Task<IEnumerable<PoHeader>> GetOpenPurchaseOrdersAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.PoHeaders
                                .Where(p => p.Status == "Approved" || p.Status == "Partially Received")
                                .AsNoTracking()
                                .ToListAsync();
        }

        public async Task<PoHeader?> GetApprovedPoDetailsAsync(int poId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.PoHeaders
                .Include(p => p.Supplier)
                .Include(p => p.PoLines)
                    .ThenInclude(l => l.ItemVariant)
                        .ThenInclude(v => v.ItemParent)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == poId && (p.Status == "Approved" || p.Status == "Partially Received"));
        }

        // ==============================================================================
        // --- ATOMIC POSTING ENGINE (PURIFIED ENTERPRISE VERSION) ---
        // ==============================================================================
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
                await context.SaveChangesAsync(); // Save early to generate the Header ID

                // 2.5 LOAD LINKED PO (If applicable)
                PoHeader? linkedPo = null;
                if (header.PurchaseOrderId.HasValue && header.PurchaseOrderId.Value > 0)
                {
                    linkedPo = await context.PoHeaders
                        .Include(p => p.PoLines)
                        .FirstOrDefaultAsync(p => p.Id == header.PurchaseOrderId.Value);
                }

                // 3. Process Lines: Inventory, MAC, and BATCH BUCKETS
                foreach (var line in lines)
                {
                    line.GrnHeaderId = header.Id;
                    await context.GrnLines.AddAsync(line);

                    // ✅ FULFILLMENT: Update the linked Purchase Order
                    if (linkedPo != null)
                    {
                        var poLine = linkedPo.PoLines.FirstOrDefault(pl => pl.ItemVariantId == line.ItemVariantId);
                        if (poLine != null)
                        {
                            poLine.ReceivedQty += line.ReceivedQty;
                            context.PoLines.Update(poLine);
                        }
                    }

                    // ✅ SUPPLIER PRICE SYNC: Update Vendor's specific catalog price
                    var itemSupplier = await context.ItemSuppliers
                        .FirstOrDefaultAsync(s => s.SupplierId == header.SupplierId && s.ItemVariantId == line.ItemVariantId);

                    if (itemSupplier != null)
                    {
                        itemSupplier.LastCostPrice = line.UnitCost;
                        itemSupplier.UpdatedAt = DateTime.Now;
                        context.ItemSuppliers.Update(itemSupplier);
                    }

                    // ✅ INVENTORY & MOVING AVERAGE COST (MAC) ENGINE
                    var variant = await context.ItemVariants.FindAsync(line.ItemVariantId);
                    if (variant != null)
                    {
                        var stockTransactions = await context.InventoryTransactions
                            .Where(t => t.ItemVariantId == variant.Id)
                            .Select(t => t.Quantity)
                            .ToListAsync();

                        decimal currentStock = stockTransactions.Sum();
                        decimal totalPhysicalReceived = line.ReceivedQty;
                        decimal newTotalQty = currentStock + totalPhysicalReceived;

                        // Calculate Moving Average Cost (MAC)
                        if (newTotalQty > 0)
                        {
                            decimal oldTotalValue = currentStock * variant.AverageCost;
                            decimal newReceivedValue = totalPhysicalReceived * line.LandedCost;

                            variant.AverageCost = Math.Round((oldTotalValue + newReceivedValue) / newTotalQty, 2);
                        }

                        // Update Global Cost (We deliberately DO NOT update retail/wholesale prices here)
                        variant.CostPrice = line.LandedCost;
                        context.ItemVariants.Update(variant);

                        // --- THE STRICT BATCH BUCKET ENGINE ---
                        string targetBatchNo = string.IsNullOrWhiteSpace(line.BatchNo)
                            ? $"SYS-{header.GrnNumber}"
                            : line.BatchNo.Trim();

                        var batchBucket = await context.ItemBatches
                            .FirstOrDefaultAsync(b => b.ItemVariantId == variant.Id && b.BatchNo == targetBatchNo);

                        if (batchBucket == null)
                        {
                            batchBucket = new ItemBatch
                            {
                                ItemVariantId = variant.Id,
                                BatchNo = targetBatchNo,
                                ExpiryDate = line.ExpiryDate,
                                ReceivedDate = header.ReceivedDate,
                                CostPrice = line.LandedCost, // Batch cost is strictly Landed Cost
                                CurrentStock = totalPhysicalReceived
                            };
                            await context.ItemBatches.AddAsync(batchBucket);
                        }
                        else
                        {
                            batchBucket.CurrentStock += totalPhysicalReceived;
                            batchBucket.CostPrice = line.LandedCost;
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
                            CreatedBy = header.CreatedBy,
                            Remarks = $"GRN Receipt via Invoice: {header.SupplierInvoiceNo} | Batch: {targetBatchNo}"
                        };
                        await context.InventoryTransactions.AddAsync(inventoryTx);
                    }
                }

                // ✅ CLOSE PURCHASE ORDER
                if (linkedPo != null)
                {
                    bool allFullyReceived = linkedPo.PoLines.All(pl => pl.ReceivedQty >= pl.OrderQty);
                    linkedPo.Status = allFullyReceived ? "Closed" : "Partially Received";
                    context.PoHeaders.Update(linkedPo);
                }

                // 4. HIT THE ACCOUNTS PAYABLE (Supplier Ledger)
                var supplier = await context.Suppliers.FindAsync(header.SupplierId);
                if (supplier != null)
                {
                    var supplierLedger = new SupplierLedger
                    {
                        SupplierId = supplier.Id,
                        TransactionDate = header.ReceivedDate,
                        TransactionType = "GRN",
                        ReferenceDocument = header.GrnNumber,
                        ChargeAmount = header.NetPayable, // The exact final total they billed us
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