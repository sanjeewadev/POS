using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.DTOs;

namespace POS.Core.Repositories
{
    public class BarcodeManagementRepository
    {
        private const string InternalBarcodePrefix = "29";
        private const string BarcodeSequenceDocumentType = "BCODE";

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public BarcodeManagementRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // =========================================================
        // LOAD BARCODE MANAGEMENT GRID
        // =========================================================

        public async Task<List<BarcodeManagementDto>> GetBarcodeManagementListAsync(
            string searchText,
            int? categoryId,
            bool showActiveOnly)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            string search = NormalizeText(searchText).ToUpperInvariant();

            var query = context.ItemVariants
                .AsNoTracking()
                .Where(v => v.ItemParent != null)
                .AsQueryable();

            if (showActiveOnly)
            {
                query = query.Where(v =>
                    !v.IsDeactivated &&
                    !v.ItemParent.IsDeactivated);
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(v => v.ItemParent.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(v =>
                    v.ItemParent.ItemCode.ToUpper().Contains(search) ||
                    v.ItemParent.ItemName.ToUpper().Contains(search) ||
                    v.SkuCode.ToUpper().Contains(search) ||
                    v.Barcode.ToUpper().Contains(search));
            }

            var rows = await query
                .Select(v => new
                {
                    VariantId = v.Id,
                    v.ItemParentId,
                    CategoryId = v.ItemParent.CategoryId,
                    v.SkuCode,
                    v.Barcode,
                    v.VariantDescription,
                    v.IsDeactivated,
                    ItemCode = v.ItemParent.ItemCode,
                    ItemName = v.ItemParent.ItemName,
                    ItemIsDeactivated = v.ItemParent.IsDeactivated,
                    CategoryName = v.ItemParent.Category != null
                        ? v.ItemParent.Category.CategoryName
                        : "Uncategorized"
                })
                .OrderBy(v => v.ItemName)
                .ThenBy(v => v.VariantDescription)
                .ThenBy(v => v.ItemCode)
                .ToListAsync();

            var results = new List<BarcodeManagementDto>();

            foreach (var row in rows)
            {
                var dto = new BarcodeManagementDto
                {
                    VariantId = row.VariantId,
                    ItemParentId = row.ItemParentId,
                    CategoryId = row.CategoryId,
                    ItemCode = row.ItemCode,
                    SkuCode = row.SkuCode,
                    ItemName = row.ItemName,
                    VariantDescription = string.IsNullOrWhiteSpace(row.VariantDescription)
                        ? "Standard"
                        : row.VariantDescription,
                    CategoryName = row.CategoryName,
                    Barcode = NormalizeText(row.Barcode),
                    IsItemDeactivated = row.ItemIsDeactivated,
                    IsVariantDeactivated = row.IsDeactivated,
                    IsSelected = false
                };

                dto.AcceptChanges();
                results.Add(dto);
            }

            return results;
        }

        // =========================================================
        // UPDATE ONE BARCODE
        // =========================================================

