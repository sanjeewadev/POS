using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.Licensing;
using POS.Core.Models.Terminals;
using POS.Core.Services.Licensing;

namespace POS.Core.Repositories
{
    public class TerminalManagementRepository
    {
        private readonly
            IDbContextFactory<AppDbContext>
            _contextFactory;

        private readonly MachineFingerprintService
            _machineFingerprintService;

        private readonly TerminalSettingsRepository
            _terminalSettingsRepository;

        public TerminalManagementRepository(
            IDbContextFactory<AppDbContext>
                contextFactory,
            MachineFingerprintService
                machineFingerprintService,
            TerminalSettingsRepository
                terminalSettingsRepository)
        {
            _contextFactory = contextFactory;

            _machineFingerprintService =
                machineFingerprintService;

            _terminalSettingsRepository =
                terminalSettingsRepository;
        }

        public async Task<
            List<RegisteredTerminalSummary>>
            GetAllAsync()
        {
            await using AppDbContext context =
                await _contextFactory
                    .CreateDbContextAsync();

            List<RegisteredTerminal> terminals =
                await context.RegisteredTerminals
                    .AsNoTracking()
                    .OrderBy(
                        terminal =>
                            terminal.TerminalNo)
                    .ThenBy(
                        terminal =>
                            terminal.TerminalName)
                    .ToListAsync();

            List<InstalledLicense> licenses =
                await context.InstalledLicenses
                    .AsNoTracking()
                    .Where(
                        license =>
                            license.IsActive &&
                            license.LicenseType ==
                                LicenseType
                                    .TerminalLicense)
                    .OrderByDescending(
                        license =>
                            license.ExpiresOn)
                    .ThenByDescending(
                        license =>
                            license.ImportedAt)
                    .ToListAsync();

            string currentMachineCode =
                NormalizeMachineCode(
                    _machineFingerprintService
                        .GetMachineCode());

            return terminals
                .Select(
                    terminal =>
                        BuildSummary(
                            terminal,
                            licenses,
                            currentMachineCode))
                .ToList();
        }

        public async Task<RegisteredTerminal?>
            GetByTerminalNoAsync(
                string terminalNo)
        {
            string safeTerminalNo =
                NormalizeText(terminalNo);

            if (string.IsNullOrWhiteSpace(
                    safeTerminalNo))
            {
                return null;
            }

            await using AppDbContext context =
                await _contextFactory
                    .CreateDbContextAsync();

            return await context
                .RegisteredTerminals
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    terminal =>
                        terminal.TerminalNo ==
                            safeTerminalNo);
        }

