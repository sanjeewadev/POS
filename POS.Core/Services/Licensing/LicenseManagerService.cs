using System;
using System.Threading.Tasks;
using POS.Core.Models.Licensing;
using POS.Core.Repositories;
using POS.Core.Services;

namespace POS.Core.Services.Licensing
{
    public class LicenseManagerService
    {
        private readonly LicenseRepository _licenseRepository;
        private readonly
            TerminalSettingsRepository
            _terminalSettingsRepository;
        private readonly
            MachineFingerprintService
            _machineFingerprintService;
        private readonly
            LicenseFileService
            _licenseFileService;
        private readonly
            LicenseSignatureService
            _licenseSignatureService;

        public LicenseManagerService(
            LicenseRepository licenseRepository,
            TerminalSettingsRepository
                terminalSettingsRepository,
            MachineFingerprintService
                machineFingerprintService,
            LicenseFileService licenseFileService,
            LicenseSignatureService
                licenseSignatureService)
        {
            _licenseRepository = licenseRepository;
            _terminalSettingsRepository =
                terminalSettingsRepository;
            _machineFingerprintService =
                machineFingerprintService;
            _licenseFileService =
                licenseFileService;
            _licenseSignatureService =
                licenseSignatureService;
        }

        public async Task<LicenseSummary>
            GetCurrentLicenseSummaryAsync()
        {
            string machineCode =
                _machineFingerprintService
                    .GetMachineCode();

            string machineName =
                _machineFingerprintService
                    .GetMachineName();

            string terminalNo =
                await GetCurrentTerminalNoAsync();

            InstalledLicense? storeLicense =
                await _licenseRepository
                    .GetActiveStoreLicenseAsync();

            InstalledLicense? terminalLicense =
                await _licenseRepository
                    .GetActiveTerminalLicenseAsync(
                        machineCode,
                        terminalNo);

            LicenseStatus storeStatus =
                ValidateInstalledLicense(
                    storeLicense,
                    LicenseType.StoreLicense,
                    out _);

            LicenseStatus terminalStatus =
                ValidateInstalledLicense(
                    terminalLicense,
                    LicenseType.TerminalLicense,
                    out _);

            if (storeLicense != null &&
                terminalLicense != null &&
                !string.Equals(
                    storeLicense.StoreId,
                    terminalLicense.StoreId,
                    StringComparison.OrdinalIgnoreCase))
            {
                terminalStatus =
                    LicenseStatus.Invalid;
            }

            LicenseStatus overallStatus =
                CalculateOverallStatus(
                    storeStatus,
                    terminalStatus);

            bool canRunCashier =
                CanRunCashier(
                    storeStatus,
                    terminalStatus);

            var summary =
                new LicenseSummary
                {
                    StoreId =
                        storeLicense?.StoreId ??
                        terminalLicense?.StoreId ??
                        string.Empty,

                    StoreName =
                        storeLicense?.StoreName ??
                        terminalLicense?.StoreName ??
                        string.Empty,

                    StoreLicenseId =
                        storeLicense?.LicenseId ??
                        string.Empty,

                    StoreLicenseStatus =
                        storeStatus,

                    StoreExpiryDate =
                        storeLicense?.ExpiresOn,

                    StoreDaysRemaining =
                        LicenseRepository
                            .GetDaysRemaining(
                                storeLicense),

                    CurrentMachineCode =
                        machineCode,

                    CurrentMachineName =
                        machineName,

                    CurrentTerminalNo =
                        terminalNo,

                    TerminalLicenseId =
                        terminalLicense?.LicenseId ??
                        string.Empty,

                    TerminalLicenseStatus =
                        terminalStatus,

                    TerminalExpiryDate =
                        terminalLicense?.ExpiresOn,

                    TerminalDaysRemaining =
                        LicenseRepository
                            .GetDaysRemaining(
                                terminalLicense),

                    OverallStatus =
                        overallStatus,

                    // BackOffice deliberately remains
                    // available for administration and
                    // renewal after expiry.
                    CanRunBackOffice = true,

                    CanRunCashier =
                        canRunCashier,

                    // Legacy property used by the
                    // current License Management UI.
                    IsReadOnlyMode =
                        !canRunCashier
                };

            ApplyStatusMessage(summary);

            return summary;
        }

