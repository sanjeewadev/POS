using System.Text.RegularExpressions;

namespace POS.Cashier.AuditTests;

internal static class ReleaseReadinessSourcePolicyAuditTests
{
    public static Task ReleaseVersionAndBackupModesAreControlledAsync()
    {
        string centralVersionSource = Read(
            "Directory.Build.props");

        Match versionMatch = Regex.Match(
            centralVersionSource,
            @"<Version>(?<version>\d+\.\d+\.\d+)</Version>",
            RegexOptions.CultureInvariant);

        AuditAssert.True(
            versionMatch.Success,
            "Directory.Build.props must define one semantic release version");

        AuditAssert.Equal(
            1,
            Regex.Matches(
                centralVersionSource,
                @"<Version>\d+\.\d+\.\d+</Version>",
                RegexOptions.CultureInvariant).Count,
            "central release-version marker count");

        string releaseInfo = Read(
            "POS.Core",
            "Configuration",
            "ProductReleaseInfo.cs");

        AuditAssert.Contains(
            releaseInfo,
            "AssemblyInformationalVersionAttribute",
            "runtime product version comes from the built assembly");

        AuditAssert.False(
            releaseInfo.Contains(
                "ProductVersion = \"",
                StringComparison.Ordinal),
            "ProductReleaseInfo must not duplicate a hard-coded release version");

        string installerBuilder = Read(
            "tools",
            "deployment",
            "Build-POS-Production-Installers.ps1");

        AuditAssert.Contains(
            installerBuilder,
            "Directory.Build.props",
            "installer builder reads the central release version");
        AuditAssert.Contains(
            installerBuilder,
            "requested installer version does not match the committed source version",
            "installer builder blocks version mismatches");
        AuditAssert.Contains(
            installerBuilder,
            "RequiredSqlServerMigration",
            "release builder source includes migration-aware application binaries");

        string commonReleaseBuilder = Read(
            "tools",
            "deployment",
            "Build-AdvancedPOS-Release.ps1");

        AuditAssert.Contains(
            commonReleaseBuilder,
            "[Parameter(Mandatory = $true)]",
            "common release builder requires one version input");
        AuditAssert.Contains(
            commonReleaseBuilder,
            "Set-CentralVersion",
            "common release builder updates the central version only");
        AuditAssert.Contains(
            commonReleaseBuilder,
            "Run-Cashier-Audit.ps1",
            "common release builder verifies the version change");
        AuditAssert.Contains(
            commonReleaseBuilder,
            "git merge --ff-only",
            "common release builder integrates by fast-forward only");
        AuditAssert.Contains(
            commonReleaseBuilder,
            "Build-POS-Production-Installers.ps1",
            "common release builder invokes the approved installer builder");

        foreach (string installerPath in new[]
        {
            Path.Combine("installers", "AdvancedPOSServer.iss"),
            Path.Combine("installers", "AdvancedPOSCashier.iss")
        })
        {
            string installer = Read(installerPath);

            AuditAssert.Contains(
                installer,
                "#error AppVersion must be supplied",
                $"{installerPath} requires the release version from the builder");

            AuditAssert.False(
                Regex.IsMatch(
                    installer,
                    @"#define\s+AppVersion\s+""\d+\.\d+\.\d+""",
                    RegexOptions.CultureInvariant),
                $"{installerPath} must not contain a stale fallback version");
        }

        string backupViewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "BackupRestoreViewModel.cs");

        AuditAssert.Contains(
            backupViewModel,
            "IsCentralSqlServer",
            "backup page detects central SQL Server mode");
        AuditAssert.Contains(
            backupViewModel,
            "CurrentDatabaseModeText",
            "backup page displays the active database mode");
        AuditAssert.Contains(
            backupViewModel,
            "CurrentDatabaseLocationLabel",
            "backup page uses a provider-aware location label");
        AuditAssert.Contains(
            backupViewModel,
            "CanUseLocalBackup",
            "local SQLite controls are centrally gated");
        AuditAssert.Contains(
            backupViewModel,
            "EnsureLocalBackupAvailable",
            "backup commands reject the wrong provider explicitly");
        AuditAssert.Contains(
            backupViewModel,
            "Backup POS Database",
            "central mode directs the user to the server backup utility");

        string backupView = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "Admin",
            "BackupRestoreView.xaml");

        AuditAssert.Contains(
            backupView,
            "Text=\"{Binding BackupModeDescription}\"",
            "backup page header explains the active provider");
        AuditAssert.Contains(
            backupView,
            "Text=\"{Binding CurrentDatabaseModeText}\"",
            "backup page shows the database mode");
        AuditAssert.Contains(
            backupView,
            "Text=\"{Binding CurrentDatabaseLocationLabel}\"",
            "backup page does not hard-code Database File");
        AuditAssert.Contains(
            backupView,
            "IsEnabled=\"{Binding CanUseLocalBackup}\"",
            "SQLite backup controls are disabled in SQL Server mode");
        AuditAssert.Contains(
            backupView,
            "Central SQL Server is active",
            "backup page shows central backup guidance");

        return Task.CompletedTask;
    }

    private static string Read(params string[] parts)
    {
        string path = Path.Combine(
            new[] { AuditPaths.RepositoryRoot }
                .Concat(parts)
                .ToArray());

        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"Required source file was not found: {path}");
        }

        return File.ReadAllText(path);
    }
}
