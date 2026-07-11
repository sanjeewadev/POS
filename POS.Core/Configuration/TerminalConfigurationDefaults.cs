namespace POS.Core.Configuration
{
    public static class TerminalConfigurationDefaults
    {
        /// <summary>
        /// Used only when a brand-new database has no terminal settings.
        /// Runtime transaction paths must read the persisted TerminalSettings row.
        /// </summary>
        public const string InitialTerminalNumber = "01";

        public const int ReceiptPaperWidth = 80;
        public const int ReceiptCopies = 1;
        public const int AutoLockTimeoutMinutes = 10;

        public const string PrinterMode = "WindowsSpooler";
        public const string DefaultLocation = "Main Store";
        public const string DrawerKickCode = "27,112,0,25,250";

        public static string BuildTerminalName(
            string terminalNumber)
        {
            string safeTerminalNumber =
                string.IsNullOrWhiteSpace(
                    terminalNumber)
                    ? InitialTerminalNumber
                    : terminalNumber.Trim();

            return $"Cashier Terminal {safeTerminalNumber}";
        }
    }
}