        public async Task UpdateSingleBarcodeAsync(int variantId, string? newBarcode)
        {
            if (variantId <= 0)
                throw new InvalidOperationException("Invalid item variant.");

            string normalizedBarcode = NormalizeBarcode(newBarcode);

            ValidateManualBarcode(normalizedBarcode, allowEmpty: true);

            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                var variant = await context.ItemVariants
                    .Include(v => v.ItemParent)
                    .FirstOrDefaultAsync(v => v.Id == variantId);

                if (variant == null)
                    throw new InvalidOperationException("Item variant was not found.");

                if (variant.IsDeactivated || variant.ItemParent.IsDeactivated)
                    throw new InvalidOperationException("Cannot update barcode for an inactive item.");

                if (!string.IsNullOrWhiteSpace(normalizedBarcode))
                {
                    await EnsureBarcodeIsUniqueAsync(
                        context,
                        normalizedBarcode,
                        excludingVariantId: variantId);
                }

                variant.Barcode = normalizedBarcode;
                variant.UpdatedAt = DateTime.Now;

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // =========================================================
        // AUTO-GENERATE INTERNAL EAN-13 BARCODES
        // =========================================================

        public async Task<BarcodeGenerationResultDto> AutoGenerateBarcodesAsync(List<int> variantIds)
        {
            var result = new BarcodeGenerationResultDto
            {
                RequestedCount = variantIds?.Distinct().Count() ?? 0
            };

            if (variantIds == null || !variantIds.Any())
                return result;

            var distinctVariantIds = variantIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                var variants = await context.ItemVariants
                    .Include(v => v.ItemParent)
                    .Where(v => distinctVariantIds.Contains(v.Id))
                    .OrderBy(v => v.Id)
                    .ToListAsync();

                foreach (var variant in variants)
                {
                    if (variant.IsDeactivated || variant.ItemParent.IsDeactivated)
                    {
                        result.SkippedInactiveCount++;
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(variant.Barcode))
                    {
                        result.SkippedAlreadyHadBarcodeCount++;
                        continue;
                    }

                    string generatedBarcode = await GenerateNextInternalEan13BarcodeAsync(context);

                    variant.Barcode = generatedBarcode;
                    variant.UpdatedAt = DateTime.Now;

                    result.GeneratedCount++;
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // =========================================================
        // BARCODE VALIDATION HELPERS
        // =========================================================

        public static bool IsValidEan13(string barcode)
        {
            string value = NormalizeBarcode(barcode);

            if (value.Length != 13 || !IsDigitsOnly(value))
                return false;

            string first12 = value.Substring(0, 12);
            int expectedCheckDigit = CalculateEan13CheckDigit(first12);
            int actualCheckDigit = value[12] - '0';

            return expectedCheckDigit == actualCheckDigit;
        }

        public static bool IsValidInternalEan13(string barcode)
        {
            string value = NormalizeBarcode(barcode);

            return value.StartsWith(InternalBarcodePrefix, StringComparison.Ordinal) &&
                   IsValidEan13(value);
        }

        public static bool IsValidUpcA(string barcode)
        {
            string value = NormalizeBarcode(barcode);

            if (value.Length != 12 || !IsDigitsOnly(value))
                return false;

            int sumOdd = 0;
            int sumEven = 0;

            for (int i = 0; i < 11; i++)
            {
                int digit = value[i] - '0';

                if (i % 2 == 0)
                    sumOdd += digit;
                else
                    sumEven += digit;
            }

            int check = (10 - ((sumOdd * 3 + sumEven) % 10)) % 10;
            return check == value[11] - '0';
        }

        public static bool IsValidEan8(string barcode)
        {
            string value = NormalizeBarcode(barcode);

            if (value.Length != 8 || !IsDigitsOnly(value))
                return false;

            int sumOdd = 0;
            int sumEven = 0;

            for (int i = 0; i < 7; i++)
            {
                int digit = value[i] - '0';

                if (i % 2 == 0)
                    sumOdd += digit;
                else
                    sumEven += digit;
            }

            int check = (10 - ((sumOdd * 3 + sumEven) % 10)) % 10;
            return check == value[7] - '0';
        }

        public static void ValidateManualBarcode(string barcode, bool allowEmpty)
        {
            string value = NormalizeBarcode(barcode);

            if (string.IsNullOrWhiteSpace(value))
            {
                if (allowEmpty)
                    return;

                throw new InvalidOperationException("Barcode is required.");
            }

            if (value.Length > 100)
                throw new InvalidOperationException("Barcode cannot be longer than 100 characters.");

            if (value.Any(char.IsWhiteSpace))
                throw new InvalidOperationException("Barcode cannot contain spaces.");

            if (value.Any(char.IsControl))
                throw new InvalidOperationException("Barcode contains invalid control characters.");

            if (value.StartsWith(InternalBarcodePrefix, StringComparison.Ordinal))
            {
                if (!IsValidInternalEan13(value))
                {
                    throw new InvalidOperationException(
                        "Internal barcodes must be valid EAN-13 numbers starting with 29 and containing a correct check digit.");
                }

                return;
            }

            if (IsDigitsOnly(value))
            {
                bool validNumericBarcode =
                    IsValidEan13(value) ||
                    IsValidUpcA(value) ||
                    IsValidEan8(value);

                if (!validNumericBarcode)
                {
                    throw new InvalidOperationException(
                        "Numeric barcode must be a valid EAN-13, UPC-A, or EAN-8 value with correct check digit.");
                }

                return;
            }

            bool customBarcodeValid = value.Length >= 4 &&
                                      value.All(c =>
                                          char.IsLetterOrDigit(c) ||
                                          c == '-' ||
                                          c == '_' ||
                                          c == '.' ||
                                          c == '/');

            if (!customBarcodeValid)
            {
                throw new InvalidOperationException(
                    "Custom barcode must be at least 4 characters and can contain only letters, numbers, dash, underscore, dot, or slash.");
            }
        }

        private static async Task<string> GenerateNextInternalEan13BarcodeAsync(AppDbContext context)
        {
            var sequence = await context.DocumentSequences
                .FirstOrDefaultAsync(s => s.DocumentType == BarcodeSequenceDocumentType);

            if (sequence == null)
            {
                sequence = new DocumentSequence
                {
                    DocumentType = BarcodeSequenceDocumentType,
                    Prefix = InternalBarcodePrefix,
                    NextSequenceNumber = 1,
                    PaddingLength = 10,
                    UpdatedAt = DateTime.Now
                };

                await context.DocumentSequences.AddAsync(sequence);
                await context.SaveChangesAsync();
            }

            while (true)
            {
                if (sequence.NextSequenceNumber <= 0)
                    sequence.NextSequenceNumber = 1;

                string sequencePart = sequence.NextSequenceNumber
                    .ToString()
                    .PadLeft(10, '0');

                if (sequencePart.Length > 10)
                    throw new InvalidOperationException("Internal barcode sequence limit has been reached.");

                string first12Digits = $"{InternalBarcodePrefix}{sequencePart}";
                int checkDigit = CalculateEan13CheckDigit(first12Digits);
                string barcode = $"{first12Digits}{checkDigit}";

                sequence.NextSequenceNumber++;
                sequence.UpdatedAt = DateTime.Now;

                bool exists = await context.ItemVariants
                    .AsNoTracking()
                    .AnyAsync(v => v.Barcode == barcode);

                if (!exists)
                    return barcode;
            }
        }

        private static int CalculateEan13CheckDigit(string first12Digits)
        {
            string value = NormalizeBarcode(first12Digits);

            if (value.Length != 12 || !IsDigitsOnly(value))
                throw new InvalidOperationException("EAN-13 check digit calculation requires exactly 12 digits.");

            int sum = 0;

            for (int i = 0; i < 12; i++)
            {
                int digit = value[i] - '0';

                // EAN-13: from the left, even positions are multiplied by 3.
                sum += (i % 2 == 0)
                    ? digit
                    : digit * 3;
            }

            return (10 - (sum % 10)) % 10;
        }

        private static async Task EnsureBarcodeIsUniqueAsync(
            AppDbContext context,
            string barcode,
            int excludingVariantId)
        {
            string normalized = NormalizeBarcode(barcode).ToUpperInvariant();

            bool exists = await context.ItemVariants
                .AsNoTracking()
                .AnyAsync(v =>
                    v.Id != excludingVariantId &&
                    v.Barcode.ToUpper() == normalized);

            if (exists)
                throw new InvalidOperationException($"Barcode '{barcode}' is already assigned to another item.");
        }

        private static bool IsDigitsOnly(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            foreach (char c in value)
            {
                if (!char.IsDigit(c))
                    return false;
            }

            return true;
        }

        private static string NormalizeBarcode(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}