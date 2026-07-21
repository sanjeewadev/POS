using POS.Core.Data.Configuration;
using POS.Core.Models.Licensing;
using POS.Core.Repositories;
using POS.Core.Services.Licensing;

namespace POS.Database.Setup;

internal sealed class LicenseImportCommandService
{
    public async Task<InstalledLicense> ImportAsync(
        string profilePath,
        string licenseFile,
        string importedBy)
    {
        string fullProfilePath = Path.GetFullPath(profilePath);
        string fullLicensePath = Path.GetFullPath(licenseFile);

        if (!File.Exists(fullProfilePath))
            throw new FileNotFoundException(
                "The encrypted database profile was not found.",
                fullProfilePath);

        if (!File.Exists(fullLicensePath))
            throw new FileNotFoundException(
                "The POS licence file was not found.",
                fullLicensePath);

        DatabaseConnectionSettings settings =
            new DatabaseConnectionSettingsStore(fullProfilePath)
                .LoadOrDefault();

        if (!settings.IsCentralSqlServer)
        {
            throw new InvalidOperationException(
                "Production network licence import requires a SQL Server profile.");
        }

        var factory = new ConfiguredDbContextFactory(settings);
        var machineFingerprint = new MachineFingerprintService();
        var terminalSettings = new TerminalSettingsRepository(factory);
        var licenseRepository = new LicenseRepository(factory);
        var licenseManager = new LicenseManagerService(
            licenseRepository,
            terminalSettings,
            machineFingerprint,
            new LicenseFileService(),
            new LicenseSignatureService(),
            new StoreSettingsRepository(factory));

        return await licenseManager.ImportLicenseFileAsync(
            fullLicensePath,
            string.IsNullOrWhiteSpace(importedBy)
                ? "Production installer"
                : importedBy.Trim());
    }
}
