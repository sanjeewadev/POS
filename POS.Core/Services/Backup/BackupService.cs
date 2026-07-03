using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models.Backup;
using POS.Core.Repositories;
using POS.Core.Services.Licensing;

namespace POS.Core.Services.Backup
{
    public class BackupService
    {
        private const string BackupExtension = ".posbak";
        private const string DatabaseEntryName = "pos_local.db";
        private const string MetadataEntryName = "backup-info.json";
        private const string ChecksumEntryName = "checksum.txt";
        private const string AppVersionEntryName = "app-version.txt";
        private const string StoreIdEntryName = "store-id.txt";

        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly StoreSettingsRepository _storeSettingsRepository;
        private readonly TerminalSettingsRepository _terminalSettingsRepository;
        private readonly MachineFingerprintService _machineFingerprintService;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true
        };

        public BackupService(
            IDbContextFactory<AppDbContext> contextFactory,
            StoreSettingsRepository storeSettingsRepository,
            TerminalSettingsRepository terminalSettingsRepository,
            MachineFingerprintService machineFingerprintService)
        {
            _contextFactory = contextFactory;
            _storeSettingsRepository = storeSettingsRepository;
            _terminalSettingsRepository = terminalSettingsRepository;
            _machineFingerprintService = machineFingerprintService;
        }

