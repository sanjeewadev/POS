using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    public class ReturnRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public ReturnRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IEnumerable<Supplier>> GetActiveSuppliersAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Suppliers.Where(s => !s.IsDeactivated).AsNoTracking().ToListAsync();
        }

        // Retrieves historical GRNs for a specific supplier to link the return
        public async Task<IEnumerable<GrnHeader>> GetSupplierInvoicesAsync(int supplierId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.GrnHeaders
                                .Where(g => g.SupplierId == supplierId && g.Status == "Posted")
                                .AsNoTracking()
                                .ToListAsync();
        }

        // --- ATOMIC RETURN SAVING ENGINE ---
        public async Task SaveSupplierReturnAsync(ReturnHeader header, List<ReturnLine> lines, bool isDraft)
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
                    await context.ReturnHeaders.AddAsync(header);
                    await context.SaveChangesAsync(); // Generates Header ID
                }
                else
                {
                    context.ReturnHeaders.Update(header);
                    await context.SaveChangesAsync();
                }

                // 2. Wipe existing lines to rebuild cleanly (if updating a Draft)
                var existingLines = await context.ReturnLines.Where(l => l.ReturnHeaderId == header.Id).ToListAsync();
                context.ReturnLines.RemoveRange(existingLines);
                await context.SaveChangesAsync();

                // 3. Insert New Lines
                foreach (var line in lines)
                {
                    line.ReturnHeaderId = header.Id;
                    await context.ReturnLines.AddAsync(line);

                    // 4. IF POSTING: Update the Physical Inventory & Financials
                    if (!isDraft)
                    {
                        // Deduct physical stock
                        var variant = await context.ItemVariants.FindAsync(line.ItemVariantId);
                        if (variant != null)
                        {
                            // In a true implementation, this inserts a negative variance into the InventoryLedger
                            // For this scope, we simulate by deducting a mapped property if it exists
                            // variant.TotalStockOnHand -= line.ReturnQty;
                            // context.ItemVariants.Update(variant);
                        }
                    }
                }

                // 5. IF POSTING: Update Accounts Payable (Supplier Ledger)
                if (!isDraft)
                {
                    var supplier = await context.Suppliers.FindAsync(header.SupplierId);
                    if (supplier != null)
                    {
                        // CRITICAL: We are REDUCING the debt we owe the supplier
                        supplier.CurrentBalance -= header.NetCredit;
                        context.Suppliers.Update(supplier);
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