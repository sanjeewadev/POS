namespace POS.Core.Utilities;

public static class UomValueResolver
{
    public const string DefaultUom = "PCS";

    public static string Resolve(
        string? preferredUom,
        string? masterUom)
    {
        string preferred = Normalize(preferredUom);
        if (!string.IsNullOrWhiteSpace(preferred))
            return preferred;

        string master = Normalize(masterUom);
        return string.IsNullOrWhiteSpace(master)
            ? DefaultUom
            : master;
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
}
