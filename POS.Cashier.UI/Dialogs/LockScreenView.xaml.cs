using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using POS.Cashier.UI.ViewModels;
using POS.Core.Services;

namespace POS.Cashier.UI.Dialogs
{
    public partial class LockScreenView : Window
    {
        private readonly AuthService _authService;
        private readonly string _username;
        private readonly string _terminalNo;
        private readonly int _shiftId;
        private readonly Func<Task<bool>> _prepareSafeExitAsync;
        private bool _isUnlocking;
        private bool _isRecoveryRunning;

        public bool IsUnlocked { get; private set; }

        public bool SafeExitRequested { get; private set; }

        public LockScreenView(
            AuthService authService,
            string username,
            string terminalNo,
            int shiftId,
            string reason,
            Func<Task<bool>> prepareSafeExitAsync)
        {
            InitializeComponent();

            _authService =
                authService ??
                throw new ArgumentNullException(
                    nameof(authService));

            _username =
                (username ?? string.Empty).Trim();

            _terminalNo =
                string.IsNullOrWhiteSpace(terminalNo)
                    ? "Unknown"
                    : terminalNo.Trim();

            _shiftId = shiftId;

            _prepareSafeExitAsync =
                prepareSafeExitAsync ??
                throw new ArgumentNullException(
                    nameof(prepareSafeExitAsync));

            UsernameText.Text = _username;
            ShiftContextText.Text =
                $"{_terminalNo} / Shift {_shiftId}";

            ReasonText.Text =
                string.IsNullOrWhiteSpace(reason)
                    ? "Locked manually."
                    : reason.Trim();

            Loaded += (_, _) =>
            {
                PasswordInput.Focus();
                Keyboard.Focus(PasswordInput);
            };
        }

        private async void UnlockButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isUnlocking || _isRecoveryRunning)
                return;

            string password =
                PasswordInput.Password;

            if (string.IsNullOrWhiteSpace(password))
            {
                ErrorText.Text =
                    "Enter the current user's password.";

                PasswordInput.Focus();
                return;
            }

            try
            {
                _isUnlocking = true;
                SetButtonsEnabled(false);
                ErrorText.Text =
                    "Checking password...";

                var (success, message) =
                    await _authService.LoginAsync(
                        _username,
                        password,
                        "CashierUnlock");

                bool sameUser =
                    string.Equals(
                        _authService.CurrentUser?.Username,
                        _username,
                        StringComparison.OrdinalIgnoreCase);

                if (!success || !sameUser)
                {
                    ErrorText.Text = message;
                    PasswordInput.Password = string.Empty;
                    PasswordInput.Focus();
                    return;
                }

                IsUnlocked = true;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                ErrorText.Text =
                    $"Unlock failed: {ex.Message}";

                PasswordInput.Password = string.Empty;
                PasswordInput.Focus();
            }
            finally
            {
                _isUnlocking = false;
                SetButtonsEnabled(true);
            }
        }

        private async void ManagerRecoveryButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isUnlocking || _isRecoveryRunning)
                return;

            try
            {
                _isRecoveryRunning = true;
                SetButtonsEnabled(false);
                ErrorText.Text = string.Empty;

                var managerViewModel =
                    new ManagerAuthViewModel(
                        _authService);

                var managerDialog =
                    new ManagerAuthDialogView(
                        managerViewModel)
                    {
                        Owner = this
                    };

                if (managerDialog.ShowDialog() != true ||
                    !managerViewModel.AuthorizedUserId.HasValue ||
                    string.IsNullOrWhiteSpace(
                        managerViewModel.AuthorizedUsername))
                {
                    return;
                }

                var actionDialog =
                    new LockRecoveryActionDialog
                    {
                        Owner = this
                    };

                if (actionDialog.ShowDialog() != true)
                    return;

                if (actionDialog.SelectedAction ==
                    LockRecoveryAction.UnlockAndContinue)
                {
                    await RecordRecoveryAuditAsync(
                        managerViewModel,
                        "LockRecoveryUnlock",
                        "Manager unlock; original cashier and shift ownership preserved.");

                    IsUnlocked = true;
                    DialogResult = true;
                    return;
                }

                if (actionDialog.SelectedAction ==
                    LockRecoveryAction.CloseApplication)
                {
                    MessageBoxResult confirmation =
                        MessageBox.Show(
                            "The active cart will be saved and the Cashier application will close.\n\n" +
                            "The shift will remain open for the original cashier. Continue?",
                            "Close Cashier Application",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                    if (confirmation != MessageBoxResult.Yes)
                        return;

                    ErrorText.Text =
                        "Saving the current cart...";

                    bool prepared =
                        await _prepareSafeExitAsync();

                    if (!prepared)
                    {
                        ErrorText.Text =
                            "The active cart could not be saved. The terminal remains locked.";
                        return;
                    }

                    await RecordRecoveryAuditAsync(
                        managerViewModel,
                        "LockRecoveryExit",
                        "Safe application exit after cart save; shift left open.");

                    SafeExitRequested = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                ErrorText.Text =
                    $"Manager recovery failed: {ex.Message}";

                LocalLogService.WriteException(
                    "Cashier",
                    "Manager lock recovery",
                    ex);
            }
            finally
            {
                _isRecoveryRunning = false;
                SetButtonsEnabled(true);
            }
        }

        private Task RecordRecoveryAuditAsync(
            ManagerAuthViewModel managerViewModel,
            string eventType,
            string actionMessage)
        {
            string message =
                $"Terminal {_terminalNo}; shift {_shiftId}; " +
                $"cashier {_username}; authorized by " +
                $"{managerViewModel.AuthorizedUsername}. " +
                actionMessage;

            return _authService.RecordSecurityAuditAsync(
                managerViewModel.AuthorizedUserId,
                managerViewModel.AuthorizedUsername,
                eventType,
                "CashierTerminalLock",
                message);
        }

        private void SetButtonsEnabled(bool isEnabled)
        {
            UnlockButton.IsEnabled = isEnabled;
            ManagerRecoveryButton.IsEnabled = isEnabled;
        }

        private void Window_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key == Key.Enter &&
                !_isRecoveryRunning)
            {
                e.Handled = true;

                UnlockButton_Click(
                    UnlockButton,
                    new RoutedEventArgs());

                return;
            }

            if (e.Key == Key.Escape ||
                (e.Key == Key.F4 &&
                 Keyboard.Modifiers.HasFlag(
                     ModifierKeys.Alt)))
            {
                e.Handled = true;
                ErrorText.Text =
                    "Use the current user's password or Manager Recovery.";
            }
        }

        protected override void OnClosing(
            CancelEventArgs e)
        {
            if (!IsUnlocked &&
                !SafeExitRequested)
            {
                e.Cancel = true;
                ErrorText.Text =
                    "Use the current user's password or Manager Recovery.";
            }

            base.OnClosing(e);
        }
    }
}
