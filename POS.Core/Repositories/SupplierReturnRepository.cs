using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Core.Repositories
{
    public class SupplierReturnRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public SupplierReturnRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // --- UI DATA FETCHERS ---
        public async Task<List<Supplier>> GetActiveSuppliersAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Suppliers
                .Where(s => !s.IsDeactivated)
                .OrderBy(s => s.CompanyName)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<GrnHeader>> GetSupplierInvoicesAsync(int supplierId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.GrnHeaders
                .Where(g => g.SupplierId == supplierId && g.Status == "Posted")
                .OrderByDescending(g => g.ReceivedDate)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<GrnLine>> GetHistoricalInvoiceLinesAsync(int grnHeaderId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.GrnLines
                .Include(l => l.ItemVariant)
                    .ThenInclude(v => v.ItemParent)
                .Where(l => l.GrnHeaderId == grnHeaderId)
                .AsNoTracking()
                .ToListAsync();
        }

        // --- ATOMIC 3-WAY RETURN ENGINE ---
        public async Task SaveSupplierReturnAsync(SupplierReturnHeader header, List<SupplierReturnLine> lines, bool isDraft)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // 1. SEQUENCE GENERATOR & HEADER SAVE
                if (header.Id == 0)
                {
                    var sequence = await context.DocumentSequences.FirstOrDefaultAsync(s => s.DocumentType == "RTN");
                    bool isNewSequence = false;

                    if (sequence == null)
                    {
                        sequence = new DocumentSequence { DocumentType = "RTN", Prefix = "RTN-", NextSequenceNumber = 1, PaddingLength = 5, UpdatedAt = DateTime.Now };
                        isNewSequence = true;
                    }

                    // E.g., RTN-202606-00001
                    header.ReturnNumber = $"{sequence.Prefix}{DateTime.Now:yyyyMM}-{sequence.NextSequenceNumber.ToString().PadLeft(sequence.PaddingLength, '0')}";

                    sequence.NextSequenceNumber++;
                    sequence.UpdatedAt = DateTime.Now;

                    if (isNewSequence)
                        await context.DocumentSequences.AddAsync(sequence);
                    else
                        context.DocumentSequences.Update(sequence);

                    header.Status = isDraft ? "Draft" : "Posted";
                    header.CreatedAt = DateTime.Now;

                    await context.SupplierReturnHeaders.AddAsync(header);
                    await context.SaveChangesAsync(); // Save to get the new Header ID for the lines
                }
                else
                {
                    // Existing Draft update logic
                    var existingHeader = await context.SupplierReturnHeaders.AsNoTracking().FirstOrDefaultAsync(h => h.Id == header.Id);
                    if (existingHeader != null && existingHeader.Status == "Posted")
                    {
                        throw new InvalidOperationException("CRITICAL: This Return has already been posted to the ledgers and cannot be modified.");
                    }

                    header.Status = isDraft ? "Draft" : "Posted";
                    context.SupplierReturnHeaders.Update(header);

                    // Wipe existing draft lines to securely rebuild the cart
                    var existingLines = await context.SupplierReturnLines.Where(l => l.ReturnHeaderId == header.Id).ToListAsync();
                    context.SupplierReturnLines.RemoveRange(existingLines);

                    await context.SaveChangesAsync();
                }

                // 2. LINE PROCESSING & INVENTORY DEDUCTION
                foreach (var line in lines)
                {
                    line.ReturnHeaderId = header.Id;
                    await context.SupplierReturnLines.AddAsync(line);

                    // If posting, deduct the stock and write the inventory ledger
                    if (!isDraft)
                    {
                        var targetBatch = await context.ItemBatches.FirstOrDefaultAsync(b => b.Id == line.ItemBatchId);
                        if (targetBatch == null)
                            throw new Exception($"CRITICAL: Target Batch ID {line.ItemBatchId} could not be found.");

                        if (targetBatch.CurrentStock < line.ReturnQty)
                            throw new Exception($"Insufficient stock in batch {targetBatch.BatchNo}. Attempted to return {line.ReturnQty}, but only {targetBatch.CurrentStock} remains in this specific batch.");

                        // Deduct physical bucket
                        targetBatch.CurrentStock -= line.ReturnQty;
                        context.ItemBatches.Update(targetBatch);

                        // Record Immutable Inventory Ledger (Negative Qty because it's leaving the warehouse)
                        var inventoryLedger = new InventoryTransaction
                        {
                            ItemVariantId = targetBatch.ItemVariantId,
                            TransactionDate = header.ReturnDate,
                            TransactionType = "SUPPLIER_RETURN",
                            ReferenceDocument = header.ReturnNumber,
                            Quantity = -line.ReturnQty,
                            UnitCost = line.HistoricalCost,
                            CreatedBy = header.AuthorizedBy,
                            Remarks = $"Reason: {line.ReasonCode} | Batch: {targetBatch.BatchNo} | Return Value: Rs. {line.CreditValue:N2}"
                        };
                        await context.InventoryTransactions.AddAsync(inventoryLedger);
                    }
                }

                // 3. ACCOUNTS PAYABLE (DEBIT NOTE) GENERATION
                if (!isDraft)
                {
                    var supplier = await context.Suppliers.FindAsync(header.SupplierId);
                    if (supplier == null) throw new Exception("Supplier not found.");

                    // Returning goods reduces the amount we owe the supplier, acting identically to a payment
                    decimal balanceAfter = supplier.CurrentBalance - header.NetCredit;

                    var debitNote = new SupplierLedger
                    {
                        SupplierId = header.SupplierId,
                        TransactionDate = header.ReturnDate,
                        TransactionType = "DEBIT_NOTE",
                        ReferenceDocument = header.ReturnNumber,
                        ChargeAmount = 0m,
                        PaymentAmount = header.NetCredit, // Treated as a credit/payment
                        BalanceAfterTransaction = balanceAfter,
                        Remarks = $"Supplier Return Debit Note. Gross: Rs. {header.GrossCredit:N2} | Fee: Rs. {header.RestockingFee:N2}"
                    };

                    await context.SupplierLedgers.AddAsync(debitNote);

                    // Update Global Supplier Debt
                    supplier.CurrentBalance = balanceAfter;
                    context.Suppliers.Update(supplier);
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