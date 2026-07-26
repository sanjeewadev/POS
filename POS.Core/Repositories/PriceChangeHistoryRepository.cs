using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.DTOs;

namespace POS.Core.Repositories
{
    public class PriceChangeHistoryRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public PriceChangeHistoryRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<List<PriceChangeOperationSummaryDto>> GetOperationsAsync(
            string searchText = "",
            string priceLevel = "All",
            string changeSource = "All",
            string changedBy = "All",
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            int maxOperations = 500)
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            IQueryable<PriceChangeHistory> filtered = ApplyFilters(
                context.PriceChangeHistories.AsNoTracking(),
                searchText,
                priceLevel,
                changeSource,
                changedBy,
                dateFrom,
                dateTo);

            maxOperations = NormalizeLimit(maxOperations);

            var numbered = await filtered
                .Where(row => row.PriceChangeNo != "")
                .GroupBy(row => row.PriceChangeNo)
                .Select(group => new
                {
                    PriceChangeNo = group.Key,
                    ChangedAt = group.Max(row => row.ChangedAt),
                    MaxId = group.Max(row => row.Id)
                })
                .OrderByDescending(row => row.ChangedAt)
                .ThenByDescending(row => row.MaxId)
                .Take(maxOperations)
                .ToListAsync();

            var legacy = await filtered
                .Where(row => row.PriceChangeNo == "")
                .OrderByDescending(row => row.ChangedAt)
                .ThenByDescending(row => row.Id)
                .Select(row => new
                {
                    row.Id,
                    row.ChangedAt
                })
                .Take(maxOperations)
                .ToListAsync();

            var selectedKeys = numbered
                .Select(row => new SelectedOperationKey
                {
                    OperationKey = "PCH:" + row.PriceChangeNo,
                    PriceChangeNo = row.PriceChangeNo,
                    ChangedAt = row.ChangedAt,
                    LegacyId = null
                })
                .Concat(legacy.Select(row => new SelectedOperationKey
                {
                    OperationKey = "LEGACY:" + row.Id,
                    PriceChangeNo = string.Empty,
                    ChangedAt = row.ChangedAt,
                    LegacyId = row.Id
                }))
                .OrderByDescending(row => row.ChangedAt)
                .ThenByDescending(row => row.LegacyId ?? 0)
                .Take(maxOperations)
                .ToList();

            if (selectedKeys.Count == 0)
                return new List<PriceChangeOperationSummaryDto>();

            string[] numbers = selectedKeys
                .Where(key => !string.IsNullOrWhiteSpace(key.PriceChangeNo))
                .Select(key => key.PriceChangeNo)
                .Distinct()
                .ToArray();
            int[] legacyIds = selectedKeys
                .Where(key => key.LegacyId.HasValue)
                .Select(key => key.LegacyId!.Value)
                .ToArray();

            List<PriceChangeHistory> allDetails = await context.PriceChangeHistories
                .AsNoTracking()
                .Where(row =>
                    numbers.Contains(row.PriceChangeNo) ||
                    legacyIds.Contains(row.Id))
                .OrderByDescending(row => row.ChangedAt)
                .ThenBy(row => row.Id)
                .ToListAsync();

