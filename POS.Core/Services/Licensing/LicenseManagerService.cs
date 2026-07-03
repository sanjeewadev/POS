using System;
using System.Threading.Tasks;
using POS.Core.Models.Licensing;
using POS.Core.Repositories;

namespace POS.Core.Services.Licensing
{
    public class LicenseManagerService
    {
        private readonly LicenseRepository _licenseRepository;
        private readonly TerminalSettingsRepository _terminalSettingsRepository;
        private readonly MachineFingerprintService _machineFingerprintService;
        private readonly LicenseFileService _licenseFileService;
        private readonly LicenseSignatureService _licenseSignatureService;

        public LicenseManagerService(
            LicenseRepository licenseRepository,
            TerminalSettingsRepository terminalSettingsRepository,
            MachineFingerprintService machineFingerprintService,
            LicenseFileService licenseFileService,
            LicenseSignatureService licenseSignatureService)
        {
            _licenseRepository = licenseRepository;
            _terminalSettingsRepository = terminalSettingsRepository;
            _machineFingerprintService = machineFingerprintService;
            _licenseFileService = licenseFileService;
            _licenseSignatureService = licenseSignatureService;
        }

        public async Task<LicenseSummary> GetCurrentLicenseSummaryAsync()
        {
            string machineCode = _machineFingerprintService.GetMachineCode();
            string machineName = _machineFingerprintService.GetMachineName();
            string terminalNo = await GetCurrentTerminalNoAsync();

            InstalledLicense? storeLicense = await _licenseRepository.GetActiveStoreLicenseAsync();
            InstalledLicense? terminalLicense = await _licenseRepository.GetActiveTerminalLicenseAsync(
                machineCode,
                terminalNo);

            LicenseStatus storeStatus = LicenseRepository.CalculateCurrentStatus(storeLicense);
            LicenseStatus terminalStatus = LicenseRepository.CalculateCurrentStatus(terminalLicense);

            LicenseStatus overallStatus = CalculateOverallStatus(storeStatus, terminalStatus);

            var summary = new LicenseSummary
            {
                StoreId = storeLicense?.StoreId ?? terminalLicense?.StoreId ?? string.Empty,
                StoreName = storeLicense?.StoreName ?? terminalLicense?.StoreName ?? string.Empty,

                StoreLicenseId = storeLicense?.LicenseId ?? string.Empty,
                StoreLicenseStatus = storeStatus,
                StoreExpiryDate = storeLicense?.ExpiresOn,
                StoreDaysRemaining = LicenseRepository.GetDaysRemaining(storeLicense),

                CurrentMachineCode = machineCode,
                CurrentMachineName = machineName,
                CurrentTerminalNo = terminalNo,

                TerminalLicenseId = terminalLicense?.LicenseId ?? string.Empty,
                TerminalLicenseStatus = terminalStatus,
                TerminalExpiryDate = terminalLicense?.ExpiresOn,
                TerminalDaysRemaining = LicenseRepository.GetDaysRemaining(terminalLicense),

                OverallStatus = overallStatus,
                CanRunBackOffice = CanRunBackOffice(storeStatus),
                CanRunCashier = CanRunCashier(storeStatus, terminalStatus),
                IsReadOnlyMode = IsReadOnlyStatus(overallStatus)
            };

            ApplyStatusMessage(summary);

            return summary;
        }

        public async Task<InstalledLicense> ImportLicenseFileAsync(string filePath, string importedBy)
        {
            var result = await _licenseFileService.ReadLicenseFileWithRawJsonAsync(filePath);
            LicenseDocument document = result.Document;

            ValidateLicenseDocumentForImport(document);

            bool signatureValid = _licenseSignatureService.VerifySignature(document);

            if (!signatureValid)
            {
                if (!_licenseSignatureService.IsPublicKeyConfigured())
                {
                    throw new InvalidOperationException(
                        "License verification public key is not configured. Add the real RSA public key after creating the private license generator.");
                }

                throw new InvalidOperationException("Invalid license signature. This license file was not created by the official license generator or it was modified.");
            }

            string currentMachineCode = _machineFingerprintService.GetMachineCode();
            string currentTerminalNo = await GetCurrentTerminalNoAsync();

            if (document.LicenseType == LicenseType.TerminalLicense)
            {
                if (!string.Equals(document.MachineCode, currentMachineCode, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "This terminal license does not belong to this computer. Please send the current machine code to support and request the correct license.");
                }

                if (!string.Equals(document.TerminalNo, currentTerminalNo, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"This terminal license is for terminal '{document.TerminalNo}', but this computer is configured as terminal '{currentTerminalNo}'.");
                }
            }

            var installedLicense = new InstalledLicense
            {
                LicenseId = document.LicenseId,
                LicenseType = document.LicenseType,
                StoreId = document.StoreId,
                StoreName = document.StoreName,
                TerminalNo = document.TerminalNo,
                MachineCode = document.MachineCode,
                IssuedOn = document.IssuedOn,
                ExpiresOn = document.ExpiresOn,
                GraceDays = document.GraceDays,
                RawLicenseJson = result.RawJson,
                Signature = document.Signature,
                LicenseStatus = LicenseStatus.Active,
                IsActive = true,
                Remarks = "Imported from license file."
            };

            return await _licenseRepository.ImportLicenseAsync(
                installedLicense,
                importedBy);
        }

