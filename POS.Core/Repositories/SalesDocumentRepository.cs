using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.DTOs;

namespace POS.Core.Repositories
{
    public sealed class SalesDocumentRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public SalesDocumentRepository(
            IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ??
                throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<SalesHeader?> GetLastCompletedSaleAsync(
            string terminalNo)
        {
            string safeTerminalNo = Normalize(terminalNo);

            if (string.IsNullOrWhiteSpace(safeTerminalNo))
            {
                throw new InvalidOperationException(
                    "Terminal number is required to load the last receipt.");
            }

            await using AppDbContext context =
                await _contextFactory.CreateDbContextAsync();

            return await context.SalesHeaders
                .Include(header => header.SalesLines)
                .Include(header => header.SalesPayments)
                .AsNoTracking()
                .Where(header =>
                    header.TerminalNo == safeTerminalNo &&
                    header.Status == "Completed" &&
                    !header.IsVoided)
                .OrderByDescending(header => header.TransactionDate)
                .ThenByDescending(header => header.Id)
                .FirstOrDefaultAsync();
        }

        public async Task<SalesHeader> GetCompletedSaleAsync(
            int salesHeaderId)
        {
            await using AppDbContext context =
                await _contextFactory.CreateDbContextAsync();

            SalesHeader? sale =
                await LoadSaleAsync(
                    context,
                    salesHeaderId,
                    asNoTracking: true);

            if (sale == null ||
                !string.Equals(
                    sale.Status,
                    "Completed",
                    StringComparison.OrdinalIgnoreCase) ||
                sale.IsVoided)
            {
                throw new InvalidOperationException(
                    "The completed sale could not be loaded.");
            }

            return sale;
        }

        public async Task<PreparedSalesDocument> PrepareReceiptAsync(
            int salesHeaderId)
        {
            await using AppDbContext context =
                await _contextFactory.CreateDbContextAsync();

            SalesHeader sale =
                await RequireCompletedSaleAsync(
                    context,
                    salesHeaderId,
                    asNoTracking: true);

            int successfulPrintCount =
                await CountSuccessfulPrintsAsync(
                    context,
                    salesHeaderId,
                    SalesDocumentTypes.Receipt);

            return new PreparedSalesDocument
            {
                Sale = sale,
                DocumentType = SalesDocumentTypes.Receipt,
                DocumentNumber = sale.InvoiceNo,
                CopyLabel = successfulPrintCount == 0
                    ? SalesDocumentCopyLabels.Original
                    : SalesDocumentCopyLabels.Reprint,
                NextCopyNumber = successfulPrintCount + 1
            };
        }

        public async Task<PreparedSalesDocument> IssueOrPrepareTaxInvoiceAsync(
            TaxInvoiceIssueRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            ValidateIssueRequest(request);

            await using AppDbContext context =
                await _contextFactory.CreateDbContextAsync();
            await using var transaction =
                await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                SalesHeader sale =
                    await RequireCompletedSaleAsync(
                        context,
                        request.SalesHeaderId,
                        asNoTracking: false);

                ValidateTaxInvoiceEligibility(sale);

                DateTime? issuedAtUtc = null;

                if (string.IsNullOrWhiteSpace(sale.TaxInvoiceNo))
                {
                    StoreSettings? settings =
                        await context.StoreSettings
                            .AsNoTracking()
                            .Where(row => row.IsActive)
                            .OrderBy(row => row.Id)
                            .FirstOrDefaultAsync();

                    string prefix = BuildTaxInvoicePrefix(settings);
                    DocumentSequence sequence =
                        await GetOrCreateTaxInvoiceSequenceAsync(
                            context,
                            prefix);

                    sale.TaxInvoiceNo =
                        $"{sequence.Prefix}" +
                        sequence.NextSequenceNumber
                            .ToString()
                            .PadLeft(sequence.PaddingLength, '0');

                    sale.DocumentType =
                        SalesDocumentTypes.TaxInvoice;

                    sale.CustomerName =
                        Truncate(Normalize(request.CustomerName), 150);
                    sale.CustomerTinSnapshot =
                        Truncate(Normalize(request.CustomerTin), 30);
                    sale.CustomerVatNoSnapshot =
                        Truncate(Normalize(request.CustomerVatNo), 30);
                    sale.CustomerAddressSnapshot =
                        Truncate(Normalize(request.CustomerAddress), 500);

                    sequence.NextSequenceNumber++;
                    sequence.UpdatedAt = DateTime.Now;

                    issuedAtUtc = DateTime.UtcNow;

                    context.SalesDocumentAudits.Add(
                        new SalesDocumentAudit
                        {
                            SalesHeaderId = sale.Id,
                            DocumentType =
                                SalesDocumentTypes.TaxInvoice,
                            DocumentNumber = sale.TaxInvoiceNo,
                            EventType =
                                SalesDocumentEventTypes.TaxInvoiceIssued,
                            CopyNumber = 0,
                            IsSuccessful = true,
                            OccurredAtUtc = issuedAtUtc.Value,
                            PerformedBy =
                                Truncate(Normalize(request.PerformedBy), 100),
                            TerminalNo =
                                Truncate(Normalize(request.TerminalNo), 20),
                            PrinterName = string.Empty,
                            ErrorMessage = string.Empty
                        });

                    await context.SaveChangesAsync();
                }
                else
                {
                    issuedAtUtc =
                        await context.SalesDocumentAudits
                            .AsNoTracking()
                            .Where(row =>
                                row.SalesHeaderId == sale.Id &&
                                row.DocumentType ==
                                    SalesDocumentTypes.TaxInvoice &&
                                row.EventType ==
                                    SalesDocumentEventTypes.TaxInvoiceIssued &&
                                row.IsSuccessful)
                            .OrderBy(row => row.OccurredAtUtc)
                            .Select(row => (DateTime?)row.OccurredAtUtc)
                            .FirstOrDefaultAsync();
                }

                int successfulPrintCount =
                    await CountSuccessfulPrintsAsync(
                        context,
                        sale.Id,
                        SalesDocumentTypes.TaxInvoice);

                await transaction.CommitAsync();

                return new PreparedSalesDocument
                {
                    Sale = sale,
                    DocumentType = SalesDocumentTypes.TaxInvoice,
                    DocumentNumber = sale.TaxInvoiceNo ?? string.Empty,
                    CopyLabel = successfulPrintCount == 0
                        ? SalesDocumentCopyLabels.Original
                        : SalesDocumentCopyLabels.Reprint,
                    NextCopyNumber = successfulPrintCount + 1,
                    TaxInvoiceIssuedAtUtc =
                        issuedAtUtc ?? DateTime.UtcNow
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<int> GetSuccessfulPrintCountAsync(
            int salesHeaderId,
            string documentType)
        {
            await using AppDbContext context =
                await _contextFactory.CreateDbContextAsync();

            return await CountSuccessfulPrintsAsync(
                context,
                salesHeaderId,
                NormalizeDocumentType(documentType));
        }

        public async Task RecordPrintResultAsync(
            int salesHeaderId,
            string documentType,
            string documentNumber,
            bool isSuccessful,
            string performedBy,
            string terminalNo,
            string printerName,
            string? errorMessage = null)
        {
            string safeDocumentType =
                NormalizeDocumentType(documentType);

            await using AppDbContext context =
                await _contextFactory.CreateDbContextAsync();
            await using var transaction =
                await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                SalesHeader sale =
                    await RequireCompletedSaleAsync(
                        context,
                        salesHeaderId,
                        asNoTracking: true);

                ValidateDocumentNumber(
                    sale,
                    safeDocumentType,
                    documentNumber);

                int successfulPrintCount =
                    await CountSuccessfulPrintsAsync(
                        context,
                        salesHeaderId,
                        safeDocumentType);

                string eventType;

                if (!isSuccessful)
                {
                    eventType =
                        SalesDocumentEventTypes.PrintFailed;
                }
                else if (successfulPrintCount == 0)
                {
                    eventType =
                        SalesDocumentEventTypes.OriginalPrinted;
                }
                else
                {
                    eventType =
                        SalesDocumentEventTypes.Reprinted;
                }

                context.SalesDocumentAudits.Add(
                    new SalesDocumentAudit
                    {
                        SalesHeaderId = salesHeaderId,
                        DocumentType = safeDocumentType,
                        DocumentNumber =
                            Normalize(documentNumber),
                        EventType = eventType,
                        CopyNumber = successfulPrintCount + 1,
                        IsSuccessful = isSuccessful,
                        OccurredAtUtc = DateTime.UtcNow,
                        PerformedBy = Truncate(Normalize(performedBy), 100),
                        TerminalNo = Truncate(Normalize(terminalNo), 20),
                        PrinterName = Truncate(Normalize(printerName), 200),
                        ErrorMessage = isSuccessful
                            ? string.Empty
                            : Truncate(
                                Normalize(errorMessage),
                                500)
                    });

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static async Task<SalesHeader> RequireCompletedSaleAsync(
            AppDbContext context,
            int salesHeaderId,
            bool asNoTracking)
        {
            SalesHeader? sale =
                await LoadSaleAsync(
                    context,
                    salesHeaderId,
                    asNoTracking);

            if (sale == null)
                throw new InvalidOperationException("Sale was not found.");

            if (!string.Equals(
                    sale.Status,
                    "Completed",
                    StringComparison.OrdinalIgnoreCase) ||
                sale.IsVoided)
            {
                throw new InvalidOperationException(
                    "Only a completed, non-voided sale can be printed.");
            }

            return sale;
        }

        private static async Task<SalesHeader?> LoadSaleAsync(
            AppDbContext context,
            int salesHeaderId,
            bool asNoTracking)
        {
            IQueryable<SalesHeader> query =
                context.SalesHeaders
                    .Include(header => header.SalesLines)
                    .Include(header => header.SalesPayments);

            if (asNoTracking)
                query = query.AsNoTracking();

            return await query.FirstOrDefaultAsync(
                header => header.Id == salesHeaderId);
        }

        private static async Task<int> CountSuccessfulPrintsAsync(
            AppDbContext context,
            int salesHeaderId,
            string documentType)
        {
            return await context.SalesDocumentAudits
                .AsNoTracking()
                .CountAsync(row =>
                    row.SalesHeaderId == salesHeaderId &&
                    row.DocumentType == documentType &&
                    row.IsSuccessful &&
                    (row.EventType ==
                        SalesDocumentEventTypes.OriginalPrinted ||
                     row.EventType ==
                        SalesDocumentEventTypes.Reprinted));
        }

        private static void ValidateTaxInvoiceEligibility(
            SalesHeader sale)
        {
            if (!sale.IsVatRegisteredSale)
            {
                throw new InvalidOperationException(
                    "A Tax Invoice cannot be issued because the store was not VAT registered for this sale.");
            }

            if (!string.Equals(
                    sale.TaxSnapshotStatus,
                    TaxSnapshotStatuses.Complete,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A Tax Invoice cannot be issued because this sale does not have complete tax snapshots.");
            }

            if (sale.SalesLines.Count == 0 ||
                sale.SalesLines.Any(line =>
                    !string.Equals(
                        line.TaxSnapshotStatus,
                        TaxSnapshotStatuses.Complete,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "A Tax Invoice cannot be issued because one or more sale lines do not have complete tax snapshots.");
            }

            if (!sale.TaxableAmountTotal.HasValue ||
                !sale.TotalVatAmount.HasValue ||
                !sale.StandardRatedAmount.HasValue ||
                !sale.ZeroRatedAmount.HasValue ||
                !sale.ExemptAmount.HasValue ||
                !sale.OutOfScopeAmount.HasValue)
            {
                throw new InvalidOperationException(
                    "A Tax Invoice cannot be issued because the saved tax totals are incomplete.");
            }

            if (string.IsNullOrWhiteSpace(
                    sale.SupplierVatNoSnapshot))
            {
                throw new InvalidOperationException(
                    "A Tax Invoice cannot be issued because the store VAT registration snapshot is missing.");
            }
        }

        private static void ValidateIssueRequest(
            TaxInvoiceIssueRequest request)
        {
            if (request.SalesHeaderId <= 0)
                throw new InvalidOperationException("Sale is required.");

            if (string.IsNullOrWhiteSpace(request.CustomerName) ||
                string.Equals(
                    request.CustomerName.Trim(),
                    "Walk-In",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Customer name is required for a Tax Invoice.");
            }

            if (string.IsNullOrWhiteSpace(request.CustomerAddress))
            {
                throw new InvalidOperationException(
                    "Customer address is required for a Tax Invoice.");
            }

            if (string.IsNullOrWhiteSpace(request.CustomerTin) &&
                string.IsNullOrWhiteSpace(request.CustomerVatNo))
            {
                throw new InvalidOperationException(
                    "Customer TIN or VAT registration number is required for a Tax Invoice.");
            }

            if (string.IsNullOrWhiteSpace(request.PerformedBy))
                throw new InvalidOperationException("Cashier name is required.");

            if (string.IsNullOrWhiteSpace(request.TerminalNo))
                throw new InvalidOperationException("Terminal number is required.");
        }

        private static async Task<DocumentSequence>
            GetOrCreateTaxInvoiceSequenceAsync(
                AppDbContext context,
                string prefix)
        {
            DocumentSequence? sequence =
                await context.DocumentSequences
                    .FirstOrDefaultAsync(row =>
                        row.DocumentType == "TAXINV");

            if (sequence != null)
            {
                if (!string.Equals(
                        sequence.Prefix,
                        prefix,
                        StringComparison.Ordinal))
                {
                    sequence.Prefix = prefix;
                }

                return sequence;
            }

            sequence = new DocumentSequence
            {
                DocumentType = "TAXINV",
                Prefix = prefix,
                NextSequenceNumber = 1,
                PaddingLength = 6,
                UpdatedAt = DateTime.Now
            };

            await context.DocumentSequences.AddAsync(sequence);
            return sequence;
        }

        private static string BuildTaxInvoicePrefix(
            StoreSettings? settings)
        {
            string prefix = Normalize(settings?.TaxInvoicePrefix);

            if (string.IsNullOrWhiteSpace(prefix))
                prefix = "TI";

            prefix = Truncate(prefix, 9);

            return prefix.EndsWith(
                "-",
                StringComparison.Ordinal)
                ? prefix
                : prefix + "-";
        }

        private static string NormalizeDocumentType(
            string? documentType)
        {
            string value = Normalize(documentType);

            if (string.Equals(
                    value,
                    SalesDocumentTypes.Receipt,
                    StringComparison.OrdinalIgnoreCase))
            {
                return SalesDocumentTypes.Receipt;
            }

            if (string.Equals(
                    value,
                    SalesDocumentTypes.TaxInvoice,
                    StringComparison.OrdinalIgnoreCase))
            {
                return SalesDocumentTypes.TaxInvoice;
            }

            throw new InvalidOperationException(
                $"Unsupported sales document type '{value}'.");
        }

        private static void ValidateDocumentNumber(
            SalesHeader sale,
            string documentType,
            string documentNumber)
        {
            string expected =
                documentType == SalesDocumentTypes.TaxInvoice
                    ? sale.TaxInvoiceNo ?? string.Empty
                    : sale.InvoiceNo;

            if (string.IsNullOrWhiteSpace(expected) ||
                !string.Equals(
                    Normalize(documentNumber),
                    expected,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The document number does not match the saved sale.");
            }
        }

        private static string Normalize(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string Truncate(
            string value,
            int maxLength)
        {
            return value.Length <= maxLength
                ? value
                : value.Substring(0, maxLength);
        }
    }
}