            var summaries = new List<PriceChangeOperationSummaryDto>();
            foreach (SelectedOperationKey key in selectedKeys)
            {
                List<PriceChangeHistory> details = key.LegacyId.HasValue
                    ? allDetails.Where(row => row.Id == key.LegacyId.Value).ToList()
                    : allDetails.Where(row => row.PriceChangeNo == key.PriceChangeNo).ToList();
                if (details.Count == 0)
                    continue;

                PriceChangeHistory latest = details
                    .OrderByDescending(row => row.ChangedAt)
                    .ThenByDescending(row => row.Id)
                    .First();
                string[] itemNames = details
                    .Select(BuildItemVariantDisplay)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value)
                    .ToArray();
                string[] sources = details
                    .Select(row => row.ChangeSource)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                string[] users = details
                    .Select(row => row.ChangedBy)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                string[] documents = details
                    .Select(row => row.SourceDocumentNo)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                summaries.Add(new PriceChangeOperationSummaryDto
                {
                    OperationKey = key.OperationKey,
                    PriceChangeNo = key.PriceChangeNo,
                    ChangedAt = details.Max(row => row.ChangedAt),
                    ChangedBy = users.Length == 1 ? users[0] : users.Length > 1 ? "Multiple" : string.Empty,
                    ChangeSource = sources.Length == 1 ? sources[0] : sources.Length > 1 ? "Mixed" : string.Empty,
                    ItemVariantSummary = itemNames.Length switch
                    {
                        0 => "Legacy price change",
                        1 => itemNames[0],
                        _ => $"{itemNames[0]} + {itemNames.Length - 1} more"
                    },
                    MasterChangeSummary = BuildMasterChangeSummary(details),
                    BatchOverrideCount = details.Count(row =>
                        row.PriceLevel.Equals("Batch", StringComparison.OrdinalIgnoreCase) &&
                        (row.ChangeAction == PriceChangeActionCodes.BatchOverrideCreated ||
                         row.ChangeAction == PriceChangeActionCodes.BatchOverrideUpdated ||
                         row.ChangeAction == PriceChangeActionCodes.BatchOverrideRemoved ||
                         row.ChangeAction == PriceChangeActionCodes.LegacyBatchChange)),
                    DetailRowCount = details.Count,
                    AffectedVariantCount = details.Select(row => row.ItemVariantId).Distinct().Count(),
                    SourceDocumentNo = documents.Length == 1 ? documents[0] : documents.Length > 1 ? $"{documents[0]} + {documents.Length - 1} more" : string.Empty,
                    ChangeReason = latest.ChangeReason
                });
            }

            return summaries
                .OrderByDescending(row => row.ChangedAt)
                .ThenByDescending(row => row.PriceChangeNo)
                .ToList();
        }

        public async Task<List<PriceChangeDetailDto>> GetOperationDetailsAsync(string operationKey)
        {
            string key = NormalizeText(operationKey);
            if (string.IsNullOrWhiteSpace(key))
                return new List<PriceChangeDetailDto>();

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            IQueryable<PriceChangeHistory> query = context.PriceChangeHistories.AsNoTracking();
            if (key.StartsWith("PCH:", StringComparison.Ordinal))
            {
                string number = key[4..];
                query = query.Where(row => row.PriceChangeNo == number);
            }
            else if (key.StartsWith("LEGACY:", StringComparison.Ordinal) &&
                     int.TryParse(key[7..], out int id))
            {
                query = query.Where(row => row.Id == id);
            }
            else
            {
                query = query.Where(row => row.PriceChangeNo == key);
            }

            return await query
                .OrderBy(row => row.PriceLevel == "Master" ? 0 : 1)
                .ThenBy(row => row.ItemDescription)
                .ThenBy(row => row.VariantDescription)
                .ThenBy(row => row.BatchNo)
                .ThenBy(row => row.Id)
                .Select(row => new PriceChangeDetailDto
                {
                    Id = row.Id,
                    PriceChangeNo = row.PriceChangeNo,
                    ChangedAt = row.ChangedAt,
                    ChangedBy = row.ChangedBy,
                    PriceLevel = row.PriceLevel,
                    ChangeAction = row.ChangeAction,
                    ChangeSource = row.ChangeSource,
                    OldPriceSource = row.OldPriceSource,
                    NewPriceSource = row.NewPriceSource,
                    ItemVariantId = row.ItemVariantId,
                    ItemBatchId = row.ItemBatchId,
                    ItemCode = row.ItemCode,
                    SkuCode = row.SkuCode,
                    Barcode = row.Barcode,
                    ItemDescription = row.ItemDescription,
                    VariantDescription = row.VariantDescription,
                    BatchNo = row.BatchNo,
                    BatchExpiryDate = row.BatchExpiryDate,
                    EffectiveCost = row.EffectiveCost,
                    OldMinimumPrice = row.OldMinimumPrice,
                    NewMinimumPrice = row.NewMinimumPrice,
                    OldRetailPrice = row.OldRetailPrice,
                    NewRetailPrice = row.NewRetailPrice,
                    OldWholesalePrice = row.OldWholesalePrice,
                    NewWholesalePrice = row.NewWholesalePrice,
                    OldMaximumPrice = row.OldMaximumPrice,
                    NewMaximumPrice = row.NewMaximumPrice,
                    SourceDocumentType = row.SourceDocumentType,
                    SourceDocumentNo = row.SourceDocumentNo,
                    ChangeReason = row.ChangeReason,
                    Remarks = row.Remarks
                })
                .ToListAsync();
        }

        // Compatibility raw-row methods.
        public async Task<List<PriceChangeHistory>> GetPriceChangeHistoryAsync(
            string searchText = "",
            string priceLevel = "All",
            string changeSource = "All",
            string changedBy = "All",
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            int maxRows = 500)
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            return await ApplyFilters(
                    context.PriceChangeHistories.AsNoTracking(),
                    searchText,
                    priceLevel,
                    changeSource,
                    changedBy,
                    dateFrom,
                    dateTo)
                .OrderByDescending(row => row.ChangedAt)
                .ThenByDescending(row => row.Id)
                .Take(NormalizeLimit(maxRows))
                .ToListAsync();
        }

        public async Task<List<PriceChangeHistory>> GetByPriceChangeNoAsync(string priceChangeNo)
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            string number = NormalizeText(priceChangeNo);
            if (string.IsNullOrWhiteSpace(number))
                return new List<PriceChangeHistory>();
            return await context.PriceChangeHistories
                .AsNoTracking()
                .Where(row => row.PriceChangeNo == number)
                .OrderBy(row => row.PriceLevel == "Master" ? 0 : 1)
                .ThenBy(row => row.ItemCode)
                .ThenBy(row => row.BatchNo)
                .ThenBy(row => row.Id)
                .ToListAsync();
        }

        public async Task<List<string>> GetChangeSourcesAsync()
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            return await context.PriceChangeHistories
                .AsNoTracking()
                .Where(row => row.ChangeSource != "")
                .Select(row => row.ChangeSource)
                .Distinct()
                .OrderBy(value => value)
                .ToListAsync();
        }

        public async Task<List<string>> GetChangedByUsersAsync()
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            return await context.PriceChangeHistories
                .AsNoTracking()
                .Where(row => row.ChangedBy != "")
                .Select(row => row.ChangedBy)
                .Distinct()
                .OrderBy(value => value)
                .ToListAsync();
        }

        public async Task<int> GetTodayChangeCountAsync()
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);
            int numbered = await context.PriceChangeHistories
                .AsNoTracking()
                .Where(row => row.ChangedAt >= today && row.ChangedAt < tomorrow && row.PriceChangeNo != "")
                .Select(row => row.PriceChangeNo)
                .Distinct()
                .CountAsync();
            int legacy = await context.PriceChangeHistories
                .AsNoTracking()
                .CountAsync(row => row.ChangedAt >= today && row.ChangedAt < tomorrow && row.PriceChangeNo == "");
            return numbered + legacy;
        }

        public async Task<PriceChangeHistory?> GetLatestChangeAsync()
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            return await context.PriceChangeHistories
                .AsNoTracking()
                .OrderByDescending(row => row.ChangedAt)
                .ThenByDescending(row => row.Id)
                .FirstOrDefaultAsync();
        }

        private static IQueryable<PriceChangeHistory> ApplyFilters(
            IQueryable<PriceChangeHistory> query,
            string searchText,
            string priceLevel,
            string changeSource,
            string changedBy,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            string search = NormalizeText(searchText).ToUpperInvariant();
            string level = NormalizeText(priceLevel);
            string source = NormalizeText(changeSource);
            string user = NormalizeText(changedBy);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(row =>
                    row.PriceChangeNo.ToUpper().Contains(search) ||
                    row.ItemCode.ToUpper().Contains(search) ||
                    row.SkuCode.ToUpper().Contains(search) ||
                    row.Barcode.ToUpper().Contains(search) ||
                    row.ItemDescription.ToUpper().Contains(search) ||
                    row.VariantDescription.ToUpper().Contains(search) ||
                    row.BatchNo.ToUpper().Contains(search) ||
                    row.ChangeAction.ToUpper().Contains(search) ||
                    row.ChangeReason.ToUpper().Contains(search) ||
                    row.Remarks.ToUpper().Contains(search) ||
                    row.SourceDocumentNo.ToUpper().Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(level) && !level.Equals("All", StringComparison.OrdinalIgnoreCase))
                query = query.Where(row => row.PriceLevel == level);
            if (!string.IsNullOrWhiteSpace(source) && !source.Equals("All", StringComparison.OrdinalIgnoreCase))
                query = query.Where(row => row.ChangeSource == source);
            if (!string.IsNullOrWhiteSpace(user) && !user.Equals("All", StringComparison.OrdinalIgnoreCase))
                query = query.Where(row => row.ChangedBy == user);
            if (dateFrom.HasValue)
            {
                DateTime from = dateFrom.Value.Date;
                query = query.Where(row => row.ChangedAt >= from);
            }
            if (dateTo.HasValue)
            {
                DateTime exclusive = dateTo.Value.Date.AddDays(1);
                query = query.Where(row => row.ChangedAt < exclusive);
            }
            return query;
        }

        private static string BuildMasterChangeSummary(IEnumerable<PriceChangeHistory> rows)
        {
            var fields = new List<string>();
            foreach (PriceChangeHistory row in rows.Where(row => row.PriceLevel.Equals("Master", StringComparison.OrdinalIgnoreCase)))
            {
                if (row.OldMinimumPrice != row.NewMinimumPrice && !fields.Contains("Minimum")) fields.Add("Minimum");
                if (row.OldRetailPrice != row.NewRetailPrice && !fields.Contains("Retail")) fields.Add("Retail");
                if (row.OldWholesalePrice != row.NewWholesalePrice && !fields.Contains("Wholesale")) fields.Add("Wholesale");
                if (row.OldMaximumPrice != row.NewMaximumPrice && !fields.Contains("Maximum")) fields.Add("Maximum");
            }
            return fields.Count == 0 ? "No master change" : string.Join(", ", fields);
        }

        private static string BuildItemVariantDisplay(PriceChangeHistory row)
        {
            string item = NormalizeText(row.ItemDescription);
            string variant = NormalizeText(row.VariantDescription);
            if (string.IsNullOrWhiteSpace(item))
                item = NormalizeText(row.ItemCode);
            return string.IsNullOrWhiteSpace(variant) || variant.Equals("Standard", StringComparison.OrdinalIgnoreCase)
                ? item
                : $"{item} - {variant}";
        }

        private static int NormalizeLimit(int value) => value <= 0 ? 500 : Math.Min(value, 5000);
        private static string NormalizeText(string? value) => (value ?? string.Empty).Trim();

        private sealed class SelectedOperationKey
        {
            public string OperationKey { get; set; } = string.Empty;
            public string PriceChangeNo { get; set; } = string.Empty;
            public DateTime ChangedAt { get; set; }
            public int? LegacyId { get; set; }
        }
    }
}
