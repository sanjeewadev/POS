using System.Threading.Tasks;
using POS.Core.Models;

namespace POS.Cashier.UI.Services
{
    public interface IReceiptPrintService
    {
        // Prints the full receipt and kicks the drawer if it's a cash sale
        Task PrintReceiptAsync(SalesHeader transaction, string printerName);

        // Kicks the drawer without printing (used for the "Drawer" UI button)
        Task OpenCashDrawerAsync(string printerName);
    }
}