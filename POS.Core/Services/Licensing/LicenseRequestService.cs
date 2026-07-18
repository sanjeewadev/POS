using POS.Core.Configuration;
using POS.Core.Models.Licensing;

namespace POS.Core.Services.Licensing
{
    public static class LicenseRequestService
    {
        public static LicenseRequestInfo CreateTerminalRequest(
            string storeId,
            string storeName,
            string terminalNo,
            string terminalName,
            string machineName,
            string machineCode,
            LicenseStatus currentStatus,
            DateTime? currentExpiryDate)
        {
            return new LicenseRequestInfo
            {
                StoreId = Normalize(storeId),
                StoreName = Normalize(storeName),
                TerminalNo = Normalize(terminalNo),
                TerminalName = Normalize(terminalName),
                MachineName = Normalize(machineName),
                MachineCode = Normalize(machineCode),
                ApplicationVersion = ProductReleaseInfo.ProductVersion,
                CurrentStatus = currentStatus,
                CurrentExpiryDate = currentExpiryDate
            };
        }

        public static LicenseRequestInfo CreateCurrentTerminalRequest(
            LicenseSummary summary)
        {
            return CreateTerminalRequest(
                summary.StoreId,
                summary.StoreName,
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
