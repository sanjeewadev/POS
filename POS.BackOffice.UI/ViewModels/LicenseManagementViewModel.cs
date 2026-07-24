using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using POS.Core.Models.Licensing;
using POS.Core.Models.Terminals;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Services.Licensing;
using LicensingStatus = POS.Core.Models.Licensing.LicenseStatus;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class LicenseManagementViewModel : ObservableObject
    {
        private readonly LicenseManagerService _licenseManagerService;
        private readonly TerminalManagementRepository _terminalRepository;
        private readonly AuthService _authService;

        public LicenseManagementViewModel(
            LicenseManagerService licenseManagerService,
            TerminalManagementRepository terminalRepository,
            AuthService authService)
        {
            _licenseManagerService = licenseManagerService;
            _terminalRepository = terminalRepository;
            _authService = authService;
            CashierTerminals = new ObservableCollection<RegisteredTerminalSummary>();

            _ = LoadAsync();
        }

        public ObservableCollection<RegisteredTerminalSummary>
            CashierTerminals { get; }

        [ObservableProperty]
        private RegisteredTerminalSummary? _selectedTerminal;

        [ObservableProperty]
        private string _storeId = "-";

        [ObservableProperty]
        private string _currentStoreName = "-";

        [ObservableProperty]
        private string _licensedStoreName = "-";

        [ObservableProperty]
        private string _storeNameIdentityMessage =
            "Store Settings supplies the operational store name.";

        [ObservableProperty]
        private string _storeNameIdentityColor = "#64748B";

        [ObservableProperty]
        private string _storeLicenseId = "-";

        [ObservableProperty]
        private string _storeLicenseStatus = "Missing";

        [ObservableProperty]
        private string _storeExpiryDate = "-";

        [ObservableProperty]
        private string _storeDaysRemaining = "-";

        [ObservableProperty]
        private string _storeLicenseStatusColor = "#EF4444";

        [ObservableProperty]
        private string _fleetStatus = "No Cashier terminals are registered.";

        [ObservableProperty]
        private string _fleetStatusColor = "#64748B";

        [ObservableProperty]
        private string _backOfficeStatus = "Available";

        [ObservableProperty]
        private string _currentComputerRole = "BackOffice only";

        [ObservableProperty]
        private string _currentComputerRequirement =
            "A terminal licence is not required on this computer.";

        [ObservableProperty]
        private string _selectedTerminalNo = "-";

        [ObservableProperty]
        private string _selectedTerminalName = "-";

        [ObservableProperty]
        private string _selectedMachineName = "-";

        [ObservableProperty]
        private string _selectedMachineCode = "-";

        [ObservableProperty]
        private string _selectedRegistrationStatus = "-";

        [ObservableProperty]
        private string _selectedLicenseId = "-";

        [ObservableProperty]
        private string _selectedLicenseStatus = "-";

        [ObservableProperty]
        private string _selectedLicenseExpiry = "-";

        [ObservableProperty]
        private string _selectedLastSeen = "-";

        [ObservableProperty]
        private string _selectedLicenseStatusColor = "#64748B";

        [ObservableProperty]
        private string _statusMessage = "Loading licence information...";

        [ObservableProperty]
        private string _statusColor = "#64748B";

        [ObservableProperty]
        private bool _isBusy;

        public bool HasSelectedTerminal => SelectedTerminal != null;

        public bool CanUseSelectedTerminalActions =>
            !IsBusy &&
            SelectedTerminal?.HasMachineAssignment == true;

        partial void OnSelectedTerminalChanged(
            RegisteredTerminalSummary? value)
        {
            ApplySelectedTerminal(value);
            OnPropertyChanged(nameof(HasSelectedTerminal));
            OnPropertyChanged(nameof(CanUseSelectedTerminalActions));
        }

        partial void OnIsBusyChanged(bool value)
        {
            OnPropertyChanged(nameof(CanUseSelectedTerminalActions));
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy)
                return;

            int selectedId = SelectedTerminal?.Id ?? 0;

            try
            {
                IsBusy = true;
                SetStatus("Loading store and terminal licence information...", "#3B82F6");

                LicenseSummary summary =
                    await _licenseManagerService.GetCurrentLicenseSummaryAsync();

                var terminals =
                    await _terminalRepository.GetAllAsync();

                ApplyStoreAndComputerSummary(summary);

                CashierTerminals.Clear();
                foreach (RegisteredTerminalSummary terminal in
                         terminals.Where(item => item.IsCashierTerminal))
                {
                    CashierTerminals.Add(terminal);
                }

                SelectedTerminal =
                    CashierTerminals.FirstOrDefault(item => item.Id == selectedId)
                    ?? CashierTerminals.FirstOrDefault(item => item.IsCurrentMachine)
                    ?? CashierTerminals.FirstOrDefault();

                ApplyFleetSummary();
                SetStatus(summary.StatusMessage, summary.StatusColor);
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Load License Management",
                    ex);

                SetStatus(
                    "Licence information could not be loaded. Technical details were saved in the local POS Logs folder.",
                    "#EF4444");
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
        private async Task ImportStoreLicenseAsync()
        {
            string? filePath = SelectLicenseFile("Select Store Licence File");
            if (string.IsNullOrWhiteSpace(filePath) || IsBusy)
                return;

            try
            {
                IsBusy = true;
                SetStatus("Importing store licence...", "#3B82F6");

                await _licenseManagerService.ImportStoreLicenseFileAsync(
                    filePath,
                    GetCurrentUserName());

                SetStatus("Store licence imported successfully.", "#10B981");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Import store licence",
                    ex);

                SetStatus($"Store licence import failed: {ex.Message}", "#EF4444");
                return;
            }
            finally
            {
                IsBusy = false;
            }

            await LoadAsync();
        }

        [RelayCommand]
        private async Task ImportSelectedTerminalLicenseAsync()
        {
            if (!EnsureSelectedTerminalWithMachine() || IsBusy)
                return;

            string? filePath = SelectLicenseFile("Select Terminal Licence File");
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            try
            {
                IsBusy = true;
                SetStatus(
                    $"Importing licence for terminal {SelectedTerminal!.TerminalNo}...",
                    "#3B82F6");

                await _licenseManagerService
                    .ImportTerminalLicenseForRegisteredTerminalAsync(
                        filePath,
                        SelectedTerminal.TerminalNo,
                        SelectedTerminal.MachineCode,
                        GetCurrentUserName());

                SetStatus(
                    $"Terminal {SelectedTerminal.TerminalNo} licence imported successfully.",
                    "#10B981");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Import selected terminal licence",
                    ex);

                SetStatus($"Terminal licence import failed: {ex.Message}", "#EF4444");
                return;
            }
            finally
            {
                IsBusy = false;
            }

            await LoadAsync();
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
                    await _licenseManagerService.GetCurrentStoreLicenseRequestAsync();

                Clipboard.SetText(request.BuildRequestText());
                SetStatus("Store licence request copied.", "#10B981");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Copy store licence request",
                    ex);

                SetStatus($"Store licence request could not be copied: {ex.Message}", "#EF4444");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task CopySelectedTerminalLicenseRequestAsync()
        {
            if (!EnsureSelectedTerminalWithMachine() || IsBusy)
                return;

            try
            {
                IsBusy = true;
                LicenseSummary storeSummary =
                    await _licenseManagerService.GetCurrentLicenseSummaryAsync();

                if (string.IsNullOrWhiteSpace(storeSummary.StoreId))
                {
                    throw new InvalidOperationException(
                        "Import the store licence first. Terminal requests require the permanent Store ID.");
                }

                LicenseRequestInfo request = LicenseRequestService.CreateTerminalRequest(
                    storeSummary.StoreId,
                    storeSummary.CurrentStoreName,
                    storeSummary.CurrentLegalName,
                    SelectedTerminal!.TerminalNo,
                    SelectedTerminal.TerminalName,
                    SelectedTerminal.MachineName,
                    SelectedTerminal.MachineCode,
                    SelectedTerminal.LicenseStatus,
                    SelectedTerminal.LicenseExpiryDate);

                Clipboard.SetText(request.BuildRequestText());
                SetStatus(
                    $"Licence request for terminal {SelectedTerminal.TerminalNo} copied.",
                    "#10B981");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Copy selected terminal licence request",
                    ex);

                SetStatus($"Terminal licence request could not be copied: {ex.Message}", "#EF4444");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void CopySelectedMachineCode()
        {
            if (!EnsureSelectedTerminalWithMachine())
                return;

            try
            {
                Clipboard.SetText(SelectedTerminal!.MachineCode);
                SetStatus("Selected terminal machine code copied.", "#10B981");
            }
            catch (Exception ex)
            {
                SetStatus($"Machine code could not be copied: {ex.Message}", "#EF4444");
            }
        }

        private void ApplyStoreAndComputerSummary(LicenseSummary summary)
        {
            StoreId = Display(summary.StoreId);
            CurrentStoreName = Display(summary.CurrentStoreName);
            LicensedStoreName = Display(summary.LicensedStoreName);
            StoreLicenseId = Display(summary.StoreLicenseId);
            StoreLicenseStatus = StatusText(summary.StoreLicenseStatus);
            StoreExpiryDate = DateText(summary.StoreExpiryDate);
            StoreDaysRemaining = DaysText(summary.StoreDaysRemaining);
            StoreLicenseStatusColor = GetLicenseStatusColor(summary.StoreLicenseStatus);
            BackOfficeStatus = "Available";
            CurrentComputerRole = string.IsNullOrWhiteSpace(summary.CurrentComputerRole)
                ? "BackOffice only"
                : summary.CurrentComputerRole;
            CurrentComputerRequirement = summary.TerminalLicenseRequired
                ? summary.CurrentTerminalEnabled
                    ? "This computer is configured for Cashier and requires its matching terminal licence."
                    : "This computer is configured for Cashier but the terminal is disabled."
                : "This computer is BackOffice-only; a terminal licence is not required here.";

            if (summary.StoreNameDiffersFromLicence)
            {
                StoreNameIdentityMessage =
                    "The signed licence contains an older store name. POS operations and new licence requests use the current Store Settings name.";
                StoreNameIdentityColor = "#B45309";
            }
            else if (string.IsNullOrWhiteSpace(summary.LicensedStoreName))
            {
                StoreNameIdentityMessage =
                    "No signed store name is installed. Copy a store licence request from this page.";
                StoreNameIdentityColor = "#1D4ED8";
            }
            else
            {
                StoreNameIdentityMessage =
                    "The current Store Settings name matches the signed store licence.";
                StoreNameIdentityColor = "#15803D";
            }
        }

        private void ApplyFleetSummary()
        {
            int registered = CashierTerminals.Count;
            int ready = CashierTerminals.Count(item => item.IsReady);
            int attention = CashierTerminals.Count(item => item.RequiresAttention);
            int disabled = CashierTerminals.Count(item => !item.IsActive);

            FleetStatus = LicenseTerminalWorkflowPolicy.BuildFleetSummary(
                registered,
                ready,
                attention,
                disabled);

            FleetStatusColor = attention > 0
                ? "#B45309"
                : registered == 0
                    ? "#64748B"
                    : "#15803D";
        }

        private void ApplySelectedTerminal(RegisteredTerminalSummary? terminal)
        {
            SelectedTerminalNo = terminal?.TerminalNo ?? "-";
            SelectedTerminalName = terminal?.TerminalName ?? "-";
            SelectedMachineName = terminal?.MachineNameDisplay ?? "-";
            SelectedMachineCode = terminal?.MachineCodeDisplay ?? "-";
            SelectedRegistrationStatus = terminal == null
                ? "-"
                : $"{terminal.ActiveStatusText} / {terminal.AssignmentStatusText}";
            SelectedLicenseId = terminal?.LicenseIdDisplay ?? "-";
            SelectedLicenseStatus = terminal?.LicenseStatusText ?? "-";
            SelectedLicenseExpiry = terminal?.LicenseExpiryDateText ?? "-";
            SelectedLastSeen = terminal?.LastSeenText ?? "-";
            SelectedLicenseStatusColor = terminal == null
                ? "#64748B"
                : GetLicenseStatusColor(terminal.LicenseStatus);
        }

        private bool EnsureSelectedTerminalWithMachine()
        {
            if (SelectedTerminal == null)
            {
                SetStatus("Select a Cashier terminal first.", "#EF4444");
                return false;
            }

            if (!SelectedTerminal.HasMachineAssignment)
            {
                SetStatus(
                    "The selected terminal has no computer assignment. Assign it from Terminal Settings on the Cashier computer first.",
                    "#EF4444");
                return false;
            }

            return true;
        }

        private static string? SelectLicenseFile(string title)
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = "POS Licence Files (*.poslic)|*.poslic|All Files (*.*)|*.*",
                Multiselect = false,
                CheckFileExists = true
            };

            return dialog.ShowDialog() == true
                ? dialog.FileName
                : null;
        }

        private string GetCurrentUserName()
        {
            string? username = _authService.CurrentUser?.Username;
            return string.IsNullOrWhiteSpace(username)
                ? "Administrator"
                : username.Trim();
        }

        private void SetStatus(string message, string color)
        {
            StatusMessage = string.IsNullOrWhiteSpace(message) ? "Ready." : message;
            StatusColor = string.IsNullOrWhiteSpace(color) ? "#64748B" : color;
        }

        private static string Display(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

        private static string DateText(DateTime? date) =>
            date.HasValue ? date.Value.ToString("yyyy-MM-dd") : "-";

        private static string DaysText(int days) =>
            days >= 0 ? $"{days} day(s)" : $"{Math.Abs(days)} day(s) expired";

        private static string StatusText(LicensingStatus status) =>
            status switch
            {
                LicensingStatus.Missing => "Missing",
                LicensingStatus.Active => "Active",
                LicensingStatus.ExpiringSoon => "Expiring Soon",
                LicensingStatus.GracePeriod => "Expired",
                LicensingStatus.ExpiredReadOnly => "Expired",
                LicensingStatus.Invalid => "Invalid",
                LicensingStatus.Revoked => "Revoked",
                _ => "Unknown"
            };

        private static string GetLicenseStatusColor(LicensingStatus status) =>
            status switch
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
