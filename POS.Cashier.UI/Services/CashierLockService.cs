using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using POS.Cashier.UI.Dialogs;
using POS.Core.Services;

namespace POS.Cashier.UI.Services
{
    /// <summary>
    /// Owns the single Cashier lock lifecycle.
    /// Automatic locking is based on application-wide input so activity
    /// inside Cashier dialogs also resets the timer.
    /// </summary>
    public sealed class CashierLockService : IDisposable
    {
        private readonly AuthService _authService;
        private readonly DispatcherTimer _timer;

        private Window? _ownerWindow;
        private int _timeoutMinutes;
        private bool _isStarted;
        private bool _isLocked;
        private bool _isDisposed;

        public CashierLockService(AuthService authService)
        {
            _authService = authService;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(10)
            };

            _timer.Tick += Timer_Tick;
        }

        public bool IsLocked => _isLocked;

        public int TimeoutMinutes => _timeoutMinutes;

        public void Start(
            Window ownerWindow,
            int timeoutMinutes)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(
                    nameof(CashierLockService));
            }

            if (ownerWindow == null)
            {
                throw new ArgumentNullException(
                    nameof(ownerWindow));
            }

            Stop();

            _ownerWindow = ownerWindow;
            _timeoutMinutes =
                Math.Clamp(timeoutMinutes, 0, 120);

            InputManager.Current.PreProcessInput +=
                InputManager_PreProcessInput;

            _isStarted = true;
            RestartTimer();
        }

        public void Stop()
        {
            _timer.Stop();

            if (_isStarted)
            {
                InputManager.Current.PreProcessInput -=
                    InputManager_PreProcessInput;
            }

            _ownerWindow = null;
            _timeoutMinutes = 0;
            _isStarted = false;
            _isLocked = false;
        }

        public void ReportActivity()
        {
            if (!_isStarted || _isLocked)
                return;

            RestartTimer();
        }

        public bool LockTerminal(
            string reason)
        {
            if (!_isStarted ||
                _isLocked ||
                _ownerWindow == null ||
                !_ownerWindow.IsVisible)
            {
                return false;
            }

            string username =
                _authService.CurrentUser?.Username
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username))
                return false;

            _timer.Stop();
            _isLocked = true;

            try
            {
                var lockWindow =
                    new LockScreenView(
                        _authService,
                        username,
                        reason)
                    {
                        Owner = _ownerWindow
                    };

                bool? result =
                    lockWindow.ShowDialog();

                return result == true &&
                       lockWindow.IsUnlocked;
            }
            finally
            {
                _isLocked = false;
                RestartTimer();
            }
        }

        private void RestartTimer()
        {
            _timer.Stop();

            if (!_isStarted ||
                _isLocked ||
                _timeoutMinutes <= 0)
            {
                return;
            }

            _timer.Interval =
                TimeSpan.FromMinutes(
                    _timeoutMinutes);

            _timer.Start();
        }

        private void InputManager_PreProcessInput(
            object sender,
            PreProcessInputEventArgs e)
        {
            if (!_isStarted || _isLocked)
                return;

            InputEventArgs? input =
                e.StagingItem.Input;

            if (input is KeyboardEventArgs ||
                input is MouseEventArgs ||
                input is TouchEventArgs ||
                input is StylusEventArgs)
            {
                RestartTimer();
            }
        }

        private void Timer_Tick(
            object? sender,
            EventArgs e)
        {
            _timer.Stop();

            LockTerminal(
                $"Locked automatically after " +
                $"{_timeoutMinutes} minute(s) of inactivity.");
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            Stop();
            _timer.Tick -= Timer_Tick;
            _isDisposed = true;
        }
    }
}
