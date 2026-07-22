using POS.Core.Configuration;

namespace POS.Core.Utilities;

public static class StoreIdentityDisplayFormatter
{
    public const string BackOfficeProductTitle =
        ProductReleaseInfo.ProductName + " BackOffice";

    public static string ResolveOperationalName(
        string? storeName,
        string? legalName,
        string? fallback = "My Store")
    {
        if (!string.IsNullOrWhiteSpace(storeName))
            return storeName.Trim();

        if (!string.IsNullOrWhiteSpace(legalName))
            return legalName.Trim();

        return (fallback ?? string.Empty).Trim();
    }

    public static string BuildBackOfficeTitle(
        string? storeName,
        string? legalName)
    {
        string operationalName =
            ResolveOperationalName(
                storeName,
                legalName,
                string.Empty);

        return string.IsNullOrWhiteSpace(operationalName)
            ? BackOfficeProductTitle
            : $"{BackOfficeProductTitle} — {operationalName}";
    }
}
