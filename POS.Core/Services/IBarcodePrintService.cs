using System.Collections.Generic;
using System.Threading.Tasks;
using POS.Core.Models;

namespace POS.Core.Services
{
    public interface IBarcodePrintService
    {
        // Fetches all installed printers on the Windows machine
        List<string> GetInstalledPrinters();

        // The core batch printing engine
        Task PrintLabelsAsync(List<BarcodePrintJobItem> items, LabelSettings settings);
    }
}