using System.Collections.Generic;
using System.Threading.Tasks;

namespace POS.Core.Interfaces
{
    public interface ITerminalHardwareService
    {
        IReadOnlyList<string> GetInstalledPrinterNames();

        Task PrintTestReceiptAsync(
            string printerName,
            int paperWidth);

        Task OpenCashDrawerAsync(
            string printerName);
    }
}
