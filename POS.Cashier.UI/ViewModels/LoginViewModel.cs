using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Cashier.UI.Views;
using POS.Core.Services;
using POS.Core.Enums; // ADDED

namespace POS.Cashier.UI.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly AuthService _authService;

        // The Action that triggers the App.xaml.cs routing
        public event Action? LoginSuccessful;

        // --- UI Bindings ---
        [ObservableProperty] private string _username = string.Empty;
        [ObservableProperty] private string _password = string.Empty;
        [ObservableProperty] private string _errorMessage = string.Empty;

        // --- Security State Bindings ---
        [ObservableProperty] private bool _isTerminalLocked = false;
        [ObservableProperty] private string _lockoutMessage = "PLEASE LOG IN";

        public bool HasOpenShift { get; private set; } = false;
        private string _lockedCashierName = string.Empty;

        public LoginViewModel(AuthService authService)
        {
            _authService = authService;
        }

        // Called by App.xaml.cs on startup
        public void InitializeShiftState(ShiftSession? activeShift)
        {
            if (activeShift != null && activeShift.Status == "Open")
            {
                HasOpenShift = true;
                IsTerminalLocked = true;
                _lockedCashierName = activeShift.CashierName;
                LockoutMessage = $"TERMINAL LOCKED TO: {_lockedCashierName.ToUpper()}";
            }
            else
            {
                HasOpenShift = false;
                IsTerminalLocked = false;
                LockoutMessage = "PLEASE LOG IN";
            }
        }

        [RelayCommand]
        public async Task LoginAsync()
        {
            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Username and password are required.";
                return;
            }

            try
            {
                var (success, message) = await _authService.LoginAsync(Username, Password);

                if (!success)
                {
                    ErrorMessage = message;
                    return;
                }

                var user = _authService.CurrentUser;
                if (user == null) return;

                // ==============================================
                // THE TERMINAL LOCKOUT LOGIC
                // ==============================================
                if (IsTerminalLocked)
                {
                    // 1. It's the SAME cashier returning -> Let them right in
                    if (user.Username.Equals(_lockedCashierName, StringComparison.OrdinalIgnoreCase))
                    {
                        LoginSuccessful?.Invoke();
                        return;
                    }

                    // 2. It's a DIFFERENT person -> Show the aggressive red locked screen!
                    var lockedWindow = new TerminalLockedView(_lockedCashierName);
                    lockedWindow.ShowDialog();

                    // 3. Did they click "Manager Override" on the red screen?
                    if (lockedWindow.IsOverrideApproved)
                    {
                        // FIXED: Using strong Enums instead of strings
                        if (user.Role == UserRole.Admin || user.Role == UserRole.Manager)
                        {
                            LoginSuccessful?.Invoke();
                            return;
                        }
                        else
                        {
                            ErrorMessage = "OVERRIDE DENIED: You do not have Manager privileges.";
                            _authService.Logout();
                            return;
                        }
                    }

                    _authService.Logout();
                    return;
                }

                // ==============================================
                // NORMAL LOGIN (Streamlined)
                // ==============================================
                // This routes directly to the Sales View. 
                // A background process should generate the ShiftSession with 0 OpeningCash.
                LoginSuccessful?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"System Error: {ex.Message}";
                _authService.Logout();
            }
        }

        public void ResetForm()
        {
            Password = string.Empty;
            ErrorMessage = string.Empty;
            _authService.Logout();
        }
    }
}