using POS.Core.Models.Licensing;

namespace POS.Core.Services.Licensing
{
    public static class LicenseTerminalWorkflowPolicy
    {
        public static LicenseStatus CalculateOverallStatus(
            LicenseStatus storeStatus,
            LicenseStatus terminalStatus,
            bool terminalLicenseRequired)
        {
            if (!terminalLicenseRequired)
                return storeStatus;

            if (storeStatus == LicenseStatus.Invalid ||
                storeStatus == LicenseStatus.Revoked)
            {
                return storeStatus;
            }

            if (storeStatus == LicenseStatus.Missing)
                return LicenseStatus.Missing;

            if (storeStatus == LicenseStatus.ExpiredReadOnly)
                return LicenseStatus.ExpiredReadOnly;

            if (storeStatus == LicenseStatus.GracePeriod)
                return LicenseStatus.GracePeriod;

            if (terminalStatus == LicenseStatus.Invalid ||
                terminalStatus == LicenseStatus.Revoked)
            {
                return terminalStatus;
            }

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

        public static bool CanRunCashier(
            LicenseStatus storeStatus,
            LicenseStatus terminalStatus,
            bool terminalLicenseRequired,
            bool terminalEnabled)
        {
            return terminalLicenseRequired &&
                   terminalEnabled &&
                   IsOperationalStatus(storeStatus) &&
                   IsOperationalStatus(terminalStatus);
        }

        private static bool IsOperationalStatus(LicenseStatus status)
        {
            return status == LicenseStatus.Active ||
                   status == LicenseStatus.ExpiringSoon;
        }

        public static string BuildCurrentComputerRole(
            bool terminalLicenseRequired,
            string? terminalNo,
            string? terminalName)
        {
            if (!terminalLicenseRequired)
                return "BackOffice only";

            string safeTerminalNo = (terminalNo ?? string.Empty).Trim();
            string safeTerminalName = (terminalName ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(safeTerminalNo))
                return "Cashier terminal";

            return string.IsNullOrWhiteSpace(safeTerminalName)
                ? $"Cashier terminal {safeTerminalNo}"
                : $"Cashier terminal {safeTerminalNo} — {safeTerminalName}";
        }

        public static string BuildFleetSummary(
            int registered,
            int ready,
            int attention,
            int disabled)
        {
            if (registered <= 0)
                return "No Cashier terminals are registered.";

            return $"{ready} ready, {attention} require attention, {disabled} disabled";
        }
    }
}
