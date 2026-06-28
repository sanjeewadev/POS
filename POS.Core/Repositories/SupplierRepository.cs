using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public class SupplierRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        private static readonly Regex SupplierCodeRegex =
            new Regex("^[A-Z0-9_-]+$", RegexOptions.Compiled);

        private static readonly Regex PhoneRegex =
            new Regex("^[0-9+\\-\\s()]{7,20}$", RegexOptions.Compiled);

        public SupplierRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // Used by dropdowns across the system:
        // PO, GRN, Supplier Return, Item Supplier assignment.
        public async Task<IEnumerable<Supplier>> GetAllAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Suppliers
                .AsNoTracking()
                .Where(s => !s.IsDeactivated)
                .OrderBy(s => s.SupplierName)
                .ThenBy(s => s.SupplierCode)
                .ToListAsync();
        }

        // Used by the Supplier Master grid.
        public async Task<IEnumerable<Supplier>> GetAllFilteredAsync(string searchTerm = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.Suppliers
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim();

                query = query.Where(s =>
                    EF.Functions.Like(s.SupplierCode, $"%{term}%") ||
                    EF.Functions.Like(s.SupplierName, $"%{term}%") ||
                    EF.Functions.Like(s.CompanyName, $"%{term}%") ||
                    EF.Functions.Like(s.ContactPerson, $"%{term}%") ||
                    EF.Functions.Like(s.Phone1, $"%{term}%") ||
                    EF.Functions.Like(s.Phone2, $"%{term}%"));
            }

            return await query
                .OrderBy(s => s.SupplierName)
                .ThenBy(s => s.SupplierCode)
                .Take(500)
                .ToListAsync();
        }

        public async Task<Supplier?> GetByIdAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<bool> IsCodeUniqueAsync(string code, int currentSupplierId = 0)
        {
            string normalizedCode = NormalizeCode(code);

            using var context = await _contextFactory.CreateDbContextAsync();

            return !await context.Suppliers.AnyAsync(s =>
                s.SupplierCode.ToUpper() == normalizedCode &&
                s.Id != currentSupplierId);
        }

        public async Task AddAsync(Supplier supplier)
        {
            if (supplier == null)
                throw new ArgumentNullException(nameof(supplier));

            NormalizeSupplierForSave(supplier, isNew: true);
            ValidateSupplier(supplier, isNew: true);

            using var context = await _contextFactory.CreateDbContextAsync();

            bool codeExists = await context.Suppliers.AnyAsync(s =>
                s.SupplierCode.ToUpper() == supplier.SupplierCode);

            if (codeExists)
                throw new InvalidOperationException($"Supplier code '{supplier.SupplierCode}' already exists.");

            DateTime now = DateTime.Now;

            supplier.CreatedAt = now;
            supplier.UpdatedAt = now;
            supplier.DeactivatedAt = supplier.IsDeactivated ? now : null;

            // New supplier balance must always start from zero.
            // Later GRN/payment/ledger modules should update this.
            supplier.CurrentBalance = 0m;

            await context.Suppliers.AddAsync(supplier);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Supplier supplier)
        {
            if (supplier == null)
                throw new ArgumentNullException(nameof(supplier));

            if (supplier.Id <= 0)
                throw new InvalidOperationException("Invalid supplier record.");

            NormalizeSupplierForSave(supplier, isNew: false);
            ValidateSupplier(supplier, isNew: false);

            using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == supplier.Id);

            if (existing == null)
                throw new InvalidOperationException("Supplier record was not found.");

            DateTime now = DateTime.Now;

            // SupplierCode is intentionally not updated after creation.
            // CurrentBalance is intentionally not updated by Supplier Master.

            existing.SupplierName = supplier.SupplierName;
            existing.CompanyName = supplier.CompanyName;
            existing.ContactPerson = supplier.ContactPerson;
            existing.Phone1 = supplier.Phone1;
            existing.Phone2 = supplier.Phone2;
            existing.Email = supplier.Email;
            existing.Address = supplier.Address;

            existing.HasVat = supplier.HasVat;
            existing.VatNumber = supplier.HasVat ? supplier.VatNumber : string.Empty;
            existing.DefaultCreditDays = supplier.DefaultCreditDays;

            existing.IsDeactivated = supplier.IsDeactivated;
            existing.UpdatedAt = now;

            if (existing.IsDeactivated)
                existing.DeactivatedAt ??= now;
            else
                existing.DeactivatedAt = null;

            await context.SaveChangesAsync();
        }

        public async Task DeactivateAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var supplier = await context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == id);

            if (supplier == null)
                return;

            DateTime now = DateTime.Now;

            supplier.IsDeactivated = true;
            supplier.UpdatedAt = now;
            supplier.DeactivatedAt ??= now;

            await context.SaveChangesAsync();
        }

        public async Task<bool> HasLinkedDataAsync(int supplierId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await HasLinkedDataAsync(context, supplierId);
        }

        public async Task DeleteAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var supplier = await context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == id);

            if (supplier == null)
                return;

            bool hasLinkedData = await HasLinkedDataAsync(context, id);

            if (hasLinkedData)
            {
                throw new InvalidOperationException(
                    "This supplier is linked to existing system records. Suspend/deactivate the supplier instead of deleting it.");
            }

            try
            {
                context.Suppliers.Remove(supplier);
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                throw new InvalidOperationException(
                    "This supplier cannot be deleted because it is linked to other records. Suspend/deactivate it instead.");
            }
        }

        private static async Task<bool> HasLinkedDataAsync(AppDbContext context, int supplierId)
        {
            // Purchase Orders
            if (await HasLinkedEntityAsync(context, context.PoHeaders, supplierId))
                return true;

            // GRNs
            if (await HasLinkedEntityAsync(context, context.GrnHeaders, supplierId))
                return true;

            // Supplier Returns
            if (await HasLinkedEntityAsync(context, context.SupplierReturnHeaders, supplierId))
                return true;

            // Supplier Ledger
            if (await HasLinkedEntityAsync(context, context.SupplierLedgers, supplierId))
                return true;

            // Item-Supplier assignment
            if (await HasLinkedEntityAsync(context, context.ItemSuppliers, supplierId))
                return true;

            return false;
        }

        private static async Task<bool> HasLinkedEntityAsync<TEntity>(
            AppDbContext context,
            IQueryable<TEntity> query,
            int supplierId) where TEntity : class
        {
            var entityType = context.Model.FindEntityType(typeof(TEntity));
            var property = entityType?.FindProperty("SupplierId");

            if (property == null)
                return false;

            if (property.ClrType == typeof(int))
            {
                return await query.AnyAsync(e =>
                    EF.Property<int>(e, "SupplierId") == supplierId);
            }

            if (property.ClrType == typeof(int?))
            {
                return await query.AnyAsync(e =>
                    EF.Property<int?>(e, "SupplierId") == supplierId);
            }

            return false;
        }

        private static void NormalizeSupplierForSave(Supplier supplier, bool isNew)
        {
            if (isNew)
            {
                supplier.SupplierCode = NormalizeCode(supplier.SupplierCode);
            }

            supplier.SupplierName = NormalizeText(supplier.SupplierName);
            supplier.CompanyName = NormalizeText(supplier.CompanyName);
            supplier.ContactPerson = NormalizeText(supplier.ContactPerson);
            supplier.Phone1 = NormalizeText(supplier.Phone1);
            supplier.Phone2 = NormalizeText(supplier.Phone2);
            supplier.Email = NormalizeText(supplier.Email).ToLowerInvariant();
            supplier.Address = NormalizeText(supplier.Address);
            supplier.VatNumber = supplier.HasVat ? NormalizeText(supplier.VatNumber) : string.Empty;
        }

        private static string NormalizeCode(string value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeText(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static void ValidateSupplier(Supplier supplier, bool isNew)
        {
            if (isNew)
            {
                ValidateSupplierCode(supplier.SupplierCode);
            }

            ValidateRequiredText(supplier.SupplierName, "Supplier name", 150);
            ValidateOptionalText(supplier.CompanyName, "Company name", 150);
            ValidateOptionalText(supplier.ContactPerson, "Contact person", 50);
            ValidateRequiredPhone(supplier.Phone1, "Phone 1");
            ValidateOptionalPhone(supplier.Phone2, "Phone 2");
            ValidateOptionalEmail(supplier.Email);
            ValidateOptionalText(supplier.Address, "Address", 250);

            if (supplier.HasVat)
            {
                ValidateRequiredText(supplier.VatNumber, "VAT number", 50);
            }

            if (supplier.DefaultCreditDays < 0)
                throw new InvalidOperationException("Default credit days cannot be negative.");

            if (supplier.DefaultCreditDays > 365)
                throw new InvalidOperationException("Default credit days cannot be greater than 365.");
        }

        private static void ValidateSupplierCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("Supplier code is required.");

            if (code.Length > 20)
                throw new InvalidOperationException("Supplier code cannot be longer than 20 characters.");

            if (!SupplierCodeRegex.IsMatch(code))
            {
                throw new InvalidOperationException(
                    "Supplier code can only contain letters, numbers, dash, and underscore.");
            }
        }

        private static void ValidateRequiredText(string value, string fieldName, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"{fieldName} is required.");

            if (value.Length > maxLength)
                throw new InvalidOperationException($"{fieldName} cannot be longer than {maxLength} characters.");
        }

        private static void ValidateOptionalText(string value, string fieldName, int maxLength)
        {
            if (!string.IsNullOrWhiteSpace(value) && value.Length > maxLength)
                throw new InvalidOperationException($"{fieldName} cannot be longer than {maxLength} characters.");
        }

        private static void ValidateRequiredPhone(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"{fieldName} is required.");

            if (!PhoneRegex.IsMatch(value))
                throw new InvalidOperationException($"{fieldName} is not valid.");
        }

        private static void ValidateOptionalPhone(string value, string fieldName)
        {
            if (!string.IsNullOrWhiteSpace(value) && !PhoneRegex.IsMatch(value))
                throw new InvalidOperationException($"{fieldName} is not valid.");
        }

        private static void ValidateOptionalEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return;

            if (email.Length > 100)
                throw new InvalidOperationException("Email cannot be longer than 100 characters.");

            try
            {
                _ = new System.Net.Mail.MailAddress(email);
            }
            catch
            {
                throw new InvalidOperationException("Email address is not valid.");
            }
        }
    }
}