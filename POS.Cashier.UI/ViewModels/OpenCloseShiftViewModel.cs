using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using POS.Core.Repositories;
using POS.Core.Services;

namespace POS.Cashier.UI.ViewModels
{
    // The Mode Switch
    public enum ShiftMode { Open, Close }

    public partial class OpenCloseShiftViewModel : ObservableObject
    {
        private readonly AuthService _authService;
        private readonly TillRepository _tillRepository;

        [ObservableProperty] private ShiftMode _currentMode;

        // --- UI Text Bindings ---
        [ObservableProperty] private string _shiftModeTitle = "OPEN NEW SHIFT / REGISTER";
        [ObservableProperty] private string _cashierName = "Unknown";
        [ObservableProperty] private string _terminalNo = "01";
        [ObservableProperty] private string _currentDate = DateTime.Now.ToString("yyyy-MM-dd");

        // --- Denomination Counters ---
        [ObservableProperty] private int _count5000 = 0;
        [ObservableProperty] private int _count1000 = 0;
        [ObservableProperty] private int _count500 = 0;
        [ObservableProperty] private int _count100 = 0;
        [ObservableProperty] private decimal _countCoins = 0;

        // --- The Final Total ---
        [ObservableProperty] private decimal _declaredAmount = 0.00m;

        public OpenCloseShiftViewModel(AuthService authService, TillRepository tillRepository)
        {
            _authService = authService;
            _tillRepository = tillRepository;

            if (_authService.CurrentUser != null)
            {
                CashierName = _authService.CurrentUser.Username;
            }
        }

        // --- Mode Initialization ---
        public void Initialize(ShiftMode mode)
        {
            CurrentMode = mode;
            ShiftModeTitle = mode == ShiftMode.Open ? "OPEN NEW SHIFT / REGISTER" : "CLOSE SHIFT (BLIND COUNT)";
        }

        // --- The Math Engine ---
        partial void OnCount5000Changed(int value) => CalculateTotal();
        partial void OnCount1000Changed(int value) => CalculateTotal();
        partial void OnCount500Changed(int value) => CalculateTotal();
        partial void OnCount100Changed(int value) => CalculateTotal();
        partial void OnCountCoinsChanged(decimal value) => CalculateTotal();

        private void CalculateTotal()
        {
            DeclaredAmount =
                (Count5000 * 5000m) +
                (Count1000 * 1000m) +
                (Count500 * 500m) +
                (Count100 * 100m) +
                CountCoins;
        }

        // --- Database Save Action (The Traffic Cop) ---
        // --- Database Save Action (The Traffic Cop) ---
        public async Task<bool> ProcessShiftAsync()
        {
            try
            {
                if (CurrentMode == ShiftMode.Open)
                {
                    // Phase 2: Open Shift (Uses Terminal string "01")
                    await _tillRepository.CreateNewShiftAsync(TerminalNo, CashierName);
                }
                else
                {
                    // Phase 4: Close Shift
                    // 1. Fetch the currently open shift for this terminal
                    var activeShift = await _tillRepository.GetActiveShiftAsync(TerminalNo);

                    if (activeShift == null)
                    {
                        MessageBox.Show("Could not find an open shift to close for this terminal.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }

                    // 2. Pass the exact Shift ID (int) to close it!
                    await _tillRepository.CloseShiftAsync(activeShift.Id, DeclaredAmount);
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to process shift: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}