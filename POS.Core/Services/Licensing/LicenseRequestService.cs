using POS.Core.Configuration;
using POS.Core.Models.Licensing;

namespace POS.Core.Services.Licensing
{
    public static class LicenseRequestService
    {
        public static LicenseRequestInfo CreateStoreRequest(
            string storeId,
            string storeName,
            string legalName,
            LicenseStatus currentStatus,
            DateTime? currentExpiryDate)
        {
            return new LicenseRequestInfo
            {
                RequestedLicenseType = LicenseType.StoreLicense,
                StoreId = Normalize(storeId),
                StoreName = Normalize(storeName),
                LegalName = Normalize(legalName),
                ApplicationVersion = ProductReleaseInfo.ProductVersion,
                CurrentStatus = currentStatus,
                CurrentExpiryDate = currentExpiryDate
            };
        }

        public static LicenseRequestInfo CreateTerminalRequest(
            string storeId,
            string storeName,
            string legalName,
            string terminalNo,
            string terminalName,
            string machineName,
            string machineCode,
            LicenseStatus currentStatus,
            DateTime? currentExpiryDate)
        {
            return new LicenseRequestInfo
            {
                RequestedLicenseType = LicenseType.TerminalLicense,
                StoreId = Normalize(storeId),
                StoreName = Normalize(storeName),
                LegalName = Normalize(legalName),
                TerminalNo = Normalize(terminalNo),
                TerminalName = Normalize(terminalName),
                MachineName = Normalize(machineName),
                MachineCode = Normalize(machineCode),
                ApplicationVersion = ProductReleaseInfo.ProductVersion,
                CurrentStatus = currentStatus,
                CurrentExpiryDate = currentExpiryDate
            };
        }

        public static LicenseRequestInfo CreateCurrentStoreRequest(
            LicenseSummary summary)
        {
            return CreateStoreRequest(
                summary.StoreId,
                summary.CurrentStoreName,
                summary.CurrentLegalName,
                summary.StoreLicenseStatus,
                summary.StoreExpiryDate);
        }

        public static LicenseRequestInfo CreateCurrentTerminalRequest(
            LicenseSummary summary)
        {
            return CreateTerminalRequest(
                summary.StoreId,
                summary.CurrentStoreName,
                summary.CurrentLegalName,
                summary.CurrentTerminalNo,
                summary.CurrentTerminalName,
                summary.CurrentMachineName,
                summary.CurrentMachineCode,
                summary.TerminalLicenseStatus,
                summary.TerminalExpiryDate);
        }

        private static string Normalize(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
