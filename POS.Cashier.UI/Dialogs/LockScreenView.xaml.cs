using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using POS.Core.Services;

namespace POS.Cashier.UI.Dialogs
{
    public partial class LockScreenView : Window
    {
        private readonly AuthService _authService;
        private readonly string _username;
        private bool _isUnlocking;

        public bool IsUnlocked { get; private set; }

        public LockScreenView(
            AuthService authService,
            string username,
            string reason)
        {
            InitializeComponent();

            _authService =
                authService ??
                throw new ArgumentNullException(
                    nameof(authService));

            _username =
                (username ?? string.Empty).Trim();

            UsernameText.Text = _username;

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
            if (_isUnlocking)
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
                UnlockButton.IsEnabled = false;
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
                UnlockButton.IsEnabled = true;
            }
        }

        private void Window_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
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
                    "Enter the current user's password to unlock.";
            }
        }

        protected override void OnClosing(
            CancelEventArgs e)
        {
            if (!IsUnlocked)
            {
                e.Cancel = true;
                ErrorText.Text =
                    "Enter the current user's password to unlock.";
            }

            base.OnClosing(e);
        }
    }
}
