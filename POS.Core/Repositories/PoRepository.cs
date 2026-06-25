using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    // DTO for the new PO Management Dashboard
    public class PoSummaryDto
    {
        public int PoHeaderId { get; set; }
        public string PoNumber { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public DateTime ExpectedDate { get; set; }
        public decimal NetPayable { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
    }

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

        public async Task<IEnumerable<PoHeader>> GetOpenPurchaseOrdersAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.PoHeaders
                                .Include(p => p.PoLines)
                                .Where(p => p.Status == "Approved" || p.Status == "Partially Received")
                                .AsNoTracking()
                                .ToListAsync();
        }

        // --- ATOMIC PO SAVING ENGINE (ENTERPRISE UPGRADED) ---
        public async Task SavePurchaseOrderAsync(PoHeader header, List<PoLine> lines, bool isDraft)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // 1. CONCURRENCY LOCK: Get safe, sequential PO Number
                if (header.Id == 0)
                {
                    var sequence = await context.DocumentSequences.FirstOrDefaultAsync(s => s.DocumentType == "PO");
                    bool isNewSequence = false;

                    if (sequence == null)
                    {
                        // Auto-create sequence if it doesn't exist yet
                        sequence = new DocumentSequence { DocumentType = "PO", Prefix = "PO-", NextSequenceNumber = 1, PaddingLength = 5, UpdatedAt = DateTime.Now };
                        isNewSequence = true;
                    }

                    header.PoNumber = $"{sequence.Prefix}{sequence.NextSequenceNumber.ToString().PadLeft(sequence.PaddingLength, '0')}";

                    sequence.NextSequenceNumber++;
                    sequence.UpdatedAt = DateTime.Now;

                    if (isNewSequence)
                        await context.DocumentSequences.AddAsync(sequence);
                    else
                        context.DocumentSequences.Update(sequence);

                    header.Status = isDraft ? "Draft" : "Approved";
                    header.CreatedAt = DateTime.Now;
                    await context.PoHeaders.AddAsync(header);
                    await context.SaveChangesAsync();
                }
                else
                {
                    // Existing PO, just update header
                    header.Status = isDraft ? "Draft" : "Approved";
                    context.PoHeaders.Update(header);
                    await context.SaveChangesAsync();
                }

                // 2. NON-DESTRUCTIVE LINE UPDATES
                var existingLines = await context.PoLines.Where(l => l.PoHeaderId == header.Id).ToListAsync();
                var incomingLineIds = lines.Select(l => l.Id).Where(id => id != 0).ToList();

                // Check for lines that were removed in the UI
                var linesToDelete = existingLines.Where(e => !incomingLineIds.Contains(e.Id)).ToList();
                foreach (var lineToDelete in linesToDelete)
                {
                    // CRITICAL BLOCK: Protect received inventory from UI deletion
                    if (lineToDelete.ReceivedQty > 0)
                    {
                        throw new Exception($"Cannot remove item (Variant ID: {lineToDelete.ItemVariantId}) because {lineToDelete.ReceivedQty} units have already been received by the warehouse.");
                    }
                    context.PoLines.Remove(lineToDelete);
                }

                // Insert or Update remaining lines safely
                foreach (var line in lines)
                {
                    if (line.Id == 0)
                    {
                        line.PoHeaderId = header.Id;
                        await context.PoLines.AddAsync(line);
                    }
                    else
                    {
                        var existingLine = existingLines.FirstOrDefault(e => e.Id == line.Id);
                        if (existingLine != null)
                        {
                            existingLine.OrderQty = line.OrderQty;
                            existingLine.ExpectedCost = line.ExpectedCost;
                            existingLine.LineDiscount = line.LineDiscount;
                            existingLine.TaxCode = line.TaxCode;
                            existingLine.TaxAmount = line.TaxAmount;
                            existingLine.LineTotal = line.LineTotal;

                            // NEVER OVERWRITE ReceivedQty HERE!
                            context.PoLines.Update(existingLine);
                        }
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

        // ==============================================================================
        // --- PO DASHBOARD ENGINE WITH ADVANCED ENTERPRISE FILTERS ---
        // ==============================================================================
        public async Task<IEnumerable<PoSummaryDto>> GetPoSummariesAsync(
            string searchTerm = "",
            int? supplierId = null,
            string statusFilter = "All",
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.PoHeaders.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var search = searchTerm.ToLower();
                query = query.Where(p =>
                    p.PoNumber.ToLower().Contains(search) ||
                    p.Supplier.SupplierName.ToLower().Contains(search));
            }

            if (supplierId.HasValue && supplierId.Value > 0)
            {
                query = query.Where(p => p.SupplierId == supplierId.Value);
            }

            if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "All")
            {
                query = query.Where(p => p.Status == statusFilter);
            }

            if (startDate.HasValue)
            {
                query = query.Where(p => p.OrderDate >= startDate.Value.Date);
            }
            if (endDate.HasValue)
            {
                var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(p => p.OrderDate <= endOfDay);
            }

            return await query
                .OrderByDescending(p => p.OrderDate)
                .Select(p => new PoSummaryDto
                {
                    PoHeaderId = p.Id,
                    PoNumber = p.PoNumber,
                    SupplierName = p.Supplier.SupplierName,
                    OrderDate = p.OrderDate,
                    ExpectedDate = p.ExpectedDate,
                    NetPayable = p.NetPayable,
                    Status = p.Status,
                    CreatedBy = p.CreatedBy
                })
                .ToListAsync();
        }

        // ==============================================================================
        // --- THE DETAIL FETCH ENGINE (For the Master-Detail Slide-out Panel) ---
        // ==============================================================================
        public async Task<PoHeader?> GetPurchaseOrderDetailsAsync(int poId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.PoHeaders
                .Include(p => p.Supplier)
                .Include(p => p.PoLines)
                    .ThenInclude(l => l.ItemVariant)
                        .ThenInclude(v => v.ItemParent)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == poId);
        }

        // ==============================================================================
        // ✅ NEW: CANCEL PURCHASE ORDER ENGINE
        // ==============================================================================
        public async Task CancelPurchaseOrderAsync(int poId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var po = await context.PoHeaders.FirstOrDefaultAsync(p => p.Id == poId);

            if (po != null)
            {
                // Architectural Safeguard: You cannot cancel a PO that is already fully fulfilled.
                if (po.Status == "Closed")
                {
                    throw new InvalidOperationException("Cannot cancel a Purchase Order that has already been fully received and closed.");
                }

                po.Status = "Canceled";
                context.PoHeaders.Update(po);
                await context.SaveChangesAsync();
            }
        }
    }
}