        public async Task<BackupResult> CreateBackupAsync(string createdBy)
        {
            string tempFolder = CreateTempFolder();

            try
            {
                string backupFolder = GetDefaultBackupFolder();
                Directory.CreateDirectory(backupFolder);

                string databasePath = await GetCurrentDatabaseFilePathAsync();

                if (!File.Exists(databasePath))
                    throw new FileNotFoundException("Current database file was not found.", databasePath);

                var storeSettings = await _storeSettingsRepository.GetOrCreateDefaultAsync();
                var terminalSettings = await _terminalSettingsRepository.GetOrCreateForCurrentMachineAsync("01");

                string storeName = string.IsNullOrWhiteSpace(storeSettings.StoreName)
                    ? "Store"
                    : storeSettings.StoreName.Trim();

                string safeStoreName = MakeSafeFileName(storeName);

                DateTime now = DateTime.Now;

                string backupFileName =
                    $"{safeStoreName}_Backup_{now:yyyyMMdd_HHmmss}{BackupExtension}";

                string backupFilePath = Path.Combine(backupFolder, backupFileName);

                string tempDatabasePath = Path.Combine(tempFolder, DatabaseEntryName);

                await CreateSqliteDatabaseBackupAsync(databasePath, tempDatabasePath);

                string checksum = await CalculateSha256Async(tempDatabasePath);

                var metadata = new BackupMetadata
                {
                    StoreId = string.IsNullOrWhiteSpace(storeSettings.Brn)
                        ? storeSettings.StoreName
                        : storeSettings.Brn,
                    StoreName = storeSettings.StoreName,
                    CreatedAt = now,
                    CreatedBy = NormalizeText(createdBy),
                    AppName = "Advance POS System",
                    AppVersion = GetAppVersion(),
                    DatabaseProvider = "SQLite",
                    DatabaseFileName = DatabaseEntryName,
                    MachineName = _machineFingerprintService.GetMachineName(),
                    TerminalNo = terminalSettings.TerminalNo,
                    Checksum = checksum,
                    Remarks = "Manual backup created from BackOffice."
                };

                string metadataPath = Path.Combine(tempFolder, MetadataEntryName);
                string checksumPath = Path.Combine(tempFolder, ChecksumEntryName);
                string appVersionPath = Path.Combine(tempFolder, AppVersionEntryName);
                string storeIdPath = Path.Combine(tempFolder, StoreIdEntryName);

                await File.WriteAllTextAsync(
                    metadataPath,
                    JsonSerializer.Serialize(metadata, _jsonOptions));

                await File.WriteAllTextAsync(checksumPath, checksum);
                await File.WriteAllTextAsync(appVersionPath, metadata.AppVersion);
                await File.WriteAllTextAsync(storeIdPath, metadata.StoreId);

                if (File.Exists(backupFilePath))
                    File.Delete(backupFilePath);

                using (var archive = ZipFile.Open(backupFilePath, ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(metadataPath, MetadataEntryName, CompressionLevel.Optimal);
                    archive.CreateEntryFromFile(tempDatabasePath, DatabaseEntryName, CompressionLevel.Optimal);
                    archive.CreateEntryFromFile(checksumPath, ChecksumEntryName, CompressionLevel.Optimal);
                    archive.CreateEntryFromFile(appVersionPath, AppVersionEntryName, CompressionLevel.Optimal);
                    archive.CreateEntryFromFile(storeIdPath, StoreIdEntryName, CompressionLevel.Optimal);
                }

                var fileInfo = new FileInfo(backupFilePath);

                return new BackupResult
                {
                    Success = true,
                    Message = "Backup created successfully.",
                    BackupFilePath = backupFilePath,
                    BackupFileName = backupFileName,
                    CreatedAt = now,
                    FileSizeBytes = fileInfo.Length,
                    Checksum = checksum
                };
            }
            catch (Exception ex)
            {
                return new BackupResult
                {
                    Success = false,
                    Message = $"Backup failed: {ex.Message}",
                    CreatedAt = DateTime.Now
                };
            }
            finally
            {
                TryDeleteDirectory(tempFolder);
            }
        }

        public async Task<BackupResult> RestoreBackupAsync(
            string backupFilePath,
            string restoredBy)
        {
            string tempFolder = string.Empty;
            string safetyCopyPath = string.Empty;

            try
            {
                ValidateBackupFilePath(backupFilePath);

                BackupPackage package = await ExtractAndValidateBackupAsync(backupFilePath);
                tempFolder = package.TempFolder;

                string currentDatabasePath = await GetCurrentDatabaseFilePathAsync();

                if (string.IsNullOrWhiteSpace(currentDatabasePath))
                    throw new InvalidOperationException("Current database path could not be resolved.");

                string currentDatabaseFolder = Path.GetDirectoryName(currentDatabasePath)
                    ?? throw new InvalidOperationException("Current database folder could not be resolved.");

                Directory.CreateDirectory(currentDatabaseFolder);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                safetyCopyPath = Path.Combine(
                    currentDatabaseFolder,
                    $"pos_local_before_restore_{timestamp}.db");

                SqliteConnection.ClearAllPools();

                if (File.Exists(currentDatabasePath))
                    File.Copy(currentDatabasePath, safetyCopyPath, overwrite: true);

                DeleteSqliteSidecarFiles(currentDatabasePath);

                File.Copy(package.DatabaseFilePath, currentDatabasePath, overwrite: true);

                DeleteSqliteSidecarFiles(currentDatabasePath);

                return new BackupResult
                {
                    Success = true,
                    Message = "Backup restored successfully. Restart the application before continuing work.",
                    BackupFilePath = backupFilePath,
                    BackupFileName = Path.GetFileName(backupFilePath),
                    CreatedAt = DateTime.Now,
                    FileSizeBytes = new FileInfo(backupFilePath).Length,
                    Checksum = package.Checksum
                };
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(safetyCopyPath) &&
                    File.Exists(safetyCopyPath))
                {
                    try
                    {
                        string currentDatabasePath = await GetCurrentDatabaseFilePathAsync();

                        SqliteConnection.ClearAllPools();
                        DeleteSqliteSidecarFiles(currentDatabasePath);
                        File.Copy(safetyCopyPath, currentDatabasePath, overwrite: true);
                    }
                    catch
                    {
                        // Do not hide original restore error.
                    }
                }

                return new BackupResult
                {
                    Success = false,
                    Message = $"Restore failed: {ex.Message}",
                    BackupFilePath = backupFilePath,
                    BackupFileName = Path.GetFileName(backupFilePath),
                    CreatedAt = DateTime.Now
                };
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(tempFolder))
                    TryDeleteDirectory(tempFolder);
            }
        }

        public async Task<BackupResult> VerifyBackupAsync(string backupFilePath)
        {
            string tempFolder = string.Empty;

            try
            {
                ValidateBackupFilePath(backupFilePath);

                BackupPackage package = await ExtractAndValidateBackupAsync(backupFilePath);
                tempFolder = package.TempFolder;

                var fileInfo = new FileInfo(backupFilePath);

                return new BackupResult
                {
                    Success = true,
                    Message =
                        $"Backup verified successfully. Store: {package.Metadata.StoreDisplayName}. Created: {package.Metadata.CreatedAtText}.",
                    BackupFilePath = backupFilePath,
                    BackupFileName = Path.GetFileName(backupFilePath),
                    CreatedAt = package.Metadata.CreatedAt,
                    FileSizeBytes = fileInfo.Length,
                    Checksum = package.Checksum
                };
            }
            catch (Exception ex)
            {
                return new BackupResult
                {
                    Success = false,
                    Message = $"Backup verification failed: {ex.Message}",
                    BackupFilePath = backupFilePath,
                    BackupFileName = Path.GetFileName(backupFilePath),
                    CreatedAt = DateTime.Now
                };
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(tempFolder))
                    TryDeleteDirectory(tempFolder);
            }
        }

        public string GetDefaultBackupFolder()
        {
            string documentsPath = Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments);

            if (string.IsNullOrWhiteSpace(documentsPath))
            {
                documentsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Advance POS System");
            }

            return Path.Combine(documentsPath, "Advance POS Backups");
        }

        public void OpenBackupFolder()
        {
            string backupFolder = GetDefaultBackupFolder();

            Directory.CreateDirectory(backupFolder);

            Process.Start(new ProcessStartInfo
            {
                FileName = backupFolder,
                UseShellExecute = true
            });
        }

        private async Task<string> GetCurrentDatabaseFilePathAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            string dataSource = context.Database.GetDbConnection().DataSource;

            if (string.IsNullOrWhiteSpace(dataSource))
                throw new InvalidOperationException("Database file path could not be detected.");

            return Path.GetFullPath(dataSource);
        }

        private static async Task CreateSqliteDatabaseBackupAsync(
            string sourceDatabasePath,
            string destinationDatabasePath)
        {
            if (File.Exists(destinationDatabasePath))
                File.Delete(destinationDatabasePath);

            string? destinationFolder = Path.GetDirectoryName(destinationDatabasePath);

            if (!string.IsNullOrWhiteSpace(destinationFolder))
                Directory.CreateDirectory(destinationFolder);

            await using var sourceConnection =
                new SqliteConnection($"Data Source={sourceDatabasePath}");

            await using var destinationConnection =
                new SqliteConnection($"Data Source={destinationDatabasePath}");

            await sourceConnection.OpenAsync();
            await destinationConnection.OpenAsync();

            sourceConnection.BackupDatabase(destinationConnection);
        }

        private async Task<BackupPackage> ExtractAndValidateBackupAsync(string backupFilePath)
        {
            string tempFolder = CreateTempFolder();

            try
            {
                string metadataPath = Path.Combine(tempFolder, MetadataEntryName);
                string databasePath = Path.Combine(tempFolder, DatabaseEntryName);
                string checksumPath = Path.Combine(tempFolder, ChecksumEntryName);

                using (var archive = ZipFile.OpenRead(backupFilePath))
                {
                    ExtractRequiredEntry(archive, MetadataEntryName, metadataPath);
                    ExtractRequiredEntry(archive, DatabaseEntryName, databasePath);
                    ExtractRequiredEntry(archive, ChecksumEntryName, checksumPath);
                }

                string metadataJson = await File.ReadAllTextAsync(metadataPath);

                var metadata = JsonSerializer.Deserialize<BackupMetadata>(metadataJson);

                if (metadata == null)
                    throw new InvalidOperationException("Backup metadata is invalid.");

                if (!File.Exists(databasePath))
                    throw new InvalidOperationException("Backup database file is missing.");

                string actualChecksum = await CalculateSha256Async(databasePath);
                string checksumFileValue = (await File.ReadAllTextAsync(checksumPath)).Trim();

                if (string.IsNullOrWhiteSpace(metadata.Checksum))
                    throw new InvalidOperationException("Backup metadata checksum is missing.");

                if (!string.Equals(metadata.Checksum, actualChecksum, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Backup metadata checksum does not match the database file.");

                if (!string.Equals(checksumFileValue, actualChecksum, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Backup checksum file does not match the database file.");

                return new BackupPackage
                {
                    TempFolder = tempFolder,
                    DatabaseFilePath = databasePath,
                    Metadata = metadata,
                    Checksum = actualChecksum
                };
            }
            catch
            {
                TryDeleteDirectory(tempFolder);
                throw;
            }
        }

        private static void ExtractRequiredEntry(
            ZipArchive archive,
            string entryName,
            string destinationPath)
        {
            var entry = archive.Entries.FirstOrDefault(e =>
                string.Equals(e.FullName, entryName, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
                throw new InvalidOperationException($"Backup package is missing '{entryName}'.");

            entry.ExtractToFile(destinationPath, overwrite: true);
        }

        private static void ValidateBackupFilePath(string backupFilePath)
        {
            if (string.IsNullOrWhiteSpace(backupFilePath))
                throw new InvalidOperationException("Backup file path is required.");

            if (!File.Exists(backupFilePath))
                throw new FileNotFoundException("Backup file was not found.", backupFilePath);

            string extension = Path.GetExtension(backupFilePath);

            if (!string.Equals(extension, BackupExtension, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid backup file type. Please select a .posbak file.");
        }

        private static async Task<string> CalculateSha256Async(string filePath)
        {
            await using FileStream stream = File.OpenRead(filePath);

            byte[] hashBytes = await SHA256.HashDataAsync(stream);

            return Convert.ToHexString(hashBytes);
        }

        private static string CreateTempFolder()
        {
            string tempFolder = Path.Combine(
                Path.GetTempPath(),
                "AdvancePOS_Backup_" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(tempFolder);

            return tempFolder;
        }

        private static void TryDeleteDirectory(string folderPath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(folderPath) &&
                    Directory.Exists(folderPath))
                {
                    Directory.Delete(folderPath, recursive: true);
                }
            }
            catch
            {
                // Temporary cleanup failure should not break the main operation.
            }
        }

        private static void DeleteSqliteSidecarFiles(string databasePath)
        {
            TryDeleteFile(databasePath + "-wal");
            TryDeleteFile(databasePath + "-shm");
            TryDeleteFile(databasePath + "-journal");
        }

        private static void TryDeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
                // Ignore cleanup failure.
            }
        }

        private static string GetAppVersion()
        {
            Version? version = Assembly.GetEntryAssembly()?.GetName().Version
                               ?? Assembly.GetExecutingAssembly().GetName().Version;

            return version?.ToString() ?? "1.0.0.0";
        }

        private static string MakeSafeFileName(string value)
        {
            string safeValue = NormalizeText(value);

            if (string.IsNullOrWhiteSpace(safeValue))
                safeValue = "Store";

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
                safeValue = safeValue.Replace(invalidChar, '_');

            return safeValue.Replace(' ', '_');
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private class BackupPackage
        {
            public string TempFolder { get; set; } = string.Empty;

            public string DatabaseFilePath { get; set; } = string.Empty;

            public BackupMetadata Metadata { get; set; } = new();

            public string Checksum { get; set; } = string.Empty;
        }
    }
}