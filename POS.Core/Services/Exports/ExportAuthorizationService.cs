using System;
using POS.Core.Services;

namespace POS.Core.Services.Exports
{
    public sealed class ExportAuthorizationService
    {
        private readonly AuthService _authService;

        public ExportAuthorizationService(AuthService authService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        public string CurrentUsername =>
            _authService.CurrentUser?.Username?.Trim() is { Length: > 0 } username
                ? username
                : "BackOffice";

        public bool CanExportOperationalDocument => _authService.IsLoggedIn;
        public bool CanExportBulkFinancialData => _authService.IsManager;

        public void EnsureOperationalDocumentAllowed()
        {
            if (!CanExportOperationalDocument)
                throw new UnauthorizedAccessException("A signed-in BackOffice user is required to export this document.");
        }

        public void EnsureBulkFinancialExportAllowed()
        {
            if (!CanExportBulkFinancialData)
                throw new UnauthorizedAccessException("Manager or Administrator access is required for this export.");
        }
    }
}
