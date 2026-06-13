using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    public class PoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public PoRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IEnumerable<Supplier>> GetActiveSuppliersAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Suppliers.Where(s => !s.IsDeactivated).AsNoTracking().ToListAsync();
        }

        // Used by the GRN page to pull POs that haven't been fully received yet
        public async Task<IEnumerable<PoHeader>> GetOpenPurchaseOrdersAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.PoHeaders
                                .Include(p => p.PoLines)
                                .Where(p => p.Status == "Approved" || p.Status == "Partially Received")
                                .AsNoTracking()
                                .ToListAsync();
        }

        // --- ATOMIC PO SAVING ENGINE ---
        public async Task SavePurchaseOrderAsync(PoHeader header, List<PoLine> lines, bool isDraft)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // 1. Finalize Header State
                header.Status = isDraft ? "Draft" : "Approved";

                if (header.Id == 0)
                {
                    header.CreatedAt = DateTime.Now;
                    await context.PoHeaders.AddAsync(header);
                    await context.SaveChangesAsync(); // Generates Header ID
                }
                else
                {
                    context.PoHeaders.Update(header);
                    await context.SaveChangesAsync();
                }

                // 2. Wipe existing lines to rebuild cleanly (Standard practice for PO updates before GRN)
                var existingLines = await context.PoLines.Where(l => l.PoHeaderId == header.Id).ToListAsync();
                context.PoLines.RemoveRange(existingLines);
                await context.SaveChangesAsync();

                // 3. Insert New Lines
                foreach (var line in lines)
                {
                    line.PoHeaderId = header.Id;
                    await context.PoLines.AddAsync(line);
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