        public async Task<LicenseStatus> CheckStoreLicenseAsync()
        {
            InstalledLicense? storeLicense = await _licenseRepository.GetActiveStoreLicenseAsync();
            return LicenseRepository.CalculateCurrentStatus(storeLicense);
        }

        public async Task<LicenseStatus> CheckTerminalLicenseAsync(string terminalNo)
        {
            string safeTerminalNo = NormalizeText(terminalNo);

            if (string.IsNullOrWhiteSpace(safeTerminalNo))
                safeTerminalNo = await GetCurrentTerminalNoAsync();

            string machineCode = _machineFingerprintService.GetMachineCode();

            InstalledLicense? terminalLicense = await _licenseRepository.GetActiveTerminalLicenseAsync(
                machineCode,
                safeTerminalNo);

            return LicenseRepository.CalculateCurrentStatus(terminalLicense);
        }

        public async Task<bool> CanRunBackOfficeAsync()
        {
            LicenseStatus storeStatus = await CheckStoreLicenseAsync();
            return CanRunBackOffice(storeStatus);
        }

        public async Task<bool> CanRunCashierAsync(string terminalNo)
        {
            LicenseStatus storeStatus = await CheckStoreLicenseAsync();
            LicenseStatus terminalStatus = await CheckTerminalLicenseAsync(terminalNo);

            return CanRunCashier(storeStatus, terminalStatus);
        }

        public async Task<bool> IsReadOnlyModeAsync()
        {
            LicenseSummary summary = await GetCurrentLicenseSummaryAsync();
            return summary.IsReadOnlyMode;
        }

        public async Task<bool> CanCreateBusinessTransactionAsync()
        {
            LicenseSummary summary = await GetCurrentLicenseSummaryAsync();

            return summary.OverallStatus == LicenseStatus.Active ||
                   summary.OverallStatus == LicenseStatus.ExpiringSoon ||
                   summary.OverallStatus == LicenseStatus.GracePeriod;
        }

        public bool CanAlwaysAccessBackupAndLicenseImport()
        {
            return true;
        }

        private async Task<string> GetCurrentTerminalNoAsync()
        {
            try
            {
                var terminalSettings = await _terminalSettingsRepository.GetOrCreateForCurrentMachineAsync("01");

                if (!string.IsNullOrWhiteSpace(terminalSettings.TerminalNo))
                    return terminalSettings.TerminalNo.Trim();
            }
            catch
            {
                // Fallback below.
            }

            return "01";
        }

        private static void ValidateLicenseDocumentForImport(LicenseDocument document)
        {
            if (document == null)
                throw new InvalidOperationException("License document is empty.");

            if (document.LicenseType != LicenseType.StoreLicense &&
                document.LicenseType != LicenseType.TerminalLicense)
            {
                throw new InvalidOperationException("Invalid license type.");
            }

            if (string.IsNullOrWhiteSpace(document.LicenseId))
                throw new InvalidOperationException("License ID is required.");

            if (string.IsNullOrWhiteSpace(document.StoreId))
                throw new InvalidOperationException("Store ID is required.");

            if (string.IsNullOrWhiteSpace(document.StoreName))
                throw new InvalidOperationException("Store name is required.");

            if (document.IssuedOn == default)
                throw new InvalidOperationException("License issued date is required.");

            if (document.ExpiresOn == default)
                throw new InvalidOperationException("License expiry date is required.");

            if (document.ExpiresOn < document.IssuedOn)
                throw new InvalidOperationException("License expiry date cannot be earlier than issued date.");

            if (document.LicenseType == LicenseType.TerminalLicense)
            {
                if (string.IsNullOrWhiteSpace(document.TerminalNo))
                    throw new InvalidOperationException("Terminal number is required for terminal license.");

                if (string.IsNullOrWhiteSpace(document.MachineCode))
                    throw new InvalidOperationException("Machine code is required for terminal license.");
            }

            if (string.IsNullOrWhiteSpace(document.Signature))
                throw new InvalidOperationException("License signature is missing.");
        }

