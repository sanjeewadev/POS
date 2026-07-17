using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Interfaces;
using POS.Core.Models;
using POS.Core.Models.Licensing;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Services.Licensing;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class TerminalSettingsViewModel :
        ObservableObject
    {
        private readonly
            TerminalSettingsRepository
            _terminalSettingsRepository;

        private readonly
            ITerminalHardwareService
            _terminalHardwareService;

        private readonly
            MachineFingerprintService
            _machineFingerprintService;

        private readonly
            LicenseManagerService
            _licenseManagerService;

        private readonly AuthService
            _authService;

        private TerminalSettings?
            _loadedSettings;

        public TerminalSettingsViewModel(
            TerminalSettingsRepository
                terminalSettingsRepository,
            ITerminalHardwareService
                terminalHardwareService,
            MachineFingerprintService
                machineFingerprintService,
            LicenseManagerService
                licenseManagerService,
            AuthService authService)
        {
            _terminalSettingsRepository =
                terminalSettingsRepository;

            _terminalHardwareService =
                terminalHardwareService;

            _machineFingerprintService =
                machineFingerprintService;

            _licenseManagerService =
                licenseManagerService;

            _authService = authService;

            AvailablePrinters =
                new ObservableCollection<string>();

            ReceiptPaperWidths =
                new ObservableCollection<int>
                {
                    58,
                    80
                };

            ReceiptCopyOptions =
                new ObservableCollection<int>
                {
                    1,
                    2,
                    3
                };
        }

        public ObservableCollection<string>
            AvailablePrinters { get; }

        public ObservableCollection<int>
            ReceiptPaperWidths { get; }

        public ObservableCollection<int>
            ReceiptCopyOptions { get; }

        // =====================================================
        // CURRENT TERMINAL
        // =====================================================

        [ObservableProperty]
        private string _terminalNo = "-";

        [ObservableProperty]
        private string _terminalName =
            string.Empty;

        [ObservableProperty]
        private string _machineName = "-";

        [ObservableProperty]
        private string _machineCode = "-";

        [ObservableProperty]
        private string _terminalStatusText = "-";

        [ObservableProperty]
        private string _terminalStatusColor =
            "#666666";

        [ObservableProperty]
        private string _terminalLicenseStatusText =
            "-";

        [ObservableProperty]
        private string _terminalLicenseExpiryText =
            "-";

        // =====================================================
        // RECEIPT PRINTER
        // =====================================================

        [ObservableProperty]
        private string _receiptPrinterName =
            string.Empty;

        [ObservableProperty]
        private int _receiptPaperWidth = 80;

        [ObservableProperty]
        private bool _autoPrintReceipt;

        [ObservableProperty]
        private int _receiptCopies = 1;

        [ObservableProperty]
        private string _printerListStatus =
            "Printer list not loaded.";

        // =====================================================
        // CASH DRAWER
        // =====================================================

        [ObservableProperty]
        private bool _enableCashDrawer;

        [ObservableProperty]
        private bool _openDrawerAfterCashSale;

        // =====================================================
        // CASHIER SECURITY
        // =====================================================

        [ObservableProperty]
        private int _autoLockTimeoutMinutes = 10;

        // =====================================================
        // PAGE STATUS
        // =====================================================

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusMessage =
            "Ready.";

        [ObservableProperty]
        private string _statusColor =
            "#666666";

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;

                SetStatus(
                    "Loading current terminal settings...",
                    "#003366");

                string machineName =
                    _machineFingerprintService
                        .GetMachineName();

                TerminalSettings? settings =
                    await _terminalSettingsRepository
                        .GetByMachineNameAsync(
                            machineName);

                if (settings == null)
                {
                    _loadedSettings = null;
                    ApplyUnassignedState(
                        machineName);
                    RefreshPrinterListCore();

                    SetStatus(
                        "This computer is not assigned as a Cashier terminal. " +
                        "Use the Cashier installer or BackOffice Terminal " +
                        "Management before configuring terminal hardware.",
                        "#B45309");

                    return;
                }

                LicenseSummary licenseSummary =
                    await _licenseManagerService
                        .GetCurrentLicenseSummaryAsync();

                _loadedSettings = settings;

                ApplySettings(
                    settings,
                    licenseSummary);

                RefreshPrinterListCore();

                SetStatus(
                    "Terminal settings loaded.",
                    "#008000");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Load Terminal Settings",
                    ex);

                SetStatus(
                    "Terminal settings could not be loaded. " +
                    "Technical details were saved in the local POS Logs folder.",
                    "#B91C1C");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SaveSettingsAsync()
        {
            if (IsBusy)
                return;

            if (_loadedSettings == null)
            {
                SetStatus(
                    "Load the terminal settings before saving.",
                    "#B91C1C");

                return;
            }

            try
            {
                IsBusy = true;

                SetStatus(
                    "Saving terminal settings...",
                    "#003366");

                ApplyEditableValuesToModel(
                    _loadedSettings);

                TerminalSettings saved =
                    await _terminalSettingsRepository
                        .SaveAsync(
                            _loadedSettings,
                            GetCurrentUserName());

                LicenseSummary licenseSummary =
                    await _licenseManagerService
                        .GetCurrentLicenseSummaryAsync();

                _loadedSettings = saved;

                ApplySettings(
                    saved,
                    licenseSummary);

                RefreshPrinterListCore();

                SetStatus(
                    "Terminal settings saved. " +
                    "Restart Cashier to apply the new settings.",
                    "#008000");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Save Terminal Settings",
                    ex);

                SetStatus(
                    $"Settings were not saved: " +
                    $"{GetFriendlyMessage(ex)}",
                    "#B91C1C");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DiscardChangesAsync()
        {
            await LoadAsync();
        }

        [RelayCommand]
        private void RefreshPrinters()
        {
            try
            {
                RefreshPrinterListCore();

                SetStatus(
                    "Windows printer list refreshed.",
                    "#008000");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Refresh Windows Printers",
                    ex);

                SetStatus(
                    "Windows printers could not be listed. " +
                    "Check the Windows Print Spooler service.",
                    "#B91C1C");
            }
        }

        [RelayCommand]
        private async Task TestPrintAsync()
        {
            if (IsBusy)
                return;

            if (string.IsNullOrWhiteSpace(
                    ReceiptPrinterName))
            {
                SetStatus(
                    "Select a Windows receipt printer first.",
                    "#B91C1C");

                return;
            }

            try
            {
                IsBusy = true;

                SetStatus(
                    "Sending a test receipt...",
                    "#003366");

                await _terminalHardwareService
                    .PrintTestReceiptAsync(
                        ReceiptPrinterName,
                        ReceiptPaperWidth);

                SetStatus(
                    "Test receipt sent to the selected printer.",
                    "#008000");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Terminal Settings Test Print",
                    ex);

                SetStatus(
                    $"Test print failed: " +
                    $"{GetFriendlyMessage(ex)}",
                    "#B91C1C");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task TestDrawerAsync()
        {
            if (IsBusy)
                return;

            if (!EnableCashDrawer)
            {
                SetStatus(
                    "Enable the cash drawer before testing it.",
                    "#B45309");

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    ReceiptPrinterName))
            {
                SetStatus(
                    "Select the receipt printer connected to the drawer.",
                    "#B91C1C");

                return;
            }

            try
            {
                IsBusy = true;

                SetStatus(
                    "Sending the drawer-open test...",
                    "#003366");

                await _terminalHardwareService
                    .OpenCashDrawerAsync(
                        ReceiptPrinterName);

                SetStatus(
                    "Drawer-open command sent.",
                    "#008000");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Terminal Settings Drawer Test",
                    ex);

                SetStatus(
                    $"Drawer test failed: " +
                    $"{GetFriendlyMessage(ex)}",
                    "#B91C1C");
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnEnableCashDrawerChanged(
            bool value)
        {
            if (!value)
                OpenDrawerAfterCashSale = false;
        }

        private void ApplyUnassignedState(
            string machineName)
        {
            TerminalNo = "-";
            TerminalName = string.Empty;
            MachineName = DisplayOrDash(
                machineName);
            MachineCode = DisplayOrDash(
                _machineFingerprintService
                    .GetMachineCode());
            TerminalStatusText = "Not assigned";
            TerminalStatusColor = "#B45309";
            TerminalLicenseStatusText = "Not applicable";
            TerminalLicenseExpiryText = "-";
            ReceiptPrinterName = string.Empty;
            ReceiptPaperWidth = 80;
            AutoPrintReceipt = false;
            ReceiptCopies = 1;
            EnableCashDrawer = false;
            OpenDrawerAfterCashSale = false;
            AutoLockTimeoutMinutes = 10;
        }

        private void ApplySettings(
            TerminalSettings settings,
            LicenseSummary licenseSummary)
        {
            TerminalNo =
                DisplayOrDash(
                    settings.TerminalNo);

            TerminalName =
                settings.TerminalName;

            MachineName =
                DisplayOrDash(
                    _machineFingerprintService
                        .GetMachineName());

            MachineCode =
                DisplayOrDash(
                    _machineFingerprintService
                        .GetMachineCode());

            TerminalStatusText =
                settings.IsActive
                    ? "Active"
                    : "Disabled";

            TerminalStatusColor =
                settings.IsActive
                    ? "#008000"
                    : "#B91C1C";

            TerminalLicenseStatusText =
                licenseSummary
                    .TerminalLicenseStatusText;

            TerminalLicenseExpiryText =
                FormatLicenseExpiry(
                    licenseSummary
                        .TerminalExpiryDate,
                    licenseSummary
                        .TerminalDaysRemaining);

            ReceiptPrinterName =
                settings.ReceiptPrinterName;

            ReceiptPaperWidth =
                settings.ReceiptPaperWidth == 58
                    ? 58
                    : 80;

            AutoPrintReceipt =
                settings.AutoPrintReceipt;

            ReceiptCopies =
                Math.Clamp(
                    settings.ReceiptCopies,
                    1,
                    3);

            EnableCashDrawer =
                settings.EnableCashDrawer;

            OpenDrawerAfterCashSale =
                settings.EnableCashDrawer &&
                settings.OpenDrawerAfterCashSale;

            AutoLockTimeoutMinutes =
                Math.Clamp(
                    settings.AutoLockTimeoutMinutes,
                    0,
                    120);
        }

        private void ApplyEditableValuesToModel(
            TerminalSettings settings)
        {
            settings.TerminalName =
                (TerminalName ??
                 string.Empty).Trim();

            settings.PrinterMode =
                "WindowsSpooler";

            settings.ReceiptPrinterName =
                (ReceiptPrinterName ??
                 string.Empty).Trim();

            settings.ReceiptPaperWidth =
                ReceiptPaperWidth;

            settings.AutoPrintReceipt =
                AutoPrintReceipt;

            settings.ReceiptCopies =
                ReceiptCopies;

            settings.EnableCashDrawer =
                EnableCashDrawer;

            settings.OpenDrawerAfterCashSale =
                EnableCashDrawer &&
                OpenDrawerAfterCashSale;

            settings.AutoLockTimeoutMinutes =
                AutoLockTimeoutMinutes;
        }

        private void RefreshPrinterListCore()
        {
            string selectedPrinter =
                (ReceiptPrinterName ??
                 string.Empty).Trim();

            var printerNames =
                _terminalHardwareService
                    .GetInstalledPrinterNames();

            AvailablePrinters.Clear();

            foreach (string printerName in
                     printerNames)
            {
                AvailablePrinters.Add(
                    printerName);
            }

            if (!string.IsNullOrWhiteSpace(
                    selectedPrinter) &&
                !AvailablePrinters.Any(
                    printer =>
                        string.Equals(
                            printer,
                            selectedPrinter,
                            StringComparison
                                .OrdinalIgnoreCase)))
            {
                AvailablePrinters.Insert(
                    0,
                    selectedPrinter);
            }

            PrinterListStatus =
                printerNames.Count == 0
                    ? "No Windows printers were found."
                    : $"{printerNames.Count} Windows " +
                      $"printer(s) found.";
        }

        private string GetCurrentUserName()
        {
            string? username =
                _authService.CurrentUser
                    ?.Username;

            return string.IsNullOrWhiteSpace(
                username)
                ? "Administrator"
                : username.Trim();
        }

        private static string FormatLicenseExpiry(
            DateTime? expiryDate,
            int daysRemaining)
        {
            if (!expiryDate.HasValue)
                return "-";

            return
                $"{expiryDate.Value:yyyy-MM-dd} " +
                $"({daysRemaining} day(s) remaining)";
        }

        private static string DisplayOrDash(
            string? value)
        {
            return string.IsNullOrWhiteSpace(
                value)
                ? "-"
                : value.Trim();
        }

        private static string GetFriendlyMessage(
            Exception exception)
        {
            if (exception is
                InvalidOperationException)
            {
                return exception.Message;
            }

            return
                "The operation could not be completed. " +
                "Technical details were saved in the local POS Logs folder.";
        }

        private void SetStatus(
            string message,
            string color)
        {
            StatusMessage =
                string.IsNullOrWhiteSpace(
                    message)
                    ? "Ready."
                    : message.Trim();

            StatusColor =
                string.IsNullOrWhiteSpace(
                    color)
                    ? "#666666"
                    : color;
        }
    }
}
