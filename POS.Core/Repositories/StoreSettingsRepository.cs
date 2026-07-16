using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public class StoreSettingsRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public StoreSettingsRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<StoreSettings?> GetActiveAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.StoreSettings
                .AsNoTracking()
                .OrderByDescending(s => s.Id)
                .FirstOrDefaultAsync(s => s.IsActive);
        }

        public async Task<StoreSettings> GetOrCreateDefaultAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.StoreSettings
                .AsNoTracking()
                .OrderByDescending(s => s.Id)
                .FirstOrDefaultAsync(s => s.IsActive);

            if (existing != null)
                return existing;

            var settings = CreateDefaultSettings();

            await context.StoreSettings.AddAsync(settings);
            await context.SaveChangesAsync();

            return settings;
        }

        public async Task<StoreSettings> SaveAsync(StoreSettings settings, string updatedBy)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            Normalize(settings);
            Validate(settings);

            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                DateTime now = DateTime.Now;
                string safeUpdatedBy = NormalizeText(updatedBy);

                StoreSettings? entity = null;

                if (settings.Id > 0)
                {
                    entity = await context.StoreSettings
                        .FirstOrDefaultAsync(s => s.Id == settings.Id);
                }

                if (entity == null)
                {
                    entity = await context.StoreSettings
                        .FirstOrDefaultAsync(s => s.IsActive);
                }

                if (entity == null)
                {
                    entity = new StoreSettings
                    {
                        CreatedAt = now,
                        IsActive = true
                    };

                    await context.StoreSettings.AddAsync(entity);
                }

                CopyToEntity(settings, entity);

                entity.IsActive = true;
                entity.UpdatedAt = now;
                entity.UpdatedBy = safeUpdatedBy;

                var otherActiveRows = await context.StoreSettings
                    .Where(s => s.IsActive && s.Id != entity.Id)
                    .ToListAsync();

                foreach (var other in otherActiveRows)
                {
                    other.IsActive = false;
                    other.UpdatedAt = now;
                    other.UpdatedBy = safeUpdatedBy;
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return await context.StoreSettings
                    .AsNoTracking()
                    .FirstAsync(s => s.Id == entity.Id);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public static StoreSettings CreateDefaultSettings()
        {
            return new StoreSettings
            {
                LegalName = "My Store",
                StoreName = "My Store",
                Brn = string.Empty,
                TaxNo = string.Empty,

                AddressLine1 = string.Empty,
                AddressLine2 = string.Empty,
                City = string.Empty,
                PostalCode = string.Empty,
                Country = "Sri Lanka",
                Phone = string.Empty,
                Email = string.Empty,

                GlobalVatRate = 0m,
                CurrencyCode = "LKR",
                CurrencySymbol = "Rs.",

                InvoicePrefix = "INV",
                PurchaseOrderPrefix = "PO",
                QuotationPrefix = "QT",

                ReceiptHeader = string.Empty,
                ReceiptFooter = "Thank You! Come Again.",
                InvoiceTerms = string.Empty,

                TimeZoneId = "Sri Lanka Standard Time",
                DateFormat = "dd/MM/yyyy",
                FinancialYearStartMonth = 1,

                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = null,
                UpdatedBy = string.Empty
            };
        }

        private static void CopyToEntity(StoreSettings source, StoreSettings target)
        {
            target.LegalName = source.LegalName;
            target.StoreName = source.StoreName;
            target.Brn = source.Brn;
            target.TaxNo = source.TaxNo;

            target.AddressLine1 = source.AddressLine1;
            target.AddressLine2 = source.AddressLine2;
            target.City = source.City;
            target.PostalCode = source.PostalCode;
            target.Country = source.Country;
            target.Phone = source.Phone;
            target.Email = source.Email;

            target.GlobalVatRate = source.GlobalVatRate;
            target.CurrencyCode = source.CurrencyCode;
            target.CurrencySymbol = source.CurrencySymbol;

            target.InvoicePrefix = source.InvoicePrefix;
            target.PurchaseOrderPrefix = source.PurchaseOrderPrefix;
            target.QuotationPrefix = source.QuotationPrefix;

            target.ReceiptHeader = source.ReceiptHeader;
            target.ReceiptFooter = source.ReceiptFooter;
            target.InvoiceTerms = source.InvoiceTerms;

            target.TimeZoneId = source.TimeZoneId;
            target.DateFormat = source.DateFormat;
            target.FinancialYearStartMonth = source.FinancialYearStartMonth;
        }

        private static void Normalize(StoreSettings settings)
        {
            settings.LegalName = NormalizeText(settings.LegalName);
            settings.StoreName = NormalizeText(settings.StoreName);
            settings.Brn = NormalizeText(settings.Brn);
            settings.TaxNo = NormalizeText(settings.TaxNo);

            settings.AddressLine1 = NormalizeText(settings.AddressLine1);
            settings.AddressLine2 = NormalizeText(settings.AddressLine2);
            settings.City = NormalizeText(settings.City);
            settings.PostalCode = NormalizeText(settings.PostalCode);
            settings.Country = NormalizeText(settings.Country);

            if (string.IsNullOrWhiteSpace(settings.Country))
                settings.Country = "Sri Lanka";

            settings.Phone = NormalizeText(settings.Phone);
            settings.Email = NormalizeText(settings.Email);

            settings.GlobalVatRate = Math.Round(settings.GlobalVatRate, 2);

            settings.CurrencyCode = NormalizeText(settings.CurrencyCode).ToUpperInvariant();
            settings.CurrencySymbol = NormalizeText(settings.CurrencySymbol);

            if (string.IsNullOrWhiteSpace(settings.CurrencyCode))
                settings.CurrencyCode = "LKR";

            if (string.IsNullOrWhiteSpace(settings.CurrencySymbol))
                settings.CurrencySymbol = "Rs.";

            settings.InvoicePrefix = NormalizeText(settings.InvoicePrefix).ToUpperInvariant();
            settings.PurchaseOrderPrefix = NormalizeText(settings.PurchaseOrderPrefix).ToUpperInvariant();
            settings.QuotationPrefix = NormalizeText(settings.QuotationPrefix).ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(settings.InvoicePrefix))
                settings.InvoicePrefix = "INV";

            if (string.IsNullOrWhiteSpace(settings.PurchaseOrderPrefix))
                settings.PurchaseOrderPrefix = "PO";

            if (string.IsNullOrWhiteSpace(settings.QuotationPrefix))
                settings.QuotationPrefix = "QT";

            settings.ReceiptHeader = NormalizeMultilineText(settings.ReceiptHeader);
            settings.ReceiptFooter = NormalizeMultilineText(settings.ReceiptFooter);
            settings.InvoiceTerms = NormalizeMultilineText(settings.InvoiceTerms);

            if (string.IsNullOrWhiteSpace(settings.ReceiptFooter))
                settings.ReceiptFooter = "Thank You! Come Again.";

            settings.TimeZoneId = NormalizeText(settings.TimeZoneId);

            if (string.IsNullOrWhiteSpace(settings.TimeZoneId))
                settings.TimeZoneId = "Sri Lanka Standard Time";

            settings.DateFormat = NormalizeText(settings.DateFormat);

            if (string.IsNullOrWhiteSpace(settings.DateFormat))
                settings.DateFormat = "dd/MM/yyyy";
        }

        private static void Validate(StoreSettings settings)
        {
            if (string.IsNullOrWhiteSpace(
                    settings.LegalName))
            {
                throw new InvalidOperationException(
                    "Business / legal name is required.");
            }

            if (string.IsNullOrWhiteSpace(
                    settings.StoreName))
            {
                throw new InvalidOperationException(
                    "Store / trading name is required.");
            }

            ValidateMaximumLength(
                settings.LegalName,
                200,
                "Business / legal name");

            ValidateMaximumLength(
                settings.StoreName,
                150,
                "Store / trading name");

            ValidateMaximumLength(
                settings.Brn,
                100,
                "Business registration number");

            ValidateMaximumLength(
                settings.TaxNo,
                100,
                "VAT registration number");

            ValidateMaximumLength(
                settings.AddressLine1,
                250,
                "Address line 1");

            ValidateMaximumLength(
                settings.AddressLine2,
                250,
                "Address line 2");

            ValidateMaximumLength(
                settings.City,
                100,
                "City");

            ValidateMaximumLength(
                settings.PostalCode,
                50,
                "Postal code");

            ValidateMaximumLength(
                settings.Country,
                100,
                "Country");

            ValidateMaximumLength(
                settings.Phone,
                100,
                "Telephone");

            ValidateMaximumLength(
                settings.Email,
                150,
                "Email");

            ValidateMaximumLength(
                settings.ReceiptHeader,
                1000,
                "Receipt header");

            ValidateMaximumLength(
                settings.ReceiptFooter,
                1000,
                "Receipt footer");

            // Legacy compatibility fields remain valid even though
            // they are no longer exposed on the final simple page.
            if (settings.GlobalVatRate < 0m ||
                settings.GlobalVatRate > 100m)
            {
                throw new InvalidOperationException(
                    "Stored legacy VAT rate must be between 0 and 100.");
            }

            if (settings.FinancialYearStartMonth < 1 ||
                settings.FinancialYearStartMonth > 12)
            {
                throw new InvalidOperationException(
                    "Stored financial year month must be between 1 and 12.");
            }
        }

        private static void ValidateMaximumLength(
            string? value,
            int maximumLength,
            string fieldName)
        {
            if ((value ?? string.Empty).Length >
                maximumLength)
            {
                throw new InvalidOperationException(
                    $"{fieldName} cannot exceed " +
                    $"{maximumLength:N0} characters.");
            }
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string NormalizeMultilineText(string? value)
        {
            return (value ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Trim();
        }
    }
}