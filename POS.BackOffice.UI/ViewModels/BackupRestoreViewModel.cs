using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using POS.Core.Data;
using POS.Core.Models.Backup;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Services.Backup;
using POS.Core.Services.Licensing;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class BackupRestoreViewModel :
        ObservableObject
    {
        private readonly BackupService _backupService;
        private readonly BackupRepository _backupRepository;
        private readonly TerminalSettingsRepository
            _terminalSettingsRepository;
        private readonly MachineFingerprintService
            _machineFingerprintService;
        private readonly AuthService _authService;

        public BackupRestoreViewModel(
            BackupService backupService,
            BackupRepository backupRepository,
            TerminalSettingsRepository
                terminalSettingsRepository,
            MachineFingerprintService
                machineFingerprintService,
            AuthService authService)
        {
            _backupService = backupService;
            _backupRepository = backupRepository;
            _terminalSettingsRepository =
                terminalSettingsRepository;
            _machineFingerprintService =
                machineFingerprintService;
            _authService = authService;

            RecentHistory =
                new ObservableCollection<BackupHistory>();

            BackupFolder =
                _backupService.GetDefaultBackupFolder();

            CurrentDatabasePath =
                DatabasePathProvider.DatabaseFilePath;

            IsAdministrator =
                _authService.IsAdmin;
        }

        public ObservableCollection<BackupHistory>
            RecentHistory { get; }

        [ObservableProperty]
        private BackupHistory? _selectedHistory;

        [ObservableProperty]
        private bool _isAdministrator;

        [ObservableProperty]
        private string _backupFolder = string.Empty;

        [ObservableProperty]
        private string _currentDatabasePath = string.Empty;

        [ObservableProperty]
        private string _currentStoreText = "-";

        [ObservableProperty]
        private string _currentDatabaseSizeText = "-";

        [ObservableProperty]
        private string _currentMigrationText = "-";

        [ObservableProperty]
        private string _lastBackupText =
            "No successful manual backup recorded.";

        [ObservableProperty]
        private string _lastRestoreText =
            "No successful restore recorded.";

        [ObservableProperty]
        private string _selectedBackupFile = string.Empty;

        [ObservableProperty]
        private string _selectedBackupStore = "-";

        [ObservableProperty]
        private string _selectedBackupCreatedAt = "-";

        [ObservableProperty]
        private string _selectedBackupVersion = "-";

        [ObservableProperty]
        private string _selectedBackupMigration = "-";

        [ObservableProperty]
        private string _selectedBackupSize = "-";

        [ObservableProperty]
        private string _verifyResultText =
            "No backup file verified.";

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        [ObservableProperty]
        private string _statusColor = "#666666";

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                IsAdministrator =
                    _authService.IsAdmin;

                SetStatus(
                    "Loading manual backup information...",
                    "#2B5B84");

                await LoadDataCoreAsync();

                if (!IsAdministrator)
                {
                    SetStatus(
                        "Only an Administrator can create or restore backups.",
                        "#B91C1C");
                }
                else
                {
                    SetStatus(
                        "Manual backup and restore are ready.",
                        "#008000");
                }
            }
            catch (Exception ex)
            {
                SetStatus(
                    $"Failed to load backup information: " +
                    $"{ex.Message}",
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
        private async Task CreateBackupAsync()
        {
            if (IsBusy ||
                !EnsureAdministrator())
            {
                return;
            }

            string userName = GetCurrentUserName();

            BackupMetadata currentInfo;

            try
            {
                currentInfo =
                    await _backupService
                        .GetCurrentDatabaseInfoAsync();
            }
            catch (Exception ex)
            {
                SetStatus(
                    $"Current database could not be read: " +
                    $"{ex.Message}",
                    "#B91C1C");
                return;
            }

            Directory.CreateDirectory(
                _backupService.GetDefaultBackupFolder());

            SaveFileDialog dialog =
                new()
                {
                    Title = "Save Manual POS Backup",
                    InitialDirectory =
                        _backupService
                            .GetDefaultBackupFolder(),
                    FileName =
                        _backupService
                            .GetSuggestedBackupFileName(
                                currentInfo.StoreDisplayName),
                    Filter =
                        "POS Backup Files (*.posbackup)|*.posbackup",
                    DefaultExt =
                        BackupService.BackupExtension,
                    AddExtension = true,
                    OverwritePrompt = true
                };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                IsBusy = true;
                SetStatus(
                    "Creating and verifying manual backup...",
                    "#2B5B84");

                BackupResult result =
                    await _backupService
                        .CreateBackupAsync(
                            dialog.FileName,
                            userName);

                await SaveHistorySafeAsync(
                    BackupActionTypes.BackupCreated,
                    result,
                    userName);

                if (result.Success)
                {
                    SelectedBackupFile =
                        result.BackupFilePath;

                    ApplyVerifiedMetadata(
                        result.Metadata);

                    VerifyResultText =
                        "Valid POS backup. Integrity and checksum passed.";

                    SetStatus(
                        $"Backup created successfully: " +
                        $"{result.BackupFileName}",
                        "#008000");

                    MessageBox.Show(
                        "Manual backup created and verified successfully.\n\n" +
                        result.BackupFilePath +
                        "\n\nKeep this file in a secure external location.",
                        "Backup Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    SetStatus(
                        result.Message,
                        "#B91C1C");

                    MessageBox.Show(
                        result.Message,
                        "Backup Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                await LoadDataCoreSafeAsync();
            }
            catch (Exception ex)
            {
                SetStatus(
                    $"Create backup failed: {ex.Message}",
                    "#B91C1C");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SelectBackupFileAsync()
        {
            string filePath =
                SelectBackupFile();

            if (string.IsNullOrWhiteSpace(filePath))
                return;

            SelectedBackupFile = filePath;

            await VerifySelectedBackupCoreAsync(
                recordHistory: false);
        }

        [RelayCommand]
        private async Task VerifyBackupAsync()
        {
            if (IsBusy)
                return;

            if (!EnsureSelectedBackupFile())
                return;

            await VerifySelectedBackupCoreAsync(
                recordHistory: true);
        }

        [RelayCommand]
        private async Task RestoreBackupAsync()
        {
            if (IsBusy ||
                !EnsureAdministrator() ||
                !EnsureSelectedBackupFile())
            {
                return;
            }

            BackupResult verification =
                await _backupService
                    .VerifyBackupAsync(
                        SelectedBackupFile);

            if (!verification.Success)
            {
                ApplyVerificationFailure(
                    verification.Message);

                MessageBox.Show(
                    verification.Message,
                    "Invalid Backup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            ApplyVerifiedMetadata(
                verification.Metadata);

            if (IsCashierRunning())
            {
                const string message =
                    "Cashier is currently running. " +
                    "Close Cashier before restoring a database.";

                SetStatus(message, "#B91C1C");

                MessageBox.Show(
                    message,
                    "Restore Blocked",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            MessageBoxResult confirmation =
                MessageBox.Show(
                    "Restore this verified backup?\n\n" +
                    "The current database will be replaced. " +
                    "A restore-safety copy will be created first.\n\n" +
                    "BackOffice will restart automatically after a successful restore.",
                    "Confirm Database Restore",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
                return;

            string userName = GetCurrentUserName();

            try
            {
                IsBusy = true;
                SetStatus(
                    "Restoring verified backup. Do not close the computer...",
                    "#C05A00");

                BackupResult result =
                    await _backupService
                        .RestoreBackupAsync(
                            SelectedBackupFile,
                            userName);

                await SaveHistorySafeAsync(
                    BackupActionTypes.BackupRestored,
                    result,
                    userName);

                if (!result.Success)
                {
                    SetStatus(
                        result.Message,
                        "#B91C1C");

                    MessageBox.Show(
                        result.Message,
                        "Restore Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    await LoadDataCoreSafeAsync();
                    return;
                }

                SetStatus(
                    "Restore completed. Restarting BackOffice...",
                    "#008000");

                MessageBox.Show(
                    "Backup restored successfully.\n\n" +
                    "BackOffice will now restart. " +
                    "Sign in again after it opens.",
                    "Restore Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                RestartApplication();
            }
            catch (Exception ex)
            {
                SetStatus(
                    $"Restore failed: {ex.Message}",
                    "#B91C1C");

                MessageBox.Show(
                    $"Restore failed: {ex.Message}",
                    "Restore Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void OpenBackupFolder()
        {
            try
            {
                _backupService.OpenBackupFolder();

                BackupFolder =
                    _backupService
                        .GetDefaultBackupFolder();

                SetStatus(
                    "Backup folder opened.",
                    "#008000");
            }
            catch (Exception ex)
            {
                SetStatus(
                    $"Failed to open backup folder: " +
                    $"{ex.Message}",
                    "#B91C1C");
            }
        }

        [RelayCommand]
        private void ClearSelectedBackupFile()
        {
            SelectedBackupFile = string.Empty;
            SelectedBackupStore = "-";
            SelectedBackupCreatedAt = "-";
            SelectedBackupVersion = "-";
            SelectedBackupMigration = "-";
            SelectedBackupSize = "-";
            VerifyResultText =
                "No backup file verified.";

            SetStatus(
                "Backup selection cleared.",
                "#666666");
        }

        private async Task VerifySelectedBackupCoreAsync(
            bool recordHistory)
        {
            if (IsBusy ||
                !EnsureSelectedBackupFile())
            {
                return;
            }

            string userName = GetCurrentUserName();

            try
            {
                IsBusy = true;
                SetStatus(
                    "Verifying backup checksum, database integrity, and schema...",
                    "#2B5B84");

                BackupResult result =
                    await _backupService
                        .VerifyBackupAsync(
                            SelectedBackupFile);

                if (recordHistory)
                {
                    await SaveHistorySafeAsync(
                        BackupActionTypes.BackupVerified,
                        result,
                        userName);
                }

                if (result.Success)
                {
                    ApplyVerifiedMetadata(
                        result.Metadata);

                    VerifyResultText =
                        "Valid POS backup. Integrity, checksum, and required tables passed.";

                    SetStatus(
                        "Backup file verified successfully.",
                        "#008000");
                }
                else
                {
                    ApplyVerificationFailure(
                        result.Message);
                }

                await LoadHistorySafeAsync();
            }
            catch (Exception ex)
            {
                ApplyVerificationFailure(
                    $"Verification failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadDataCoreAsync()
        {
            BackupFolder =
                _backupService
                    .GetDefaultBackupFolder();

            CurrentDatabasePath =
                DatabasePathProvider.DatabaseFilePath;

            BackupMetadata currentInfo =
                await _backupService
                    .GetCurrentDatabaseInfoAsync();

            CurrentStoreText =
                $"{currentInfo.StoreDisplayName} " +
                $"({DisplayOrDash(currentInfo.StoreId)})";

            CurrentDatabaseSizeText =
                currentInfo.DisplayDatabaseSize;

            CurrentMigrationText =
                currentInfo.MigrationDisplay;

            BackupHistory? latestBackup =
                await _backupRepository
                    .GetLatestBackupAsync();

            BackupHistory? latestRestore =
                await _backupRepository
                    .GetLatestRestoreAsync();

            LastBackupText =
                latestBackup == null
                    ? "No successful manual backup recorded."
                    : $"{latestBackup.CreatedAtText} - " +
                      $"{latestBackup.BackupFileNameDisplay}";

            LastRestoreText =
                latestRestore == null
                    ? "No successful restore recorded."
                    : $"{latestRestore.CreatedAtText} - " +
                      $"{latestRestore.BackupFileNameDisplay}";

            await LoadHistoryAsync();
        }

        private async Task LoadHistoryAsync()
        {
            var history =
                await _backupRepository
                    .GetRecentHistoryAsync(30);

            RecentHistory.Clear();

            foreach (BackupHistory row in history)
                RecentHistory.Add(row);
        }

        private async Task LoadDataCoreSafeAsync()
        {
            try
            {
                await LoadDataCoreAsync();
            }
            catch
            {
                // Preserve the operation result on screen.
            }
        }

        private async Task LoadHistorySafeAsync()
        {
            try
            {
                await LoadHistoryAsync();
            }
            catch
            {
                // Verification result is more important.
            }
        }

        private async Task SaveHistorySafeAsync(
            string actionType,
            BackupResult result,
            string createdBy)
        {
            try
            {
                var terminalSettings =
                    await _terminalSettingsRepository
                        .GetOrCreateForCurrentMachineAsync("01");

                BackupHistory history =
                    BackupRepository
                        .CreateHistoryFromResult(
                            actionType,
                            result,
                            createdBy,
                            _machineFingerprintService
                                .GetMachineName(),
                            terminalSettings.TerminalNo);

                await _backupRepository
                    .AddHistoryAsync(history);
            }
            catch
            {
                // The main operation must not fail only
                // because history writing failed.
            }
        }

        private void ApplyVerifiedMetadata(
            BackupMetadata? metadata)
        {
            if (metadata == null)
            {
                SelectedBackupStore = "-";
                SelectedBackupCreatedAt = "-";
                SelectedBackupVersion = "-";
                SelectedBackupMigration = "-";
                SelectedBackupSize = "-";
                return;
            }

            SelectedBackupStore =
                $"{metadata.StoreDisplayName} " +
                $"({DisplayOrDash(metadata.StoreId)})";

            SelectedBackupCreatedAt =
                metadata.CreatedAtText;

            SelectedBackupVersion =
                DisplayOrDash(metadata.AppVersion);

            SelectedBackupMigration =
                metadata.MigrationDisplay;

            SelectedBackupSize =
                metadata.DisplayDatabaseSize;
        }

        private void ApplyVerificationFailure(
            string message)
        {
            SelectedBackupStore = "-";
            SelectedBackupCreatedAt = "-";
            SelectedBackupVersion = "-";
            SelectedBackupMigration = "-";
            SelectedBackupSize = "-";
            VerifyResultText = message;

            SetStatus(message, "#B91C1C");
        }

        private bool EnsureAdministrator()
        {
            if (_authService.IsAdmin)
                return true;

            const string message =
                "Only an Administrator can create or restore backups.";

            SetStatus(message, "#B91C1C");

            MessageBox.Show(
                message,
                "Administrator Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return false;
        }

        private bool EnsureSelectedBackupFile()
        {
            if (!string.IsNullOrWhiteSpace(
                    SelectedBackupFile) &&
                File.Exists(SelectedBackupFile))
            {
                return true;
            }

            string selectedFile =
                SelectBackupFile();

            if (string.IsNullOrWhiteSpace(selectedFile))
                return false;

            SelectedBackupFile = selectedFile;
            return true;
        }

        private string GetCurrentUserName()
        {
            return string.IsNullOrWhiteSpace(
                _authService.CurrentUser?.Username)
                ? "Administrator"
                : _authService.CurrentUser.Username;
        }

        private static string SelectBackupFile()
        {
            OpenFileDialog dialog =
                new()
                {
                    Title = "Select POS Backup",
                    Filter =
                        "POS Backup Files (*.posbackup;*.posbak)|*.posbackup;*.posbak",
                    Multiselect = false,
                    CheckFileExists = true
                };

            return dialog.ShowDialog() == true
                ? dialog.FileName
                : string.Empty;
        }

        private static bool IsCashierRunning()
        {
            try
            {
                return Process.GetProcessesByName(
                        "POS.Cashier.UI")
                    .Any();
            }
            catch
            {
                return false;
            }
        }

        private static void RestartApplication()
        {
            string? executablePath =
                Environment.ProcessPath;

            if (string.IsNullOrWhiteSpace(
                    executablePath) ||
                !File.Exists(executablePath))
            {
                MessageBox.Show(
                    "BackOffice could not restart automatically. " +
                    "Close it now and open it again manually.",
                    "Restart Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Application.Current.Shutdown();
                return;
            }

            Process.Start(
                new ProcessStartInfo
                {
                    FileName = executablePath,
                    UseShellExecute = true
                });

            Application.Current.Shutdown();
        }

        private static string DisplayOrDash(
            string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "-"
                : value.Trim();
        }

        private void SetStatus(
            string message,
            string color)
        {
            StatusMessage =
                string.IsNullOrWhiteSpace(message)
                    ? "Ready."
                    : message;

            StatusColor =
                string.IsNullOrWhiteSpace(color)
                    ? "#666666"
                    : color;
        }
    }
}
