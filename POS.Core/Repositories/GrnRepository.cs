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

        // Fetch suppliers for the dropdown
        public async Task<IEnumerable<Supplier>> GetActiveSuppliersAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Suppliers.Where(s => !s.IsDeactivated).AsNoTracking().ToListAsync();
        }

        // --- ATOMIC POSTING ENGINE ---
        public async Task PostGrnAsync(GrnHeader header, List<GrnLine> lines)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Start the unbreakable transaction
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // 1. Finalize and Save the GRN Header
                header.Status = "Posted";
                header.CreatedAt = DateTime.Now;
                await context.GrnHeaders.AddAsync(header);
                await context.SaveChangesAsync(); // Saves to generate the Header ID

                // 2. Process Lines, Inventory, and Costs
                foreach (var line in lines)
                {
                    line.GrnHeaderId = header.Id;
                    await context.GrnLines.AddAsync(line);

                    // 3. Update the Item Master Variant
                    var variant = await context.ItemVariants.FindAsync(line.ItemVariantId);
                    if (variant != null)
                    {
                        // Update Selling Prices based on the new GRN inputs
                        variant.CostPrice = line.LandedCost;
                        variant.RetailPrice = line.RetailPrice;
                        variant.WholesalePrice = line.WholesalePrice;
                        variant.MinimumPrice = line.MinimumPrice;

                        // Calculate new Moving Average Cost 
                        // Formula: ((OldQty * OldAvgCost) + (NewQty * LandedCost)) / TotalQty
                        // Note: Requires fetching current stock from your StockLedger table
                        // variant.AverageCost = ... (Implementation depends on your specific Ledger structure)

                        context.ItemVariants.Update(variant);
                    }

                    // 4. Update the Physical Stock Ledger
                    // Here you would insert a record into an InventoryTransaction table:
                    // context.InventoryLedgers.Add(new InventoryLedger { VariantId = line.ItemVariantId, Qty = line.ReceivedQty + line.FocQty, Type = "IN-GRN" ... });
                }

                // 5. Hit the Accounts Payable (Supplier Ledger)
                var supplier = await context.Suppliers.FindAsync(header.SupplierId);
                if (supplier != null)
                {
                    // Increase the debt we owe this supplier
                    supplier.CurrentBalance += header.NetPayable;
                    context.Suppliers.Update(supplier);

                    // Log this debt in the Supplier Statement/Ledger
                    // context.SupplierLedgers.Add(new SupplierLedger { SupplierId = supplier.Id, Type = "GRN", ChargeAmount = header.NetPayable ... });
                }

                // 6. Commit all changes simultaneously
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                // If ANYTHING fails, wipe the database changes to prevent corruption
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}