using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.DTOs;

namespace POS.Core.Repositories
{
    public sealed class CustomerCreditRepository
    {
        private static readonly string[] SupportedPaymentMethods =
        {
            "Cash",
            "Card",
            "Cheque",
            "Bank Transfer"
        };

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public CustomerCreditRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<CustomerCreditValidationDto> ValidateCreditAsync(int customerId, decimal requestedAmount)
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            CustomerMaster? customer = await context.CustomerMasters
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == customerId);

            return BuildValidation(customer, requestedAmount);
        }

        public async Task<List<CustomerMaster>> GetCreditCustomersAsync(string searchTerm = "")
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            string term = (searchTerm ?? string.Empty).Trim().ToLowerInvariant();

            IQueryable<CustomerMaster> query = context.CustomerMasters
                .AsNoTracking()
                .Where(row => row.IsActive && row.IsCreditEnabled);

            if (!string.IsNullOrWhiteSpace(term))
            {
                query = query.Where(row =>
                    row.CustomerCode.ToLower().Contains(term) ||
                    row.FullName.ToLower().Contains(term) ||
                    row.Phone.Contains(term) ||
                    row.CompanyName.ToLower().Contains(term));
            }

            return await query
                .OrderBy(row => row.FullName)
                .Take(100)
                .ToListAsync();
        }

        public async Task<CustomerAccountSummaryDto> GetAccountSummaryAsync(
            int customerId,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();

            CustomerMaster customer = await context.CustomerMasters
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == customerId)
                ?? throw new InvalidOperationException("Customer account was not found.");

            List<CustomerLedger> allEntries = await context.CustomerLedgers
                .AsNoTracking()
                .Where(row => row.CustomerMasterId == customerId)
                .OrderBy(row => row.TransactionDate)
                .ThenBy(row => row.Id)
                .ToListAsync();

            DateTime today = DateTime.Today;
            List<CustomerOpenInvoiceDto> openInvoices = allEntries
                .Where(row => row.DebitAmount > 0m && row.OutstandingAmount > 0m)
                .Select(row => new CustomerOpenInvoiceDto
                {
                    LedgerId = row.Id,
                    SalesHeaderId = row.SalesHeaderId,
                    InvoiceNo = row.DocumentRef,
                    InvoiceDate = row.TransactionDate,
                    DueDate = row.DueDate,
                    OriginalAmount = Money(row.OriginalAmount > 0m ? row.OriginalAmount : row.DebitAmount),
                    AllocatedAmount = Money(row.AllocatedAmount),
                    OutstandingAmount = Money(row.OutstandingAmount),
                    Status = ResolveStatus(row, today),
                    OverdueDays = row.DueDate.HasValue && row.DueDate.Value.Date < today
                        ? (today - row.DueDate.Value.Date).Days
                        : 0
                })
                .OrderBy(row => row.DueDate ?? DateTime.MaxValue)
                .ThenBy(row => row.InvoiceDate)
                .ToList();

            decimal runningBalance = 0m;
            var chronologicalRows = new List<CustomerLedgerStatementRowDto>();
            foreach (CustomerLedger entry in allEntries)
            {
                runningBalance = Money(runningBalance + entry.DebitAmount - entry.CreditAmount);
                if (startDate.HasValue && entry.TransactionDate < startDate.Value.Date)
                    continue;
                if (endDate.HasValue && entry.TransactionDate >= endDate.Value.Date.AddDays(1))
                    continue;

                chronologicalRows.Add(new CustomerLedgerStatementRowDto
                {
                    LedgerId = entry.Id,
                    TransactionDate = entry.TransactionDate,
                    DocumentRef = entry.DocumentRef,
                    TransactionType = entry.TransactionType,
                    DueDate = entry.DueDate,
                    DebitAmount = Money(entry.DebitAmount),
                    CreditAmount = Money(entry.CreditAmount),
                    RunningBalance = runningBalance,
                    OutstandingAmount = Money(entry.OutstandingAmount),
                    Status = ResolveStatus(entry, today),
                    Remarks = entry.Remarks,
                    ProcessedBy = entry.ProcessedBy
                });
            }

            decimal currentBalance = Money(allEntries.Sum(row => row.DebitAmount - row.CreditAmount));
            decimal overdueAmount = Money(openInvoices
                .Where(row => row.OverdueDays > 0)
                .Sum(row => row.OutstandingAmount));

            decimal Aging(int minDays, int? maxDays) => Money(openInvoices
                .Where(row => row.OverdueDays >= minDays && (!maxDays.HasValue || row.OverdueDays <= maxDays.Value))
                .Sum(row => row.OutstandingAmount));

            return new CustomerAccountSummaryDto
            {
                CustomerId = customer.Id,
                CustomerCode = customer.CustomerCode,
                CustomerName = string.IsNullOrWhiteSpace(customer.CompanyName) ? customer.FullName : customer.CompanyName,
                CustomerType = customer.CustomerType,
                CreditLimit = Money(customer.CreditLimit),
                CurrentBalance = currentBalance,
                AvailableCredit = Money(Math.Max(0m, customer.CreditLimit - currentBalance)),
                OverdueAmount = overdueAmount,
                AgingCurrent = Aging(0, 0),
                Aging1To30 = Aging(1, 30),
                Aging31To60 = Aging(31, 60),
                Aging61To90 = Aging(61, 90),
                AgingOver90 = Aging(91, null),
                OpenInvoices = openInvoices,
                StatementRows = chronologicalRows
                    .OrderByDescending(row => row.TransactionDate)
                    .ThenByDescending(row => row.LedgerId)
                    .ToList()
            };
        }

        public async Task<CustomerPaymentResultDto> ReceivePaymentAsync(CustomerPaymentRequest request)
        {
            ValidatePaymentRequest(request);

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                CustomerPaymentReceipt? existingReceipt = await context.CustomerPaymentReceipts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(row => row.ReceiptToken == request.ReceiptToken);

                if (existingReceipt != null)
                {
                    await transaction.RollbackAsync();
                    return await LoadPaymentResultAsync(context, existingReceipt.Id);
                }

                CustomerMaster customer = await context.CustomerMasters
                    .FirstOrDefaultAsync(row => row.Id == request.CustomerId)
                    ?? throw new InvalidOperationException("Customer account was not found.");

                if (!customer.IsActive)
                    throw new InvalidOperationException("Customer account is inactive.");

                List<CustomerLedger> openDebits = await context.CustomerLedgers
                    .Where(row => row.CustomerMasterId == customer.Id && row.DebitAmount > 0m && row.OutstandingAmount > 0m)
                    .OrderBy(row => row.DueDate ?? DateTime.MaxValue)
                    .ThenBy(row => row.TransactionDate)
                    .ThenBy(row => row.Id)
                    .ToListAsync();

                decimal outstanding = Money(openDebits.Sum(row => row.OutstandingAmount));
                if (outstanding <= 0m)
                    throw new InvalidOperationException("This customer has no outstanding invoices.");

                decimal amount = Money(request.Amount);
                if (amount > outstanding)
                    throw new InvalidOperationException($"Payment cannot exceed the outstanding balance of Rs. {outstanding:N2}.");

                ShiftSession? shift = null;
                if (request.ShiftSessionId.HasValue)
                {
                    shift = await context.ShiftSessions
                        .FirstOrDefaultAsync(row => row.Id == request.ShiftSessionId.Value)
                        ?? throw new InvalidOperationException("Cashier shift was not found.");

                    if (!string.Equals(shift.Status, ShiftStatusCodes.Open, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Customer payment requires an open shift.");

                    if (!string.Equals(shift.TerminalNo, request.TerminalNo, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Payment terminal does not match the open shift.");
                }
                else if (string.IsNullOrWhiteSpace(request.DestinationAccount))
                {
                    throw new InvalidOperationException("BackOffice payments require a destination account or counter reference.");
                }

                DocumentSequence sequence = await GetOrCreateReceiptSequenceAsync(context);
                string receiptNo = $"{sequence.Prefix}{sequence.NextSequenceNumber.ToString().PadLeft(sequence.PaddingLength, '0')}";
                sequence.NextSequenceNumber++;
                sequence.UpdatedAt = DateTime.Now;

                var receipt = new CustomerPaymentReceipt
                {
                    ReceiptNo = receiptNo,
                    ReceiptToken = request.ReceiptToken,
                    CustomerMasterId = customer.Id,
                    PaymentDate = request.PaymentDate,
                    PaymentMethod = NormalizePaymentMethod(request.PaymentMethod),
                    Amount = amount,
                    ReferenceNo = Truncate(request.ReferenceNo.Trim(), 100),
                    BankOrCardType = Truncate(request.BankOrCardType.Trim(), 100),
                    DestinationAccount = Truncate(request.DestinationAccount.Trim(), 100),
                    ProcessedBy = Truncate(request.ProcessedBy.Trim(), 100),
                    TerminalNo = Truncate(request.TerminalNo.Trim(), 20),
                    ShiftSessionId = request.ShiftSessionId,
                    Remarks = Truncate(request.Remarks.Trim(), 255),
                    CreatedAt = DateTime.Now
                };

                context.CustomerPaymentReceipts.Add(receipt);
                await context.SaveChangesAsync();

                var creditEntry = new CustomerLedger
                {
                    CustomerMasterId = customer.Id,
                    CustomerPaymentReceiptId = receipt.Id,
                    TransactionDate = request.PaymentDate,
                    DocumentRef = receiptNo,
                    TransactionType = CustomerCreditCodes.PaymentReceived,
                    DebitAmount = 0m,
                    CreditAmount = amount,
                    OriginalAmount = amount,
                    AllocatedAmount = amount,
                    OutstandingAmount = 0m,
                    Status = CustomerCreditCodes.Paid,
                    ProcessedBy = receipt.ProcessedBy,
                    Remarks = BuildPaymentRemarks(receipt)
                };

                context.CustomerLedgers.Add(creditEntry);
                await context.SaveChangesAsync();

                decimal remaining = amount;
                foreach (CustomerLedger debit in openDebits)
                {
                    if (remaining <= 0m)
                        break;

                    decimal allocationAmount = Money(Math.Min(remaining, debit.OutstandingAmount));
                    debit.AllocatedAmount = Money(debit.AllocatedAmount + allocationAmount);
                    debit.OutstandingAmount = Money(debit.OutstandingAmount - allocationAmount);
                    debit.Status = ResolvePersistedStatus(debit, request.PaymentDate.Date);

                    context.CustomerLedgerAllocations.Add(new CustomerLedgerAllocation
                    {
                        CustomerMasterId = customer.Id,
                        DebitLedgerId = debit.Id,
                        CreditLedgerId = creditEntry.Id,
                        CustomerPaymentReceiptId = receipt.Id,
                        Amount = allocationAmount,
                        CreatedAt = DateTime.Now,
                        CreatedBy = receipt.ProcessedBy
                    });

                    remaining = Money(remaining - allocationAmount);
                }

                if (remaining != 0m)
                    throw new InvalidOperationException("Payment allocation did not reconcile to zero.");

                if (string.Equals(receipt.PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase) && shift != null)
                {
                    var movement = new CashMovement
                    {
                        ShiftSessionId = shift.Id,
                        MovementType = CashMovementTypeCodes.PaidIn,
                        Amount = amount,
                        ReasonCategory = "Customer Payment",
                        Remarks = Truncate($"Customer payment {receiptNo} / {customer.CustomerCode}", 255),
                        CashierName = receipt.ProcessedBy,
                        AuthorizedBy = receipt.ProcessedBy,
                        Timestamp = request.PaymentDate,
                        ReferenceVoucherNo = receiptNo
                    };
                    context.CashMovements.Add(movement);
                    await context.SaveChangesAsync();
                    receipt.CashMovementId = movement.Id;
                }

                customer.CurrentBalance = Money(Math.Max(0m, outstanding - amount));
                customer.UpdatedAt = DateTime.Now;
                customer.UpdatedBy = receipt.ProcessedBy;

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                return await LoadPaymentResultAsync(context, receipt.Id);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                await using AppDbContext retryContext = await _contextFactory.CreateDbContextAsync();
                CustomerPaymentReceipt? existing = await retryContext.CustomerPaymentReceipts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(row => row.ReceiptToken == request.ReceiptToken);
                if (existing != null)
                    return await LoadPaymentResultAsync(retryContext, existing.Id);
                throw;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<CustomerPaymentReceiptDocumentDto> GetPaymentReceiptDocumentAsync(int receiptId)
        {
            if (receiptId <= 0)
                throw new ArgumentOutOfRangeException(nameof(receiptId));

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();

            CustomerPaymentReceipt receipt = await context.CustomerPaymentReceipts
                .AsNoTracking()
                .Include(row => row.CustomerMaster)
                .FirstOrDefaultAsync(row => row.Id == receiptId)
                ?? throw new InvalidOperationException("Customer payment receipt was not found.");

            List<CustomerLedgerAllocation> allocations = await context.CustomerLedgerAllocations
                .AsNoTracking()
                .Include(row => row.DebitLedger)
                .Where(row => row.CustomerPaymentReceiptId == receiptId)
                .OrderBy(row => row.Id)
                .ToListAsync();

            StoreSettings? settings = await context.StoreSettings
                .AsNoTracking()
                .Where(row => row.IsActive)
                .OrderBy(row => row.Id)
                .FirstOrDefaultAsync();

            decimal remainingBalance = await context.CustomerLedgers
                .AsNoTracking()
                .Where(row => row.CustomerMasterId == receipt.CustomerMasterId)
                .SumAsync(row => (decimal?)(row.DebitAmount - row.CreditAmount)) ?? 0m;

            string address = string.Join(", ", new[]
            {
                settings?.AddressLine1,
                settings?.AddressLine2,
                settings?.City,
                settings?.PostalCode
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

            return new CustomerPaymentReceiptDocumentDto
            {
                StoreName = string.IsNullOrWhiteSpace(settings?.StoreName)
                    ? settings?.LegalName ?? "Store"
                    : settings.StoreName,
                StoreAddress = address,
                StorePhone = settings?.Phone ?? string.Empty,
                ReceiptNo = receipt.ReceiptNo,
                PaymentDate = receipt.PaymentDate,
                CustomerCode = receipt.CustomerMaster?.CustomerCode ?? string.Empty,
                CustomerName = receipt.CustomerMaster == null
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(receipt.CustomerMaster.CompanyName)
                        ? receipt.CustomerMaster.FullName
                        : receipt.CustomerMaster.CompanyName,
                PaymentMethod = receipt.PaymentMethod,
                Amount = Money(receipt.Amount),
                ReferenceNo = receipt.ReferenceNo,
                BankOrCardType = receipt.BankOrCardType,
                DestinationAccount = receipt.DestinationAccount,
                ProcessedBy = receipt.ProcessedBy,
                TerminalNo = receipt.TerminalNo,
                Remarks = receipt.Remarks,
                RemainingBalance = Money(remainingBalance),
                Allocations = allocations.Select(row => new CustomerLedgerAllocationDto
                {
                    InvoiceNo = row.DebitLedger?.DocumentRef ?? string.Empty,
                    Amount = Money(row.Amount)
                }).ToList()
            };
        }

        private static CustomerCreditValidationDto BuildValidation(CustomerMaster? customer, decimal requestedAmount)
        {
            requestedAmount = Money(requestedAmount);
            if (customer == null)
                return Invalid("Customer account was not found.");
            if (!customer.IsActive)
                return Invalid("Customer account is inactive.", customer);
            if (!customer.IsCreditEnabled)
                return Invalid("Credit is not enabled for this customer.", customer);
            if (!string.Equals(customer.CreditStatus, "Active", StringComparison.OrdinalIgnoreCase))
                return Invalid($"Customer credit status is {customer.CreditStatus}.", customer);
            if (customer.IsCreditLocked)
                return Invalid("Customer credit account is locked.", customer);
            if (customer.CreditLimit <= 0m)
                return Invalid("Customer credit limit is not configured.", customer);

            decimal available = Money(Math.Max(0m, customer.CreditLimit - customer.CurrentBalance));
            if (requestedAmount <= 0m)
                return Invalid("Customer Credit amount must be greater than zero.", customer, available);
            if (requestedAmount > available)
                return Invalid($"Available customer credit is Rs. {available:N2}.", customer, available);

            return new CustomerCreditValidationDto
            {
                CustomerId = customer.Id,
                CustomerCode = customer.CustomerCode,
                CustomerName = customer.FullName,
                CreditLimit = Money(customer.CreditLimit),
                CurrentBalance = Money(customer.CurrentBalance),
                AvailableCredit = available,
                CreditDays = customer.CreditDays,
                CanUseCredit = true,
                Message = "Customer Credit is available."
            };
        }

        private static CustomerCreditValidationDto Invalid(string message, CustomerMaster? customer = null, decimal? available = null) =>
            new()
            {
                CustomerId = customer?.Id ?? 0,
                CustomerCode = customer?.CustomerCode ?? string.Empty,
                CustomerName = customer?.FullName ?? string.Empty,
                CreditLimit = Money(customer?.CreditLimit ?? 0m),
                CurrentBalance = Money(customer?.CurrentBalance ?? 0m),
                AvailableCredit = available ?? Money(Math.Max(0m, (customer?.CreditLimit ?? 0m) - (customer?.CurrentBalance ?? 0m))),
                CreditDays = customer?.CreditDays ?? 0,
                CanUseCredit = false,
                Message = message
            };

        private static async Task<CustomerPaymentResultDto> LoadPaymentResultAsync(AppDbContext context, int receiptId)
        {
            CustomerPaymentReceipt receipt = await context.CustomerPaymentReceipts
                .AsNoTracking()
                .FirstAsync(row => row.Id == receiptId);

            List<CustomerLedgerAllocation> allocations = await context.CustomerLedgerAllocations
                .Include(row => row.DebitLedger)
                .AsNoTracking()
                .Where(row => row.CustomerPaymentReceiptId == receipt.Id)
                .OrderBy(row => row.Id)
                .ToListAsync();

            CustomerMaster customer = await context.CustomerMasters
                .AsNoTracking()
                .FirstAsync(row => row.Id == receipt.CustomerMasterId);

            return new CustomerPaymentResultDto
            {
                ReceiptId = receipt.Id,
                ReceiptNo = receipt.ReceiptNo,
                Amount = receipt.Amount,
                RemainingBalance = customer.CurrentBalance,
                Allocations = allocations.Select(row => new CustomerLedgerAllocationDto
                {
                    InvoiceNo = row.DebitLedger?.DocumentRef ?? string.Empty,
                    Amount = row.Amount
                }).ToList()
            };
        }

        private static async Task<DocumentSequence> GetOrCreateReceiptSequenceAsync(AppDbContext context)
        {
            DocumentSequence? sequence = await context.DocumentSequences
                .FirstOrDefaultAsync(row => row.DocumentType == CustomerCreditCodes.ReceiptSequenceType);
            if (sequence != null)
                return sequence;

            sequence = new DocumentSequence
            {
                DocumentType = CustomerCreditCodes.ReceiptSequenceType,
                Prefix = CustomerCreditCodes.ReceiptPrefix,
                NextSequenceNumber = 1,
                PaddingLength = CustomerCreditCodes.ReceiptPaddingLength,
                UpdatedAt = DateTime.Now
            };
            context.DocumentSequences.Add(sequence);
            return sequence;
        }

        private static void ValidatePaymentRequest(CustomerPaymentRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (request.ReceiptToken == Guid.Empty)
                throw new InvalidOperationException("Customer payment token is required.");
            if (request.CustomerId <= 0)
                throw new InvalidOperationException("Select a customer account.");
            if (Money(request.Amount) <= 0m)
                throw new InvalidOperationException("Payment amount must be greater than zero.");
            string method = NormalizePaymentMethod(request.PaymentMethod);
            if (!SupportedPaymentMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unsupported customer payment method.");
            if (string.IsNullOrWhiteSpace(request.ProcessedBy))
                throw new InvalidOperationException("Processed user is required.");
            if ((method == "Card" || method == "Cheque" || method == "Bank Transfer") && string.IsNullOrWhiteSpace(request.ReferenceNo))
                throw new InvalidOperationException($"{method} reference is required.");
        }

        private static string NormalizePaymentMethod(string? value)
        {
            string method = (value ?? string.Empty).Trim();
            return SupportedPaymentMethods.FirstOrDefault(row => row.Equals(method, StringComparison.OrdinalIgnoreCase))
                ?? method;
        }

        private static string BuildPaymentRemarks(CustomerPaymentReceipt receipt)
        {
            var parts = new List<string> { receipt.PaymentMethod };
            if (!string.IsNullOrWhiteSpace(receipt.ReferenceNo))
                parts.Add($"Ref {receipt.ReferenceNo}");
            if (!string.IsNullOrWhiteSpace(receipt.DestinationAccount))
                parts.Add(receipt.DestinationAccount);
            if (!string.IsNullOrWhiteSpace(receipt.Remarks))
                parts.Add(receipt.Remarks);
            return Truncate(string.Join(" / ", parts), 255);
        }

        private static string ResolveStatus(CustomerLedger row, DateTime today)
        {
            if (row.DebitAmount <= 0m || row.OutstandingAmount <= 0m)
                return CustomerCreditCodes.Paid;
            if (row.DueDate.HasValue && row.DueDate.Value.Date < today)
                return CustomerCreditCodes.Overdue;
            if (row.AllocatedAmount > 0m)
                return CustomerCreditCodes.PartPaid;
            return CustomerCreditCodes.Open;
        }

        private static string ResolvePersistedStatus(CustomerLedger row, DateTime today) => ResolveStatus(row, today);

        private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
    }
}
