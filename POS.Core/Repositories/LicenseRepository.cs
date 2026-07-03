using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models.Licensing;

namespace POS.Core.Repositories
{
    public class LicenseRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public LicenseRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<InstalledLicense?> GetActiveStoreLicenseAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var license = await context.InstalledLicenses
                .AsNoTracking()
                .Where(l => l.IsActive && l.LicenseType == LicenseType.StoreLicense)
                .OrderByDescending(l => l.ExpiresOn)
                .ThenByDescending(l => l.ImportedAt)
                .FirstOrDefaultAsync();

            ApplyCurrentStatus(license);

            return license;
        }

        public async Task<InstalledLicense?> GetActiveTerminalLicenseAsync(string machineCode, string terminalNo)
        {
            string safeMachineCode = NormalizeMachineCode(machineCode);
            string safeTerminalNo = NormalizeText(terminalNo);

            if (string.IsNullOrWhiteSpace(safeMachineCode) || string.IsNullOrWhiteSpace(safeTerminalNo))
                return null;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var license = await context.InstalledLicenses
                .AsNoTracking()
                .Where(l =>
                    l.IsActive &&
                    l.LicenseType == LicenseType.TerminalLicense &&
                    l.MachineCode == safeMachineCode &&
                    l.TerminalNo == safeTerminalNo)
                .OrderByDescending(l => l.ExpiresOn)
                .ThenByDescending(l => l.ImportedAt)
                .FirstOrDefaultAsync();

            ApplyCurrentStatus(license);

            return license;
        }

        public async Task<List<InstalledLicense>> GetAllInstalledLicensesAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var licenses = await context.InstalledLicenses
                .AsNoTracking()
                .OrderByDescending(l => l.ImportedAt)
                .ThenByDescending(l => l.Id)
                .ToListAsync();

            foreach (var license in licenses)
            {
                ApplyCurrentStatus(license);
            }

            return licenses;
        }

        public async Task<List<InstalledLicense>> GetActiveTerminalLicensesForStoreAsync(string storeId)
        {
            string safeStoreId = NormalizeText(storeId);

            await using var context = await _contextFactory.CreateDbContextAsync();

            var licenses = await context.InstalledLicenses
                .AsNoTracking()
                .Where(l =>
                    l.IsActive &&
                    l.LicenseType == LicenseType.TerminalLicense &&
                    l.StoreId == safeStoreId)
                .OrderBy(l => l.TerminalNo)
                .ThenByDescending(l => l.ImportedAt)
                .ToListAsync();

            foreach (var license in licenses)
            {
                ApplyCurrentStatus(license);
            }

            return licenses;
        }

        public async Task<InstalledLicense> ImportLicenseAsync(InstalledLicense license, string importedBy)
        {
            if (license == null)
                throw new ArgumentNullException(nameof(license));

            Normalize(license);
            Validate(license);

            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                DateTime now = DateTime.Now;
                string safeImportedBy = NormalizeText(importedBy);

                await DeactivatePreviousLicensesAsync(context, license, now, safeImportedBy);

                license.Id = 0;
                license.IsActive = true;
                license.ImportedAt = now;
                license.ImportedBy = safeImportedBy;
                license.LastVerifiedAt = now;
                license.LicenseStatus = CalculateCurrentStatus(license, now);

                await context.InstalledLicenses.AddAsync(license);
                await context.SaveChangesAsync();

                await transaction.CommitAsync();

                return await context.InstalledLicenses
                    .AsNoTracking()
                    .FirstAsync(l => l.Id == license.Id);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeactivateLicenseAsync(int installedLicenseId, string updatedBy)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var license = await context.InstalledLicenses
                .FirstOrDefaultAsync(l => l.Id == installedLicenseId);

            if (license == null)
                return;

            license.IsActive = false;
            license.LastVerifiedAt = DateTime.Now;
            license.Remarks = AppendRemark(
                license.Remarks,
                $"Deactivated by {NormalizeText(updatedBy)} on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            await context.SaveChangesAsync();
        }

        public async Task RefreshCurrentStatusesAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var licenses = await context.InstalledLicenses
                .Where(l => l.IsActive)
                .ToListAsync();

            DateTime now = DateTime.Now;

            foreach (var license in licenses)
            {
                license.LicenseStatus = CalculateCurrentStatus(license, now);
                license.LastVerifiedAt = now;
            }

            await context.SaveChangesAsync();
        }

        public static LicenseStatus CalculateCurrentStatus(InstalledLicense? license, DateTime? currentDate = null)
        {
            if (license == null)
                return LicenseStatus.Missing;

            if (!license.IsActive)
                return LicenseStatus.Revoked;

            if (license.LicenseStatus == LicenseStatus.Invalid ||
                license.LicenseStatus == LicenseStatus.Revoked)
            {
                return license.LicenseStatus;
            }

            DateTime today = (currentDate ?? DateTime.Now).Date;
            DateTime expiryDate = license.ExpiresOn.Date;
            DateTime graceEndDate = expiryDate.AddDays(Math.Max(license.GraceDays, 0));

            if (today <= expiryDate)
            {
                int daysRemaining = (expiryDate - today).Days;

                if (daysRemaining <= 30)
                    return LicenseStatus.ExpiringSoon;

                return LicenseStatus.Active;
            }

            if (today <= graceEndDate)
                return LicenseStatus.GracePeriod;

            return LicenseStatus.ExpiredReadOnly;
        }

        public static int GetDaysRemaining(InstalledLicense? license, DateTime? currentDate = null)
        {
            if (license == null)
                return 0;

            DateTime today = (currentDate ?? DateTime.Now).Date;
            return (license.ExpiresOn.Date - today).Days;
        }

        public static bool IsOperationalStatus(LicenseStatus status)
        {
            return status == LicenseStatus.Active ||
                   status == LicenseStatus.ExpiringSoon ||
                   status == LicenseStatus.GracePeriod;
        }

        public static bool IsReadOnlyStatus(LicenseStatus status)
        {
            return status == LicenseStatus.ExpiredReadOnly ||
                   status == LicenseStatus.Invalid ||
                   status == LicenseStatus.Missing ||
                   status == LicenseStatus.Revoked;
        }

        private static async Task DeactivatePreviousLicensesAsync(
            AppDbContext context,
            InstalledLicense newLicense,
            DateTime now,
            string updatedBy)
        {
            IQueryable<InstalledLicense> query = context.InstalledLicenses
                .Where(l => l.IsActive && l.LicenseType == newLicense.LicenseType);

            if (newLicense.LicenseType == LicenseType.StoreLicense)
            {
                query = query.Where(l => l.StoreId == newLicense.StoreId);
            }
            else
            {
                query = query.Where(l =>
                    l.StoreId == newLicense.StoreId &&
                    l.TerminalNo == newLicense.TerminalNo);
            }

            var oldLicenses = await query.ToListAsync();

            foreach (var oldLicense in oldLicenses)
            {
                oldLicense.IsActive = false;
                oldLicense.LastVerifiedAt = now;
                oldLicense.Remarks = AppendRemark(
                    oldLicense.Remarks,
                    $"Replaced by license {newLicense.LicenseId} on {now:yyyy-MM-dd HH:mm:ss} by {updatedBy}");
            }
        }

        private static void ApplyCurrentStatus(InstalledLicense? license)
        {
            if (license == null)
                return;

            license.LicenseStatus = CalculateCurrentStatus(license);
        }

        private static void Normalize(InstalledLicense license)
        {
            license.LicenseId = NormalizeText(license.LicenseId);
            license.StoreId = NormalizeText(license.StoreId);
            license.StoreName = NormalizeText(license.StoreName);
            license.TerminalNo = NormalizeText(license.TerminalNo);
            license.MachineCode = NormalizeMachineCode(license.MachineCode);
            license.ImportedBy = NormalizeText(license.ImportedBy);
            license.RawLicenseJson = NormalizeMultilineText(license.RawLicenseJson);
            license.Signature = NormalizeText(license.Signature);
            license.Remarks = NormalizeMultilineText(license.Remarks);

            license.IssuedOn = license.IssuedOn.Date;
            license.ExpiresOn = license.ExpiresOn.Date;

            if (license.GraceDays < 0)
                license.GraceDays = 0;
        }

        private static void Validate(InstalledLicense license)
        {
            if (string.IsNullOrWhiteSpace(license.LicenseId))
                throw new InvalidOperationException("License ID is required.");

            if (license.LicenseType != LicenseType.StoreLicense &&
                license.LicenseType != LicenseType.TerminalLicense)
            {
                throw new InvalidOperationException("Invalid license type.");
            }

            if (string.IsNullOrWhiteSpace(license.StoreId))
                throw new InvalidOperationException("Store ID is required.");

            if (string.IsNullOrWhiteSpace(license.StoreName))
                throw new InvalidOperationException("Store name is required.");

            if (license.IssuedOn == default)
                throw new InvalidOperationException("License issued date is required.");

            if (license.ExpiresOn == default)
                throw new InvalidOperationException("License expiry date is required.");

            if (license.ExpiresOn < license.IssuedOn)
                throw new InvalidOperationException("License expiry date cannot be earlier than issued date.");

            if (string.IsNullOrWhiteSpace(license.Signature))
                throw new InvalidOperationException("License signature is required.");

            if (license.LicenseType == LicenseType.TerminalLicense)
            {
                if (string.IsNullOrWhiteSpace(license.TerminalNo))
                    throw new InvalidOperationException("Terminal number is required for terminal license.");

                if (string.IsNullOrWhiteSpace(license.MachineCode))
                    throw new InvalidOperationException("Machine code is required for terminal license.");
            }
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string NormalizeMachineCode(string? value)
        {
            return NormalizeText(value).ToUpperInvariant();
        }

        private static string NormalizeMultilineText(string? value)
        {
            return (value ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Trim();
        }

        private static string AppendRemark(string? currentRemarks, string newRemark)
        {
            string safeCurrentRemarks = NormalizeMultilineText(currentRemarks);
            string safeNewRemark = NormalizeText(newRemark);

            if (string.IsNullOrWhiteSpace(safeCurrentRemarks))
                return safeNewRemark;

            if (string.IsNullOrWhiteSpace(safeNewRemark))
                return safeCurrentRemarks;

            return $"{safeCurrentRemarks}\n{safeNewRemark}";
        }
    }
}