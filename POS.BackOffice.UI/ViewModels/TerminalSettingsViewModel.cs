using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class TerminalSettingsViewModel : ObservableObject
    {
        private readonly TerminalSettingsRepository _repository;

        private int _settingsId;
        private DateTime _createdAt;

        public TerminalSettingsViewModel(TerminalSettingsRepository repository)
        {
            _repository = repository;

            PrinterModes = new ObservableCollection<string>
            {
                "WindowsSpooler",
                "RawComSerial",
                "NetworkEscPos"
            };

            ReceiptPaperWidths = new ObservableCollection<int>
            {
                58,
                80
            };

            ScannerSuffixActions = new ObservableCollection<string>
            {
                "Enter",
                "Tab",
                "None"
            };

            ScaleBaudRates = new ObservableCollection<int>
            {
                9600,
                19200,
                38400,
                57600,
                115200
            };

            ComPorts = new ObservableCollection<string>
            {
                "COM1",
                "COM2",
                "COM3",
                "COM4",
                "COM5",
                "COM6",
                "COM7",
                "COM8",
                "COM9"
            };

            Locations = new ObservableCollection<string>
            {
                "Main Store",
                "Warehouse",
                "Front Counter",
                "Back Office"
            };

            AvailablePrinters = new ObservableCollection<string>();

            _ = LoadAsync();
        }

        public ObservableCollection<string> PrinterModes { get; }

        public ObservableCollection<int> ReceiptPaperWidths { get; }

        public ObservableCollection<string> ScannerSuffixActions { get; }

        public ObservableCollection<int> ScaleBaudRates { get; }

        public ObservableCollection<string> ComPorts { get; }

        public ObservableCollection<string> Locations { get; }

        public ObservableCollection<string> AvailablePrinters { get; }

        // =========================================================
        // TERMINAL IDENTITY
        // =========================================================

        [ObservableProperty]
        private string _terminalNo = "01";

        [ObservableProperty]
        private string _terminalName = "Cashier Terminal 01";

        [ObservableProperty]
        private string _machineName = Environment.MachineName;

        [ObservableProperty]
        private string _location = "Main Store";

        [ObservableProperty]
        private bool _isActive = true;

        // =========================================================
        // RECEIPT PRINTER
        // =========================================================

        [ObservableProperty]
        private string _printerMode = "WindowsSpooler";

        [ObservableProperty]
        private string _receiptPrinterName = "POS-80";

        [ObservableProperty]
        private int _receiptPaperWidth = 80;

        [ObservableProperty]
        private bool _autoPrintReceipt = true;

        [ObservableProperty]
        private int _receiptCopies = 1;

        // =========================================================
        // CASH DRAWER
        // =========================================================

        [ObservableProperty]
        private bool _enableCashDrawer = true;

        [ObservableProperty]
        private string _drawerKickCode = "27,112,0,25,250";

        [ObservableProperty]
        private bool _openDrawerAfterCashSale = true;

        // =========================================================
        // SCANNER
        // =========================================================

        [ObservableProperty]
        private string _scannerSuffixAction = "Enter";

        // =========================================================
        // SCALE
        // =========================================================

        [ObservableProperty]
        private bool _enableScale = false;

        [ObservableProperty]
        private string _scaleComPort = "COM1";

        [ObservableProperty]
        private int _scaleBaudRate = 9600;

        // =========================================================
        // POLE DISPLAY
        // =========================================================

        [ObservableProperty]
        private bool _enablePoleDisplay = false;

        [ObservableProperty]
        private string _poleDisplayComPort = "COM2";

        [ObservableProperty]
        private string _poleWelcomeMessage = "WELCOME";

        // =========================================================
        // EFTPOS
        // =========================================================

        [ObservableProperty]
        private bool _enableEftpos = false;

        [ObservableProperty]
        private string _eftposProvider = string.Empty;

        [ObservableProperty]
        private string _eftposPortOrIp = string.Empty;

        // =========================================================
        // CASHIER SECURITY
        // =========================================================

        [ObservableProperty]
        private int _autoLockTimeoutMinutes = 10;

        // =========================================================
        // STATUS
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        [ObservableProperty]
        private string _statusColor = "#64748B";

        // =========================================================
        // COMPATIBILITY PROPERTIES FOR OLD DRAFT BINDINGS
        // =========================================================

        public string TargetPrinter
        {
            get => ReceiptPrinterName;
            set
            {
                ReceiptPrinterName = value;
                OnPropertyChanged();
            }
        }

        public bool EnableDrawer
        {
            get => EnableCashDrawer;
            set
            {
                EnableCashDrawer = value;
                OnPropertyChanged();
            }
        }

        public string KickCode
        {
            get => DrawerKickCode;
            set
            {
                DrawerKickCode = value;
                OnPropertyChanged();
            }
        }

        public string ScanSuffix
        {
            get => ScannerSuffixAction;
            set
            {
                ScannerSuffixAction = value;
                OnPropertyChanged();
            }
        }

        public string ScalePort
        {
            get => ScaleComPort;
            set
            {
                ScaleComPort = value;
                OnPropertyChanged();
            }
        }

        public string PoleCom
        {
            get => PoleDisplayComPort;
            set
            {
                PoleDisplayComPort = value;
                OnPropertyChanged();
            }
        }

        public string PoleWelcome
        {
            get => PoleWelcomeMessage;
            set
            {
                PoleWelcomeMessage = value;
                OnPropertyChanged();
            }
        }

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
                SetStatus("Loading terminal settings...", "#3B82F6");

                TerminalSettings settings = await _repository.GetOrCreateForCurrentMachineAsync("01");

                ApplySettingsToViewModel(settings);
                RefreshAvailablePrintersList();

                SetStatus("Terminal settings loaded.", "#10B981");
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to load terminal settings: {ex.Message}", "#EF4444");
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

            try
            {
                IsBusy = true;
                SetStatus("Saving terminal settings...", "#3B82F6");

                TerminalSettings settings = BuildSettingsFromViewModel();

                TerminalSettings savedSettings = await _repository.SaveAsync(
                    settings,
                    "BackOffice");

                ApplySettingsToViewModel(savedSettings);
                RefreshAvailablePrintersList();

                SetStatus("Terminal settings saved successfully.", "#10B981");
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
        private async Task DiscardChangesAsync()
        {
            if (IsBusy)
                return;

            await LoadAsync();
        }

        [RelayCommand]
        private void RefreshPrinters()
        {
            RefreshAvailablePrintersList();

            SetStatus(
                "Printer list refreshed. Enter the exact Windows printer name if it is not listed.",
                "#3B82F6");
        }

        [RelayCommand]
        private void TestPrint()
        {
            if (string.IsNullOrWhiteSpace(ReceiptPrinterName))
            {
                SetStatus("Select or enter receipt printer name before test print.", "#EF4444");
                return;
            }

            SetStatus(
                "Test print command is ready. Hardware print service will be connected after this settings page is saved.",
                "#F59E0B");
        }

        [RelayCommand]
        private void KickDrawer()
        {
            if (!EnableCashDrawer)
            {
                SetStatus("Cash drawer is disabled.", "#F59E0B");
                return;
            }

            if (string.IsNullOrWhiteSpace(DrawerKickCode))
            {
                SetStatus("Drawer kick code is required.", "#EF4444");
                return;
            }

            SetStatus(
                "Kick drawer command is ready. Hardware drawer service will be connected after this settings page is saved.",
                "#F59E0B");
        }

        // =========================================================
        // MAPPING
        // =========================================================

        private void ApplySettingsToViewModel(TerminalSettings settings)
        {
            if (settings == null)
                settings = TerminalSettingsRepository.CreateDefaultSettings("01", Environment.MachineName);

            _settingsId = settings.Id;
            _createdAt = settings.CreatedAt;

            TerminalNo = settings.TerminalNo;
            TerminalName = settings.TerminalName;
            MachineName = settings.MachineName;
            Location = settings.Location;
            IsActive = settings.IsActive;

            PrinterMode = settings.PrinterMode;
            ReceiptPrinterName = settings.ReceiptPrinterName;
            ReceiptPaperWidth = settings.ReceiptPaperWidth;
            AutoPrintReceipt = settings.AutoPrintReceipt;
            ReceiptCopies = settings.ReceiptCopies;

            EnableCashDrawer = settings.EnableCashDrawer;
            DrawerKickCode = settings.DrawerKickCode;
            OpenDrawerAfterCashSale = settings.OpenDrawerAfterCashSale;

            ScannerSuffixAction = settings.ScannerSuffixAction;

            EnableScale = settings.EnableScale;
            ScaleComPort = settings.ScaleComPort;
            ScaleBaudRate = settings.ScaleBaudRate;

            EnablePoleDisplay = settings.EnablePoleDisplay;
            PoleDisplayComPort = settings.PoleDisplayComPort;
            PoleWelcomeMessage = settings.PoleWelcomeMessage;

            EnableEftpos = settings.EnableEftpos;
            EftposProvider = settings.EftposProvider;
            EftposPortOrIp = settings.EftposPortOrIp;

            AutoLockTimeoutMinutes =
                settings.AutoLockTimeoutMinutes;

            RaiseCompatibilityPropertyChanges();
        }

        private TerminalSettings BuildSettingsFromViewModel()
        {
            return new TerminalSettings
            {
                Id = _settingsId,
                CreatedAt = _createdAt == default ? DateTime.Now : _createdAt,

                TerminalNo = TerminalNo,
                TerminalName = TerminalName,
                MachineName = MachineName,
                Location = Location,
                IsActive = IsActive,

                PrinterMode = PrinterMode,
                ReceiptPrinterName = ReceiptPrinterName,
                ReceiptPaperWidth = ReceiptPaperWidth,
                AutoPrintReceipt = AutoPrintReceipt,
                ReceiptCopies = ReceiptCopies,

                EnableCashDrawer = EnableCashDrawer,
                DrawerKickCode = DrawerKickCode,
                OpenDrawerAfterCashSale = OpenDrawerAfterCashSale,

                ScannerSuffixAction = ScannerSuffixAction,

                EnableScale = EnableScale,
                ScaleComPort = ScaleComPort,
                ScaleBaudRate = ScaleBaudRate,

                EnablePoleDisplay = EnablePoleDisplay,
                PoleDisplayComPort = PoleDisplayComPort,
                PoleWelcomeMessage = PoleWelcomeMessage,

                EnableEftpos = EnableEftpos,
                EftposProvider = EftposProvider,
                EftposPortOrIp = EftposPortOrIp,

                AutoLockTimeoutMinutes =
                    AutoLockTimeoutMinutes
            };
        }

        private void RefreshAvailablePrintersList()
        {
            AvailablePrinters.Clear();

            AddPrinterOption(ReceiptPrinterName);
            AddPrinterOption("POS-80");
            AddPrinterOption("EPSON TM-T82 Receipt");
            AddPrinterOption("Xprinter XP-80");
            AddPrinterOption("Microsoft Print to PDF");
        }

        private void AddPrinterOption(string? printerName)
        {
            string safePrinterName = (printerName ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(safePrinterName))
                return;

            bool alreadyExists = AvailablePrinters.Any(p =>
                string.Equals(p, safePrinterName, StringComparison.OrdinalIgnoreCase));

            if (!alreadyExists)
                AvailablePrinters.Add(safePrinterName);
        }

        private void RaiseCompatibilityPropertyChanges()
        {
            OnPropertyChanged(nameof(TargetPrinter));
            OnPropertyChanged(nameof(EnableDrawer));
            OnPropertyChanged(nameof(KickCode));
            OnPropertyChanged(nameof(ScanSuffix));
            OnPropertyChanged(nameof(ScalePort));
            OnPropertyChanged(nameof(PoleCom));
            OnPropertyChanged(nameof(PoleWelcome));
        }

        private void SetStatus(string message, string color)
        {
            StatusMessage = message;
            StatusColor = color;
        }
    }
}