        public async Task<RegisteredTerminal?>
            GetByMachineCodeAsync(
                string machineCode)
        {
            string safeMachineCode =
                NormalizeMachineCode(
                    machineCode);

            if (string.IsNullOrWhiteSpace(
                    safeMachineCode))
            {
                return null;
            }

            await using AppDbContext context =
                await _contextFactory
                    .CreateDbContextAsync();

            return await context
                .RegisteredTerminals
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    terminal =>
                        terminal.MachineCode ==
                            safeMachineCode);
        }

        public async Task<RegisteredTerminal>
            RegisterOrUpdateCurrentMachineAsync(
                string updatedBy)
        {
            string machineCode =
                NormalizeMachineCode(
                    _machineFingerprintService
                        .GetMachineCode());

            string machineName =
                NormalizeText(
                    _machineFingerprintService
                        .GetMachineName());

            if (string.IsNullOrWhiteSpace(
                    machineCode))
            {
                throw new InvalidOperationException(
                    "The current machine code is unavailable.");
            }

            TerminalSettings settings =
                await _terminalSettingsRepository
                    .GetOrCreateForCurrentMachineAsync();

            string terminalNo =
                NormalizeText(
                    settings.TerminalNo);

            if (string.IsNullOrWhiteSpace(
                    terminalNo))
            {
                throw new InvalidOperationException(
                    "The current terminal number is unavailable.");
            }

            await using AppDbContext context =
                await _contextFactory
                    .CreateDbContextAsync();

            await using var transaction =
                await context.Database
                    .BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                RegisteredTerminal? entity =
                    await context.RegisteredTerminals
                        .FirstOrDefaultAsync(
                            terminal =>
                                terminal.MachineCode ==
                                    machineCode);

                entity ??=
                    await context.RegisteredTerminals
                        .FirstOrDefaultAsync(
                            terminal =>
                                terminal.TerminalNo ==
                                    terminalNo);

                int currentId =
                    entity?.Id ?? 0;

                bool terminalNumberConflict =
                    await context.RegisteredTerminals
                        .AnyAsync(
                            terminal =>
                                terminal.TerminalNo ==
                                    terminalNo &&
                                terminal.Id !=
                                    currentId);

                if (terminalNumberConflict)
                {
                    throw new InvalidOperationException(
                        $"Terminal number '{terminalNo}' " +
                        "is already registered to another machine.");
                }

                bool machineCodeConflict =
                    await context.RegisteredTerminals
                        .AnyAsync(
                            terminal =>
                                terminal.MachineCode ==
                                    machineCode &&
                                terminal.Id !=
                                    currentId);

                if (machineCodeConflict)
                {
                    throw new InvalidOperationException(
                        "This machine code is already registered " +
                        "to another terminal.");
                }

                DateTime now = DateTime.Now;

                if (entity == null)
                {
                    entity =
                        new RegisteredTerminal
                        {
                            CreatedAt = now
                        };

                    await context.RegisteredTerminals
                        .AddAsync(entity);
                }

                entity.TerminalNo =
                    terminalNo;

                entity.TerminalName =
                    NormalizeTerminalName(
                        settings.TerminalName,
                        terminalNo);

                entity.MachineName =
                    machineName;

                entity.MachineCode =
                    machineCode;

                entity.Location =
                    NormalizeText(
                        settings.Location);

                if (string.IsNullOrWhiteSpace(
                        entity.Location))
                {
                    entity.Location =
                        "Main Store";
                }

                entity.IsCashierTerminal = true;
                entity.IsBackOfficeAllowed = true;
                entity.IsActive =
                    settings.IsActive;

                entity.UpdatedAt = now;

                entity.UpdatedBy =
                    NormalizeText(updatedBy);

                entity.Remarks =
                    AppendRemark(
                        entity.Remarks,
                        "Current machine registration refreshed.");

                TerminalSettings? settingsEntity =
                    await context.TerminalSettings
                        .FirstOrDefaultAsync(
                            terminal =>
                                terminal.Id ==
                                    settings.Id);

                if (settingsEntity != null)
                {
                    settingsEntity.MachineName =
                        machineName;

                    settingsEntity.TerminalName =
                        entity.TerminalName;

                    settingsEntity.UpdatedAt = now;

                    settingsEntity.UpdatedBy =
                        NormalizeText(updatedBy);
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return await context
                    .RegisteredTerminals
                    .AsNoTracking()
                    .FirstAsync(
                        terminal =>
                            terminal.Id ==
                                entity.Id);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<RegisteredTerminal>
            AssignCurrentMachineAsync(
                string terminalNo,
                string terminalName,
                string updatedBy)
        {
            string safeTerminalNo = NormalizeText(terminalNo);
            string safeTerminalName = NormalizeTerminalName(
                terminalName,
                safeTerminalNo);
            string machineCode = NormalizeMachineCode(
                _machineFingerprintService.GetMachineCode());
            string machineName = NormalizeText(
                _machineFingerprintService.GetMachineName());

            if (string.IsNullOrWhiteSpace(safeTerminalNo))
            {
                throw new InvalidOperationException(
                    "Terminal number is required.");
            }

            if (safeTerminalNo.Length > 20)
            {
                throw new InvalidOperationException(
                    "Terminal number cannot exceed 20 characters.");
            }

            if (string.IsNullOrWhiteSpace(machineCode) ||
                string.IsNullOrWhiteSpace(machineName))
            {
                throw new InvalidOperationException(
                    "The current computer identity is unavailable.");
            }

            await using AppDbContext context =
                await _contextFactory.CreateDbContextAsync();

            await using var transaction =
                await context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            try
            {
                RegisteredTerminal? target =
                    await context.RegisteredTerminals
                        .FirstOrDefaultAsync(item =>
                            item.TerminalNo == safeTerminalNo);

                RegisteredTerminal? currentMachineTerminal =
                    await context.RegisteredTerminals
                        .FirstOrDefaultAsync(item =>
                            item.MachineCode == machineCode);

                if (currentMachineTerminal != null &&
                    (target == null ||
                     currentMachineTerminal.Id != target.Id))
                {
                    throw new InvalidOperationException(
                        $"This computer is already assigned to terminal " +
                        $"'{currentMachineTerminal.TerminalNo}'. Release that " +
                        "assignment before selecting another terminal number.");
                }

                if (target != null &&
                    !string.IsNullOrWhiteSpace(target.MachineCode) &&
                    !string.Equals(
                        NormalizeMachineCode(target.MachineCode),
                        machineCode,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Terminal '{safeTerminalNo}' is assigned to " +
                        $"'{target.MachineName}'. Use RELEASE COMPUTER from " +
                        "Terminal Management before assigning it here.");
                }

                DateTime now = DateTime.Now;
                string safeUpdatedBy = NormalizeText(updatedBy);

                if (target == null)
                {
                    target = new RegisteredTerminal
                    {
                        TerminalNo = safeTerminalNo,
                        CreatedAt = now
                    };
                    await context.RegisteredTerminals.AddAsync(target);
                }

                target.TerminalName = safeTerminalName;
                target.MachineName = machineName;
                target.MachineCode = machineCode;
                target.IsCashierTerminal = true;
                target.IsBackOfficeAllowed = true;
                target.IsActive = true;
                target.UpdatedAt = now;
                target.UpdatedBy = safeUpdatedBy;
                target.Remarks = AppendRemark(
                    target.Remarks,
                    "Cashier enabled on the current computer.");

                TerminalSettings? settings =
                    await context.TerminalSettings
                        .FirstOrDefaultAsync(item =>
                            item.MachineName == machineName);

                settings ??=
                    await context.TerminalSettings
                        .FirstOrDefaultAsync(item =>
                            item.TerminalNo == safeTerminalNo);

                if (settings != null &&
                    !string.IsNullOrWhiteSpace(settings.MachineName) &&
                    !string.Equals(
                        settings.MachineName,
                        machineName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Terminal settings for '{safeTerminalNo}' are still " +
                        $"assigned to '{settings.MachineName}'. Release the " +
                        "old computer before continuing.");
                }

                if (settings == null)
                {
                    settings = TerminalSettingsRepository
                        .CreateDefaultSettings(
                            safeTerminalNo,
                            machineName);
                    settings.CreatedAt = now;
                    await context.TerminalSettings.AddAsync(settings);
                }

                settings.TerminalNo = safeTerminalNo;
                settings.TerminalName = safeTerminalName;
                settings.MachineName = machineName;
                settings.IsActive = true;
                settings.UpdatedAt = now;
                settings.UpdatedBy = safeUpdatedBy;

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return await context.RegisteredTerminals
                    .AsNoTracking()
                    .FirstAsync(item => item.Id == target.Id);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Compatibility overload retained for older callers.
        public Task<RegisteredTerminal>
            RegisterOrUpdateCurrentMachineAsync(
                bool isCashierTerminal,
                string updatedBy)
        {
            return RegisterOrUpdateCurrentMachineAsync(
                updatedBy);
        }

        public async Task RenameAsync(
            int terminalId,
            string terminalName,
            string updatedBy)
        {
            string safeTerminalName =
                NormalizeText(
                    terminalName);

            if (string.IsNullOrWhiteSpace(
                    safeTerminalName))
            {
                throw new InvalidOperationException(
                    "Terminal name is required.");
            }

            if (safeTerminalName.Length > 120)
            {
                throw new InvalidOperationException(
                    "Terminal name cannot exceed 120 characters.");
            }

            await using AppDbContext context =
                await _contextFactory
                    .CreateDbContextAsync();

            await using var transaction =
                await context.Database
                    .BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                RegisteredTerminal? terminal =
                    await context.RegisteredTerminals
                        .FirstOrDefaultAsync(
                            item =>
                                item.Id ==
                                    terminalId);

                if (terminal == null)
                {
                    throw new InvalidOperationException(
                        "The selected terminal no longer exists.");
                }

                terminal.TerminalName =
                    safeTerminalName;

                terminal.UpdatedAt =
                    DateTime.Now;

                terminal.UpdatedBy =
                    NormalizeText(updatedBy);

                List<TerminalSettings>
                    linkedSettings =
                        await context.TerminalSettings
                            .Where(
                                settings =>
                                    settings.TerminalNo ==
                                        terminal.TerminalNo)
                            .ToListAsync();

                foreach (TerminalSettings settings
                         in linkedSettings)
                {
                    settings.TerminalName =
                        safeTerminalName;

                    settings.UpdatedAt =
                        terminal.UpdatedAt;

                    settings.UpdatedBy =
                        terminal.UpdatedBy;
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task ReleaseMachineAssignmentAsync(
            int terminalId,
            string updatedBy)
        {
            await using AppDbContext context =
                await _contextFactory
                    .CreateDbContextAsync();

            await using var transaction =
                await context.Database
                    .BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

            try
            {
                RegisteredTerminal? terminal =
                    await context.RegisteredTerminals
                        .FirstOrDefaultAsync(
                            item =>
                                item.Id ==
                                    terminalId);

                if (terminal == null)
                {
                    throw new InvalidOperationException(
                        "The selected terminal no longer exists.");
                }

                string terminalNo =
                    NormalizeText(terminal.TerminalNo);

                bool hasOpenShift =
                    await context.ShiftSessions
                        .AnyAsync(
                            shift =>
                                shift.TerminalNo == terminalNo &&
                                (shift.Status == "Open" ||
                                 shift.Status == "Closing"));

                if (hasOpenShift)
                {
                    throw new InvalidOperationException(
                        $"Terminal '{terminalNo}' has an open or closing shift. " +
                        "Close the shift before releasing the machine assignment.");
                }

                bool hasActiveCart =
                    await context.CashierCartSessions
                        .AnyAsync(
                            cart =>
                                cart.TerminalNo == terminalNo &&
                                (cart.Status == CashierCartStatusCodes.Active ||
                                 cart.Status == CashierCartStatusCodes.Held));

                if (hasActiveCart)
                {
                    throw new InvalidOperationException(
                        $"Terminal '{terminalNo}' has an active or held cart. " +
                        "Complete, cancel, or recall the cart before releasing " +
                        "the machine assignment.");
                }

                if (string.IsNullOrWhiteSpace(terminal.MachineName) &&
                    string.IsNullOrWhiteSpace(terminal.MachineCode))
                {
                    return;
                }

                string previousMachine =
                    string.IsNullOrWhiteSpace(terminal.MachineName)
                        ? "unknown computer"
                        : terminal.MachineName;

                DateTime now = DateTime.Now;
                string safeUpdatedBy = NormalizeText(updatedBy);

                terminal.MachineName = string.Empty;
                terminal.MachineCode = string.Empty;
                terminal.LicenseId = string.Empty;
                terminal.LicenseExpiryDate = null;
                terminal.LicenseLastCheckedAt = null;
                terminal.UpdatedAt = now;
                terminal.UpdatedBy = safeUpdatedBy;
                terminal.Remarks = AppendRemark(
                    terminal.Remarks,
                    $"Machine assignment released from {previousMachine}.");

                List<TerminalSettings> linkedSettings =
                    await context.TerminalSettings
                        .Where(
                            settings =>
                                settings.TerminalNo == terminalNo)
                        .ToListAsync();

                foreach (TerminalSettings settings in linkedSettings)
                {
                    settings.MachineName = string.Empty;
                    settings.UpdatedAt = now;
                    settings.UpdatedBy = safeUpdatedBy;
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
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
            await using AppDbContext context =
                await _contextFactory
                    .CreateDbContextAsync();

            await using var transaction =
                await context.Database
                    .BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                RegisteredTerminal? terminal =
                    await context.RegisteredTerminals
                        .FirstOrDefaultAsync(
                            item =>
                                item.Id ==
                                    terminalId);

                if (terminal == null)
                {
                    throw new InvalidOperationException(
                        "The selected terminal no longer exists.");
                }

                terminal.IsActive =
                    isActive;

                terminal.UpdatedAt =
                    DateTime.Now;

                terminal.UpdatedBy =
                    NormalizeText(updatedBy);

                terminal.Remarks =
                    AppendRemark(
                        terminal.Remarks,
                        isActive
                            ? "Terminal activated."
                            : "Terminal disabled.");

                List<TerminalSettings>
                    linkedSettings =
                        await context.TerminalSettings
                            .Where(
                                settings =>
                                    settings.TerminalNo ==
                                        terminal.TerminalNo)
                            .ToListAsync();

                foreach (TerminalSettings settings
                         in linkedSettings)
                {
                    settings.IsActive =
                        isActive;

                    settings.UpdatedAt =
                        terminal.UpdatedAt;

                    settings.UpdatedBy =
                        terminal.UpdatedBy;
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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
            string safeTerminalNo =
                NormalizeText(terminalNo);

            string safeMachineCode =
                NormalizeMachineCode(
                    machineCode);

            if (string.IsNullOrWhiteSpace(
                    safeTerminalNo) &&
                string.IsNullOrWhiteSpace(
                    safeMachineCode))
            {
                return;
            }

            await using AppDbContext context =
                await _contextFactory
                    .CreateDbContextAsync();

            RegisteredTerminal? terminal =
                await context.RegisteredTerminals
                    .FirstOrDefaultAsync(
                        item =>
                            (!string.IsNullOrWhiteSpace(
                                safeMachineCode) &&
                             item.MachineCode ==
                                safeMachineCode) ||
                            (!string.IsNullOrWhiteSpace(
                                safeTerminalNo) &&
                             item.TerminalNo ==
                                safeTerminalNo));

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

        private static RegisteredTerminalSummary
            BuildSummary(
                RegisteredTerminal terminal,
                List<InstalledLicense> licenses,
                string currentMachineCode)
        {
            InstalledLicense? license = null;

            if (terminal.IsCashierTerminal &&
                !string.IsNullOrWhiteSpace(terminal.MachineCode))
            {
                license =
                    licenses
                        .Where(
                            item =>
                                item.TerminalNo ==
                                    terminal.TerminalNo &&
                                (string.IsNullOrWhiteSpace(
                                    terminal.MachineCode) ||
                                 item.MachineCode ==
                                    terminal.MachineCode))
                        .OrderByDescending(
                            item =>
                                item.ExpiresOn)
                        .ThenByDescending(
                            item =>
                                item.ImportedAt)
                        .FirstOrDefault();
            }

            LicenseStatus status =
                terminal.IsCashierTerminal
                    ? LicenseRepository
                        .CalculateCurrentStatus(
                            license)
                    : LicenseStatus.Active;

            return new RegisteredTerminalSummary
            {
                Id = terminal.Id,

                TerminalNo =
                    terminal.TerminalNo,

                TerminalName =
                    terminal.TerminalName,

                MachineName =
                    terminal.MachineName,

                MachineCode =
                    terminal.MachineCode,

                IsCashierTerminal =
                    terminal.IsCashierTerminal,

                IsActive =
                    terminal.IsActive,

                IsCurrentMachine =
                    !string.IsNullOrWhiteSpace(
                        currentMachineCode) &&
                    string.Equals(
                        NormalizeMachineCode(
                            terminal.MachineCode),
                        currentMachineCode,
                        StringComparison.OrdinalIgnoreCase),

                LicenseId =
                    terminal.IsCashierTerminal
                        ? license?.LicenseId ??
                          terminal.LicenseId
                        : "Not Required",

                LicenseStatus =
                    status,

                LicenseExpiryDate =
                    terminal.IsCashierTerminal
                        ? license?.ExpiresOn ??
                          terminal.LicenseExpiryDate
                        : null,

                CreatedAt =
                    terminal.CreatedAt,

                UpdatedAt =
                    terminal.UpdatedAt,

                LastLoginAt =
                    terminal.LastLoginAt,

                LastSaleAt =
                    terminal.LastSaleAt,

                UpdatedBy =
                    terminal.UpdatedBy
            };
        }

        private static string NormalizeTerminalName(
            string? terminalName,
            string terminalNo)
        {
            string safeName =
                NormalizeText(
                    terminalName);

            return string.IsNullOrWhiteSpace(
                safeName)
                ? $"Terminal {terminalNo}"
                : safeName;
        }

        private static string AppendRemark(
            string? existing,
            string message)
        {
            string safeExisting =
                NormalizeText(existing);

            string safeMessage =
                NormalizeText(message);

            if (string.IsNullOrWhiteSpace(
                    safeExisting))
            {
                return safeMessage;
            }

            if (string.IsNullOrWhiteSpace(
                    safeMessage))
            {
                return safeExisting;
            }

            string combined =
                $"{safeExisting} | {safeMessage}";

            return combined.Length <= 500
                ? combined
                : combined[^500..];
        }

        private static string NormalizeText(
            string? value)
        {
            return (value ?? string.Empty)
                .Trim();
        }

        private static string NormalizeMachineCode(
            string? value)
        {
            return NormalizeText(value)
                .ToUpperInvariant();
        }
    }
}
