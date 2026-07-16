using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public class TerminalSettingsRepository
    {
        private readonly
            IDbContextFactory<AppDbContext>
            _contextFactory;

        public TerminalSettingsRepository(
            IDbContextFactory<AppDbContext>
                contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<TerminalSettings?>
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

            return await context.TerminalSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    terminal =>
                        terminal.TerminalNo ==
                        safeTerminalNo);
        }

        public async Task<TerminalSettings?>
            GetByMachineNameAsync(
                string machineName)
        {
            string safeMachineName =
                NormalizeText(machineName);

            if (string.IsNullOrWhiteSpace(
                    safeMachineName))
            {
                return null;
            }

            await using AppDbContext context =
                await _contextFactory
                    .CreateDbContextAsync();

            return await context.TerminalSettings
                .AsNoTracking()
                .OrderByDescending(
                    terminal => terminal.Id)
                .FirstOrDefaultAsync(
                    terminal =>
                        terminal.MachineName ==
                            safeMachineName);
        }

        public async Task<TerminalSettings>
            GetOrCreateDefaultAsync(
                string terminalNo)
        {
            string safeTerminalNo =
                NormalizeText(terminalNo);

            if (string.IsNullOrWhiteSpace(
                    safeTerminalNo))
            {
                safeTerminalNo =
                    TerminalConfigurationDefaults
                        .InitialTerminalNumber;
            }

            await using AppDbContext context =
                await _contextFactory
                    .CreateDbContextAsync();

            TerminalSettings? existing =
                await context.TerminalSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        terminal =>
                            terminal.TerminalNo ==
                            safeTerminalNo);

            if (existing != null)
                return existing;

            TerminalSettings settings =
                CreateDefaultSettings(
                    safeTerminalNo,
                    Environment.MachineName);

            await context.TerminalSettings
                .AddAsync(settings);

            await context.SaveChangesAsync();

            return settings;
        }

        public async Task<TerminalSettings>
            GetOrCreateForCurrentMachineAsync(
                string fallbackTerminalNo =
                    TerminalConfigurationDefaults
                        .InitialTerminalNumber)
        {
            string machineName =
                Environment.MachineName.Trim();

            await using AppDbContext context =
                await _contextFactory
                    .CreateDbContextAsync();

            TerminalSettings? existingForMachine =
                await context.TerminalSettings
                    .AsNoTracking()
                    .OrderByDescending(
                        terminal => terminal.Id)
                    .FirstOrDefaultAsync(
                        terminal =>
                            terminal.MachineName ==
                                machineName);

            if (existingForMachine != null)
                return existingForMachine;

            string safeTerminalNo =
                NormalizeText(
                    fallbackTerminalNo);

            if (string.IsNullOrWhiteSpace(
                    safeTerminalNo))
            {
                safeTerminalNo =
                    TerminalConfigurationDefaults
                        .InitialTerminalNumber;
            }

            TerminalSettings? existingByTerminal =
                await context.TerminalSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        terminal =>
                            terminal.TerminalNo ==
                            safeTerminalNo);

            if (existingByTerminal != null)
                return existingByTerminal;

            TerminalSettings settings =
                CreateDefaultSettings(
                    safeTerminalNo,
                    machineName);

            await context.TerminalSettings
                .AddAsync(settings);

            await context.SaveChangesAsync();

            return settings;
        }

        public async Task<TerminalSettings>
            SaveAsync(
                TerminalSettings settings,
                string updatedBy)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(
                    nameof(settings));
            }

            Normalize(settings);
            Validate(settings);

            await using AppDbContext context =
                await _contextFactory
                    .CreateDbContextAsync();

            await using var transaction =
                await context.Database
                    .BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                TerminalSettings? entity = null;

                if (settings.Id > 0)
                {
                    entity =
                        await context.TerminalSettings
                            .FirstOrDefaultAsync(
                                terminal =>
                                    terminal.Id ==
                                    settings.Id);
                }

                if (entity == null)
                {
                    entity =
                        await context.TerminalSettings
                            .FirstOrDefaultAsync(
                                terminal =>
                                    terminal.TerminalNo ==
                                    settings.TerminalNo);
                }

                int currentId =
                    entity?.Id ?? 0;

                bool duplicateTerminalNo =
                    await context.TerminalSettings
                        .AnyAsync(
                            terminal =>
                                terminal.TerminalNo ==
                                    settings.TerminalNo &&
                                terminal.Id != currentId);

                if (duplicateTerminalNo)
                {
                    throw new InvalidOperationException(
                        $"Terminal number '{settings.TerminalNo}' " +
                        "is already used by another terminal.");
                }

                DateTime now = DateTime.Now;

                if (entity == null)
                {
                    entity =
                        new TerminalSettings
                        {
                            TerminalNo =
                                settings.TerminalNo,

                            MachineName =
                                settings.MachineName,

                            IsActive = true,
                            CreatedAt = now
                        };

                    await context.TerminalSettings
                        .AddAsync(entity);
                }

                // Terminal identity and activation are managed
                // by Terminal Management. This page may rename
                // the current terminal, but it must not reassign
                // its number, machine, or active state.
                string persistedTerminalNo =
                    entity.TerminalNo;

                string persistedMachineName =
                    entity.MachineName;

                bool persistedIsActive =
                    entity.IsActive;

                CopySettings(
                    settings,
                    entity);

                entity.TerminalNo =
                    string.IsNullOrWhiteSpace(
                        persistedTerminalNo)
                        ? settings.TerminalNo
                        : persistedTerminalNo;

                entity.MachineName =
                    string.IsNullOrWhiteSpace(
                        persistedMachineName)
                        ? settings.MachineName
                        : persistedMachineName;

                entity.IsActive =
                    persistedIsActive;

                entity.UpdatedAt = now;

                entity.UpdatedBy =
                    NormalizeText(updatedBy);

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return await context.TerminalSettings
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

        public static TerminalSettings
            CreateDefaultSettings(
                string terminalNo =
                    TerminalConfigurationDefaults
                        .InitialTerminalNumber,
                string? machineName = null)
        {
            string safeTerminalNo =
                NormalizeText(terminalNo);

            if (string.IsNullOrWhiteSpace(
                    safeTerminalNo))
            {
                safeTerminalNo =
                    TerminalConfigurationDefaults
                        .InitialTerminalNumber;
            }

            string safeMachineName =
                NormalizeText(machineName);

            if (string.IsNullOrWhiteSpace(
                    safeMachineName))
            {
                safeMachineName =
                    Environment.MachineName;
            }

            return new TerminalSettings
            {
                TerminalNo =
                    safeTerminalNo,

                TerminalName =
                    TerminalConfigurationDefaults
                        .BuildTerminalName(
                            safeTerminalNo),

                MachineName =
                    safeMachineName,

                Location =
                    TerminalConfigurationDefaults
                        .DefaultLocation,

                PrinterMode =
                    TerminalConfigurationDefaults
                        .PrinterMode,

                ReceiptPrinterName =
                    string.Empty,

                ReceiptPaperWidth =
                    TerminalConfigurationDefaults
                        .ReceiptPaperWidth,
                AutoPrintReceipt = false,
                ReceiptCopies =
                    TerminalConfigurationDefaults
                        .ReceiptCopies,

                EnableCashDrawer = false,

                DrawerKickCode =
                    TerminalConfigurationDefaults
                        .DrawerKickCode,

                OpenDrawerAfterCashSale =
                    false,

                ScannerSuffixAction =
                    "Enter",

                EnableScale = false,
                ScaleComPort = "COM1",
                ScaleBaudRate = 9600,

                EnablePoleDisplay = false,
                PoleDisplayComPort = "COM2",

                PoleWelcomeMessage =
                    "WELCOME",

                EnableEftpos = false,

                EftposProvider =
                    string.Empty,

                EftposPortOrIp =
                    string.Empty,

                AutoLockTimeoutMinutes =
                    TerminalConfigurationDefaults
                        .AutoLockTimeoutMinutes,

                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = null,

                UpdatedBy =
                    string.Empty
            };
        }

        private static void CopySettings(
            TerminalSettings source,
            TerminalSettings target)
        {
            target.TerminalName =
                source.TerminalName;

            target.Location =
                source.Location;

            target.PrinterMode =
                "WindowsSpooler";

            target.ReceiptPrinterName =
                source.ReceiptPrinterName;

            target.ReceiptPaperWidth =
                source.ReceiptPaperWidth;

            target.AutoPrintReceipt =
                source.AutoPrintReceipt;

            target.ReceiptCopies =
                source.ReceiptCopies;

            target.EnableCashDrawer =
                source.EnableCashDrawer;

            target.DrawerKickCode =
                source.DrawerKickCode;

            target.OpenDrawerAfterCashSale =
                source.EnableCashDrawer &&
                source.OpenDrawerAfterCashSale;

            // The following legacy fields stay in the
            // database for compatibility. They are not
            // exposed on the final simple page.
            target.ScannerSuffixAction =
                source.ScannerSuffixAction;

            target.EnableScale =
                source.EnableScale;

            target.ScaleComPort =
                source.ScaleComPort;

            target.ScaleBaudRate =
                source.ScaleBaudRate;

            target.EnablePoleDisplay =
                source.EnablePoleDisplay;

            target.PoleDisplayComPort =
                source.PoleDisplayComPort;

            target.PoleWelcomeMessage =
                source.PoleWelcomeMessage;

            target.EnableEftpos =
                source.EnableEftpos;

            target.EftposProvider =
                source.EftposProvider;

            target.EftposPortOrIp =
                source.EftposPortOrIp;

            target.AutoLockTimeoutMinutes =
                source.AutoLockTimeoutMinutes;
        }

        private static void Normalize(
            TerminalSettings settings)
        {
            settings.TerminalNo =
                NormalizeText(
                    settings.TerminalNo);

            if (string.IsNullOrWhiteSpace(
                    settings.TerminalNo))
            {
                settings.TerminalNo =
                    TerminalConfigurationDefaults
                        .InitialTerminalNumber;
            }

            settings.TerminalName =
                NormalizeText(
                    settings.TerminalName);

            if (string.IsNullOrWhiteSpace(
                    settings.TerminalName))
            {
                settings.TerminalName =
                    TerminalConfigurationDefaults
                        .BuildTerminalName(
                            settings.TerminalNo);
            }

            settings.MachineName =
                NormalizeText(
                    settings.MachineName);

            if (string.IsNullOrWhiteSpace(
                    settings.MachineName))
            {
                settings.MachineName =
                    Environment.MachineName;
            }

            settings.Location =
                NormalizeText(
                    settings.Location);

            if (string.IsNullOrWhiteSpace(
                    settings.Location))
            {
                settings.Location =
                    TerminalConfigurationDefaults
                        .DefaultLocation;
            }

            settings.PrinterMode =
                TerminalConfigurationDefaults
                    .PrinterMode;

            settings.ReceiptPrinterName =
                NormalizeText(
                    settings.ReceiptPrinterName);

            if (settings.ReceiptPaperWidth != 58 &&
                settings.ReceiptPaperWidth != 80)
            {
                settings.ReceiptPaperWidth =
                    TerminalConfigurationDefaults
                        .ReceiptPaperWidth;
            }

            if (settings.ReceiptCopies <= 0)
            {
                settings.ReceiptCopies =
                    TerminalConfigurationDefaults
                        .ReceiptCopies;
            }

            settings.DrawerKickCode =
                NormalizeText(
                    settings.DrawerKickCode);

            if (string.IsNullOrWhiteSpace(
                    settings.DrawerKickCode))
            {
                settings.DrawerKickCode =
                    TerminalConfigurationDefaults
                        .DrawerKickCode;
            }

            if (!settings.EnableCashDrawer)
            {
                settings.OpenDrawerAfterCashSale =
                    false;
            }

            settings.ScannerSuffixAction =
                NormalizeText(
                    settings.ScannerSuffixAction);

            if (string.IsNullOrWhiteSpace(
                    settings.ScannerSuffixAction))
            {
                settings.ScannerSuffixAction =
                    "Enter";
            }

            settings.ScaleComPort =
                NormalizeText(
                    settings.ScaleComPort)
                    .ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(
                    settings.ScaleComPort))
            {
                settings.ScaleComPort =
                    "COM1";
            }

            if (settings.ScaleBaudRate <= 0)
            {
                settings.ScaleBaudRate =
                    9600;
            }

            settings.PoleDisplayComPort =
                NormalizeText(
                    settings.PoleDisplayComPort)
                    .ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(
                    settings.PoleDisplayComPort))
            {
                settings.PoleDisplayComPort =
                    "COM2";
            }

            settings.PoleWelcomeMessage =
                NormalizeText(
                    settings.PoleWelcomeMessage);

            if (string.IsNullOrWhiteSpace(
                    settings.PoleWelcomeMessage))
            {
                settings.PoleWelcomeMessage =
                    "WELCOME";
            }

            settings.EftposProvider =
                NormalizeText(
                    settings.EftposProvider);

            settings.EftposPortOrIp =
                NormalizeText(
                    settings.EftposPortOrIp);
        }

        private static void Validate(
            TerminalSettings settings)
        {
            if (string.IsNullOrWhiteSpace(
                    settings.TerminalNo))
            {
                throw new InvalidOperationException(
                    "Terminal number is required.");
            }

            if (string.IsNullOrWhiteSpace(
                    settings.TerminalName))
            {
                throw new InvalidOperationException(
                    "Terminal name is required.");
            }

            if (string.IsNullOrWhiteSpace(
                    settings.MachineName))
            {
                throw new InvalidOperationException(
                    "Machine name is required.");
            }

            if (settings.AutoLockTimeoutMinutes <
                    0 ||
                settings.AutoLockTimeoutMinutes >
                    120)
            {
                throw new InvalidOperationException(
                    "Auto-lock timeout must be " +
                    "between 0 and 120 minutes. " +
                    "Use 0 to disable automatic locking.");
            }

            if (settings.ReceiptPaperWidth != 58 &&
                settings.ReceiptPaperWidth != 80)
            {
                throw new InvalidOperationException(
                    "Receipt paper width must be " +
                    "58 mm or 80 mm.");
            }

            if (settings.ReceiptCopies < 1 ||
                settings.ReceiptCopies > 3)
            {
                throw new InvalidOperationException(
                    "Receipt copies must be " +
                    "between 1 and 3.");
            }

            bool printerIsRequired =
                settings.AutoPrintReceipt ||
                settings.EnableCashDrawer;

            if (printerIsRequired &&
                string.IsNullOrWhiteSpace(
                    settings.ReceiptPrinterName))
            {
                throw new InvalidOperationException(
                    "Select a Windows receipt printer " +
                    "when automatic printing or the " +
                    "cash drawer is enabled.");
            }
        }

        private static string NormalizeText(
            string? value)
        {
            return (value ?? string.Empty)
                .Trim();
        }
    }
}
