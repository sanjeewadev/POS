using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Services;

namespace POS.Cashier.UI.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly AuthService _authService;
        private readonly POS.Core.Repositories.TillRepository _tillRepository;

        public event Func<Task<bool>>? LoginCompletedAsync;
        public event Func<Task>? ManagerOverrideRequested;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isTerminalLocked;

        [ObservableProperty]
        private string _lockoutMessage = "PLEASE LOG IN";

        [ObservableProperty]
        private bool _isBusy;

        public bool HasOpenShift { get; private set; }

        private string _openShiftCashierName = string.Empty;
        private int _openShiftId = 0;

        public LoginViewModel(AuthService authService, POS.Core.Repositories.TillRepository tillRepository)
        {
            _authService = authService;
            _tillRepository = tillRepository;
        }

        [RelayCommand]
        public async Task RequestManagerOverrideAsync()
        {
            if (ManagerOverrideRequested != null)
                await ManagerOverrideRequested.Invoke();
        }

        public async Task ProcessManagerOverrideAsync(string managerName)
        {
            if (_openShiftId <= 0) return;

            try
            {
                IsBusy = true;
                bool success = await _tillRepository.ForceCloseAbandonedShiftAsync(_openShiftId, managerName);

                if (success)
                {
                    // Reset UI to clean login state
                    HasOpenShift = false;
                    IsTerminalLocked = false;
                    _openShiftId = 0;
                    _openShiftCashierName = string.Empty;
                    LockoutMessage = "LOGIN TO OPEN A SHIFT";
                    ErrorMessage = "Shift successfully force-closed. You may now log in.";
                }
                else
                {
                    ErrorMessage = "Failed to force close the active shift.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Manager override failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void InitializeShiftState(
            ShiftSession? activeShift)
        {
            if (activeShift != null &&
                string.Equals(
                    activeShift.Status,
                    "Open",
                    StringComparison.OrdinalIgnoreCase))
            {
                HasOpenShift = true;
                IsTerminalLocked = true;
                _openShiftId = activeShift.Id;

                _openShiftCashierName =
                    activeShift.CashierName?.Trim()
                    ?? string.Empty;

                LockoutMessage =
                    $"OPEN SHIFT: {_openShiftCashierName.ToUpperInvariant()}";
            }
            else
            {
                HasOpenShift = false;
                IsTerminalLocked = false;
                _openShiftCashierName = string.Empty;
                LockoutMessage =
                    "LOGIN TO OPEN A SHIFT";
            }
        }

        [RelayCommand]
        public async Task LoginAsync()
        {
            if (IsBusy)
                return;

            ErrorMessage = string.Empty;

            string safeUsername =
                (Username ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(safeUsername) ||
                string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage =
                    "Username and password are required.";
                return;
            }

            try
            {
                IsBusy = true;

                var (success, message) =
                    await _authService.LoginAsync(
                        safeUsername,
                        Password,
                        "Cashier");

                if (!success)
                {
                    ErrorMessage = message;
                    return;
                }

                var user = _authService.CurrentUser;

                if (user == null)
                {
                    ErrorMessage =
                        "The authenticated user could not be loaded.";
                    _authService.Logout();
                    return;
                }

                if (HasOpenShift &&
                    !string.Equals(
                        user.Username,
                        _openShiftCashierName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    ErrorMessage =
                        $"This terminal has an open shift for " +
                        $"'{_openShiftCashierName}'. " +
                        "That cashier must log in.";

                    _authService.Logout();
                    Password = string.Empty;
                    return;
                }

                if (LoginCompletedAsync == null)
                {
                    ErrorMessage =
                        "The Cashier login route is unavailable.";
                    _authService.Logout();
                    return;
                }

                bool completed =
                    await LoginCompletedAsync.Invoke();

                if (!completed)
                {
                    ErrorMessage = HasOpenShift
                        ? "The open shift could not be resumed."
                        : "Shift opening was cancelled. Login again to continue.";

                    _authService.Logout();
                    Password = string.Empty;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage =
                    $"Login failed: {ex.Message}";

                _authService.Logout();
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ResetForm()
        {
            Username = string.Empty;
            Password = string.Empty;
            ErrorMessage = string.Empty;
            _authService.Logout();
        }
    }
}
