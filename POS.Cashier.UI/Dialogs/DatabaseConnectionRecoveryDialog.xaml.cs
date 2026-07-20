using POS.Cashier.UI.Services;
using POS.Core.Data.Configuration;
using POS.Core.Services;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace POS.Cashier.UI.Dialogs
{
    public partial class DatabaseConnectionRecoveryDialog : Window
    {
        private readonly Func<Task>? _retryAsync;
        private Exception _currentFailure;
        private bool _isBusy;
        private bool _restartAvailable;

        public bool ConnectionRestored { get; private set; }

        public DatabaseConnectionRecoveryDialog(
            Exception failure,
            Func<Task>? retryAsync)
        {
            _currentFailure = failure ??
                throw new ArgumentNullException(
                    nameof(failure));

            _retryAsync = retryAsync;

            InitializeComponent();
        }

        private void Window_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            ApplyFailure(_currentFailure);
            RetryButton.IsEnabled = _retryAsync != null;

            if (_retryAsync == null)
            {
                ActionStatusTextBlock.Text =
                    "The saved profile could not be loaded. Repair the " +
                    "connection and restart Cashier.";
            }
        }

        private void Window_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key != Key.Escape || _isBusy)
                return;

            DialogResult = false;
            e.Handled = true;
        }

        private async void RetryButton_OnClick(
            object sender,
            RoutedEventArgs e)
        {
            if (_isBusy || _retryAsync == null)
                return;

            try
            {
                SetBusy(true);
                ActionStatusTextBlock.Text =
                    "Retrying the saved store connection...";

                await _retryAsync();

                ConnectionRestored = true;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                _currentFailure = ex;

                LocalLogService.WriteException(
                    "Cashier",
                    "Connection recovery retry",
                    ex);

                ApplyFailure(ex);
                ActionStatusTextBlock.Text =
                    "The store connection is still unavailable. Repair the " +
                    "saved connection or save diagnostics.";
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void RepairButton_OnClick(
            object sender,
            RoutedEventArgs e)
        {
            if (_isBusy)
                return;

            try
            {
                SetBusy(true);

                bool launched =
                    CashierConfigurationLauncher.TryLaunch(
                        out Process? process,
                        out string message);

                ActionStatusTextBlock.Text = message;

                if (!launched || process == null)
                    return;

                await process.WaitForExitAsync();

                _restartAvailable = true;
                ActionStatusTextBlock.Text =
                    "The configuration wizard closed. Choose Restart Cashier " +
                    "to load the repaired encrypted connection profile.";
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Wait for connection repair wizard",
                    ex);

                ActionStatusTextBlock.Text =
                    "The repair wizard could not be monitored. Close Cashier " +
                    "and open it again after configuration is complete.";
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void DiagnosticsButton_OnClick(
            object sender,
            RoutedEventArgs e)
        {
            if (_isBusy)
                return;

            try
            {
                SetBusy(true);
                ActionStatusTextBlock.Text =
                    "Creating a connection diagnostic report...";

                string reportPath =
                    await CashierConnectionDiagnosticsService
                        .CreateReportAsync(
                            _currentFailure);

                CashierConnectionDiagnosticsService
                    .TryOpenReportFolder(
                        reportPath,
                        out string message);

                ActionStatusTextBlock.Text = message;
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Create connection diagnostics",
                    ex);

                ActionStatusTextBlock.Text =
                    "The diagnostic report could not be created. Technical " +
                    "details were saved in the local POS Logs folder.";
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void RestartButton_OnClick(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                string? executable =
                    Environment.ProcessPath;

                if (string.IsNullOrWhiteSpace(executable))
                {
                    throw new InvalidOperationException(
                        "The Cashier executable path is unavailable.");
                }

                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = executable,
                        WorkingDirectory = AppContext.BaseDirectory,
                        UseShellExecute = true
                    });

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Restart after connection repair",
                    ex);

                ActionStatusTextBlock.Text =
                    "Cashier could not restart automatically. Close this " +
                    "window and open the Cashier shortcut again.";
            }
        }

        private void ExitButton_OnClick(
            object sender,
            RoutedEventArgs e)
        {
            if (_isBusy)
                return;

            DialogResult = false;
        }

        private void ApplyFailure(Exception exception)
        {
            FailureMessageTextBlock.Text =
                string.IsNullOrWhiteSpace(exception.Message)
                    ? "The store database connection is unavailable."
                    : exception.Message.Trim();

            DatabaseConnectionSettingsStore store =
                new();

            ProfilePathTextBlock.Text =
                store.SettingsPath;

            try
            {
                DatabaseConnectionSettings settings =
                    store.LoadOrDefault();

                EndpointTextBlock.Text =
                    settings.IsCentralSqlServer
                        ? $"{settings.ServerHost}," +
                          $"{settings.ServerPort} / " +
                          settings.DatabaseName
                        : settings.DisplayName;
            }
            catch (Exception ex)
            {
                EndpointTextBlock.Text =
                    "The encrypted connection profile could not be read: " +
                    ex.Message;
            }
        }

        protected override void OnClosing(
            CancelEventArgs e)
        {
            if (_isBusy)
            {
                e.Cancel = true;
                ActionStatusTextBlock.Text =
                    "Wait for the current recovery operation to finish.";
                return;
            }

            base.OnClosing(e);
        }

        private void SetBusy(bool value)
        {
            _isBusy = value;
            RetryButton.IsEnabled =
                !value && _retryAsync != null;
            RepairButton.IsEnabled = !value;
            DiagnosticsButton.IsEnabled = !value;
            RestartButton.IsEnabled =
                !value && _restartAvailable;
            ExitButton.IsEnabled = !value;
        }
    }
}
