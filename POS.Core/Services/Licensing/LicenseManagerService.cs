using System;
using System.Threading.Tasks;
using POS.Core.Models;
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
        private readonly
            StoreSettingsRepository
            _storeSettingsRepository;

        public LicenseManagerService(
            LicenseRepository licenseRepository,
            TerminalSettingsRepository
                terminalSettingsRepository,
            MachineFingerprintService
                machineFingerprintService,
            LicenseFileService licenseFileService,
            LicenseSignatureService
                licenseSignatureService,
            StoreSettingsRepository
                storeSettingsRepository)
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
            _storeSettingsRepository =
                storeSettingsRepository;
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

            TerminalSettings? currentTerminalSettings =
                await GetCurrentTerminalSettingsAsync();

            string terminalNo =
                currentTerminalSettings?.TerminalNo
                    ?.Trim() ??
                string.Empty;

            InstalledLicense? storeLicense =
                await _licenseRepository
                    .GetActiveStoreLicenseAsync();

            InstalledLicense? terminalLicense =
                await _licenseRepository
                    .GetActiveTerminalLicenseAsync(
                        machineCode,
                        terminalNo);

            StoreSettings? storeSettings =
                await _storeSettingsRepository
                    .GetActiveAsync();

            string licensedStoreName =
                FirstNonBlank(
                    storeLicense?.StoreName,
                    terminalLicense?.StoreName);

            string currentStoreName =
                FirstNonBlank(
                    storeSettings?.StoreName,
                    storeSettings?.LegalName,
                    licensedStoreName);

            string currentLegalName =
                FirstNonBlank(
                    storeSettings?.LegalName,
                    currentStoreName);

            LicenseStatus storeStatus =
                ValidateInstalledLicense(
                    storeLicense,
                    LicenseType.StoreLicense,
                    out _);

            bool terminalLicenseRequired =
                currentTerminalSettings != null;

            bool currentTerminalEnabled =
                currentTerminalSettings?.IsActive == true;

            LicenseStatus terminalStatus =
                terminalLicenseRequired
                    ? ValidateInstalledLicense(
                        terminalLicense,
                        LicenseType.TerminalLicense,
                        out _)
                    : LicenseStatus.Missing;

            if (terminalLicenseRequired &&
                storeLicense != null &&
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
                LicenseTerminalWorkflowPolicy
                    .CalculateOverallStatus(
                        storeStatus,
                        terminalStatus,
                        terminalLicenseRequired);

            bool canRunCashier =
                LicenseTerminalWorkflowPolicy
                    .CanRunCashier(
                        storeStatus,
                        terminalStatus,
                        terminalLicenseRequired,
                        currentTerminalEnabled);

            var summary =
                new LicenseSummary
                {
                    StoreId =
                        storeLicense?.StoreId ??
                        terminalLicense?.StoreId ??
                        string.Empty,

                    // Operational display/request identity always comes
                    // from Store Settings. The signed licence name remains
                    // available separately for audit and mismatch display.
                    StoreName =
                        currentStoreName,

                    CurrentStoreName =
                        currentStoreName,

                    CurrentLegalName =
                        currentLegalName,

                    LicensedStoreName =
                        licensedStoreName,

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

                    CurrentTerminalName =
                        currentTerminalSettings?.TerminalName
                            ?.Trim() ??
                        string.Empty,

                    TerminalLicenseRequired =
                        terminalLicenseRequired,

                    CurrentTerminalEnabled =
                        currentTerminalEnabled,

                    CurrentComputerRole =
                        LicenseTerminalWorkflowPolicy
                            .BuildCurrentComputerRole(
                                terminalLicenseRequired,
                                terminalNo,
                                currentTerminalSettings?.TerminalName),

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

                    // Legacy property used by existing UI code.
                    // A BackOffice-only computer is not a locked Cashier.
                    IsReadOnlyMode =
                        terminalLicenseRequired &&
                        !canRunCashier
                };

            ApplyStatusMessage(summary);

            return summary;
        }

        public async Task<LicenseRequestInfo>
            GetCurrentStoreLicenseRequestAsync()
        {
            LicenseSummary summary =
                await GetCurrentLicenseSummaryAsync();

            return LicenseRequestService
                .CreateCurrentStoreRequest(
                    summary);
        }

        public async Task<LicenseRequestInfo>
            GetCurrentTerminalLicenseRequestAsync()
        {
            LicenseSummary summary =
                await GetCurrentLicenseSummaryAsync();

            return LicenseRequestService
                .CreateCurrentTerminalRequest(
                    summary);
        }

        public async Task<InstalledLicense>
            ImportTerminalLicenseFileAsync(
                string filePath,
                string importedBy)
        {
            var result =
                await _licenseFileService
                    .ReadLicenseFileWithRawJsonAsync(
                        filePath);

            if (!result.Document.IsTerminalLicense)
            {
                throw new InvalidOperationException(
                    "The selected file is not a terminal licence. " +
                    "Store licences must be imported from BackOffice.");
            }

            return await ImportLicenseFileAsync(
                filePath,
                importedBy);
        }

        public Task<InstalledLicense>
            ImportStoreLicenseFileAsync(
                string filePath,
                string importedBy)
        {
            return ImportLicenseFileAsync(
                filePath,
                importedBy,
                requiredType: LicenseType.StoreLicense);
        }

        public Task<InstalledLicense>
            ImportTerminalLicenseForRegisteredTerminalAsync(
                string filePath,
                string terminalNo,
                string machineCode,
                string importedBy)
        {
            string safeTerminalNo =
                NormalizeText(terminalNo);

            string safeMachineCode =
                NormalizeText(machineCode)
                    .ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(safeTerminalNo) ||
                string.IsNullOrWhiteSpace(safeMachineCode))
            {
                throw new InvalidOperationException(
                    "Select a registered terminal with a machine assignment first.");
            }

            return ImportLicenseFileAsync(
                filePath,
                importedBy,
                expectedTerminalNo: safeTerminalNo,
                expectedMachineCode: safeMachineCode,
                requiredType: LicenseType.TerminalLicense);
        }

        public async Task<InstalledLicense>
            ImportLicenseFileAsync(
                string filePath,
                string importedBy,
                string? expectedTerminalNo = null,
                string? expectedMachineCode = null,
                LicenseType? requiredType = null)
        {
            var result =
                await _licenseFileService
                    .ReadLicenseFileWithRawJsonAsync(
                        filePath);

            LicenseDocument document =
                result.Document;

            ValidateLicenseDocumentForImport(
                document);

            if (requiredType.HasValue &&
                document.LicenseType != requiredType.Value)
            {
                string expectedText =
                    requiredType == LicenseType.StoreLicense
                        ? "store"
                        : "terminal";

                throw new InvalidOperationException(
                    $"The selected file is not a {expectedText} licence.");
            }

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
                string.IsNullOrWhiteSpace(expectedMachineCode)
                    ? _machineFingerprintService
                        .GetMachineCode()
                    : expectedMachineCode.Trim()
                        .ToUpperInvariant();

            string currentTerminalNo =
                string.IsNullOrWhiteSpace(expectedTerminalNo)
                    ? await GetCurrentTerminalNoAsync()
                    : expectedTerminalNo.Trim();

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
                    string targetText =
                        string.IsNullOrWhiteSpace(expectedTerminalNo)
                            ? "this computer"
                            : $"selected terminal '{currentTerminalNo}'";

                    throw new InvalidOperationException(
                        "The selected terminal licence was created for " +
                        $"machine '{document.MachineCode}', but {targetText} " +
                        $"requires '{currentMachineCode}'.");
                }

                if (!string.Equals(
                        document.TerminalNo,
                        currentTerminalNo,
                        StringComparison
                            .OrdinalIgnoreCase))
                {
                    string targetText =
                        string.IsNullOrWhiteSpace(expectedTerminalNo)
                            ? "this computer"
                            : "the selected terminal";

                    throw new InvalidOperationException(
                        $"This licence is for terminal " +
                        $"'{document.TerminalNo}', but " +
                        $"{targetText} is terminal '{currentTerminalNo}'.");
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

            return summary.IsReadOnlyMode;
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
            TerminalSettings? settings =
                await GetCurrentTerminalSettingsAsync();

            return settings?.TerminalNo
                ?.Trim() ??
                string.Empty;
        }

        private async Task<TerminalSettings?>
            GetCurrentTerminalSettingsAsync()
        {
            try
            {
                string machineName =
                    _machineFingerprintService
                        .GetMachineName();

                return await _terminalSettingsRepository
                    .GetByMachineNameAsync(
                        machineName);
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

        private static void ApplyStatusMessage(
            LicenseSummary summary)
        {
            if (!summary.TerminalLicenseRequired)
            {
                summary.StatusMessage =
                    summary.StoreLicenseStatus switch
                    {
                        LicenseStatus.Active =>
                            "Store licence is active. This computer is BackOffice-only; a terminal licence is not required here.",
                        LicenseStatus.ExpiringSoon =>
                            $"Store licence expires in {Math.Max(summary.StoreDaysRemaining, 0)} day(s). BackOffice remains available; this computer does not require a terminal licence.",
                        LicenseStatus.GracePeriod =>
                            "A legacy store-licence grace state is installed. BackOffice remains available for recovery and renewal. This computer does not require a terminal licence.",
                        LicenseStatus.ExpiredReadOnly =>
                            "The store licence has expired. BackOffice remains available for administration, backup, reports, and renewal. This computer does not require a terminal licence.",
                        LicenseStatus.Missing =>
                            "The store licence is missing. BackOffice remains available for licence import and administration. This computer does not require a terminal licence.",
                        LicenseStatus.Invalid =>
                            "The installed store licence is invalid. BackOffice remains available for recovery and renewal.",
                        LicenseStatus.Revoked =>
                            "The installed store licence was replaced or deactivated. BackOffice remains available for recovery and renewal.",
                        _ =>
                            "Store licence status could not be determined. BackOffice remains available."
                    };

                summary.StatusColor =
                    summary.StoreLicenseStatus switch
                    {
                        LicenseStatus.Active => "#10B981",
                        LicenseStatus.ExpiringSoon => "#F59E0B",
                        _ => "#EF4444"
                    };

                return;
            }

            if (!summary.CurrentTerminalEnabled)
            {
                summary.StatusMessage =
                    "This Cashier terminal is disabled. BackOffice remains available; enable the terminal from Terminal Management before using Cashier.";
                summary.StatusColor = "#EF4444";
                return;
            }

            switch (summary.OverallStatus)
            {
                case LicenseStatus.Active:
                    summary.StatusMessage =
                        "Store and current terminal licences are active. BackOffice and Cashier are available.";
                    summary.StatusColor = "#10B981";
                    break;

                case LicenseStatus.ExpiringSoon:
                    int days = Math.Min(
                        Math.Max(summary.StoreDaysRemaining, 0),
                        Math.Max(summary.TerminalDaysRemaining, 0));
                    summary.StatusMessage =
                        $"A required licence expires in {days} day(s). Renew before expiry. BackOffice and Cashier remain available.";
                    summary.StatusColor = "#F59E0B";
                    break;

                case LicenseStatus.GracePeriod:
                    summary.StatusMessage =
                        "A legacy licence grace state is installed. Cashier is locked until a current annual licence is imported. BackOffice remains available.";
                    summary.StatusColor = "#EF4444";
                    break;

                case LicenseStatus.ExpiredReadOnly:
                    summary.StatusMessage =
                        "A required annual licence has expired. Cashier is locked. BackOffice remains available for administration, backup, reports, and renewal.";
                    summary.StatusColor = "#EF4444";
                    break;

                case LicenseStatus.Missing:
                    summary.StatusMessage =
                        "The store licence or this terminal's licence is missing. Cashier is locked. BackOffice remains available.";
                    summary.StatusColor = "#EF4444";
                    break;

                case LicenseStatus.Invalid:
                    summary.StatusMessage =
                        "A required licence is invalid or belongs to another store or terminal. Cashier is locked. BackOffice remains available.";
                    summary.StatusColor = "#EF4444";
                    break;

                case LicenseStatus.Revoked:
                    summary.StatusMessage =
                        "A required licence was replaced or deactivated. Cashier is locked. BackOffice remains available.";
                    summary.StatusColor = "#EF4444";
                    break;

                default:
                    summary.StatusMessage =
                        "Licence status could not be determined. Cashier is locked. BackOffice remains available.";
                    summary.StatusColor = "#EF4444";
                    break;
            }
        }

        private static string NormalizeText(
            string? value)
        {
            return (value ?? string.Empty).Trim();
        }
        private static string FirstNonBlank(
            params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return string.Empty;
        }

    }
}
