using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using POS.Core.Models.Backup;
using POS.Core.Repositories;
using POS.Core.Services.Backup;
using POS.Core.Services.Licensing;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class BackupRestoreViewModel : ObservableObject
    {
        private readonly BackupService _backupService;
        private readonly BackupRepository _backupRepository;
        private readonly TerminalSettingsRepository _terminalSettingsRepository;
        private readonly MachineFingerprintService _machineFingerprintService;

        public BackupRestoreViewModel(
            BackupService backupService,
            BackupRepository backupRepository,
            TerminalSettingsRepository terminalSettingsRepository,
            MachineFingerprintService machineFingerprintService)
        {
            _backupService = backupService;
            _backupRepository = backupRepository;
            _terminalSettingsRepository = terminalSettingsRepository;
            _machineFingerprintService = machineFingerprintService;

            RecentHistory = new ObservableCollection<BackupHistory>();

            BackupFolder = _backupService.GetDefaultBackupFolder();

            _ = LoadAsync();
        }

        public ObservableCollection<BackupHistory> RecentHistory { get; }

        [ObservableProperty]
        private BackupHistory? _selectedHistory;

        [ObservableProperty]
        private string _backupFolder = string.Empty;

        [ObservableProperty]
        private string _lastBackupText = "No backup created yet.";

        [ObservableProperty]
        private string _lastRestoreText = "No restore recorded yet.";

        [ObservableProperty]
        private string _selectedBackupFile = string.Empty;

        [ObservableProperty]
        private string _verifyResultText = "No backup file verified yet.";

        [ObservableProperty]
        private string _lastCreatedBackupFile = string.Empty;

        [ObservableProperty]
        private string _lastCreatedBackupChecksum = string.Empty;

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
                SetStatus("Loading backup information...", "#3B82F6");

                await LoadDataCoreAsync();

                SetStatus("Backup information loaded.", "#10B981");
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to load backup information: {ex.Message}", "#EF4444");
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
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                SetStatus("Creating backup. Please wait...", "#3B82F6");

                BackupResult result = await _backupService.CreateBackupAsync("BackOffice");

                await SaveHistorySafeAsync(
                    BackupActionTypes.BackupCreated,
                    result,
                    "BackOffice");

                if (result.Success)
                {
                    LastCreatedBackupFile = result.BackupFilePath;
                    LastCreatedBackupChecksum = result.Checksum;
                    SelectedBackupFile = result.BackupFilePath;

                    SetStatus(
                        $"Backup created successfully: {result.BackupFileName}",
                        "#10B981");
                }
                else
                {
                    SetStatus(result.Message, "#EF4444");
                }

                await LoadDataCoreAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"Create backup failed: {ex.Message}", "#EF4444");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RestoreBackupAsync()
        {
            if (IsBusy)
                return;

            string filePath = SelectedBackupFile;

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                filePath = SelectBackupFile();

                if (string.IsNullOrWhiteSpace(filePath))
                    return;

                SelectedBackupFile = filePath;
            }

            MessageBoxResult confirm = MessageBox.Show(
                "Restoring a backup will replace the current database.\n\n" +
                "Make sure all POS windows are closed and no cashier is using the system.\n\n" +
                "Continue restore?",
                "Confirm Restore",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                IsBusy = true;
                SetStatus("Restoring backup. Please wait...", "#F59E0B");

                BackupResult result = await _backupService.RestoreBackupAsync(
                    filePath,
                    "BackOffice");

                await SaveHistorySafeAsync(
                    BackupActionTypes.BackupRestored,
                    result,
                    "BackOffice");

                if (result.Success)
                {
                    LastRestoreText =
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {result.BackupFileName}";

                    SetStatus(
                        "Backup restored successfully. Restart the application before continuing work.",
                        "#10B981");

                    MessageBox.Show(
                        "Backup restored successfully.\n\nPlease close and restart the application before continuing work.",
                        "Restore Completed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    SetStatus(result.Message, "#EF4444");
                }

                await LoadDataCoreSafeAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"Restore failed: {ex.Message}", "#EF4444");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task VerifyBackupAsync()
        {
            if (IsBusy)
                return;

            string filePath = SelectedBackupFile;

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                filePath = SelectBackupFile();

                if (string.IsNullOrWhiteSpace(filePath))
                    return;

                SelectedBackupFile = filePath;
            }

            try
            {
                IsBusy = true;
                SetStatus("Verifying backup file...", "#3B82F6");

                BackupResult result = await _backupService.VerifyBackupAsync(filePath);

                VerifyResultText = result.Message;

                await SaveHistorySafeAsync(
                    BackupActionTypes.BackupVerified,
                    result,
                    "BackOffice");

                if (result.Success)
                {
                    SetStatus("Backup file verified successfully.", "#10B981");
                }
                else
                {
                    SetStatus(result.Message, "#EF4444");
                }

                await LoadDataCoreAsync();
            }
            catch (Exception ex)
            {
                VerifyResultText = $"Verification failed: {ex.Message}";
                SetStatus(VerifyResultText, "#EF4444");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void SelectBackupFileForAction()
        {
            string filePath = SelectBackupFile();

            if (string.IsNullOrWhiteSpace(filePath))
                return;

            SelectedBackupFile = filePath;

            SetStatus("Backup file selected.", "#3B82F6");
        }

        [RelayCommand]
        private void OpenBackupFolder()
        {
            try
            {
                _backupService.OpenBackupFolder();

                BackupFolder = _backupService.GetDefaultBackupFolder();

                SetStatus("Backup folder opened.", "#10B981");
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to open backup folder: {ex.Message}", "#EF4444");
            }
        }

        [RelayCommand]
        private void CopyBackupFolder()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(BackupFolder))
                {
                    SetStatus("Backup folder path is empty.", "#EF4444");
                    return;
                }

                Clipboard.SetText(BackupFolder);

                SetStatus("Backup folder path copied to clipboard.", "#10B981");
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to copy backup folder path: {ex.Message}", "#EF4444");
            }
        }

        [RelayCommand]
        private void ClearSelectedBackupFile()
        {
            SelectedBackupFile = string.Empty;
            VerifyResultText = "No backup file verified yet.";

            SetStatus("Selected backup file cleared.", "#64748B");
        }

        // =========================================================
        // PRIVATE METHODS
        // =========================================================

        private async Task LoadDataCoreAsync()
        {
            BackupFolder = _backupService.GetDefaultBackupFolder();

            BackupHistory? latestBackup = await _backupRepository.GetLatestBackupAsync();
            BackupHistory? latestRestore = await _backupRepository.GetLatestRestoreAsync();

            LastBackupText = latestBackup == null
                ? "No backup created yet."
                : $"{latestBackup.CreatedAtText} - {latestBackup.BackupFileNameDisplay}";

            LastRestoreText = latestRestore == null
                ? "No restore recorded yet."
                : $"{latestRestore.CreatedAtText} - {latestRestore.BackupFileNameDisplay}";

            var history = await _backupRepository.GetRecentHistoryAsync(50);

            RecentHistory.Clear();

            foreach (var row in history)
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
                // After restore, the user should restart the app.
                // Do not hide the successful restore message because of a history reload issue.
            }
        }

        private async Task SaveHistorySafeAsync(
            string actionType,
            BackupResult result,
            string createdBy)
        {
            try
            {
                var terminalSettings = await _terminalSettingsRepository
                    .GetOrCreateForCurrentMachineAsync("01");

                string machineName = _machineFingerprintService.GetMachineName();
                string terminalNo = terminalSettings.TerminalNo;

                BackupHistory history = BackupRepository.CreateHistoryFromResult(
                    actionType,
                    result,
                    createdBy,
                    machineName,
                    terminalNo);

                await _backupRepository.AddHistoryAsync(history);
            }
            catch
            {
                // Backup/restore/verify operation result should not fail only because history logging failed.
            }
        }

        private static string SelectBackupFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Backup File",
                Filter = "POS Backup Files (*.posbak)|*.posbak|All Files (*.*)|*.*",
                Multiselect = false,
                CheckFileExists = true
            };

            bool? result = dialog.ShowDialog();

            if (result != true)
                return string.Empty;

            return dialog.FileName;
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