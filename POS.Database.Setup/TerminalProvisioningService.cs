using System.Data;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Data.Configuration;
using POS.Core.Models;
using POS.Core.Models.Terminals;
using POS.Core.Repositories;
using POS.Core.Services.Licensing;

namespace POS.Database.Setup;

internal sealed class TerminalProvisioningService
{
    public async Task<TerminalProvisioningResult> ConfigureAsync(
        string host,
        int port,
        string databaseName,
        string applicationLogin,
        string applicationPassword,
        string terminalNo,
        string terminalName,
        string location,
        string profilePath,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        string safeTerminalNo = NormalizeRequired(
            terminalNo,
            "Terminal number",
            20);

        string safeTerminalName = NormalizeOptional(
            terminalName,
            TerminalConfigurationDefaults.BuildTerminalName(safeTerminalNo),
            100);

        string safeLocation = NormalizeOptional(
            location,
            TerminalConfigurationDefaults.DefaultLocation,
            100);

        string safeUpdatedBy = NormalizeOptional(
            updatedBy,
            "Production terminal installer",
            100);

        var settings = DatabaseConnectionSettings.CreateSqlServer(
            host,
            port,
            databaseName,
            applicationLogin,
            applicationPassword);

        var factory = new ConfiguredDbContextFactory(settings);
        var machineFingerprint = new MachineFingerprintService();

        string machineName = machineFingerprint.GetMachineName().Trim();
        string machineCode = machineFingerprint.GetMachineCode().Trim();

        await using AppDbContext context =
            await factory.CreateDbContextAsync(cancellationToken);

        await using var transaction =
            await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        try
        {
            TerminalSettings? byMachine =
                await context.TerminalSettings
                    .FirstOrDefaultAsync(
                        row => row.MachineName == machineName,
                        cancellationToken);

            TerminalSettings? byNumber =
                await context.TerminalSettings
                    .FirstOrDefaultAsync(
                        row => row.TerminalNo == safeTerminalNo,
                        cancellationToken);

            if (byMachine != null &&
                byNumber != null &&
                byMachine.Id != byNumber.Id)
            {
                throw new InvalidOperationException(
                    "The terminal database contains conflicting machine and " +
                    "terminal-number assignments. Resolve them in Terminal " +
                    "Management before continuing.");
            }

            if (byMachine != null &&
                !string.Equals(
                    byMachine.TerminalNo,
                    safeTerminalNo,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"This computer is already assigned to terminal " +
                    $"'{byMachine.TerminalNo}'. Use Terminal Management " +
                    "to release it before assigning a different number.");
            }

            if (byNumber != null &&
                !string.IsNullOrWhiteSpace(byNumber.MachineName) &&
                !string.Equals(
                    byNumber.MachineName,
                    machineName,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Terminal '{safeTerminalNo}' is already assigned to " +
                    $"computer '{byNumber.MachineName}'.");
            }

            RegisteredTerminal? registeredByMachine =
                await context.RegisteredTerminals
                    .FirstOrDefaultAsync(
                        row => row.MachineCode == machineCode,
                        cancellationToken);

            RegisteredTerminal? registeredByNumber =
                await context.RegisteredTerminals
                    .FirstOrDefaultAsync(
                        row => row.TerminalNo == safeTerminalNo,
                        cancellationToken);

            if (registeredByMachine != null &&
                !string.Equals(
                    registeredByMachine.TerminalNo,
                    safeTerminalNo,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"This machine code is already registered as terminal " +
                    $"'{registeredByMachine.TerminalNo}'.");
            }

            if (registeredByNumber != null &&
                !string.Equals(
                    registeredByNumber.MachineCode,
                    machineCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Terminal '{safeTerminalNo}' is registered to another " +
                    "machine code.");
            }

            TerminalSettings entity =
                byMachine ??
                byNumber ??
                TerminalSettingsRepository.CreateDefaultSettings(
                    safeTerminalNo,
                    machineName);

            bool isNew = entity.Id == 0;
            DateTime now = DateTime.Now;

            entity.TerminalNo = safeTerminalNo;
            entity.TerminalName = safeTerminalName;
            entity.MachineName = machineName;
            entity.Location = safeLocation;
            entity.IsActive = true;
            entity.UpdatedAt = now;
            entity.UpdatedBy = safeUpdatedBy;

            if (isNew)
            {
                entity.CreatedAt = now;
                await context.TerminalSettings.AddAsync(
                    entity,
                    cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var terminalSettingsRepository =
            new TerminalSettingsRepository(factory);

        var terminalManagementRepository =
            new TerminalManagementRepository(
                factory,
                machineFingerprint,
                terminalSettingsRepository);

        RegisteredTerminal registered =
            await terminalManagementRepository
                .RegisterOrUpdateCurrentMachineAsync(safeUpdatedBy);

        new ProfileCommandService().WriteSqlServerProfile(
            profilePath,
            host,
            port,
            databaseName,
            applicationLogin,
            applicationPassword);

        await new ServerProvisioningService()
            .VerifyApplicationLoginAsync(
                host,
                port,
                databaseName,
                applicationLogin,
                applicationPassword,
                cancellationToken);

        return new TerminalProvisioningResult(
            registered.TerminalNo,
            registered.TerminalName,
            registered.MachineName,
            registered.MachineCode,
            registered.Location,
            Path.GetFullPath(profilePath));
    }

    private static string NormalizeRequired(
        string value,
        string label,
        int maximumLength)
    {
        string result = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(result))
            throw new ArgumentException($"{label} is required.");

        if (result.Length > maximumLength)
            throw new ArgumentException(
                $"{label} cannot exceed {maximumLength} characters.");

        return result;
    }

    private static string NormalizeOptional(
        string value,
        string defaultValue,
        int maximumLength)
    {
        string result = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(result))
            result = defaultValue;

        if (result.Length > maximumLength)
            throw new ArgumentException(
                $"The value cannot exceed {maximumLength} characters.");

        return result;
    }
}

internal sealed record TerminalProvisioningResult(
    string TerminalNo,
    string TerminalName,
    string MachineName,
    string MachineCode,
    string Location,
    string ProfilePath);
