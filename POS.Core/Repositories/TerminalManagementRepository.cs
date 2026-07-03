using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models.Licensing;
using POS.Core.Models.Terminals;
using POS.Core.Services.Licensing;

namespace POS.Core.Repositories
{
    public class TerminalManagementRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly MachineFingerprintService _machineFingerprintService;
        private readonly TerminalSettingsRepository _terminalSettingsRepository;

        public TerminalManagementRepository(
            IDbContextFactory<AppDbContext> contextFactory,
            MachineFingerprintService machineFingerprintService,
            TerminalSettingsRepository terminalSettingsRepository)
        {
            _contextFactory = contextFactory;
            _machineFingerprintService = machineFingerprintService;
            _terminalSettingsRepository = terminalSettingsRepository;
        }

        public async Task<List<RegisteredTerminalSummary>> GetAllAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var terminals = await context.RegisteredTerminals
                .AsNoTracking()
                .OrderBy(t => t.TerminalNo)
                .ThenBy(t => t.TerminalName)
                .ToListAsync();

            var terminalLicenses = await context.InstalledLicenses
                .AsNoTracking()
                .Where(l => l.IsActive && l.LicenseType == LicenseType.TerminalLicense)
                .OrderByDescending(l => l.ExpiresOn)
                .ThenByDescending(l => l.ImportedAt)
                .ToListAsync();

