using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using POS.Core.Models.Licensing;
using POS.Core.Services;
using POS.Core.Services.Licensing;
using LicensingStatus = POS.Core.Models.Licensing.LicenseStatus;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class LicenseManagementViewModel : ObservableObject
    {
        private readonly LicenseManagerService _licenseManagerService;

        public LicenseManagementViewModel(LicenseManagerService licenseManagerService)
        {
            _licenseManagerService = licenseManagerService;

            _ = LoadAsync();
        }

        // =========================================================
        // STORE LICENSE DISPLAY
        // =========================================================

        [ObservableProperty]
        private string _storeId = "-";

        [ObservableProperty]
        private string _currentStoreName = "-";

        [ObservableProperty]
        private string _licensedStoreName = "-";

        [ObservableProperty]
        private string _storeNameIdentityMessage =
            "Store Settings is the operational name used by POS.";

        [ObservableProperty]
        private string _storeNameIdentityColor = "#64748B";

        [ObservableProperty]
        private string _licenseId = "-";

        [ObservableProperty]
        private string _licenseStatus = "Missing";

        [ObservableProperty]
        private string _expiryDate = "-";

        [ObservableProperty]
        private string _daysRemaining = "-";

        [ObservableProperty]
        private string _storeLicenseStatusColor = "#EF4444";

        // =========================================================
        // CURRENT MACHINE / TERMINAL
        // =========================================================

        [ObservableProperty]
        private string _currentMachineCode = "-";

        [ObservableProperty]
        private string _currentMachineName = "-";

        [ObservableProperty]
        private string _currentTerminalNo = "-";

        // =========================================================
        // TERMINAL LICENSE DISPLAY
        // =========================================================

        [ObservableProperty]
        private string _terminalLicenseId = "-";

        [ObservableProperty]
        private string _terminalLicenseStatus = "Missing";

        [ObservableProperty]
        private string _terminalExpiryDate = "-";

        [ObservableProperty]
        private string _terminalDaysRemaining = "-";

        [ObservableProperty]
        private string _terminalLicenseStatusColor = "#EF4444";

        // =========================================================
        // OVERALL DISPLAY
        // =========================================================

        [ObservableProperty]
        private string _overallStatus = "Missing";

        [ObservableProperty]
        private string _overallStatusColor = "#EF4444";

        [ObservableProperty]
        private string _statusMessage = "Loading license information...";

        [ObservableProperty]
        private string _statusColor = "#64748B";

        [ObservableProperty]
        private bool _canRunBackOffice;

        [ObservableProperty]
        private bool _canRunCashier;

        [ObservableProperty]
        private bool _isReadOnlyMode;

        [ObservableProperty]
        private bool _isBusy;

        // =========================================================
        // COMMANDS
        // =========================================================

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                SetStatus("Loading license information...", "#3B82F6");

                LicenseSummary summary = await _licenseManagerService.GetCurrentLicenseSummaryAsync();

                ApplySummary(summary);

                SetStatus(summary.StatusMessage, summary.StatusColor);
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to load license information: {ex.Message}", "#EF4444");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadAsync();
        }

        [RelayCommand]
        private async Task ImportLicenseAsync()
        {
            if (IsBusy)
                return;

            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Select License File",
                    Filter = "POS License Files (*.poslic)|*.poslic|All Files (*.*)|*.*",
                    Multiselect = false,
                    CheckFileExists = true
                };

                bool? result = dialog.ShowDialog();

                if (result != true)
                    return;

                IsBusy = true;
                SetStatus("Importing license file...", "#3B82F6");

                await _licenseManagerService.ImportLicenseFileAsync(
                    dialog.FileName,
                    "BackOffice");

                SetStatus("License imported successfully.", "#10B981");

                await LoadAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"License import failed: {ex.Message}", "#EF4444");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void CopyMachineCode()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CurrentMachineCode) ||
                    CurrentMachineCode == "-")
                {
                    SetStatus("Machine code is not available yet.", "#EF4444");
                    return;
                }

                Clipboard.SetText(CurrentMachineCode);

                SetStatus("Machine code copied to clipboard.", "#10B981");
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to copy machine code: {ex.Message}", "#EF4444");
            }
        }

        [RelayCommand]
        private async Task CopyStoreLicenseRequestAsync()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;

                LicenseRequestInfo request =
                    await _licenseManagerService
                        .GetCurrentStoreLicenseRequestAsync();

                Clipboard.SetText(
                    request.BuildRequestText());

                SetStatus(
                    "Store licence request copied. Store Settings supplied the current store name.",
                    "#10B981");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Copy store licence request",
                    ex);

                SetStatus(
                    "The store licence request could not be copied. Technical details were saved in the local POS Logs folder.",
                    "#EF4444");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task CopyTerminalLicenseRequestAsync()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;

                LicenseRequestInfo request =
                    await _licenseManagerService
                        .GetCurrentTerminalLicenseRequestAsync();

                if (string.IsNullOrWhiteSpace(
                        request.StoreId))
                {
                    throw new InvalidOperationException(
                        "Import the store licence first. A terminal licence must use the permanent Store ID from the active store licence.");
                }

                Clipboard.SetText(
                    request.BuildRequestText());

                SetStatus(
                    "Terminal licence request copied. Store Settings supplied the current store name.",
                    "#10B981");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Copy terminal licence request",
                    ex);

                SetStatus(
                    $"The terminal licence request could not be copied: {ex.Message}",
                    "#EF4444");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // MAPPING
        // =========================================================

        private void ApplySummary(LicenseSummary summary)
        {
            StoreId = ToDisplayText(summary.StoreId);
            CurrentStoreName = ToDisplayText(
                summary.CurrentStoreName);
            LicensedStoreName = ToDisplayText(
                summary.LicensedStoreName);

            if (summary.StoreNameDiffersFromLicence)
            {
                StoreNameIdentityMessage =
                    "The signed licence contains an older store name. POS operations and new licence requests use the current Store Settings name.";
                StoreNameIdentityColor = "#B45309";
            }
            else if (string.IsNullOrWhiteSpace(
                         summary.LicensedStoreName))
            {
                StoreNameIdentityMessage =
                    "No signed store name is installed yet. Create the request from this page so the License Generator receives the current Store Settings name.";
                StoreNameIdentityColor = "#1D4ED8";
            }
            else
            {
                StoreNameIdentityMessage =
                    "The current Store Settings name matches the signed licence name.";
                StoreNameIdentityColor = "#15803D";
            }

            LicenseId = ToDisplayText(summary.StoreLicenseId);
            LicenseStatus = ToStatusText(summary.StoreLicenseStatus);
            ExpiryDate = ToDateText(summary.StoreExpiryDate);
            DaysRemaining = ToDaysText(summary.StoreDaysRemaining);
            StoreLicenseStatusColor = GetStatusColor(summary.StoreLicenseStatus);

            CurrentMachineCode = ToDisplayText(summary.CurrentMachineCode);
            CurrentMachineName = ToDisplayText(summary.CurrentMachineName);
            CurrentTerminalNo = ToDisplayText(summary.CurrentTerminalNo);

            TerminalLicenseId = ToDisplayText(summary.TerminalLicenseId);
            TerminalLicenseStatus = ToStatusText(summary.TerminalLicenseStatus);
            TerminalExpiryDate = ToDateText(summary.TerminalExpiryDate);
            TerminalDaysRemaining = ToDaysText(summary.TerminalDaysRemaining);
            TerminalLicenseStatusColor = GetStatusColor(summary.TerminalLicenseStatus);

            OverallStatus = ToStatusText(summary.OverallStatus);
            OverallStatusColor = GetStatusColor(summary.OverallStatus);

            CanRunBackOffice = summary.CanRunBackOffice;
            CanRunCashier = summary.CanRunCashier;
            IsReadOnlyMode = summary.IsReadOnlyMode;
        }

        private void SetStatus(string message, string color)
        {
            StatusMessage = string.IsNullOrWhiteSpace(message)
                ? "Ready."
                : message;

            StatusColor = string.IsNullOrWhiteSpace(color)
                ? "#64748B"
                : color;
        }

        private static string ToDisplayText(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "-"
                : value.Trim();
        }

        private static string ToDateText(DateTime? date)
        {
            return date.HasValue
                ? date.Value.ToString("yyyy-MM-dd")
                : "-";
        }

        private static string ToDaysText(int days)
        {
            if (days > 0)
                return $"{days} day(s)";

            if (days == 0)
                return "0 day(s)";

            return $"{Math.Abs(days)} day(s) expired";
        }

        private static string ToStatusText(LicensingStatus status)
        {
            return status switch
            {
                LicensingStatus.Missing => "Missing",
                LicensingStatus.Active => "Active",
                LicensingStatus.ExpiringSoon => "Expiring Soon",
                LicensingStatus.GracePeriod => "Expired",
                LicensingStatus.ExpiredReadOnly => "Expired / Cashier Locked",
                LicensingStatus.Invalid => "Invalid",
                LicensingStatus.Revoked => "Revoked",
                _ => "Unknown"
            };
        }

        private static string GetStatusColor(LicensingStatus status)
        {
            return status switch
            {
                LicensingStatus.Active => "#10B981",
                LicensingStatus.ExpiringSoon => "#F59E0B",
                LicensingStatus.GracePeriod => "#F97316",
                LicensingStatus.ExpiredReadOnly => "#EF4444",
                LicensingStatus.Invalid => "#EF4444",
                LicensingStatus.Missing => "#EF4444",
                LicensingStatus.Revoked => "#EF4444",
                _ => "#64748B"
            };
        }
    }
}