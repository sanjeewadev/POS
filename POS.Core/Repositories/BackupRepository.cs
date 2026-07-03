using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models.Backup;

namespace POS.Core.Repositories
{
    public class BackupRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public BackupRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<BackupHistory> AddHistoryAsync(BackupHistory history)
        {
            if (history == null)
                throw new ArgumentNullException(nameof(history));

            Normalize(history);

            await using var context = await _contextFactory.CreateDbContextAsync();

            await context.BackupHistory.AddAsync(history);
            await context.SaveChangesAsync();

            return history;
        }

        public async Task<BackupHistory?> GetLatestBackupAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.BackupHistory
                .AsNoTracking()
                .Where(h => h.ActionType == BackupActionTypes.BackupCreated)
                .OrderByDescending(h => h.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<BackupHistory?> GetLatestRestoreAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.BackupHistory
                .AsNoTracking()
                .Where(h => h.ActionType == BackupActionTypes.BackupRestored)
                .OrderByDescending(h => h.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<List<BackupHistory>> GetRecentHistoryAsync(int count = 30)
        {
            if (count <= 0)
                count = 30;

            if (count > 200)
                count = 200;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.BackupHistory
                .AsNoTracking()
                .OrderByDescending(h => h.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<BackupHistory>> GetRecentBackupHistoryAsync(int count = 30)
        {
            if (count <= 0)
                count = 30;

            if (count > 200)
                count = 200;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.BackupHistory
                .AsNoTracking()
                .Where(h =>
                    h.ActionType == BackupActionTypes.BackupCreated ||
                    h.ActionType == BackupActionTypes.BackupFailed)
                .OrderByDescending(h => h.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<BackupHistory>> GetRecentRestoreHistoryAsync(int count = 30)
        {
            if (count <= 0)
                count = 30;

            if (count > 200)
                count = 200;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.BackupHistory
                .AsNoTracking()
                .Where(h =>
                    h.ActionType == BackupActionTypes.BackupRestored ||
                    h.ActionType == BackupActionTypes.RestoreFailed)
                .OrderByDescending(h => h.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task ClearOldHistoryAsync(int keepLatestCount = 500)
        {
            if (keepLatestCount < 100)
                keepLatestCount = 100;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var keepIds = await context.BackupHistory
                .AsNoTracking()
                .OrderByDescending(h => h.CreatedAt)
                .Take(keepLatestCount)
                .Select(h => h.Id)
                .ToListAsync();

            var oldRows = await context.BackupHistory
                .Where(h => !keepIds.Contains(h.Id))
                .ToListAsync();

            if (oldRows.Count == 0)
                return;

            context.BackupHistory.RemoveRange(oldRows);

            await context.SaveChangesAsync();
        }

        public static BackupHistory CreateHistoryFromResult(
            string actionType,
            BackupResult result,
            string createdBy,
            string machineName,
            string terminalNo)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            string finalActionType = actionType;

            if (!result.Success)
            {
                finalActionType = actionType switch
                {
                    BackupActionTypes.BackupCreated => BackupActionTypes.BackupFailed,
                    BackupActionTypes.BackupRestored => BackupActionTypes.RestoreFailed,
                    BackupActionTypes.BackupVerified => BackupActionTypes.VerifyFailed,
                    _ => actionType
                };
            }

            return new BackupHistory
            {
                ActionType = finalActionType,
                BackupFilePath = result.BackupFilePath,
                BackupFileName = result.BackupFileName,
                Success = result.Success,
                Message = result.Message,
                FileSizeBytes = result.FileSizeBytes,
                Checksum = result.Checksum,
                CreatedAt = result.CreatedAt,
                CreatedBy = createdBy,
                MachineName = machineName,
                TerminalNo = terminalNo
            };
        }

        private static void Normalize(BackupHistory history)
        {
            history.ActionType = NormalizeText(history.ActionType);

            if (string.IsNullOrWhiteSpace(history.ActionType))
                history.ActionType = "Unknown";

            history.BackupFilePath = NormalizeText(history.BackupFilePath);
            history.BackupFileName = NormalizeText(history.BackupFileName);
            history.Message = NormalizeText(history.Message);
            history.Checksum = NormalizeText(history.Checksum);
            history.CreatedBy = NormalizeText(history.CreatedBy);
            history.MachineName = NormalizeText(history.MachineName);
            history.TerminalNo = NormalizeText(history.TerminalNo);

            if (history.CreatedAt == default)
                history.CreatedAt = DateTime.Now;
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }

    public static class BackupActionTypes
    {
        public const string BackupCreated = "BackupCreated";
        public const string BackupRestored = "BackupRestored";
        public const string BackupVerified = "BackupVerified";

        public const string BackupFailed = "BackupFailed";
        public const string RestoreFailed = "RestoreFailed";
        public const string VerifyFailed = "VerifyFailed";
    }
}