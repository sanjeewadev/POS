using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models.Licensing;
using POS.Core.Models.Terminals;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Services.Licensing;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class TerminalManagementViewModel :
        ObservableObject
    {
        private readonly TerminalManagementRepository
            _repository;

        private readonly AuthService
            _authService;

        private readonly LicenseManagerService
            _licenseManagerService;

        public TerminalManagementViewModel(
            TerminalManagementRepository
                repository,
            AuthService authService,
            LicenseManagerService licenseManagerService)
        {
            _repository = repository;
            _authService = authService;
            _licenseManagerService = licenseManagerService;

            Terminals =
                new ObservableCollection<
                    RegisteredTerminalSummary>();

            IsAdministrator =
                _authService.IsAdmin;
        }

        public ObservableCollection<
            RegisteredTerminalSummary>
            Terminals { get; }

        [ObservableProperty]
        private RegisteredTerminalSummary?
            _selectedTerminal;

        [ObservableProperty]
        private bool _isAdministrator;

        [ObservableProperty]
        private bool _isBusy;

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
        private string _currentMachineText = "-";

        [ObservableProperty]
        private string _terminalStatusText = "-";

        [ObservableProperty]
        private string _licenseStatusText = "-";

        [ObservableProperty]
        private string _licenseExpiryText = "-";

        [ObservableProperty]
        private string _registeredAtText = "-";

        [ObservableProperty]
        private string _updatedAtText = "-";

        [ObservableProperty]
        private string _updatedByText = "-";

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        [ObservableProperty]
        private string _statusColor = "#666666";

        [ObservableProperty]
        private string _registeredSummary = "0";

        [ObservableProperty]
        private string _enabledSummary = "0";

        [ObservableProperty]
        private string _readySummary = "0";

        [ObservableProperty]
        private string _attentionSummary = "0";

        [ObservableProperty]
        private string _lastSeenText = "-";

        public bool HasSelectedTerminal =>
            SelectedTerminal != null;

        public bool CanManage =>
            IsAdministrator &&
            !IsBusy;

        public bool CanManageSelected =>
            CanManage &&
            HasSelectedTerminal;

        public bool CanEnableSelected =>
            CanManageSelected &&
            SelectedTerminal?.IsActive == false;

        public bool CanDisableSelected =>
            CanManageSelected &&
            SelectedTerminal?.IsActive == true;

        public bool CanReleaseSelected =>
            CanManageSelected &&
            SelectedTerminal?.HasMachineAssignment == true;

        public bool CanCopyLicenseRequest =>
            HasSelectedTerminal &&
            SelectedTerminal?.HasMachineAssignment == true &&
            !IsBusy;

        partial void OnSelectedTerminalChanged(
            RegisteredTerminalSummary? value)
        {
            ApplySelectedTerminal(value);

            OnPropertyChanged(
                nameof(HasSelectedTerminal));

            OnPropertyChanged(
                nameof(CanManageSelected));

            OnPropertyChanged(nameof(CanEnableSelected));
            OnPropertyChanged(nameof(CanDisableSelected));
            OnPropertyChanged(nameof(CanReleaseSelected));
            OnPropertyChanged(nameof(CanCopyLicenseRequest));
        }

        partial void OnIsAdministratorChanged(
            bool value)
        {
            OnPropertyChanged(
                nameof(CanManage));

            OnPropertyChanged(
                nameof(CanManageSelected));

            OnPropertyChanged(nameof(CanEnableSelected));
            OnPropertyChanged(nameof(CanDisableSelected));
            OnPropertyChanged(nameof(CanReleaseSelected));
        }

        partial void OnIsBusyChanged(
            bool value)
        {
            OnPropertyChanged(
                nameof(CanManage));

            OnPropertyChanged(
                nameof(CanManageSelected));

            OnPropertyChanged(nameof(CanEnableSelected));
            OnPropertyChanged(nameof(CanDisableSelected));
            OnPropertyChanged(nameof(CanReleaseSelected));
            OnPropertyChanged(nameof(CanCopyLicenseRequest));
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy)
                return;

            int selectedId =
                SelectedTerminal?.Id ?? 0;

            try
            {
                IsBusy = true;

                IsAdministrator =
                    _authService.IsAdmin;

                SetStatus(
                    "Loading registered terminals...",
                    "#2B5B84");

                var rows =
                    await _repository
                        .GetAllAsync();

                Terminals.Clear();

                foreach (RegisteredTerminalSummary
                         row in rows)
                {
                    Terminals.Add(row);
                }

                SelectedTerminal =
                    Terminals.FirstOrDefault(
                        terminal => terminal.Id == selectedId)
                    ?? Terminals.FirstOrDefault(
                        terminal => terminal.IsCurrentMachine)
                    ?? Terminals.FirstOrDefault();

                ApplyFleetSummary();

                if (!IsAdministrator)
                {
                    SetStatus(
                        "Only an Administrator can change terminal records.",
                        "#B91C1C");
                }
                else if (Terminals.Count == 0)
                {
                    SetStatus(
                        "No Cashier terminals are registered. Open Terminal Settings on the computer that will run Cashier.",
                        "#C05A00");
                }
                else
                {
                    SetStatus(
                        $"{Terminals.Count} Cashier terminal record(s) loaded.",
                        "#008000");
                }
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Terminal Management load",
                    ex);

                SetStatus(
                    "Terminal records could not be loaded. " +
                    "Technical details were saved in the local POS Logs folder.",
                    "#B91C1C");
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
        private async Task SaveTerminalNameAsync()
        {
            if (!EnsureSelectedTerminal() ||
                !EnsureAdministrator() ||
                IsBusy)
            {
                return;
            }

            string safeName =
                (TerminalName ??
                 string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(
                    safeName))
            {
                SetStatus(
                    "Terminal name is required.",
                    "#B91C1C");
                return;
            }

            try
            {
                IsBusy = true;

                int terminalId =
                    SelectedTerminal!.Id;

                await _repository.RenameAsync(
                    terminalId,
                    safeName,
                    GetCurrentUserName());

                await ReloadAndSelectAsync(
                    terminalId);

                SetStatus(
                    "Terminal name saved.",
                    "#008000");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Rename terminal",
                    ex);

                SetStatus(
                    $"Terminal name could not be saved: " +
                    $"{ex.Message}",
                    "#B91C1C");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task EnableTerminalAsync()
        {
            if (!EnsureSelectedTerminal() ||
                !EnsureAdministrator())
            {
                return;
            }

            await ChangeActiveStatusAsync(
                true);
        }

        [RelayCommand]
        private async Task DisableTerminalAsync()
        {
            if (!EnsureSelectedTerminal() ||
                !EnsureAdministrator())
            {
                return;
            }

            string warning =
                SelectedTerminal!.IsCurrentMachine
                    ? "Disable this terminal?\n\n" +
                      "This is the current computer. " +
                      "Cashier will be blocked the next time it starts."
                    : "Disable this terminal?\n\n" +
                      "Cashier will be blocked on that terminal " +
                      "the next time it starts.";

            MessageBoxResult confirmation =
                MessageBox.Show(
                    warning,
                    "Disable Terminal",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (confirmation !=
                MessageBoxResult.Yes)
            {
                return;
            }

            await ChangeActiveStatusAsync(
                false);
        }

        [RelayCommand]
        private async Task ReleaseMachineAssignmentAsync()
        {
            if (!EnsureSelectedTerminal() ||
                !EnsureAdministrator() ||
                IsBusy)
            {
                return;
            }

            if (SelectedTerminal!.HasMachineAssignment == false)
            {
                SetStatus(
                    "The selected terminal has no machine assignment.",
                    "#C05A00");
                return;
            }

            string warning =
                $"Release the machine assignment for Terminal {SelectedTerminal.TerminalNo}?\n\n" +
                $"Assigned computer: {SelectedTerminal.MachineNameDisplay}\n\n" +
                "Historical sales will be preserved. The terminal cannot be " +
                "released while it has an open shift or an active/held cart. " +
                "The replacement computer will require a new machine-bound licence.";

            MessageBoxResult confirmation =
                MessageBox.Show(
                    warning,
                    "Release Terminal Machine",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
                return;

            try
            {
                IsBusy = true;
                int terminalId = SelectedTerminal.Id;

                SetStatus(
                    "Releasing the selected terminal machine assignment...",
                    "#2B5B84");

                await _repository.ReleaseMachineAssignmentAsync(
                    terminalId,
                    GetCurrentUserName());

                await ReloadAndSelectAsync(terminalId);

                SetStatus(
                    "Machine assignment released. The terminal is ready for a replacement computer.",
                    "#008000");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Release terminal machine assignment",
                    ex);

                SetStatus(
                    $"Machine assignment could not be released: {ex.Message}",
                    "#B91C1C");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void CopyMachineCode()
        {
            if (!EnsureSelectedTerminal())
                return;

            try
            {
                if (string.IsNullOrWhiteSpace(
                        SelectedTerminal!
                            .MachineCode) ||
                    SelectedTerminal
                        .MachineCode == "-")
                {
                    SetStatus(
                        "The selected terminal has no machine code.",
                        "#B91C1C");
                    return;
                }

                Clipboard.SetText(
                    SelectedTerminal
                        .MachineCode);

                SetStatus(
                    "Machine code copied.",
                    "#008000");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Copy terminal machine code",
                    ex);

                SetStatus(
                    "The machine code could not be copied.",
                    "#B91C1C");
            }
        }

        [RelayCommand]
        private async Task CopyLicenseRequestAsync()
        {
            if (!EnsureSelectedTerminal() ||
                IsBusy)
            {
                return;
            }

            if (SelectedTerminal!.HasMachineAssignment == false)
            {
                SetStatus(
                    "The selected terminal has no machine assignment.",
                    "#B91C1C");
                return;
            }

            try
            {
                IsBusy = true;

                LicenseSummary storeSummary =
                    await _licenseManagerService
                        .GetCurrentLicenseSummaryAsync();

                LicenseRequestInfo request =
                    LicenseRequestService
                        .CreateTerminalRequest(
                            storeSummary.StoreId,
                            storeSummary.CurrentStoreName,
                            storeSummary.CurrentLegalName,
                            SelectedTerminal.TerminalNo,
                            SelectedTerminal.TerminalName,
                            SelectedTerminal.MachineName,
                            SelectedTerminal.MachineCode,
                            SelectedTerminal.LicenseStatus,
                            SelectedTerminal.LicenseExpiryDate);

                Clipboard.SetText(
                    request.BuildRequestText());

                SetStatus(
                    "Terminal licence request copied.",
                    "#008000");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Copy terminal licence request",
                    ex);

                SetStatus(
                    "The terminal licence request could not be copied. " +
                    "Technical details were saved in the local POS Logs folder.",
                    "#B91C1C");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ChangeActiveStatusAsync(
            bool isActive)
        {
            if (IsBusy ||
                SelectedTerminal == null)
            {
                return;
            }

            try
            {
                IsBusy = true;

                int terminalId =
                    SelectedTerminal.Id;

                await _repository
                    .SetActiveStatusAsync(
                        terminalId,
                        isActive,
                        GetCurrentUserName());

                await ReloadAndSelectAsync(
                    terminalId);

                SetStatus(
                    isActive
                        ? "Terminal enabled."
                        : "Terminal disabled.",
                    isActive
                        ? "#008000"
                        : "#C05A00");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Change terminal status",
                    ex);

                SetStatus(
                    $"Terminal status could not be changed: " +
                    $"{ex.Message}",
                    "#B91C1C");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ReloadAndSelectAsync(
            int terminalId)
        {
            var rows =
                await _repository
                    .GetAllAsync();

            Terminals.Clear();

            foreach (RegisteredTerminalSummary
                     row in rows)
            {
                Terminals.Add(row);
            }

            SelectedTerminal =
                Terminals.FirstOrDefault(
                    terminal => terminal.Id == terminalId)
                ?? Terminals.FirstOrDefault();

            ApplyFleetSummary();
        }

        private void ApplySelectedTerminal(
            RegisteredTerminalSummary?
                terminal)
        {
            if (terminal == null)
            {
                TerminalNo = "-";
                TerminalName = string.Empty;
                MachineName = "-";
                MachineCode = "-";
                CurrentMachineText = "-";
                TerminalStatusText = "-";
                LicenseStatusText = "-";
                LicenseExpiryText = "-";
                RegisteredAtText = "-";
                UpdatedAtText = "-";
                UpdatedByText = "-";
                LastSeenText = "-";
                return;
            }

            TerminalNo =
                terminal.TerminalNo;

            TerminalName =
                terminal.TerminalName;

            MachineName =
                terminal.MachineNameDisplay;

            MachineCode =
                terminal.MachineCodeDisplay;

            CurrentMachineText =
                terminal.CurrentMachineText;

            TerminalStatusText =
                terminal.ActiveStatusText;

            LicenseStatusText =
                terminal.LicenseStatusText;

            LicenseExpiryText =
                terminal.LicenseExpiryDateText;

            RegisteredAtText =
                terminal.RegisteredAtText;

            UpdatedAtText =
                terminal.UpdatedAtText;

            UpdatedByText =
                string.IsNullOrWhiteSpace(terminal.UpdatedBy)
                    ? "-"
                    : terminal.UpdatedBy;

            LastSeenText = terminal.LastSeenText;
        }

        private void ApplyFleetSummary()
        {
            int registered = Terminals.Count;
            int enabled = Terminals.Count(item => item.IsActive);
            int ready = Terminals.Count(item => item.IsReady);
            int attention = Terminals.Count(item => item.RequiresAttention);

            RegisteredSummary = registered.ToString();
            EnabledSummary = enabled.ToString();
            ReadySummary = ready.ToString();
            AttentionSummary = attention.ToString();
        }

        private bool EnsureAdministrator()
        {
            if (_authService.IsAdmin)
                return true;

            SetStatus(
                "Only an Administrator can change terminal records.",
                "#B91C1C");

            return false;
        }

        private bool EnsureSelectedTerminal()
        {
            if (SelectedTerminal != null)
                return true;

            SetStatus(
                "Select a terminal first.",
                "#B91C1C");

            return false;
        }

        private string GetCurrentUserName()
        {
            return string.IsNullOrWhiteSpace(
                _authService.CurrentUser
                    ?.Username)
                ? "Administrator"
                : _authService.CurrentUser
                    .Username;
        }

        private void SetStatus(
            string message,
            string color)
        {
            StatusMessage =
                string.IsNullOrWhiteSpace(
                    message)
                    ? "Ready."
                    : message;

            StatusColor =
                string.IsNullOrWhiteSpace(
                    color)
                    ? "#666666"
                    : color;
        }
    }
}
