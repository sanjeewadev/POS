using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    // Lightweight DTO perfectly mapped to your UI DataGrid columns
    public class SupplierLedgerEntryDto
    {
        public int Id { get; set; }
        public DateTime EntryDate { get; set; }
        public string ReferenceNo { get; set; } = string.Empty;
        public string EntryType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal ChargeAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RunningBalance { get; set; }
    }

    public class SupplierLedgerRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public SupplierLedgerRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IEnumerable<Supplier>> GetActiveSuppliersAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Suppliers.Where(s => !s.IsDeactivated).AsNoTracking().ToListAsync();
        }

        // --- CHRONOLOGICAL STATEMENT ENGINE ---
        public async Task<List<SupplierLedgerEntryDto>> GetLedgerEntriesAsync(int supplierId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var rawEntries = await context.SupplierLedgers
                .Where(l => l.SupplierId == supplierId)
                .OrderBy(l => l.TransactionDate)
                .ThenBy(l => l.Id) // Tie-breaker ensures exact chronological order if dates match
                .AsNoTracking()
                .ToListAsync();

            var statement = new List<SupplierLedgerEntryDto>();
            decimal currentBalance = 0m;

            // Loop oldest to newest to mathematically calculate the running balance
            foreach (var entry in rawEntries)
            {
                currentBalance += entry.ChargeAmount;
                currentBalance -= entry.PaymentAmount;

                // Format a beautiful description combining Remarks and Payment Details
                string description = entry.Remarks;
                if (!string.IsNullOrWhiteSpace(entry.PaymentMethod) && entry.PaymentMethod != "Cash")
                {
                    description += $" [{entry.PaymentMethod}";
                    if (!string.IsNullOrWhiteSpace(entry.BankName)) description += $" - {entry.BankName}";
                    if (!string.IsNullOrWhiteSpace(entry.ReferenceNumber)) description += $" Ref: {entry.ReferenceNumber}";
                    description += "]";
                }

                statement.Add(new SupplierLedgerEntryDto
                {
                    Id = entry.Id,
                    EntryDate = entry.TransactionDate,
                    ReferenceNo = entry.ReferenceDocument,
                    EntryType = entry.TransactionType,
                    Description = description.Trim(),
                    ChargeAmount = entry.ChargeAmount,
                    PaidAmount = entry.PaymentAmount,
                    RunningBalance = currentBalance
                });
            }

            // Reverse the list so the UI displays the newest transactions at the very top
            statement.Reverse();
            return statement;
        }

        // --- SECURE PAYMENT POSTING ---
        public async Task PostPaymentAsync(SupplierLedger paymentEntry)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // 1. Get the current exact balance from the database
                decimal currentBalance = await context.SupplierLedgers
                    .Where(l => l.SupplierId == paymentEntry.SupplierId)
                    .SumAsync(l => l.ChargeAmount - l.PaymentAmount);

                paymentEntry.BalanceAfterTransaction = currentBalance - paymentEntry.PaymentAmount;

                // 2. Generate a secure Sequence Number (e.g., PAY-000001)
                var sequence = await context.DocumentSequences.FirstOrDefaultAsync(s => s.DocumentType == "PAY");
                if (sequence == null)
                {
                    sequence = new DocumentSequence { DocumentType = "PAY", Prefix = "PAY-", NextSequenceNumber = 1, PaddingLength = 6, UpdatedAt = DateTime.Now };
                    await context.DocumentSequences.AddAsync(sequence);
                }

                if (string.IsNullOrWhiteSpace(paymentEntry.ReferenceDocument))
                {
                    paymentEntry.ReferenceDocument = $"{sequence.Prefix}{sequence.NextSequenceNumber.ToString().PadLeft(sequence.PaddingLength, '0')}";

                    sequence.NextSequenceNumber++;
                    sequence.UpdatedAt = DateTime.Now;
                    context.DocumentSequences.Update(sequence);
                }

                // 3. Post to the Ledger safely
                await context.SupplierLedgers.AddAsync(paymentEntry);
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