using System.Data;
using System.IO;
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

        bool wasAlreadyConfigured = false;
        RegisteredTerminal registered = null!;

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
                throw new SetupUserException(
                    "TERMINAL_DATABASE_CONFLICT",
                    "The terminal database contains conflicting machine and " +
                    "terminal-number assignments. Resolve them in BackOffice " +
                    "Terminal Management before continuing.");
            }

            if (byMachine != null &&
                !string.Equals(
                    byMachine.TerminalNo,
                    safeTerminalNo,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new SetupUserException(
                    "MACHINE_ASSIGNED_TO_DIFFERENT_TERMINAL",
                    $"This computer is already assigned to terminal " +
                    $"'{byMachine.TerminalNo}'. Release that machine assignment " +
                    "in BackOffice Terminal Management before assigning a " +
                    "different terminal number.");
            }

            if (byNumber != null &&
                !string.IsNullOrWhiteSpace(byNumber.MachineName) &&
                !string.Equals(
                    byNumber.MachineName,
                    machineName,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new SetupUserException(
                    "TERMINAL_ASSIGNED_TO_OTHER_COMPUTER",
                    $"Terminal '{safeTerminalNo}' is already assigned to " +
                    $"computer '{byNumber.MachineName}'. Choose another terminal, " +
                    "or release the existing machine assignment in BackOffice " +
                    "Terminal Management.");
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
                registeredByNumber != null &&
                registeredByMachine.Id != registeredByNumber.Id)
            {
                throw new SetupUserException(
                    "REGISTERED_TERMINAL_CONFLICT",
                    "The registered-terminal database contains conflicting " +
                    "machine and terminal-number assignments. Resolve them in " +
                    "BackOffice Terminal Management before continuing.");
            }

            if (registeredByMachine != null &&
                !string.Equals(
                    registeredByMachine.TerminalNo,
                    safeTerminalNo,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new SetupUserException(
                    "MACHINE_CODE_ASSIGNED_TO_DIFFERENT_TERMINAL",
                    $"This computer is already registered as terminal " +
                    $"'{registeredByMachine.TerminalNo}'. Release that machine " +
                    "assignment in BackOffice Terminal Management before " +
                    "assigning a different terminal number.");
            }

            if (registeredByNumber != null &&
                !string.IsNullOrWhiteSpace(registeredByNumber.MachineCode) &&
                !string.Equals(
                    registeredByNumber.MachineCode,
                    machineCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new SetupUserException(
                    "TERMINAL_REGISTERED_TO_OTHER_MACHINE",
                    $"Terminal '{safeTerminalNo}' is registered to another " +
                    "computer. Release the existing machine assignment in " +
                    "BackOffice Terminal Management before continuing.");
            }

            wasAlreadyConfigured =
                byMachine != null &&
                byNumber != null &&
                byMachine.Id == byNumber.Id &&
                registeredByMachine != null &&
                registeredByNumber != null &&
                registeredByMachine.Id == registeredByNumber.Id &&
                string.Equals(
                    registeredByMachine.MachineName,
                    machineName,
                    StringComparison.OrdinalIgnoreCase);

            TerminalSettings terminalSettings =
                byMachine ??
                byNumber ??
                TerminalSettingsRepository.CreateDefaultSettings(
                    safeTerminalNo,
                    machineName);

            bool isNewSettings = terminalSettings.Id == 0;
            DateTime now = DateTime.Now;

            terminalSettings.TerminalNo = safeTerminalNo;
            terminalSettings.TerminalName = safeTerminalName;
            terminalSettings.MachineName = machineName;
            terminalSettings.Location = safeLocation;
            terminalSettings.IsActive = true;
            terminalSettings.UpdatedAt = now;
            terminalSettings.UpdatedBy = safeUpdatedBy;

            if (isNewSettings)
            {
                terminalSettings.CreatedAt = now;
                await context.TerminalSettings.AddAsync(
                    terminalSettings,
                    cancellationToken);
            }

            registered =
                registeredByMachine ??
                registeredByNumber ??
                new RegisteredTerminal
                {
                    CreatedAt = now
                };

            bool isNewRegistration = registered.Id == 0;

            registered.TerminalNo = safeTerminalNo;
            registered.TerminalName = safeTerminalName;
            registered.MachineName = machineName;
            registered.MachineCode = machineCode;
            registered.Location = safeLocation;
            registered.IsCashierTerminal = true;
            registered.IsBackOfficeAllowed = true;
            registered.IsActive = true;
            registered.UpdatedAt = now;
            registered.UpdatedBy = safeUpdatedBy;
            registered.Remarks = AppendRemark(
                registered.Remarks,
                wasAlreadyConfigured
                    ? "Production terminal configuration verified again."
                    : "Production terminal machine assignment configured.");

            if (isNewRegistration)
            {
                await context.RegisteredTerminals.AddAsync(
                    registered,
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
            Path.GetFullPath(profilePath),
            wasAlreadyConfigured);
    }

    private static string AppendRemark(
        string existing,
        string message)
    {
        string safeExisting = (existing ?? string.Empty).Trim();
        string safeMessage = (message ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(safeExisting))
            return safeMessage;

        if (string.IsNullOrWhiteSpace(safeMessage))
            return safeExisting;

        string combined =
            $"{safeExisting} | {DateTime.Now:yyyy-MM-dd HH:mm}: {safeMessage}";

        return combined.Length <= 500
            ? combined
            : combined[^500..];
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
    string ProfilePath,
    bool WasAlreadyConfigured);
