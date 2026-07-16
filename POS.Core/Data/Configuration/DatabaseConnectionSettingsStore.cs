using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using POS.Core.Data;

namespace POS.Core.Data.Configuration;

public sealed class DatabaseConnectionSettingsStore
{
    private const string SettingsFileName = "database.connection.dat";
    private static readonly byte[] FileHeader =
        Encoding.ASCII.GetBytes("ERPOSDB1");
    private static readonly byte[] OptionalEntropy =
        Encoding.UTF8.GetBytes("EasyRobin.POS.DatabaseConnection.v1");

    private readonly string _settingsPath;

    public DatabaseConnectionSettingsStore()
        : this(Path.Combine(
            DatabasePathProvider.DatabaseFolderPath,
            SettingsFileName))
    {
    }

    public DatabaseConnectionSettingsStore(string settingsPath)
    {
        if (string.IsNullOrWhiteSpace(settingsPath))
            throw new ArgumentException(
                "A database settings path is required.",
                nameof(settingsPath));

        _settingsPath = Path.GetFullPath(settingsPath);
    }

    public string SettingsPath => _settingsPath;

    public DatabaseConnectionSettings LoadOrDefault()
    {
        if (!File.Exists(_settingsPath))
            return DatabaseConnectionSettings.CreateStandaloneSqlite();

        try
        {
            byte[] fileBytes = File.ReadAllBytes(_settingsPath);
            if (fileBytes.Length <= FileHeader.Length ||
                !fileBytes.AsSpan(0, FileHeader.Length)
                    .SequenceEqual(FileHeader))
            {
                throw new DatabaseConfigurationException(
                    "The local database connection settings file is not valid.");
            }

            byte[] encryptedPayload =
                fileBytes.AsSpan(FileHeader.Length).ToArray();

            byte[] clearPayload = ProtectedData.Unprotect(
                encryptedPayload,
                OptionalEntropy,
                DataProtectionScope.CurrentUser);

            DatabaseConnectionSettings? settings =
                JsonSerializer.Deserialize<DatabaseConnectionSettings>(
                    clearPayload);

            if (settings == null)
            {
                throw new DatabaseConfigurationException(
                    "The local database connection settings are empty.");
            }

            settings.Validate();
            return settings;
        }
        catch (DatabaseConfigurationException)
        {
            throw;
        }
        catch (CryptographicException ex)
        {
            throw new DatabaseConfigurationException(
                "The database connection settings could not be decrypted for the current Windows user.",
                ex);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new DatabaseConfigurationException(
                "The database connection settings could not be read.",
                ex);
        }
    }

    public void Save(DatabaseConnectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        string? folder = Path.GetDirectoryName(_settingsPath);
        if (string.IsNullOrWhiteSpace(folder))
        {
            throw new DatabaseConfigurationException(
                "The database connection settings folder is invalid.");
        }

        string temporaryPath =
            _settingsPath + ".tmp-" + Guid.NewGuid().ToString("N");

        try
        {
            Directory.CreateDirectory(folder);

            byte[] clearPayload = JsonSerializer.SerializeToUtf8Bytes(settings);
            byte[] encryptedPayload = ProtectedData.Protect(
                clearPayload,
                OptionalEntropy,
                DataProtectionScope.CurrentUser);

            byte[] fileBytes = new byte[FileHeader.Length + encryptedPayload.Length];
            FileHeader.CopyTo(fileBytes, 0);
            encryptedPayload.CopyTo(fileBytes, FileHeader.Length);

            File.WriteAllBytes(temporaryPath, fileBytes);
            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        catch (CryptographicException ex)
        {
            throw new DatabaseConfigurationException(
                "The database connection settings could not be encrypted for the current Windows user.",
                ex);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            throw new DatabaseConfigurationException(
                "The database connection settings could not be saved.",
                ex);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch
            {
                // A leftover temporary file contains only DPAPI-protected data.
            }
        }
    }

    public void ResetToStandaloneSqlite() =>
        Save(DatabaseConnectionSettings.CreateStandaloneSqlite());
}
