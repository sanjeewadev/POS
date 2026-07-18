using Microsoft.Win32;
using POS.Cashier.UI.Services;
using POS.Core.Configuration;
using POS.Core.Models.Licensing;
using POS.Core.Services;
using POS.Core.Services.Licensing;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace POS.Cashier.UI.Dialogs
{
    public partial class CashierLicenseRecoveryDialog : Window
    {
        private readonly LicenseManagerService
            _licenseManagerService;

        private LicenseSummary _summary;

        private LicenseRequestInfo? _requestInfo;

        private bool _isBusy;

        public bool ActivationCompleted { get; private set; }

        public CashierLicenseRecoveryDialog(
            LicenseManagerService licenseManagerService,
            LicenseSummary initialSummary)
        {
            _licenseManagerService =
                licenseManagerService ??
                throw new ArgumentNullException(
                    nameof(licenseManagerService));

            _summary =
                initialSummary ??
                throw new ArgumentNullException(
                    nameof(initialSummary));

            InitializeComponent();
        }

        private async void Window_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            await RefreshAsync();
        }

        private void Window_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key != Key.Escape || _isBusy)
                return;

            DialogResult = false;
            e.Handled = true;
        }

        private async Task RefreshAsync(
            string? successMessage = null)
        {
            if (_isBusy)
                return;

            try
            {
                SetBusy(true);

                _summary =
                    await _licenseManagerService
                        .GetCurrentLicenseSummaryAsync();

                _requestInfo =
                    await _licenseManagerService
                        .GetCurrentTerminalLicenseRequestAsync();

                ApplySummary(
                    _summary,
                    successMessage);
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Cashier licence recovery refresh",
                    ex);

                SetActionStatus(
                    "Licence information could not be refreshed. Technical " +
                    "details were saved in the local POS Logs folder.");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void ApplySummary(
            LicenseSummary summary,
            string? successMessage)
        {
            StoreIdTextBlock.Text = Display(summary.StoreId);

            TerminalTextBlock.Text =
                string.IsNullOrWhiteSpace(
                    summary.CurrentTerminalName)
                    ? Display(summary.CurrentTerminalNo)
                    : $"{Display(summary.CurrentTerminalNo)} - " +
                      summary.CurrentTerminalName.Trim();

            MachineNameTextBlock.Text =
                Display(summary.CurrentMachineName);

            MachineCodeTextBox.Text =
                Display(summary.CurrentMachineCode);

            StoreStatusTextBlock.Text =
                BuildStatusText(
                    summary.StoreLicenseStatus,
                    summary.StoreExpiryDate);

            TerminalStatusTextBlock.Text =
                BuildStatusText(
                    summary.TerminalLicenseStatus,
                    summary.TerminalExpiryDate);

            ExpiryTextBlock.Text =
                summary.TerminalExpiryDate.HasValue
                    ? summary.TerminalExpiryDate.Value
                        .ToString("yyyy-MM-dd")
                    : "-";

            VersionTextBlock.Text =
                ProductReleaseInfo.ProductVersion;

            ActivationCompleted =
                summary.CanRunCashier;

            ContinueButton.IsEnabled =
                ActivationCompleted;

            bool storeOperational =
                IsOperational(
                    summary.StoreLicenseStatus);

            if (summary.CanRunCashier)
            {
                StatusPanel.Style =
                    (Style)FindResource(
                        "CashierSummarySuccessPanel");

                StatusTitleTextBlock.Text =
                    "CASHIER LICENCE IS VALID";

                StatusMessageTextBlock.Text =
                    successMessage ??
                    summary.StatusMessage;

                SetActionStatus(
                    "Activation is complete. Continue to the Cashier login.");
            }
            else if (!storeOperational)
            {
                StatusPanel.Style =
                    (Style)FindResource(
                        "CashierSummaryDangerPanel");

                StatusTitleTextBlock.Text =
                    "STORE LICENCE REQUIRES BACKOFFICE";

                StatusMessageTextBlock.Text =
                    "The store licence is missing, expired, invalid, or revoked. " +
                    "Renew or import the store licence from Advanced POS BackOffice " +
                    "on the server computer. This window remains available for " +
                    "machine-code and terminal-licence recovery.";

                SetActionStatus(
                    "Cashier sales remain locked until both store and terminal " +
                    "licences are valid.");
            }
            else
            {
                StatusPanel.Style =
                    (Style)FindResource(
                        "CashierSummaryDangerPanel");

                StatusTitleTextBlock.Text =
                    "TERMINAL LICENCE REQUIRED";

                StatusMessageTextBlock.Text =
                    summary.TerminalLicenseStatus ==
                        LicenseStatus.ExpiredReadOnly
                        ? "This terminal licence has expired. Copy or save the " +
                          "licence request, obtain a renewed signed terminal " +
                          "licence, and import it here."
                        : "This computer does not have a valid terminal licence. " +
                          "Copy or save the licence request, then import the " +
                          "signed terminal licence created for this exact machine.";

                SetActionStatus(
                    "A valid signed terminal licence is required before login.");
            }
        }

        private void CopyMachineCode_Click(
            object sender,
            RoutedEventArgs e)
        {
            CopyText(
                _summary.CurrentMachineCode,
                "Machine code copied to the clipboard.",
                "The machine code is not available.");
        }

        private void CopyLicenseRequest_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_requestInfo == null)
            {
                SetActionStatus(
                    "The terminal licence request is not available yet.");
                return;
            }

            CopyText(
                _requestInfo.BuildRequestText(),
                "Terminal licence request copied to the clipboard.",
                "The terminal licence request is empty.");
        }

        private void SaveLicenseRequest_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_requestInfo == null)
            {
                SetActionStatus(
                    "The terminal licence request is not available yet.");
                return;
            }

            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Save Terminal Licence Request",
                    FileName =
                        _requestInfo
                            .BuildSuggestedFileName(),
                    DefaultExt = ".txt",
                    Filter =
                        "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    AddExtension = true,
                    OverwritePrompt = true
                };

                if (dialog.ShowDialog(this) != true)
                    return;

                File.WriteAllText(
                    dialog.FileName,
                    _requestInfo.BuildRequestText());

                SetActionStatus(
                    $"Terminal licence request saved to {dialog.FileName}");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Save terminal licence request",
                    ex);

                SetActionStatus(
                    "The terminal licence request could not be saved. " +
                    "Technical details were saved in the local POS Logs folder.");
            }
        }

        private async void ImportLicense_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isBusy)
                return;

            var dialog = new OpenFileDialog
            {
                Title = "Import Advanced POS Terminal Licence",
                Filter =
                    "POS Licence Files (*.poslic)|*.poslic|All Files (*.*)|*.*",
                Multiselect = false,
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) != true)
                return;

            try
            {
                SetBusy(true);
                SetActionStatus(
                    "Validating and importing the terminal licence...");

                await _licenseManagerService
                    .ImportTerminalLicenseFileAsync(
                        dialog.FileName,
                        $"Cashier activation - {Environment.UserName}");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Import terminal licence",
                    ex);

                SetActionStatus(
                    BuildFriendlyImportFailure(
                        ex));
                return;
            }
            finally
            {
                SetBusy(false);
            }

            await RefreshAsync(
                "The terminal licence was imported and verified successfully.");
        }

        private async void CheckAgain_Click(
            object sender,
            RoutedEventArgs e)
        {
            await RefreshAsync();
        }

        private void ConfigureConnection_Click(
            object sender,
            RoutedEventArgs e)
        {
            CashierConfigurationLauncher.TryLaunch(
                out string message);

            SetActionStatus(message);
        }

        private void Continue_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!ActivationCompleted)
            {
                SetActionStatus(
                    "A valid store licence and terminal licence are required.");
                return;
            }

            DialogResult = true;
        }

        private void Exit_Click(
            object sender,
            RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void CopyText(
            string? value,
            string successMessage,
            string missingMessage)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value == "-")
            {
                SetActionStatus(missingMessage);
                return;
            }

            try
            {
                Clipboard.SetText(value);
                SetActionStatus(successMessage);
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Copy licence recovery information",
                    ex);

                SetActionStatus(
                    "The information could not be copied. Technical details " +
                    "were saved in the local POS Logs folder.");
            }
        }

        private void SetBusy(bool value)
        {
            _isBusy = value;
            ImportLicenseButton.IsEnabled = !value;
            ContinueButton.IsEnabled =
                !value && ActivationCompleted;
        }

        private void SetActionStatus(string message)
        {
            ActionStatusTextBlock.Text =
                string.IsNullOrWhiteSpace(message)
                    ? "Ready."
                    : message;
        }

        private static string BuildFriendlyImportFailure(
            Exception exception)
        {
            string message =
                exception.Message ??
                string.Empty;

            if (message.Contains(
                    "not a terminal licence",
                    StringComparison.OrdinalIgnoreCase))
            {
                return message;
            }

            if (message.Contains(
                    "different computer",
                    StringComparison.OrdinalIgnoreCase) ||
                message.Contains(
                    "machine '",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "The selected licence was created for a different " +
                       "computer. Generate a new terminal licence using the " +
                       "machine code displayed in this window.";
            }

            if (message.Contains(
                    "this license is for terminal",
                    StringComparison.OrdinalIgnoreCase))
            {
                return message;
            }

            if (message.Contains(
                    "Store ID",
                    StringComparison.OrdinalIgnoreCase) ||
                message.Contains(
                    "store licence",
                    StringComparison.OrdinalIgnoreCase) ||
                message.Contains(
                    "store license",
                    StringComparison.OrdinalIgnoreCase))
            {
                return message;
            }

            if (message.Contains(
                    "signature",
                    StringComparison.OrdinalIgnoreCase) ||
                message.Contains(
                    "signing key",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "The selected licence is invalid, damaged, modified, " +
                       "or was not created with the configured production " +
                       "signing key. The existing licence was not replaced.";
            }

            return "The terminal licence could not be imported. The existing " +
                   "licence was not replaced. Technical details were saved in " +
                   "the local POS Logs folder.";
        }

        private static string BuildStatusText(
            LicenseStatus status,
            DateTime? expiryDate)
        {
            string statusText =
                LicenseRequestInfo
                    .ToStatusText(status);

            return expiryDate.HasValue
                ? $"{statusText} - {expiryDate.Value:yyyy-MM-dd}"
                : statusText;
        }

        private static bool IsOperational(
            LicenseStatus status)
        {
            return status == LicenseStatus.Active ||
                   status == LicenseStatus.ExpiringSoon;
        }

        private static string Display(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "-"
                : value.Trim();
        }
    }
}