            return terminals
                .Select(t => BuildSummary(t, terminalLicenses))
                .ToList();
        }

        public async Task<RegisteredTerminal?> GetByTerminalNoAsync(string terminalNo)
        {
            string safeTerminalNo = NormalizeText(terminalNo);

            if (string.IsNullOrWhiteSpace(safeTerminalNo))
                return null;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.RegisteredTerminals
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TerminalNo == safeTerminalNo);
        }

        public async Task<RegisteredTerminal?> GetByMachineCodeAsync(string machineCode)
        {
            string safeMachineCode = NormalizeMachineCode(machineCode);

            if (string.IsNullOrWhiteSpace(safeMachineCode))
                return null;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.RegisteredTerminals
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.MachineCode == safeMachineCode);
        }

        public async Task<RegisteredTerminal> RegisterOrUpdateCurrentMachineAsync(
            bool isCashierTerminal,
            string updatedBy)
        {
            string machineCode = _machineFingerprintService.GetMachineCode();
            string machineName = _machineFingerprintService.GetMachineName();

            var terminalSettings = await _terminalSettingsRepository
                .GetOrCreateForCurrentMachineAsync("01");

            var terminal = new RegisteredTerminal
            {
                TerminalNo = terminalSettings.TerminalNo,
                TerminalName = terminalSettings.TerminalName,
                MachineName = machineName,
                MachineCode = machineCode,
                Location = terminalSettings.Location,
                IsCashierTerminal = isCashierTerminal,
                IsBackOfficeAllowed = true,
                IsActive = true,
                Remarks = "Registered from current machine."
            };

            return await SaveAsync(terminal, updatedBy);
        }

        public async Task<RegisteredTerminal> SaveAsync(
            RegisteredTerminal terminal,
            string updatedBy)
        {
            if (terminal == null)
                throw new ArgumentNullException(nameof(terminal));

            Normalize(terminal);
            Validate(terminal);

            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                DateTime now = DateTime.Now;
                string safeUpdatedBy = NormalizeText(updatedBy);

                RegisteredTerminal? entity = null;

                if (terminal.Id > 0)
                {
                    entity = await context.RegisteredTerminals
                        .FirstOrDefaultAsync(t => t.Id == terminal.Id);
                }

                if (entity == null)
                {
                    entity = await context.RegisteredTerminals
                        .FirstOrDefaultAsync(t => t.TerminalNo == terminal.TerminalNo);
                }

                int currentId = entity?.Id ?? 0;

                bool duplicateTerminalNoExists = await context.RegisteredTerminals.AnyAsync(t =>
                    t.TerminalNo == terminal.TerminalNo &&
                    t.Id != currentId);

                if (duplicateTerminalNoExists)
                    throw new InvalidOperationException($"Terminal number '{terminal.TerminalNo}' is already registered.");

                if (!string.IsNullOrWhiteSpace(terminal.MachineCode))
                {
                    bool duplicateMachineCodeExists = await context.RegisteredTerminals.AnyAsync(t =>
                        t.MachineCode == terminal.MachineCode &&
                        t.Id != currentId);

                    if (duplicateMachineCodeExists)
                        throw new InvalidOperationException("This machine code is already registered to another terminal.");
                }

                if (entity == null)
                {
                    entity = new RegisteredTerminal
                    {
                        CreatedAt = now
                    };

                    await context.RegisteredTerminals.AddAsync(entity);
                }

                CopyToEntity(terminal, entity);

                entity.UpdatedAt = now;
                entity.UpdatedBy = safeUpdatedBy;

                await ApplyLicenseSnapshotAsync(context, entity, now);

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return await context.RegisteredTerminals
                    .AsNoTracking()
                    .FirstAsync(t => t.Id == entity.Id);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task SetActiveStatusAsync(
            int terminalId,
            bool isActive,
            string updatedBy)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var terminal = await context.RegisteredTerminals
                .FirstOrDefaultAsync(t => t.Id == terminalId);

            if (terminal == null)
                return;

            terminal.IsActive = isActive;
            terminal.UpdatedAt = DateTime.Now;
            terminal.UpdatedBy = NormalizeText(updatedBy);
            terminal.Remarks = AppendRemark(
                terminal.Remarks,
                isActive ? "Terminal activated." : "Terminal deactivated.");

            await context.SaveChangesAsync();
        }

        public async Task UpdateLastLoginAsync(
            string terminalNo,
            string machineCode)
        {
            await UpdateActivityAsync(
                terminalNo,
                machineCode,
                updateLogin: true,
                updateSale: false);
        }

        public async Task UpdateLastSaleAsync(
            string terminalNo,
            string machineCode)
        {
            await UpdateActivityAsync(
                terminalNo,
                machineCode,
                updateLogin: false,
                updateSale: true);
        }

        private async Task UpdateActivityAsync(
            string terminalNo,
            string machineCode,
            bool updateLogin,
            bool updateSale)
        {
            string safeTerminalNo = NormalizeText(terminalNo);
            string safeMachineCode = NormalizeMachineCode(machineCode);

            if (string.IsNullOrWhiteSpace(safeTerminalNo) &&
                string.IsNullOrWhiteSpace(safeMachineCode))
            {
                return;
            }

            await using var context = await _contextFactory.CreateDbContextAsync();

            var terminal = await context.RegisteredTerminals
                .FirstOrDefaultAsync(t =>
                    (!string.IsNullOrWhiteSpace(safeMachineCode) && t.MachineCode == safeMachineCode) ||
                    (!string.IsNullOrWhiteSpace(safeTerminalNo) && t.TerminalNo == safeTerminalNo));

            if (terminal == null)
                return;

            DateTime now = DateTime.Now;

            if (updateLogin)
                terminal.LastLoginAt = now;

            if (updateSale)
                terminal.LastSaleAt = now;

            terminal.UpdatedAt = now;

            await context.SaveChangesAsync();
        }

        private static RegisteredTerminalSummary BuildSummary(
            RegisteredTerminal terminal,
            List<InstalledLicense> terminalLicenses)
        {
            InstalledLicense? license = null;

            if (terminal.IsCashierTerminal)
            {
                license = terminalLicenses
                    .Where(l =>
                        l.TerminalNo == terminal.TerminalNo &&
                        (string.IsNullOrWhiteSpace(terminal.MachineCode) ||
                         l.MachineCode == terminal.MachineCode))
                    .OrderByDescending(l => l.ExpiresOn)
                    .ThenByDescending(l => l.ImportedAt)
                    .FirstOrDefault();
            }

            LicenseStatus licenseStatus = terminal.IsCashierTerminal
                ? LicenseRepository.CalculateCurrentStatus(license)
                : LicenseStatus.Active;

            return new RegisteredTerminalSummary
            {
                Id = terminal.Id,
                TerminalNo = terminal.TerminalNo,
                TerminalName = terminal.TerminalName,
                MachineName = terminal.MachineName,
                MachineCode = terminal.MachineCode,
                Location = terminal.Location,
                IsCashierTerminal = terminal.IsCashierTerminal,
                IsBackOfficeAllowed = terminal.IsBackOfficeAllowed,
                IsActive = terminal.IsActive,
                LicenseId = terminal.IsCashierTerminal
                    ? license?.LicenseId ?? terminal.LicenseId
                    : "Not Required",
                LicenseStatus = licenseStatus,
                LicenseExpiryDate = terminal.IsCashierTerminal
                    ? license?.ExpiresOn ?? terminal.LicenseExpiryDate
                    : null,
                LastLoginAt = terminal.LastLoginAt,
                LastSaleAt = terminal.LastSaleAt
            };
        }

        private static async Task ApplyLicenseSnapshotAsync(
            AppDbContext context,
            RegisteredTerminal terminal,
            DateTime now)
        {
            if (!terminal.IsCashierTerminal)
            {
                terminal.LicenseId = string.Empty;
                terminal.LicenseExpiryDate = null;
                terminal.LicenseLastCheckedAt = now;
                return;
            }

            if (string.IsNullOrWhiteSpace(terminal.TerminalNo) ||
                string.IsNullOrWhiteSpace(terminal.MachineCode))
            {
                terminal.LicenseLastCheckedAt = now;
                return;
            }

            var license = await context.InstalledLicenses
                .AsNoTracking()
                .Where(l =>
                    l.IsActive &&
                    l.LicenseType == LicenseType.TerminalLicense &&
                    l.TerminalNo == terminal.TerminalNo &&
                    l.MachineCode == terminal.MachineCode)
                .OrderByDescending(l => l.ExpiresOn)
                .ThenByDescending(l => l.ImportedAt)
                .FirstOrDefaultAsync();

            terminal.LicenseId = license?.LicenseId ?? string.Empty;
            terminal.LicenseExpiryDate = license?.ExpiresOn;
            terminal.LicenseLastCheckedAt = now;
        }

        private static void CopyToEntity(
            RegisteredTerminal source,
            RegisteredTerminal target)
        {
            target.TerminalNo = source.TerminalNo;
            target.TerminalName = source.TerminalName;
            target.MachineName = source.MachineName;
            target.MachineCode = source.MachineCode;
            target.Location = source.Location;
            target.IsCashierTerminal = source.IsCashierTerminal;
            target.IsBackOfficeAllowed = source.IsBackOfficeAllowed;
            target.IsActive = source.IsActive;
            target.Remarks = source.Remarks;
        }

        private static void Normalize(RegisteredTerminal terminal)
        {
            terminal.TerminalNo = NormalizeText(terminal.TerminalNo);

            if (string.IsNullOrWhiteSpace(terminal.TerminalNo))
                terminal.TerminalNo = "01";

            terminal.TerminalName = NormalizeText(terminal.TerminalName);

            if (string.IsNullOrWhiteSpace(terminal.TerminalName))
                terminal.TerminalName = $"Terminal {terminal.TerminalNo}";

            terminal.MachineName = NormalizeText(terminal.MachineName);
            terminal.MachineCode = NormalizeMachineCode(terminal.MachineCode);

            terminal.Location = NormalizeText(terminal.Location);

            if (string.IsNullOrWhiteSpace(terminal.Location))
                terminal.Location = "Main Store";

            terminal.UpdatedBy = NormalizeText(terminal.UpdatedBy);
            terminal.Remarks = NormalizeMultilineText(terminal.Remarks);
            terminal.LicenseId = NormalizeText(terminal.LicenseId);
        }

        private static void Validate(RegisteredTerminal terminal)
        {
            if (string.IsNullOrWhiteSpace(terminal.TerminalNo))
                throw new InvalidOperationException("Terminal number is required.");

            if (string.IsNullOrWhiteSpace(terminal.TerminalName))
                throw new InvalidOperationException("Terminal name is required.");

            if (string.IsNullOrWhiteSpace(terminal.Location))
                throw new InvalidOperationException("Terminal location is required.");
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
            string safeCurrent = NormalizeMultilineText(currentRemarks);
            string safeNew = NormalizeText(newRemark);

            if (string.IsNullOrWhiteSpace(safeCurrent))
                return safeNew;

            if (string.IsNullOrWhiteSpace(safeNew))
                return safeCurrent;

            return $"{safeCurrent}\n{safeNew}";
        }
    }
}