        private static LicenseStatus CalculateOverallStatus(
            LicenseStatus storeStatus,
            LicenseStatus terminalStatus)
        {
            if (IsInvalidHardStatus(storeStatus))
                return storeStatus;

            if (storeStatus == LicenseStatus.Missing)
                return LicenseStatus.Missing;

            if (storeStatus == LicenseStatus.ExpiredReadOnly)
                return LicenseStatus.ExpiredReadOnly;

            if (storeStatus == LicenseStatus.GracePeriod)
                return LicenseStatus.GracePeriod;

            if (IsInvalidHardStatus(terminalStatus))
                return terminalStatus;

            if (terminalStatus == LicenseStatus.Missing)
                return LicenseStatus.Missing;

            if (terminalStatus == LicenseStatus.ExpiredReadOnly)
                return LicenseStatus.ExpiredReadOnly;

            if (terminalStatus == LicenseStatus.GracePeriod)
                return LicenseStatus.GracePeriod;

            if (storeStatus == LicenseStatus.ExpiringSoon ||
                terminalStatus == LicenseStatus.ExpiringSoon)
            {
                return LicenseStatus.ExpiringSoon;
            }

            return LicenseStatus.Active;
        }

        private static bool CanRunBackOffice(LicenseStatus storeStatus)
        {
            return storeStatus == LicenseStatus.Active ||
                   storeStatus == LicenseStatus.ExpiringSoon ||
                   storeStatus == LicenseStatus.GracePeriod ||
                   storeStatus == LicenseStatus.ExpiredReadOnly;
        }

        private static bool CanRunCashier(
            LicenseStatus storeStatus,
            LicenseStatus terminalStatus)
        {
            return IsOperationalStatus(storeStatus) &&
                   IsOperationalStatus(terminalStatus);
        }

        private static bool IsOperationalStatus(LicenseStatus status)
        {
            return status == LicenseStatus.Active ||
                   status == LicenseStatus.ExpiringSoon ||
                   status == LicenseStatus.GracePeriod;
        }

        private static bool IsReadOnlyStatus(LicenseStatus status)
        {
            return status == LicenseStatus.Missing ||
                   status == LicenseStatus.Invalid ||
                   status == LicenseStatus.Revoked ||
                   status == LicenseStatus.ExpiredReadOnly;
        }

        private static bool IsInvalidHardStatus(LicenseStatus status)
        {
            return status == LicenseStatus.Invalid ||
                   status == LicenseStatus.Revoked;
        }

        private static void ApplyStatusMessage(LicenseSummary summary)
        {
            switch (summary.OverallStatus)
            {
                case LicenseStatus.Active:
                    summary.StatusMessage = "License is active.";
                    summary.StatusColor = "#10B981";
                    break;

                case LicenseStatus.ExpiringSoon:
                    summary.StatusMessage = "License is active but expiring soon. Please renew before expiry.";
                    summary.StatusColor = "#F59E0B";
                    break;

                case LicenseStatus.GracePeriod:
                    summary.StatusMessage = "License has expired but is still inside the grace period. Renew immediately.";
                    summary.StatusColor = "#F97316";
                    break;

                case LicenseStatus.ExpiredReadOnly:
                    summary.StatusMessage = "License expired. System is in read-only mode. Import a renewed license to continue transactions.";
                    summary.StatusColor = "#EF4444";
                    break;

                case LicenseStatus.Missing:
                    summary.StatusMessage = "License is missing. Import store license and terminal license.";
                    summary.StatusColor = "#EF4444";
                    break;

                case LicenseStatus.Invalid:
                    summary.StatusMessage = "License is invalid. Import a valid license file.";
                    summary.StatusColor = "#EF4444";
                    break;

                case LicenseStatus.Revoked:
                    summary.StatusMessage = "License was revoked or replaced. Import a valid active license.";
                    summary.StatusColor = "#EF4444";
                    break;

                default:
                    summary.StatusMessage = "License status could not be determined.";
                    summary.StatusColor = "#64748B";
                    break;
            }
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}