using System;
using System.Threading.Tasks;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;

namespace POS.Cashier.UI.Services
{
    public sealed class CashDrawerAuditService
    {
        private readonly TillRepository _tillRepository;
        private readonly TerminalSettingsRepository _terminalSettingsRepository;
        private readonly IReceiptPrintService _printService;

        public CashDrawerAuditService(
            TillRepository tillRepository,
            TerminalSettingsRepository terminalSettingsRepository,
            IReceiptPrintService printService)
        {
            _tillRepository = tillRepository;
            _terminalSettingsRepository = terminalSettingsRepository;
            _printService = printService;
        }

        public async Task<CashDrawerEvent> OpenAsync(
            int shiftSessionId,
            string terminalNo,
            string cashierName,
            string eventType,
            string reason,
            string note,
            string authorizedBy,
            int? salesHeaderId = null,
            int? cashMovementId = null)
        {
            TerminalSettings? settings = await _terminalSettingsRepository
                .GetByTerminalNoAsync(terminalNo);

            bool succeeded = false;
            string failure = string.Empty;

            try
            {
                if (settings == null || !settings.EnableCashDrawer)
                    throw new InvalidOperationException("Cash drawer is disabled in Terminal Settings.");
                if (string.IsNullOrWhiteSpace(settings.ReceiptPrinterName))
                    throw new InvalidOperationException("No receipt printer is configured for the cash drawer.");

                await _printService.OpenCashDrawerAsync(settings.ReceiptPrinterName);
                succeeded = true;
            }
            catch (Exception ex)
            {
                failure = ex.Message;
            }

            CashDrawerEvent saved = await _tillRepository.RecordCashDrawerEventAsync(
                new CashDrawerEventRequest
                {
                    ShiftSessionId = shiftSessionId,
                    TerminalNo = terminalNo,
                    CashierName = cashierName,
                    EventType = eventType,
                    Reason = reason,
                    Note = note,
                    AuthorizedBy = authorizedBy,
                    SalesHeaderId = salesHeaderId,
                    CashMovementId = cashMovementId,
                    Succeeded = succeeded,
                    FailureMessage = failure
                });

            if (!succeeded)
                throw new InvalidOperationException(failure);

            return saved;
        }
    }
}
