using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models.Terminals;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class TerminalManagementViewModel : ObservableObject
    {
        private readonly TerminalManagementRepository _repository;

        private int _terminalId;

        public TerminalManagementViewModel(TerminalManagementRepository repository)
        {
            _repository = repository;

            Terminals = new ObservableCollection<RegisteredTerminalSummary>();

            Locations = new ObservableCollection<string>
            {
                "Main Store",
                "Front Counter",
                "Back Office",
                "Warehouse"
            };

            _ = LoadAsync();
        }

        public ObservableCollection<RegisteredTerminalSummary> Terminals { get; }

        public ObservableCollection<string> Locations { get; }

        [ObservableProperty]
        private RegisteredTerminalSummary? _selectedTerminal;

        partial void OnSelectedTerminalChanged(RegisteredTerminalSummary? value)
        {
            if (value != null)
                ApplySelectedTerminal(value);
        }

        // =========================================================
        // EDIT FIELDS
        // =========================================================

        [ObservableProperty]
        private string _terminalNo = string.Empty;

        [ObservableProperty]
        private string _terminalName = string.Empty;

        [ObservableProperty]
        private string _machineName = string.Empty;

        [ObservableProperty]
        private string _machineCode = string.Empty;

        [ObservableProperty]
        private string _location = "Main Store";

        [ObservableProperty]
        private bool _isCashierTerminal = true;

        [ObservableProperty]
        private bool _isBackOfficeAllowed = true;

        [ObservableProperty]
        private bool _isActive = true;

        [ObservableProperty]
        private string _licenseId = "-";

        [ObservableProperty]
        private string _licenseStatusText = "-";

        [ObservableProperty]
        private string _licenseExpiryDateText = "-";

        [ObservableProperty]
        private string _lastLoginText = "-";

        [ObservableProperty]
        private string _lastSaleText = "-";

        [ObservableProperty]
        private string _remarks = string.Empty;

        // =========================================================
        // STATUS
        // =========================================================

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        [ObservableProperty]
        private string _statusColor = "#64748B";

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
                SetStatus("Loading terminals...", "#3B82F6");

                var rows = await _repository.GetAllAsync();

                Terminals.Clear();

                foreach (var row in rows)
                    Terminals.Add(row);

                SetStatus("Terminal list loaded.", "#10B981");
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to load terminals: {ex.Message}", "#EF4444");
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
        private void NewTerminal()
        {
            SelectedTerminal = null;
            _terminalId = 0;

            TerminalNo = GenerateNextTerminalNo();
            TerminalName = $"Terminal {TerminalNo}";
            MachineName = string.Empty;
            MachineCode = string.Empty;
            Location = "Main Store";
            IsCashierTerminal = true;
            IsBackOfficeAllowed = true;
            IsActive = true;

            LicenseId = "-";
            LicenseStatusText = "Missing";
            LicenseExpiryDateText = "-";
            LastLoginText = "-";
            LastSaleText = "-";
            Remarks = string.Empty;

            SetStatus("Ready to create a new terminal.", "#3B82F6");
        }

        [RelayCommand]
        private async Task RegisterCurrentMachineAsync()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                SetStatus("Registering current machine...", "#3B82F6");

                var savedTerminal = await _repository.RegisterOrUpdateCurrentMachineAsync(
                    isCashierTerminal: true,
                    updatedBy: "BackOffice");

                await LoadAsync();

                SelectedTerminal = Terminals.FirstOrDefault(t => t.Id == savedTerminal.Id);

                SetStatus("Current machine registered successfully.", "#10B981");
            }
            catch (Exception ex)
            {
                SetStatus($"Register current machine failed: {ex.Message}", "#EF4444");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SaveTerminalAsync()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                SetStatus("Saving terminal...", "#3B82F6");

                var terminal = BuildTerminalFromInputs();

                var savedTerminal = await _repository.SaveAsync(
                    terminal,
                    "BackOffice");

                await LoadAsync();

                SelectedTerminal = Terminals.FirstOrDefault(t => t.Id == savedTerminal.Id);

                SetStatus("Terminal saved successfully.", "#10B981");
            }
            catch (Exception ex)
            {
                SetStatus($"Save failed: {ex.Message}", "#EF4444");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ActivateTerminalAsync()
        {
            if (_terminalId <= 0)
            {
                SetStatus("Select a terminal first.", "#EF4444");
                return;
            }

            await SetActiveStatusAsync(true);
        }

        [RelayCommand]
        private async Task DeactivateTerminalAsync()
        {
            if (_terminalId <= 0)
            {
                SetStatus("Select a terminal first.", "#EF4444");
                return;
            }

            await SetActiveStatusAsync(false);
        }

        [RelayCommand]
        private void CopyMachineCode()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(MachineCode) || MachineCode == "-")
                {
                    SetStatus("Machine code is empty.", "#EF4444");
                    return;
                }

                Clipboard.SetText(MachineCode);
                SetStatus("Machine code copied to clipboard.", "#10B981");
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to copy machine code: {ex.Message}", "#EF4444");
            }
        }

        // =========================================================
        // PRIVATE METHODS
        // =========================================================

        private async Task SetActiveStatusAsync(bool active)
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;

                await _repository.SetActiveStatusAsync(
                    _terminalId,
                    active,
                    "BackOffice");

                await LoadAsync();

                SelectedTerminal = Terminals.FirstOrDefault(t => t.Id == _terminalId);

                SetStatus(
                    active ? "Terminal activated." : "Terminal deactivated.",
                    active ? "#10B981" : "#F59E0B");
            }
            catch (Exception ex)
            {
                SetStatus($"Status update failed: {ex.Message}", "#EF4444");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplySelectedTerminal(RegisteredTerminalSummary terminal)
        {
            _terminalId = terminal.Id;

            TerminalNo = terminal.TerminalNo;
            TerminalName = terminal.TerminalName;
            MachineName = terminal.MachineName;
            MachineCode = terminal.MachineCode;
            Location = terminal.Location;
            IsCashierTerminal = terminal.IsCashierTerminal;
            IsBackOfficeAllowed = terminal.IsBackOfficeAllowed;
            IsActive = terminal.IsActive;

            LicenseId = terminal.LicenseIdDisplay;
            LicenseStatusText = terminal.LicenseStatusText;
            LicenseExpiryDateText = terminal.LicenseExpiryDateText;
            LastLoginText = terminal.LastLoginText;
            LastSaleText = terminal.LastSaleText;
            Remarks = string.Empty;
        }

        private RegisteredTerminal BuildTerminalFromInputs()
        {
            return new RegisteredTerminal
            {
                Id = _terminalId,
                TerminalNo = TerminalNo,
                TerminalName = TerminalName,
                MachineName = MachineName,
                MachineCode = MachineCode,
                Location = Location,
                IsCashierTerminal = IsCashierTerminal,
                IsBackOfficeAllowed = IsBackOfficeAllowed,
                IsActive = IsActive,
                Remarks = Remarks
            };
        }

        private string GenerateNextTerminalNo()
        {
            int maxNo = 0;

            foreach (var terminal in Terminals)
            {
                if (int.TryParse(terminal.TerminalNo, out int number))
                    maxNo = Math.Max(maxNo, number);
            }

            int nextNo = maxNo + 1;

            if (nextNo <= 0)
                nextNo = 1;

            return nextNo.ToString("00");
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
    }
}