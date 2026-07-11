using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models.Backup;
using POS.Core.Models.Licensing;
using POS.Core.Repositories;
using POS.Core.Services.Licensing;

namespace POS.Core.Services.Backup
{
    public class BackupService
    {
        public const string BackupExtension = ".posbackup";
        public const string LegacyBackupExtension = ".posbak";

        private const int CurrentFormatVersion = 1;
        private const string DatabaseEntryName = "pos_local.db";
        private const string MetadataEntryName = "backup-info.json";
        private const string ChecksumEntryName = "checksum.txt";

        private static readonly string[] RequiredTables =
        {
            "__EFMigrationsHistory",
            "Users",
            "StoreSettings",
            "TerminalSettings",
            "InstalledLicenses",
            "BackupHistory"
        };

        private readonly IDbContextFactory<AppDbContext>
            _contextFactory;

        private readonly StoreSettingsRepository
            _storeSettingsRepository;

        private readonly TerminalSettingsRepository
            _terminalSettingsRepository;

        private readonly MachineFingerprintService
            _machineFingerprintService;

        private readonly JsonSerializerOptions _jsonOptions =
            new()
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = false
            };

        public BackupService(
            IDbContextFactory<AppDbContext> contextFactory,
            StoreSettingsRepository storeSettingsRepository,
            TerminalSettingsRepository terminalSettingsRepository,
            MachineFingerprintService machineFingerprintService)
        {
            _contextFactory = contextFactory;
            _storeSettingsRepository =
                storeSettingsRepository;
            _terminalSettingsRepository =
                terminalSettingsRepository;
            _machineFingerprintService =
                machineFingerprintService;
        }

        public async Task<BackupMetadata>
            GetCurrentDatabaseInfoAsync()
        {
            string databasePath =
                DatabasePathProvider.DatabaseFilePath;

            if (!File.Exists(databasePath))
            {
                throw new FileNotFoundException(
                    "The current POS database was not found.",
                    databasePath);
            }

            return await BuildCurrentMetadataAsync(
                databasePath,
                createdBy: string.Empty,
                includeChecksum: false);
        }

        public string GetDefaultBackupFolder()
        {
            string documentsPath =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments);

