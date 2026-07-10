using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public class SupplierLedgerSupplierLookupDto
    {
        public int Id { get; set; }
        public string SupplierCode { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public decimal CurrentBalance { get; set; }

        public string DisplayText
        {
            get
            {
                string code = NormalizeText(SupplierCode);
                string name = NormalizeText(SupplierName);
                string company = NormalizeText(CompanyName);

                string main = string.IsNullOrWhiteSpace(code)
                    ? name
                    : $"{code} - {name}";

                if (!string.IsNullOrWhiteSpace(company) &&
                    !company.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{main} ({company})";
                }

                return main;
            }
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }

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
        public string PaymentMethod { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string PaymentReferenceNo { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        public bool IsPaid { get; set; }
        public DateTime CreatedAt { get; set; }

        public string DisplayType
        {
            get
            {
                return EntryType switch
                {
                    "GRN" => "GRN",
                    "DEBIT_NOTE" => "Supplier Return",
                    "CREDIT_NOTE" => "Credit Note",
                    "PAYMENT" => "Payment",
                    "OPENING_BALANCE" => "Opening Balance",
                    _ => EntryType
                };
            }
        }

        public decimal NetMovement => ChargeAmount - PaidAmount;
    }

    public class SupplierLedgerSummaryDto
    {
        public decimal TotalBilled { get; set; }
        public decimal TotalCredits { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal NetOutstanding { get; set; }
    }

    public class SupplierLedgerRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public SupplierLedgerRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<List<SupplierLedgerSupplierLookupDto>> GetActiveSuppliersAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Suppliers
                .AsNoTracking()
                .Where(s => !s.IsDeactivated)
                .OrderBy(s => s.SupplierCode)
                .ThenBy(s => s.SupplierName)
                .Select(s => new SupplierLedgerSupplierLookupDto
                {
                    Id = s.Id,
                    SupplierCode = s.SupplierCode,
                    SupplierName = s.SupplierName,
                    CompanyName = s.CompanyName,
                    CurrentBalance = s.CurrentBalance
                })
                .ToListAsync();
        }

        public async Task<List<SupplierLedgerEntryDto>> GetLedgerEntriesAsync(int supplierId)
        {
            if (supplierId <= 0)
                return new List<SupplierLedgerEntryDto>();

            using var context = await _contextFactory.CreateDbContextAsync();

            var rawEntries = await context.SupplierLedgers
                .AsNoTracking()
                .Where(l => l.SupplierId == supplierId)
                .OrderBy(l => l.TransactionDate)
                .ThenBy(l => l.Id)
                .Select(l => new
                {
                    l.Id,
                    l.TransactionDate,
                    l.TransactionType,
                    l.ReferenceDocument,
                    l.ChargeAmount,
                    l.PaymentAmount,
                    l.PaymentMethod,
                    l.BankName,
                    l.ReferenceNumber,
                    l.DueDate,
                    l.IsPaid,
                    l.Remarks,
                    l.CreatedAt
                })
                .ToListAsync();

            var statement = new List<SupplierLedgerEntryDto>();
            decimal runningBalance = 0m;

            foreach (var entry in rawEntries)
            {
                string transactionType = NormalizeLedgerType(entry.TransactionType);
                decimal charge = RoundMoney(entry.ChargeAmount);
                decimal creditOrPayment = RoundMoney(entry.PaymentAmount);

                runningBalance += charge;
                runningBalance -= creditOrPayment;
                runningBalance = RoundMoney(runningBalance);

                statement.Add(new SupplierLedgerEntryDto
                {
                    Id = entry.Id,
                    EntryDate = entry.TransactionDate,
                    ReferenceNo = entry.ReferenceDocument,
                    EntryType = transactionType,
                    Description = BuildDescription(transactionType, entry.Remarks, entry.PaymentMethod, entry.BankName, entry.ReferenceNumber),
                    ChargeAmount = charge,
                    PaidAmount = creditOrPayment,
                    RunningBalance = runningBalance,
                    PaymentMethod = entry.PaymentMethod,
                    BankName = entry.BankName,
                    PaymentReferenceNo = entry.ReferenceNumber,
                    DueDate = entry.DueDate,
                    IsPaid = entry.IsPaid,
                    CreatedAt = entry.CreatedAt
                });
            }

            statement.Reverse();
            return statement;
        }

        public async Task<SupplierLedgerSummaryDto> GetLedgerSummaryAsync(int supplierId)
        {
            var rows = await GetLedgerEntriesAsync(supplierId);

            return new SupplierLedgerSummaryDto
            {
                TotalBilled = RoundMoney(rows.Where(r => r.EntryType == "GRN").Sum(r => r.ChargeAmount)),
                TotalCredits = RoundMoney(rows.Where(r => IsCreditType(r.EntryType)).Sum(r => r.PaidAmount)),
                TotalPaid = RoundMoney(rows.Where(r => r.EntryType == "PAYMENT").Sum(r => r.PaidAmount)),
                NetOutstanding = rows.Any() ? RoundMoney(rows.First().RunningBalance) : 0m
            };
        }

        public async Task<decimal> GetCurrentOutstandingAsync(int supplierId)
        {
            if (supplierId <= 0)
                return 0m;

            using var context = await _contextFactory.CreateDbContextAsync();

            var rows = await context.SupplierLedgers
                .AsNoTracking()
                .Where(l => l.SupplierId == supplierId)
                .Select(l => new { l.ChargeAmount, l.PaymentAmount })
                .ToListAsync();

            if (!rows.Any())
            {
                var supplierBalance = await context.Suppliers
                    .AsNoTracking()
                    .Where(s => s.Id == supplierId)
                    .Select(s => (decimal?)s.CurrentBalance)
                    .FirstOrDefaultAsync();

                return RoundMoney(supplierBalance ?? 0m);
            }

            return RoundMoney(rows.Sum(l => l.ChargeAmount - l.PaymentAmount));
        }

        public async Task PostPaymentAsync(SupplierLedger paymentEntry)
        {
            if (paymentEntry == null)
                throw new ArgumentNullException(nameof(paymentEntry));

            NormalizePaymentEntry(paymentEntry);
            ValidatePaymentEntry(paymentEntry);

            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                var supplier = await context.Suppliers
                    .FirstOrDefaultAsync(s => s.Id == paymentEntry.SupplierId && !s.IsDeactivated)
                    ?? throw new InvalidOperationException("Selected supplier is inactive or missing.");

                var existingRows = await context.SupplierLedgers
                    .AsNoTracking()
                    .Where(l => l.SupplierId == paymentEntry.SupplierId)
                    .Select(l => new { l.ChargeAmount, l.PaymentAmount })
                    .ToListAsync();

                decimal currentBalance = existingRows.Any()
                    ? existingRows.Sum(l => l.ChargeAmount - l.PaymentAmount)
                    : supplier.CurrentBalance;

                currentBalance = RoundMoney(currentBalance);

                if (currentBalance <= 0)
                    throw new InvalidOperationException("This supplier has no outstanding balance to pay.");

                if (paymentEntry.PaymentAmount > currentBalance)
                {
                    throw new InvalidOperationException(
                        $"Payment amount cannot exceed outstanding balance. Outstanding balance is Rs. {currentBalance:N2}.");
                }

                DateTime now = DateTime.Now;

                if (string.IsNullOrWhiteSpace(paymentEntry.ReferenceDocument))
                    paymentEntry.ReferenceDocument = await GenerateDocumentNumberAsync(context, "PAY");

                decimal newBalance = RoundMoney(currentBalance - paymentEntry.PaymentAmount);

                paymentEntry.Id = 0;
                paymentEntry.TransactionType = "PAYMENT";
                paymentEntry.ChargeAmount = 0m;
                paymentEntry.BalanceAfterTransaction = newBalance;
                paymentEntry.IsPaid = true;
                paymentEntry.DueDate = paymentEntry.TransactionDate.Date;
                paymentEntry.CreatedAt = now;

                if (string.IsNullOrWhiteSpace(paymentEntry.CreatedBy))
                    paymentEntry.CreatedBy = "Admin";

                supplier.CurrentBalance = newBalance;

                await context.SupplierLedgers.AddAsync(paymentEntry);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static async Task<string> GenerateDocumentNumberAsync(AppDbContext context, string documentType)
        {
            var sequence = await context.DocumentSequences.FirstOrDefaultAsync(s => s.DocumentType == documentType);

            if (sequence == null)
            {
                sequence = new DocumentSequence
                {
                    DocumentType = documentType,
                    Prefix = $"{documentType}-",
                    NextSequenceNumber = 1,
                    PaddingLength = documentType == "PAY" ? 6 : 5,
                    UpdatedAt = DateTime.Now
                };

                await context.DocumentSequences.AddAsync(sequence);
                await context.SaveChangesAsync();
            }

            string number = $"{sequence.Prefix}{sequence.NextSequenceNumber.ToString().PadLeft(sequence.PaddingLength, '0')}";

            sequence.NextSequenceNumber++;
            sequence.UpdatedAt = DateTime.Now;

            await context.SaveChangesAsync();

            return number;
        }

        private static void NormalizePaymentEntry(SupplierLedger paymentEntry)
        {
            paymentEntry.TransactionType = "PAYMENT";
            paymentEntry.ReferenceDocument = NormalizeText(paymentEntry.ReferenceDocument);
            paymentEntry.PaymentMethod = NormalizeText(paymentEntry.PaymentMethod);
            paymentEntry.BankName = NormalizeText(paymentEntry.BankName);
            paymentEntry.ReferenceNumber = NormalizeText(paymentEntry.ReferenceNumber);
            paymentEntry.Remarks = NormalizeText(paymentEntry.Remarks);
            paymentEntry.CreatedBy = NormalizeText(paymentEntry.CreatedBy);
            paymentEntry.TransactionDate = paymentEntry.TransactionDate.Date;

            paymentEntry.ChargeAmount = 0m;
            paymentEntry.PaymentAmount = RoundMoney(paymentEntry.PaymentAmount);
        }

        private static void ValidatePaymentEntry(SupplierLedger paymentEntry)
        {
            if (paymentEntry.SupplierId <= 0)
                throw new InvalidOperationException("Supplier is required.");

            if (paymentEntry.PaymentAmount <= 0)
                throw new InvalidOperationException("Payment amount must be greater than zero.");

            if (paymentEntry.TransactionDate.Date > DateTime.Now.Date.AddDays(1))
                throw new InvalidOperationException("Payment date cannot be in the far future.");

            if (paymentEntry.TransactionDate.Date < new DateTime(2000, 1, 1))
                throw new InvalidOperationException("Payment date is not valid.");

            if (string.IsNullOrWhiteSpace(paymentEntry.PaymentMethod))
                throw new InvalidOperationException("Payment method is required.");

            if (paymentEntry.PaymentMethod.Length > 50)
                throw new InvalidOperationException("Payment method cannot be longer than 50 characters.");

            if (paymentEntry.BankName.Length > 100)
                throw new InvalidOperationException("Bank name cannot be longer than 100 characters.");

            if (paymentEntry.ReferenceNumber.Length > 50)
                throw new InvalidOperationException("Reference number cannot be longer than 50 characters.");

            if (paymentEntry.Remarks.Length > 250)
                throw new InvalidOperationException("Remarks cannot be longer than 250 characters.");

            string method = paymentEntry.PaymentMethod.ToUpperInvariant();
            bool isCash = method == "CASH";
            bool isCheque = method.Contains("CHEQUE");
            bool isBankTransfer = method.Contains("BANK") || method.Contains("TRANSFER");
            bool isCard = method.Contains("CARD");

            if (!isCash && string.IsNullOrWhiteSpace(paymentEntry.ReferenceNumber))
                throw new InvalidOperationException("Reference number / cheque number is required for the selected payment method.");

            if ((isCheque || isBankTransfer) && string.IsNullOrWhiteSpace(paymentEntry.BankName))
                throw new InvalidOperationException("Bank name is required for cheque and bank transfer payments.");

            if (isCard && string.IsNullOrWhiteSpace(paymentEntry.ReferenceNumber))
                throw new InvalidOperationException("Card payment reference number is required.");
        }

        private static string BuildDescription(string transactionType, string? remarks, string? paymentMethod, string? bankName, string? referenceNumber)
        {
            string description = NormalizeText(remarks);

            if (string.IsNullOrWhiteSpace(description))
            {
                description = transactionType switch
                {
                    "GRN" => "Goods received note",
                    "DEBIT_NOTE" => "Supplier return / debit note",
                    "CREDIT_NOTE" => "Supplier credit note",
                    "PAYMENT" => "Supplier payment",
                    "OPENING_BALANCE" => "Opening balance",
                    _ => transactionType
                };
            }

            if (transactionType != "PAYMENT")
                return description;

            string method = NormalizeText(paymentMethod);
            string bank = NormalizeText(bankName);
            string reference = NormalizeText(referenceNumber);

            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(method))
                parts.Add(method);

            if (!string.IsNullOrWhiteSpace(bank))
                parts.Add(bank);

            if (!string.IsNullOrWhiteSpace(reference))
                parts.Add($"Ref: {reference}");

            if (!parts.Any())
                return description;

            return $"{description} [{string.Join(" - ", parts)}]";
        }

        private static string NormalizeLedgerType(string? value)
        {
            string type = NormalizeText(value).ToUpperInvariant();

            if (type == "SUPPLIER_RETURN" || type == "RETURN")
                return "DEBIT_NOTE";

            if (type == "SUPPLIER_PAYMENT")
                return "PAYMENT";

            if (string.IsNullOrWhiteSpace(type))
                return "UNKNOWN";

            return type;
        }

        private static bool IsCreditType(string entryType)
        {
            return entryType == "DEBIT_NOTE" || entryType == "CREDIT_NOTE" || entryType == "SUPPLIER_RETURN";
        }

        private static decimal RoundMoney(decimal value)
        {
            return Math.Round(value, 2);
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