        public async Task<InstalledLicense>
            ImportLicenseFileAsync(
                string filePath,
                string importedBy)
        {
            var result =
                await _licenseFileService
                    .ReadLicenseFileWithRawJsonAsync(
                        filePath);

            LicenseDocument document =
                result.Document;

            ValidateLicenseDocumentForImport(
                document);

            if (!_licenseSignatureService
                    .IsPublicKeyConfigured())
            {
                throw new InvalidOperationException(
                    "The production license public " +
                    "key is not installed in this " +
                    "POS build.");
            }

            if (!string.Equals(
                    document.KeyId,
                    _licenseSignatureService
                        .GetConfiguredKeyId(),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "This license was created with " +
                    "a different signing key.");
            }

            if (!_licenseSignatureService
                    .VerifySignature(document))
            {
                throw new InvalidOperationException(
                    "Invalid license signature. " +
                    "The file is modified or was " +
                    "not created by the official " +
                    "License Generator.");
            }

            string currentMachineCode =
                _machineFingerprintService
                    .GetMachineCode();

            string currentTerminalNo =
                await GetCurrentTerminalNoAsync();

            if (document.IsStoreLicense)
            {
                InstalledLicense? currentStoreLicense =
                    await _licenseRepository
                        .GetActiveStoreLicenseAsync();

                if (currentStoreLicense != null)
                {
                    LicenseStatus currentStoreStatus =
                        ValidateInstalledLicense(
                            currentStoreLicense,
                            LicenseType.StoreLicense,
                            out LicenseDocument?
                                currentStoreDocument);

                    if (currentStoreStatus ==
                            LicenseStatus.Invalid ||
                        currentStoreStatus ==
                            LicenseStatus.Revoked ||
                        currentStoreDocument == null)
                    {
                        throw new InvalidOperationException(
                            "The currently installed store license is invalid. " +
                            "Restore the correct store license before renewal.");
                    }

                    if (!string.Equals(
                            currentStoreDocument.StoreId,
                            document.StoreId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            "A different Store ID is already installed. " +
                            "Use the same Store ID for renewal.");
                    }
                }
            }

            if (document.IsTerminalLicense)
            {
                if (!string.Equals(
                        document.MachineCode,
                        currentMachineCode,
                        StringComparison
                            .OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "This terminal license belongs " +
                        "to a different computer.");
                }

                if (!string.Equals(
                        document.TerminalNo,
                        currentTerminalNo,
                        StringComparison
                            .OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"This license is for terminal " +
                        $"'{document.TerminalNo}', but " +
                        $"this computer is configured as " +
                        $"terminal '{currentTerminalNo}'.");
                }

                InstalledLicense? storeLicense =
                    await _licenseRepository
                        .GetActiveStoreLicenseAsync();

                LicenseStatus storeLicenseStatus =
                    ValidateInstalledLicense(
                        storeLicense,
                        LicenseType.StoreLicense,
                        out LicenseDocument?
                            storeLicenseDocument);

                if (storeLicense == null ||
                    storeLicenseDocument == null)
                {
                    throw new InvalidOperationException(
                        "Import the store license before " +
                        "importing a terminal license.");
                }

                if (storeLicenseStatus ==
                        LicenseStatus.Invalid ||
                    storeLicenseStatus ==
                        LicenseStatus.Revoked)
                {
                    throw new InvalidOperationException(
                        "The installed store license is invalid.");
                }

                if (!string.Equals(
                        storeLicenseDocument.StoreId,
                        document.StoreId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "The terminal license Store ID " +
                        "does not match the installed " +
                        "store license.");
                }
            }

            var installedLicense =
                new InstalledLicense
                {
                    LicenseId =
                        document.LicenseId,

                    LicenseType =
                        document.LicenseType,

                    StoreId =
                        document.StoreId,

                    StoreName =
                        document.StoreName,

                    TerminalNo =
                        document.TerminalNo,

                    MachineCode =
                        document.MachineCode,

                    IssuedOn =
                        document.IssuedOn,

                    ExpiresOn =
                        document.ExpiresOn,

                    GraceDays = 0,

                    RawLicenseJson =
                        result.RawJson,

                    Signature =
                        document.Signature,

                    LicenseStatus =
                        LicenseStatus.Active,

                    IsActive = true,

                    Remarks =
                        $"Imported license format " +
                        $"{document.SchemaVersion}, " +
                        $"key {document.KeyId}."
                };

            return await _licenseRepository
                .ImportLicenseAsync(
                    installedLicense,
                    importedBy);
        }

