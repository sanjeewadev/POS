using System.Collections.Generic;
using System.Threading.Tasks;
using POS.Core.Models;

namespace POS.Core.Services
{
    public interface IBarcodePrintService
    {
        List<string> GetInstalledPrinters();

        Task PrintLabelsAsync(
            List<BarcodePrintJobItem> items,
            LabelSettings settings);
    }
}