            if (string.IsNullOrWhiteSpace(documentsPath))
            {
                documentsPath = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder
                            .LocalApplicationData),
                    "Advanced POS System");
            }

            return Path.Combine(
                documentsPath,
                "Advanced POS Backups");
        }

        public string GetSuggestedBackupFileName(
            string? storeName = null)
        {
            string safeStoreName =
                MakeSafeFileName(storeName);

            return
                $"POS_Backup_{safeStoreName}_" +
                $"{DateTime.Now:yyyyMMdd_HHmmss}" +
                BackupExtension;
        }

        public void OpenBackupFolder()
        {
            string folder = GetDefaultBackupFolder();
            Directory.CreateDirectory(folder);

            Process.Start(
                new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
        }

        public async Task<BackupResult> CreateBackupAsync(
            string destinationFilePath,
            string createdBy)
        {
            string tempFolder = CreateTempFolder();
            string temporaryPackagePath = string.Empty;

            try
            {
                string finalPath =
                    NormalizeNewBackupPath(
                        destinationFilePath);

                string destinationFolder =
                    Path.GetDirectoryName(finalPath)
                    ?? throw new InvalidOperationException(
                        "Backup destination folder is invalid.");

                Directory.CreateDirectory(destinationFolder);

                string currentDatabasePath =
                    DatabasePathProvider.DatabaseFilePath;

                if (!File.Exists(currentDatabasePath))
                {
                    throw new FileNotFoundException(
                        "The current POS database was not found.",
                        currentDatabasePath);
                }

                string snapshotPath = Path.Combine(
                    tempFolder,
                    DatabaseEntryName);

                await CreateSqliteSnapshotAsync(
                    currentDatabasePath,
                    snapshotPath);

                DatabaseValidation validation =
                    await ValidatePosDatabaseAsync(snapshotPath);

                string checksum =
                    await CalculateSha256Async(snapshotPath);

                BackupMetadata metadata =
                    await BuildCurrentMetadataAsync(
                        snapshotPath,
                        createdBy,
                        includeChecksum: false);

                metadata.FormatVersion =
                    CurrentFormatVersion;
                metadata.Checksum = checksum;
                metadata.DatabaseSizeBytes =
                    new FileInfo(snapshotPath).Length;
                metadata.DatabaseMigrationId =
                    validation.LatestMigrationId;
                metadata.Remarks =
                    "Manual backup created from BackOffice.";

                string metadataPath = Path.Combine(
                    tempFolder,
                    MetadataEntryName);

                string checksumPath = Path.Combine(
                    tempFolder,
                    ChecksumEntryName);

                await File.WriteAllTextAsync(
                    metadataPath,
                    JsonSerializer.Serialize(
                        metadata,
                        _jsonOptions),
                    new UTF8Encoding(false));

                await File.WriteAllTextAsync(
                    checksumPath,
                    checksum,
                    new UTF8Encoding(false));

                temporaryPackagePath =
                    Path.Combine(
                        destinationFolder,
                        $".{Path.GetFileName(finalPath)}." +
                        $"{Guid.NewGuid():N}.tmp");

                using (ZipArchive archive =
                       ZipFile.Open(
                           temporaryPackagePath,
                           ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(
                        metadataPath,
                        MetadataEntryName,
                        CompressionLevel.Optimal);

                    archive.CreateEntryFromFile(
                        snapshotPath,
                        DatabaseEntryName,
                        CompressionLevel.Optimal);

                    archive.CreateEntryFromFile(
                        checksumPath,
                        ChecksumEntryName,
                        CompressionLevel.Optimal);
                }

                BackupPackage verifiedPackage =
                    await ExtractAndValidateBackupAsync(
                        temporaryPackagePath,
                        allowTemporaryExtension: true);

                TryDeleteDirectory(
                    verifiedPackage.TempFolder);

                File.Move(
                    temporaryPackagePath,
                    finalPath,
                    overwrite: true);

                temporaryPackagePath = string.Empty;

                FileInfo fileInfo = new(finalPath);

                return new BackupResult
                {
                    Success = true,
                    Message =
                        "Manual backup created and verified successfully.",
                    BackupFilePath = finalPath,
                    BackupFileName =
                        Path.GetFileName(finalPath),
                    CreatedAt = metadata.CreatedAt,
                    FileSizeBytes = fileInfo.Length,
                    Checksum = checksum,
                    Metadata = metadata
                };
            }
            catch (Exception ex)
            {
                return new BackupResult
                {
                    Success = false,
                    Message =
                        $"Backup failed: {ex.Message}",
                    BackupFilePath =
                        destinationFilePath ?? string.Empty,
                    BackupFileName =
                        string.IsNullOrWhiteSpace(
                            destinationFilePath)
                            ? string.Empty
                            : Path.GetFileName(
                                destinationFilePath),
                    CreatedAt = DateTime.Now
                };
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(
                        temporaryPackagePath))
                {
                    TryDeleteFile(temporaryPackagePath);
                }

                TryDeleteDirectory(tempFolder);
            }
        }

        public async Task<BackupResult> VerifyBackupAsync(
            string backupFilePath)
        {
            string tempFolder = string.Empty;

            try
            {
                ValidateBackupFilePath(backupFilePath);

                BackupPackage package =
                    await ExtractAndValidateBackupAsync(
                        backupFilePath);

                tempFolder = package.TempFolder;

                FileInfo fileInfo =
                    new(backupFilePath);

                return new BackupResult
                {
                    Success = true,
                    Message =
                        "Backup verified successfully.",
                    BackupFilePath = backupFilePath,
                    BackupFileName =
                        Path.GetFileName(backupFilePath),
                    CreatedAt =
                        package.Metadata.CreatedAt,
                    FileSizeBytes = fileInfo.Length,
                    Checksum = package.Checksum,
                    Metadata = package.Metadata
                };
            }
            catch (Exception ex)
            {
                return new BackupResult
                {
                    Success = false,
                    Message =
                        $"Backup verification failed: " +
                        $"{ex.Message}",
                    BackupFilePath =
                        backupFilePath ?? string.Empty,
                    BackupFileName =
                        string.IsNullOrWhiteSpace(
                            backupFilePath)
                            ? string.Empty
                            : Path.GetFileName(
                                backupFilePath),
                    CreatedAt = DateTime.Now
                };
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(tempFolder))
                    TryDeleteDirectory(tempFolder);
            }
        }

        public async Task<BackupResult> RestoreBackupAsync(
            string backupFilePath,
            string restoredBy)
        {
            string packageTempFolder = string.Empty;
            string stagedDatabasePath = string.Empty;
            string safetyCopyPath = string.Empty;
            bool databaseWasReplaced = false;

            string currentDatabasePath =
                DatabasePathProvider.DatabaseFilePath;

            try
            {
                ValidateBackupFilePath(backupFilePath);

                BackupPackage package =
                    await ExtractAndValidateBackupAsync(
                        backupFilePath);

                packageTempFolder = package.TempFolder;

                await EnsureBackupMigrationIsSupportedAsync(
                    package.Validation.LatestMigrationId);

                string currentDatabaseFolder =
                    DatabasePathProvider.DatabaseFolderPath;

                Directory.CreateDirectory(
                    currentDatabaseFolder);

                string restoreSafetyFolder =
                    Path.Combine(
                        currentDatabaseFolder,
                        "RestoreSafety");

                Directory.CreateDirectory(
                    restoreSafetyFolder);

                DeleteOldRestoreSafetyFiles(
                    restoreSafetyFolder);

                safetyCopyPath = Path.Combine(
                    restoreSafetyFolder,
                    $"BeforeRestore_" +
                    $"{DateTime.Now:yyyyMMdd_HHmmss}.db");

                if (File.Exists(currentDatabasePath))
                {
                    await CreateSqliteSnapshotAsync(
                        currentDatabasePath,
                        safetyCopyPath);

                    await ValidatePosDatabaseAsync(
                        safetyCopyPath);
                }

                stagedDatabasePath = Path.Combine(
                    currentDatabaseFolder,
                    $"pos_local.restore." +
                    $"{Guid.NewGuid():N}.tmp");

                File.Copy(
                    package.DatabaseFilePath,
                    stagedDatabasePath,
                    overwrite: true);

                await ValidatePosDatabaseAsync(
                    stagedDatabasePath);

                SqliteConnection.ClearAllPools();
                DeleteSqliteSidecarFiles(
                    currentDatabasePath);

                if (File.Exists(currentDatabasePath))
                {
                    File.Replace(
                        stagedDatabasePath,
                        currentDatabasePath,
                        destinationBackupFileName: null,
                        ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(
                        stagedDatabasePath,
                        currentDatabasePath);
                }

                stagedDatabasePath = string.Empty;
                databaseWasReplaced = true;

                DeleteSqliteSidecarFiles(
                    currentDatabasePath);

                await ValidatePosDatabaseAsync(
                    currentDatabasePath);

                return new BackupResult
                {
                    Success = true,
                    Message =
                        "Backup restored successfully. " +
                        "BackOffice must restart before work continues.",
                    BackupFilePath = backupFilePath,
                    BackupFileName =
                        Path.GetFileName(backupFilePath),
                    CreatedAt = DateTime.Now,
                    FileSizeBytes =
                        new FileInfo(backupFilePath).Length,
                    Checksum = package.Checksum,
                    SafetyCopyPath = safetyCopyPath,
                    Metadata = package.Metadata
                };
            }
            catch (Exception ex)
            {
                if (databaseWasReplaced &&
                    !string.IsNullOrWhiteSpace(
                        safetyCopyPath) &&
                    File.Exists(safetyCopyPath))
                {
                    try
                    {
                        SqliteConnection.ClearAllPools();
                        DeleteSqliteSidecarFiles(
                            currentDatabasePath);

                        string rollbackStagingPath =
                            currentDatabasePath +
                            $".rollback.{Guid.NewGuid():N}.tmp";

                        File.Copy(
                            safetyCopyPath,
                            rollbackStagingPath,
                            overwrite: true);

                        if (File.Exists(
                                currentDatabasePath))
                        {
                            File.Replace(
                                rollbackStagingPath,
                                currentDatabasePath,
                                destinationBackupFileName:
                                    null,
                                ignoreMetadataErrors: true);
                        }
                        else
                        {
                            File.Move(
                                rollbackStagingPath,
                                currentDatabasePath);
                        }

                        DeleteSqliteSidecarFiles(
                            currentDatabasePath);
                    }
                    catch
                    {
                        // Preserve the original restore error.
                    }
                }

                return new BackupResult
                {
                    Success = false,
                    Message =
                        $"Restore failed: {ex.Message}",
                    BackupFilePath =
                        backupFilePath ?? string.Empty,
                    BackupFileName =
                        string.IsNullOrWhiteSpace(
                            backupFilePath)
                            ? string.Empty
                            : Path.GetFileName(
                                backupFilePath),
                    CreatedAt = DateTime.Now,
                    SafetyCopyPath = safetyCopyPath
                };
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(
                        stagedDatabasePath))
                {
                    TryDeleteFile(stagedDatabasePath);
                }

                if (!string.IsNullOrWhiteSpace(
                        packageTempFolder))
                {
                    TryDeleteDirectory(
                        packageTempFolder);
                }
            }
        }

        private async Task<BackupMetadata>
            BuildCurrentMetadataAsync(
                string databasePath,
                string createdBy,
                bool includeChecksum)
        {
            var storeSettings =
                await _storeSettingsRepository
                    .GetOrCreateDefaultAsync();

            var terminalSettings =
                await _terminalSettingsRepository
                    .GetOrCreateForCurrentMachineAsync();

            string storeId = string.IsNullOrWhiteSpace(
                storeSettings.Brn)
                ? storeSettings.StoreName
                : storeSettings.Brn;

            await using AppDbContext context =
                await _contextFactory
                    .CreateDbContextAsync();

            var storeLicense =
                await context.InstalledLicenses
                    .AsNoTracking()
                    .Where(license =>
                        license.IsActive &&
                        license.LicenseType ==
                            LicenseType.StoreLicense)
                    .OrderByDescending(
                        license => license.ExpiresOn)
                    .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(
                    storeLicense?.StoreId))
            {
                storeId = storeLicense.StoreId;
            }

            DatabaseValidation validation =
                await ValidatePosDatabaseAsync(
                    databasePath);

            return new BackupMetadata
            {
                FormatVersion =
                    CurrentFormatVersion,
                StoreId =
                    NormalizeText(storeId),
                StoreName =
                    NormalizeText(
                        storeSettings.StoreName),
                CreatedAt = DateTime.Now,
                CreatedBy =
                    NormalizeText(createdBy),
                AppName =
                    "Advanced POS System",
                AppVersion = GetAppVersion(),
                DatabaseProvider = "SQLite",
                DatabaseFileName =
                    Path.GetFileName(databasePath),
                DatabaseSizeBytes =
                    new FileInfo(databasePath).Length,
                DatabaseMigrationId =
                    validation.LatestMigrationId,
                MachineName =
                    _machineFingerprintService
                        .GetMachineName(),
                TerminalNo =
                    NormalizeText(
                        terminalSettings.TerminalNo),
                Checksum = includeChecksum
                    ? await CalculateSha256Async(
                        databasePath)
                    : string.Empty,
                Remarks = string.Empty
            };
        }

        private async Task EnsureBackupMigrationIsSupportedAsync(
            string backupMigrationId)
        {
            if (string.IsNullOrWhiteSpace(
                    backupMigrationId))
            {
                throw new InvalidOperationException(
                    "Backup database migration information is missing.");
            }

            await using AppDbContext context =
                await _contextFactory
                    .CreateDbContextAsync();

            IReadOnlyList<string> applicationMigrations =
                context.Database
                    .GetMigrations()
                    .ToList();

            if (!applicationMigrations.Contains(
                    backupMigrationId,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "This backup was created by a newer or incompatible POS version.");
            }
        }

        private static async Task
            CreateSqliteSnapshotAsync(
                string sourceDatabasePath,
                string destinationDatabasePath)
        {
            if (!File.Exists(sourceDatabasePath))
            {
                throw new FileNotFoundException(
                    "Source database file was not found.",
                    sourceDatabasePath);
            }

            TryDeleteFile(destinationDatabasePath);

            string? destinationFolder =
                Path.GetDirectoryName(
                    destinationDatabasePath);

            if (!string.IsNullOrWhiteSpace(
                    destinationFolder))
            {
                Directory.CreateDirectory(
                    destinationFolder);
            }

            SqliteConnectionStringBuilder sourceBuilder =
                new()
                {
                    DataSource = sourceDatabasePath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false
                };

            SqliteConnectionStringBuilder
                destinationBuilder =
                    new()
                    {
                        DataSource =
                            destinationDatabasePath,
                        Mode = SqliteOpenMode.ReadWriteCreate,
                        Pooling = false
                    };

            await using SqliteConnection
                sourceConnection =
                    new(sourceBuilder.ToString());

            await using SqliteConnection
                destinationConnection =
                    new(destinationBuilder.ToString());

            await sourceConnection.OpenAsync();
            await destinationConnection.OpenAsync();

            sourceConnection.BackupDatabase(
                destinationConnection);
        }

        private async Task<BackupPackage>
            ExtractAndValidateBackupAsync(
                string backupFilePath,
                bool allowTemporaryExtension = false)
        {
            if (!allowTemporaryExtension)
                ValidateBackupFilePath(backupFilePath);

            string tempFolder = CreateTempFolder();

            try
            {
                string metadataPath = Path.Combine(
                    tempFolder,
                    MetadataEntryName);

                string databasePath = Path.Combine(
                    tempFolder,
                    DatabaseEntryName);

                string checksumPath = Path.Combine(
                    tempFolder,
                    ChecksumEntryName);

                using (ZipArchive archive =
                       ZipFile.OpenRead(backupFilePath))
                {
                    ExtractRequiredEntry(
                        archive,
                        MetadataEntryName,
                        metadataPath);

                    ExtractRequiredEntry(
                        archive,
                        DatabaseEntryName,
                        databasePath);

                    ExtractRequiredEntry(
                        archive,
                        ChecksumEntryName,
                        checksumPath);
                }

                string metadataJson =
                    await File.ReadAllTextAsync(
                        metadataPath);

                BackupMetadata? metadata =
                    JsonSerializer.Deserialize<
                        BackupMetadata>(
                        metadataJson,
                        _jsonOptions);

                if (metadata == null)
                {
                    throw new InvalidOperationException(
                        "Backup metadata is invalid.");
                }

                if (metadata.FormatVersion !=
                    CurrentFormatVersion)
                {
                    throw new InvalidOperationException(
                        "Unsupported backup format version.");
                }

                string actualChecksum =
                    await CalculateSha256Async(
                        databasePath);

                string checksumFileValue =
                    (await File.ReadAllTextAsync(
                        checksumPath)).Trim();

                if (string.IsNullOrWhiteSpace(
                        metadata.Checksum))
                {
                    throw new InvalidOperationException(
                        "Backup checksum is missing.");
                }

                if (!string.Equals(
                        metadata.Checksum,
                        actualChecksum,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        checksumFileValue,
                        actualChecksum,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Backup checksum does not match. " +
                        "The file is damaged or was modified.");
                }

                FileInfo databaseFile =
                    new(databasePath);

                if (metadata.DatabaseSizeBytes > 0 &&
                    metadata.DatabaseSizeBytes !=
                        databaseFile.Length)
                {
                    throw new InvalidOperationException(
                        "Backup database size does not match its metadata.");
                }

                DatabaseValidation validation =
                    await ValidatePosDatabaseAsync(
                        databasePath);

                if (!string.IsNullOrWhiteSpace(
                        metadata.DatabaseMigrationId) &&
                    !string.Equals(
                        metadata.DatabaseMigrationId,
                        validation.LatestMigrationId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Backup migration information does not match the database.");
                }

                metadata.DatabaseSizeBytes =
                    databaseFile.Length;

                metadata.DatabaseMigrationId =
                    validation.LatestMigrationId;

                return new BackupPackage
                {
                    TempFolder = tempFolder,
                    DatabaseFilePath =
                        databasePath,
                    Metadata = metadata,
                    Checksum = actualChecksum,
                    Validation = validation
                };
            }
            catch
            {
                TryDeleteDirectory(tempFolder);
                throw;
            }
        }

        private static async Task<DatabaseValidation>
            ValidatePosDatabaseAsync(string databasePath)
        {
            if (!File.Exists(databasePath))
            {
                throw new FileNotFoundException(
                    "Backup database file is missing.",
                    databasePath);
            }

            SqliteConnectionStringBuilder builder =
                new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false
                };

            await using SqliteConnection connection =
                new(builder.ToString());

            await connection.OpenAsync();

            await using (SqliteCommand checkCommand =
                         connection.CreateCommand())
            {
                checkCommand.CommandText =
                    "PRAGMA quick_check;";

                object? result =
                    await checkCommand
                        .ExecuteScalarAsync();

                if (!string.Equals(
                        result?.ToString(),
                        "ok",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "SQLite integrity check failed.");
                }
            }

            foreach (string table in RequiredTables)
            {
                await using SqliteCommand tableCommand =
                    connection.CreateCommand();

                tableCommand.CommandText =
                    "SELECT COUNT(*) " +
                    "FROM sqlite_master " +
                    "WHERE type = 'table' " +
                    "AND name = $tableName;";

                tableCommand.Parameters.AddWithValue(
                    "$tableName",
                    table);

                long count = Convert.ToInt64(
                    await tableCommand
                        .ExecuteScalarAsync());

                if (count != 1)
                {
                    throw new InvalidOperationException(
                        $"Required POS table '{table}' is missing.");
                }
            }

            await using SqliteCommand migrationCommand =
                connection.CreateCommand();

            migrationCommand.CommandText =
                "SELECT MigrationId " +
                "FROM __EFMigrationsHistory " +
                "ORDER BY MigrationId DESC " +
                "LIMIT 1;";

            string migrationId =
                Convert.ToString(
                    await migrationCommand
                        .ExecuteScalarAsync())
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(migrationId))
            {
                throw new InvalidOperationException(
                    "Database migration information is missing.");
            }

            return new DatabaseValidation
            {
                LatestMigrationId = migrationId
            };
        }

        private static void ExtractRequiredEntry(
            ZipArchive archive,
            string entryName,
            string destinationPath)
        {
            List<ZipArchiveEntry> matchingEntries =
                archive.Entries
                    .Where(entry =>
                        string.Equals(
                            entry.FullName,
                            entryName,
                            StringComparison
                                .OrdinalIgnoreCase))
                    .ToList();

            if (matchingEntries.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Backup package must contain exactly one '{entryName}' entry.");
            }

            ZipArchiveEntry entry =
                matchingEntries[0];

            if (entry.Length <= 0)
            {
                throw new InvalidOperationException(
                    $"Backup entry '{entryName}' is empty.");
            }

            entry.ExtractToFile(
                destinationPath,
                overwrite: true);
        }

        private static void ValidateBackupFilePath(
            string backupFilePath)
        {
            if (string.IsNullOrWhiteSpace(
                    backupFilePath))
            {
                throw new InvalidOperationException(
                    "Backup file path is required.");
            }

            if (!File.Exists(backupFilePath))
            {
                throw new FileNotFoundException(
                    "Backup file was not found.",
                    backupFilePath);
            }

            string extension =
                Path.GetExtension(backupFilePath);

            bool supported =
                string.Equals(
                    extension,
                    BackupExtension,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    extension,
                    LegacyBackupExtension,
                    StringComparison.OrdinalIgnoreCase);

            if (!supported)
            {
                throw new InvalidOperationException(
                    "Invalid backup file type. " +
                    "Select a .posbackup or legacy .posbak file.");
            }
        }

        private static string NormalizeNewBackupPath(
            string destinationFilePath)
        {
            if (string.IsNullOrWhiteSpace(
                    destinationFilePath))
            {
                throw new InvalidOperationException(
                    "Choose where to save the backup.");
            }

            string fullPath =
                Path.GetFullPath(
                    destinationFilePath.Trim());

            string extension =
                Path.GetExtension(fullPath);

            if (string.IsNullOrWhiteSpace(extension))
                return fullPath + BackupExtension;

            if (!string.Equals(
                    extension,
                    BackupExtension,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "New backups must use the .posbackup file type.");
            }

            return fullPath;
        }

        private static async Task<string>
            CalculateSha256Async(string filePath)
        {
            await using FileStream stream =
                File.OpenRead(filePath);

            byte[] hashBytes =
                await SHA256.HashDataAsync(stream);

            return Convert.ToHexString(hashBytes);
        }

        private static string CreateTempFolder()
        {
            string folder = Path.Combine(
                Path.GetTempPath(),
                "AdvancedPOS_Backup_" +
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(folder);
            return folder;
        }

        private static void DeleteOldRestoreSafetyFiles(
            string restoreSafetyFolder)
        {
            foreach (string oldFile in
                     Directory.GetFiles(
                         restoreSafetyFolder,
                         "BeforeRestore_*.db"))
            {
                TryDeleteFile(oldFile);
            }
        }

        private static void DeleteSqliteSidecarFiles(
            string databasePath)
        {
            TryDeleteFile(databasePath + "-wal");
            TryDeleteFile(databasePath + "-shm");
            TryDeleteFile(databasePath + "-journal");
        }

        private static void TryDeleteDirectory(
            string folderPath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(
                        folderPath) &&
                    Directory.Exists(folderPath))
                {
                    Directory.Delete(
                        folderPath,
                        recursive: true);
                }
            }
            catch
            {
                // Temporary cleanup failure is non-fatal.
            }
        }

        private static void TryDeleteFile(
            string filePath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(
                        filePath) &&
                    File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch
            {
                // Cleanup failure is non-fatal.
            }
        }

        private static string GetAppVersion()
        {
            Version? version =
                Assembly.GetEntryAssembly()
                    ?.GetName().Version
                ?? Assembly.GetExecutingAssembly()
                    .GetName().Version;

            return version?.ToString()
                   ?? "1.0.0.0";
        }

        private static string MakeSafeFileName(
            string? value)
        {
            string safeValue =
                NormalizeText(value);

            if (string.IsNullOrWhiteSpace(
                    safeValue))
            {
                safeValue = "Store";
            }

            foreach (char invalidCharacter in
                     Path.GetInvalidFileNameChars())
            {
                safeValue =
                    safeValue.Replace(
                        invalidCharacter,
                        '_');
            }

            return safeValue.Replace(' ', '_');
        }

        private static string NormalizeText(
            string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private sealed class BackupPackage
        {
            public string TempFolder { get; set; } =
                string.Empty;

            public string DatabaseFilePath { get; set; } =
                string.Empty;

            public BackupMetadata Metadata { get; set; } =
                new();

            public string Checksum { get; set; } =
                string.Empty;

            public DatabaseValidation Validation { get; set; } =
                new();
        }

        private sealed class DatabaseValidation
        {
            public string LatestMigrationId { get; set; } =
                string.Empty;
        }
    }
}