        public async Task<LicenseStatus>
            CheckStoreLicenseAsync()
        {
            InstalledLicense? storeLicense =
                await _licenseRepository
                    .GetActiveStoreLicenseAsync();

            return ValidateInstalledLicense(
                storeLicense,
                LicenseType.StoreLicense,
                out _);
        }

        public async Task<LicenseStatus>
            CheckTerminalLicenseAsync(
                string terminalNo)
        {
            string safeTerminalNo =
                NormalizeText(terminalNo);

            if (string.IsNullOrWhiteSpace(
                    safeTerminalNo))
            {
                safeTerminalNo =
                    await GetCurrentTerminalNoAsync();
            }

            string machineCode =
                _machineFingerprintService
                    .GetMachineCode();

            InstalledLicense? terminalLicense =
                await _licenseRepository
                    .GetActiveTerminalLicenseAsync(
                        machineCode,
                        safeTerminalNo);

            return ValidateInstalledLicense(
                terminalLicense,
                LicenseType.TerminalLicense,
                out _);
        }

        public Task<bool>
            CanRunBackOfficeAsync()
        {
            return Task.FromResult(true);
        }

        public async Task<bool>
            CanRunCashierAsync(
                string terminalNo)
        {
            LicenseSummary summary =
                await GetCurrentLicenseSummaryAsync();

            return summary.CanRunCashier;
        }

        public async Task<bool>
            IsReadOnlyModeAsync()
        {
            LicenseSummary summary =
                await GetCurrentLicenseSummaryAsync();

            return !summary.CanRunCashier;
        }

        public async Task<bool>
            CanCreateBusinessTransactionAsync()
        {
            LicenseSummary summary =
                await GetCurrentLicenseSummaryAsync();

            return summary.CanRunCashier;
        }

        public bool
            CanAlwaysAccessBackupAndLicenseImport()
        {
            return true;
        }

        private async Task<string>
            GetCurrentTerminalNoAsync()
        {
            try
            {
                string machineName =
                    _machineFingerprintService
                        .GetMachineName();

                var settings =
                    await _terminalSettingsRepository
                        .GetByMachineNameAsync(
                            machineName);

                return settings?.TerminalNo
                    ?.Trim() ??
                    string.Empty;
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "POS.Core",
                    "Resolve current terminal for licensing",
                    ex);

                throw new InvalidOperationException(
                    "The current terminal identity could not be resolved.",
                    ex);
            }
        }

        private static void
            ValidateLicenseDocumentForImport(
                LicenseDocument document)
        {
            if (document == null)
            {
                throw new InvalidOperationException(
                    "License document is empty.");
            }

            if (document.SchemaVersion !=
                LicensePolicy.CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    "Unsupported license format.");
            }

            if (string.IsNullOrWhiteSpace(
                    document.KeyId))
            {
                throw new InvalidOperationException(
                    "Key ID is missing.");
            }

            if (document.LicenseType !=
                    LicenseType.StoreLicense &&
                document.LicenseType !=
                    LicenseType.TerminalLicense)
            {
                throw new InvalidOperationException(
                    "Invalid license type.");
            }

            if (string.IsNullOrWhiteSpace(
                    document.LicenseId))
            {
                throw new InvalidOperationException(
                    "License ID is required.");
            }

            if (string.IsNullOrWhiteSpace(
                    document.StoreId))
            {
                throw new InvalidOperationException(
                    "Store ID is required.");
            }

            if (string.IsNullOrWhiteSpace(
                    document.StoreName))
            {
                throw new InvalidOperationException(
                    "Store name is required.");
            }

            if (document.IssuedOn == default ||
                document.ExpiresOn == default)
            {
                throw new InvalidOperationException(
                    "Issued and expiry dates are " +
                    "required.");
            }

