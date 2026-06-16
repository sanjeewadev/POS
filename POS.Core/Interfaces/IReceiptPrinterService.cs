using POS.Core.Models;

namespace POS.Core.Interfaces
{
    public interface IReceiptPrinterService
    {
        void PrintCashVoucher(CashMovement movement);
        void OpenCashDrawer();
    }
}