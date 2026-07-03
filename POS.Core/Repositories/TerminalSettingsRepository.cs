using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public class TerminalSettingsRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public TerminalSettingsRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<TerminalSettings?> GetByTerminalNoAsync(string terminalNo)
        {
            string safeTerminalNo = NormalizeText(terminalNo);

            if (string.IsNullOrWhiteSpace(safeTerminalNo))
                return null;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.TerminalSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TerminalNo == safeTerminalNo);
        }

        public async Task<TerminalSettings?> GetByMachineNameAsync(string machineName)
        {
            string safeMachineName = NormalizeText(machineName);

            if (string.IsNullOrWhiteSpace(safeMachineName))
                return null;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.TerminalSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.MachineName == safeMachineName && t.IsActive);
        }

        public async Task<TerminalSettings> GetOrCreateDefaultAsync(string terminalNo)
        {
            string safeTerminalNo = NormalizeText(terminalNo);

            if (string.IsNullOrWhiteSpace(safeTerminalNo))
                safeTerminalNo = "01";

            await using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.TerminalSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TerminalNo == safeTerminalNo);

            if (existing != null)
                return existing;

            var settings = CreateDefaultSettings(safeTerminalNo, Environment.MachineName);

            await context.TerminalSettings.AddAsync(settings);
            await context.SaveChangesAsync();

            return settings;
        }

        public async Task<TerminalSettings> GetOrCreateForCurrentMachineAsync(string fallbackTerminalNo = "01")
        {
            string machineName = Environment.MachineName;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var existingForMachine = await context.TerminalSettings
                .AsNoTracking()
                .OrderByDescending(t => t.Id)
                .FirstOrDefaultAsync(t => t.MachineName == machineName && t.IsActive);

            if (existingForMachine != null)
                return existingForMachine;

            string safeTerminalNo = NormalizeText(fallbackTerminalNo);

            if (string.IsNullOrWhiteSpace(safeTerminalNo))
                safeTerminalNo = "01";

            var existingByTerminal = await context.TerminalSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TerminalNo == safeTerminalNo);

            if (existingByTerminal != null)
                return existingByTerminal;

            var settings = CreateDefaultSettings(safeTerminalNo, machineName);

            await context.TerminalSettings.AddAsync(settings);
            await context.SaveChangesAsync();

            return settings;
        }

        public async Task<TerminalSettings> SaveAsync(TerminalSettings settings, string updatedBy)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            Normalize(settings);
            Validate(settings);

            await using var context = await _contextFactory.CreateDbContextAsync();

            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                DateTime now = DateTime.Now;
                string safeUpdatedBy = NormalizeText(updatedBy);

                TerminalSettings? entity = null;

                if (settings.Id > 0)
                {
                    entity = await context.TerminalSettings
                        .FirstOrDefaultAsync(t => t.Id == settings.Id);
                }

                if (entity == null)
                {
                    entity = await context.TerminalSettings
                        .FirstOrDefaultAsync(t => t.TerminalNo == settings.TerminalNo);
                }

                int currentEntityId = entity?.Id ?? 0;

                bool duplicateTerminalNoExists = await context.TerminalSettings.AnyAsync(t =>
                    t.TerminalNo == settings.TerminalNo &&
                    t.Id != currentEntityId);

                if (duplicateTerminalNoExists)
                    throw new InvalidOperationException($"Terminal number '{settings.TerminalNo}' is already used by another terminal.");

                if (entity == null)
                {
                    entity = new TerminalSettings
                    {
                        CreatedAt = now,
                        IsActive = true
                    };

                    await context.TerminalSettings.AddAsync(entity);
                }

                CopyToEntity(settings, entity);

                entity.IsActive = settings.IsActive;
                entity.UpdatedAt = now;
                entity.UpdatedBy = safeUpdatedBy;

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return await context.TerminalSettings
                    .AsNoTracking()
                    .FirstAsync(t => t.Id == entity.Id);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public static TerminalSettings CreateDefaultSettings(string terminalNo = "01", string? machineName = null)
        {
            string safeTerminalNo = NormalizeText(terminalNo);

            if (string.IsNullOrWhiteSpace(safeTerminalNo))
                safeTerminalNo = "01";

            string safeMachineName = NormalizeText(machineName);

            if (string.IsNullOrWhiteSpace(safeMachineName))
                safeMachineName = Environment.MachineName;

            return new TerminalSettings
            {
                TerminalNo = safeTerminalNo,
                TerminalName = $"Cashier Terminal {safeTerminalNo}",
                MachineName = safeMachineName,
                Location = "Main Store",

                PrinterMode = "WindowsSpooler",
                ReceiptPrinterName = "POS-80",
                ReceiptPaperWidth = 80,
                AutoPrintReceipt = true,
                ReceiptCopies = 1,

                EnableCashDrawer = true,
                DrawerKickCode = "27,112,0,25,250",
                OpenDrawerAfterCashSale = true,

                ScannerSuffixAction = "Enter",

                EnableScale = false,
                ScaleComPort = "COM1",
                ScaleBaudRate = 9600,

                EnablePoleDisplay = false,
                PoleDisplayComPort = "COM2",
                PoleWelcomeMessage = "WELCOME",

                EnableEftpos = false,
                EftposProvider = string.Empty,
                EftposPortOrIp = string.Empty,

                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = null,
                UpdatedBy = string.Empty
            };
        }

        private static void CopyToEntity(TerminalSettings source, TerminalSettings target)
        {
            target.TerminalNo = source.TerminalNo;
            target.TerminalName = source.TerminalName;
            target.MachineName = source.MachineName;
            target.Location = source.Location;

            target.PrinterMode = source.PrinterMode;
            target.ReceiptPrinterName = source.ReceiptPrinterName;
            target.ReceiptPaperWidth = source.ReceiptPaperWidth;
            target.AutoPrintReceipt = source.AutoPrintReceipt;
            target.ReceiptCopies = source.ReceiptCopies;

            target.EnableCashDrawer = source.EnableCashDrawer;
            target.DrawerKickCode = source.DrawerKickCode;
            target.OpenDrawerAfterCashSale = source.OpenDrawerAfterCashSale;

            target.ScannerSuffixAction = source.ScannerSuffixAction;

            target.EnableScale = source.EnableScale;
            target.ScaleComPort = source.ScaleComPort;
            target.ScaleBaudRate = source.ScaleBaudRate;

            target.EnablePoleDisplay = source.EnablePoleDisplay;
            target.PoleDisplayComPort = source.PoleDisplayComPort;
            target.PoleWelcomeMessage = source.PoleWelcomeMessage;

            target.EnableEftpos = source.EnableEftpos;
            target.EftposProvider = source.EftposProvider;
            target.EftposPortOrIp = source.EftposPortOrIp;
        }

        private static void Normalize(TerminalSettings settings)
        {
            settings.TerminalNo = NormalizeText(settings.TerminalNo);

            if (string.IsNullOrWhiteSpace(settings.TerminalNo))
                settings.TerminalNo = "01";

            settings.TerminalName = NormalizeText(settings.TerminalName);

            if (string.IsNullOrWhiteSpace(settings.TerminalName))
                settings.TerminalName = $"Cashier Terminal {settings.TerminalNo}";

            settings.MachineName = NormalizeText(settings.MachineName);

            if (string.IsNullOrWhiteSpace(settings.MachineName))
                settings.MachineName = Environment.MachineName;

            settings.Location = NormalizeText(settings.Location);

            if (string.IsNullOrWhiteSpace(settings.Location))
                settings.Location = "Main Store";

            settings.PrinterMode = NormalizeText(settings.PrinterMode);

            if (string.IsNullOrWhiteSpace(settings.PrinterMode))
                settings.PrinterMode = "WindowsSpooler";

            settings.ReceiptPrinterName = NormalizeText(settings.ReceiptPrinterName);

            if (string.IsNullOrWhiteSpace(settings.ReceiptPrinterName))
                settings.ReceiptPrinterName = "POS-80";

            if (settings.ReceiptPaperWidth <= 0)
                settings.ReceiptPaperWidth = 80;

            if (settings.ReceiptCopies <= 0)
                settings.ReceiptCopies = 1;

            settings.DrawerKickCode = NormalizeText(settings.DrawerKickCode);

            if (string.IsNullOrWhiteSpace(settings.DrawerKickCode))
                settings.DrawerKickCode = "27,112,0,25,250";

            settings.ScannerSuffixAction = NormalizeText(settings.ScannerSuffixAction);

            if (string.IsNullOrWhiteSpace(settings.ScannerSuffixAction))
                settings.ScannerSuffixAction = "Enter";

            settings.ScaleComPort = NormalizeText(settings.ScaleComPort).ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(settings.ScaleComPort))
                settings.ScaleComPort = "COM1";

            if (settings.ScaleBaudRate <= 0)
                settings.ScaleBaudRate = 9600;

            settings.PoleDisplayComPort = NormalizeText(settings.PoleDisplayComPort).ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(settings.PoleDisplayComPort))
                settings.PoleDisplayComPort = "COM2";

            settings.PoleWelcomeMessage = NormalizeText(settings.PoleWelcomeMessage);

            if (string.IsNullOrWhiteSpace(settings.PoleWelcomeMessage))
                settings.PoleWelcomeMessage = "WELCOME";

            settings.EftposProvider = NormalizeText(settings.EftposProvider);
            settings.EftposPortOrIp = NormalizeText(settings.EftposPortOrIp);
        }

        private static void Validate(TerminalSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.TerminalNo))
                throw new InvalidOperationException("Terminal number is required.");

            if (string.IsNullOrWhiteSpace(settings.TerminalName))
                throw new InvalidOperationException("Terminal name is required.");

            if (string.IsNullOrWhiteSpace(settings.MachineName))
                throw new InvalidOperationException("Machine name is required.");

            if (string.IsNullOrWhiteSpace(settings.Location))
                throw new InvalidOperationException("Terminal location is required.");

            if (string.IsNullOrWhiteSpace(settings.PrinterMode))
                throw new InvalidOperationException("Printer mode is required.");

            if (string.IsNullOrWhiteSpace(settings.ReceiptPrinterName))
                throw new InvalidOperationException("Receipt printer name is required.");

            if (settings.ReceiptPaperWidth != 58 && settings.ReceiptPaperWidth != 80)
                throw new InvalidOperationException("Receipt paper width must be 58 or 80.");

            if (settings.ReceiptCopies < 1 || settings.ReceiptCopies > 5)
                throw new InvalidOperationException("Receipt copies must be between 1 and 5.");

            if (settings.EnableCashDrawer && string.IsNullOrWhiteSpace(settings.DrawerKickCode))
                throw new InvalidOperationException("Drawer kick code is required when cash drawer is enabled.");

            if (string.IsNullOrWhiteSpace(settings.ScannerSuffixAction))
                throw new InvalidOperationException("Scanner suffix action is required.");

            if (settings.EnableScale)
            {
                if (string.IsNullOrWhiteSpace(settings.ScaleComPort))
                    throw new InvalidOperationException("Scale COM port is required when scale is enabled.");

                if (settings.ScaleBaudRate <= 0)
                    throw new InvalidOperationException("Scale baud rate is invalid.");
            }

            if (settings.EnablePoleDisplay)
            {
                if (string.IsNullOrWhiteSpace(settings.PoleDisplayComPort))
                    throw new InvalidOperationException("Pole display COM port is required when pole display is enabled.");

                if (string.IsNullOrWhiteSpace(settings.PoleWelcomeMessage))
                    throw new InvalidOperationException("Pole display welcome message is required.");
            }
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}