using System.Reflection;

namespace POS.Core.Configuration;

public static class ProductReleaseInfo
{
    public const string ProductName = "Advanced POS";

    public static string ProductVersion { get; } =
        ResolveProductVersion();

    public const string RequiredSqlServerMigration =
        "20260903063514_ExpandDocumentSequenceLengthProperly";

    private static string ResolveProductVersion()
    {
        string? informationalVersion =
            typeof(ProductReleaseInfo)
                .Assembly
                .GetCustomAttribute<
                    AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            int metadataSeparator =
                informationalVersion.IndexOf('+');

            return metadataSeparator < 0
                ? informationalVersion
                : informationalVersion[..metadataSeparator];
        }

        Version? assemblyVersion =
            typeof(ProductReleaseInfo)
                .Assembly
                .GetName()
                .Version;

        return assemblyVersion == null
            ? "0.0.0"
            : $"{assemblyVersion.Major}." +
              $"{assemblyVersion.Minor}." +
              $"{assemblyVersion.Build}";
    }
}
