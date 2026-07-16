using System.Text.RegularExpressions;

namespace POS.Database.Setup;

internal static partial class SqlName
{
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIdentifierRegex();

    public static string RequireSafeIdentifier(string value, string label)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (!SafeIdentifierRegex().IsMatch(trimmed))
        {
            throw new ArgumentException(
                $"{label} must start with a letter and contain only letters, numbers, and underscores (maximum 128 characters).");
        }

        return trimmed;
    }

    public static string Quote(string identifier) =>
        $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

    public static string EscapeLiteral(string value) =>
        (value ?? string.Empty).Replace("'", "''", StringComparison.Ordinal);
}