            if (document.ExpiresOn.Date <
                document.IssuedOn.Date)
            {
                throw new InvalidOperationException(
                    "Expiry date cannot be earlier " +
                    "than issued date.");
            }

            if (document.IsStoreLicense)
            {
                if (!string.IsNullOrWhiteSpace(
                        document.TerminalNo) ||
                    !string.IsNullOrWhiteSpace(
                        document.MachineCode))
                {
                    throw new InvalidOperationException(
                        "Store license contains " +
                        "unexpected terminal data.");
                }
            }

            if (document.IsTerminalLicense)
            {
                if (string.IsNullOrWhiteSpace(
                        document.TerminalNo))
                {
                    throw new InvalidOperationException(
                        "Terminal number is required.");
                }

                if (string.IsNullOrWhiteSpace(
                        document.MachineCode))
                {
                    throw new InvalidOperationException(
                        "Machine code is required.");
                }
            }

            if (string.IsNullOrWhiteSpace(
                    document.Signature))
            {
                throw new InvalidOperationException(
                    "License signature is missing.");
            }
        }

        private LicenseStatus ValidateInstalledLicense(
            InstalledLicense? installedLicense,
            LicenseType expectedType,
            out LicenseDocument? signedDocument)
        {
            signedDocument = null;

            if (installedLicense == null)
                return LicenseStatus.Missing;

            if (!installedLicense.IsActive)
                return LicenseStatus.Revoked;

            if (!_licenseSignatureService
                    .IsPublicKeyConfigured())
            {
                return LicenseStatus.Invalid;
            }

            try
            {
                LicenseDocument document =
                    _licenseFileService.ReadLicenseJson(
                        installedLicense.RawLicenseJson);

                ValidateLicenseDocumentForImport(document);

                if (document.LicenseType != expectedType)
                    return LicenseStatus.Invalid;

                if (!string.Equals(
                        document.KeyId,
                        _licenseSignatureService
                            .GetConfiguredKeyId(),
                        StringComparison.Ordinal))
                {
                    return LicenseStatus.Invalid;
                }

                if (!_licenseSignatureService
                        .VerifySignature(document))
                {
                    return LicenseStatus.Invalid;
                }

                if (!InstalledRecordMatchesSignedDocument(
                        installedLicense,
                        document))
                {
                    return LicenseStatus.Invalid;
                }

                // Use only signed values for all runtime decisions.
                ApplySignedValues(
                    installedLicense,
                    document);

                signedDocument = document;

                return LicenseRepository
                    .CalculateCurrentStatus(
                        installedLicense);
            }
            catch
            {
                return LicenseStatus.Invalid;
            }
        }

        private static bool
            InstalledRecordMatchesSignedDocument(
                InstalledLicense installedLicense,
                LicenseDocument document)
        {
            return
                string.Equals(
                    installedLicense.LicenseId,
                    document.LicenseId,
                    StringComparison.Ordinal) &&
                installedLicense.LicenseType ==
                    document.LicenseType &&
                string.Equals(
                    installedLicense.StoreId,
                    document.StoreId,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    installedLicense.StoreName,
                    document.StoreName,
                    StringComparison.Ordinal) &&
                string.Equals(
                    installedLicense.TerminalNo,
                    document.TerminalNo,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    installedLicense.MachineCode,
                    document.MachineCode,
                    StringComparison.OrdinalIgnoreCase) &&
                installedLicense.IssuedOn.Date ==
                    document.IssuedOn.Date &&
                installedLicense.ExpiresOn.Date ==
                    document.ExpiresOn.Date &&
                string.Equals(
                    installedLicense.Signature,
                    document.Signature,
                    StringComparison.Ordinal);
        }

        private static void ApplySignedValues(
            InstalledLicense installedLicense,
            LicenseDocument document)
        {
            installedLicense.LicenseId =
                document.LicenseId;
            installedLicense.LicenseType =
                document.LicenseType;
            installedLicense.StoreId =
                document.StoreId;
            installedLicense.StoreName =
                document.StoreName;
            installedLicense.TerminalNo =
                document.TerminalNo;
            installedLicense.MachineCode =
                document.MachineCode;
            installedLicense.IssuedOn =
                document.IssuedOn.Date;
            installedLicense.ExpiresOn =
                document.ExpiresOn.Date;
            installedLicense.GraceDays = 0;
            installedLicense.Signature =
                document.Signature;
        }

        private static LicenseStatus
            CalculateOverallStatus(
                LicenseStatus storeStatus,
                LicenseStatus terminalStatus)
        {
            if (storeStatus ==
                    LicenseStatus.Invalid ||
                storeStatus ==
                    LicenseStatus.Revoked)
            {
                return storeStatus;
            }

            if (storeStatus ==
                LicenseStatus.Missing)
            {
                return LicenseStatus.Missing;
            }

            if (storeStatus ==
                LicenseStatus.ExpiredReadOnly)
            {
                return LicenseStatus
                    .ExpiredReadOnly;
            }

            if (terminalStatus ==
                    LicenseStatus.Invalid ||
                terminalStatus ==
                    LicenseStatus.Revoked)
            {
                return terminalStatus;
            }

            if (terminalStatus ==
                LicenseStatus.Missing)
            {
                return LicenseStatus.Missing;
            }

            if (terminalStatus ==
                LicenseStatus.ExpiredReadOnly)
            {
                return LicenseStatus
                    .ExpiredReadOnly;
            }

            if (storeStatus ==
                    LicenseStatus.ExpiringSoon ||
                terminalStatus ==
                    LicenseStatus.ExpiringSoon)
            {
                return LicenseStatus
                    .ExpiringSoon;
            }

            return LicenseStatus.Active;
        }

        private static bool CanRunCashier(
            LicenseStatus storeStatus,
            LicenseStatus terminalStatus)
        {
            return LicenseRepository
                       .IsOperationalStatus(
                           storeStatus) &&
                   LicenseRepository
                       .IsOperationalStatus(
                           terminalStatus);
        }

        private static void ApplyStatusMessage(
            LicenseSummary summary)
        {
            switch (summary.OverallStatus)
            {
                case LicenseStatus.Active:
                    summary.StatusMessage =
                        "License is active. " +
                        "BackOffice and Cashier " +
                        "are available.";

                    summary.StatusColor =
                        "#10B981";
                    break;

                case LicenseStatus.ExpiringSoon:
                    int days = Math.Min(
                        Math.Max(
                            summary.StoreDaysRemaining,
                            0),
                        Math.Max(
                            summary
                                .TerminalDaysRemaining,
                            0));

                    summary.StatusMessage =
                        $"License expires in " +
                        $"{days} day(s). Renew " +
                        $"before expiry. BackOffice " +
                        $"and Cashier remain available.";

                    summary.StatusColor =
                        "#F59E0B";
                    break;

                case LicenseStatus.ExpiredReadOnly:
                    summary.StatusMessage =
                        "The annual license has " +
                        "expired. Cashier is locked. " +
                        "BackOffice remains fully " +
                        "available for administration, " +
                        "backup, reports, and renewal.";

                    summary.StatusColor =
                        "#EF4444";
                    break;

                case LicenseStatus.Missing:
                    summary.StatusMessage =
                        "Required store or terminal " +
                        "license is missing. Cashier " +
                        "is locked. BackOffice remains " +
                        "available.";

                    summary.StatusColor =
                        "#EF4444";
                    break;

                case LicenseStatus.Invalid:
                    summary.StatusMessage =
                        "The installed license is " +
                        "invalid or belongs to another " +
                        "store. Cashier is locked. " +
                        "BackOffice remains available.";

                    summary.StatusColor =
                        "#EF4444";
                    break;

                case LicenseStatus.Revoked:
                    summary.StatusMessage =
                        "The installed license was " +
                        "replaced or deactivated. " +
                        "Cashier is locked. BackOffice " +
                        "remains available.";

                    summary.StatusColor =
                        "#EF4444";
                    break;

                default:
                    summary.StatusMessage =
                        "License status could not be " +
                        "determined. Cashier is locked. " +
                        "BackOffice remains available.";

                    summary.StatusColor =
                        "#EF4444";
                    break;
            }
        }

        private static string NormalizeText(
            string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
