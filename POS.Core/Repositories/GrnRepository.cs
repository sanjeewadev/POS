using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public class GrnRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public GrnRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task PostGrnAsync(GrnHeader grnHeader)
        {
            // 1. Create the Micro-Connection
            using var context = _contextFactory.CreateDbContext();

            // 2. Open the Secure Transaction Bubble
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // STEP A: Save the GRN Document
                // EF Core is smart enough to save the Header, grab the new ID, 
                // and automatically apply it to all the attached GrnDetails!
                await context.GrnHeaders.AddAsync(grnHeader);

                // STEP B: Update the Item Master Prices
                foreach (var detail in grnHeader.GrnDetails)
                {
                    var item = await context.Items.FindAsync(detail.ItemId);
                    if (item != null)
                    {
                        // Overwrite the old cost price with the new supplier cost
                        item.CostPrice = detail.UnitCost;

                        // If the manager typed a new retail selling price in the grid, update it
                        if (detail.RetailPrice > 0)
                        {
                            item.RetailPrice = detail.RetailPrice;
                        }

                        // NOTE: If you create a 'StockLedger' table later to track physical quantities,
                        // you will add the stock increase logic right here!

                        context.Items.Update(item);
                    }
                }

                // STEP C: Push all changes to the database safely
                await context.SaveChangesAsync();

                // STEP D: The power didn't go out. Lock it in permanently!
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                // If anything fails, destroy the bubble. Roll back ALL changes.
                await transaction.RollbackAsync();
                throw; // Send the error back to the ViewModel to alert the user
            }
        }
    }
}