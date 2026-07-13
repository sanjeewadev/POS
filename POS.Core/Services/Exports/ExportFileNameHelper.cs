using System;
using System.IO;
using System.Linq;

namespace POS.Core.Services.Exports
{
    public static class ExportFileNameHelper
    {
        public static string Build(string value, string extension)
        {
            string safeValue = SafePart(value, "Export");
            string safeExtension = (extension ?? string.Empty).Trim();
            if (!safeExtension.StartsWith(".", StringComparison.Ordinal))
                safeExtension = "." + safeExtension;
            if (safeExtension == ".")
                safeExtension = ".pdf";
            return safeValue + safeExtension;
        }

        public static string Create(string prefix, string reference, string extension)
        {
            string safePrefix = SafePart(prefix, "Export");
            string safeReference = SafePart(reference, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            string safeExtension = (extension ?? string.Empty).Trim().TrimStart('.');
            if (string.IsNullOrWhiteSpace(safeExtension))
                safeExtension = "pdf";

            return $"{safePrefix}_{safeReference}.{safeExtension}";
        }

        public static string SafePart(string? value, string fallback = "Document")
        {
            string text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            const string windowsReserved = "<>:\"/\\|?*";
            text = new string(text.Select(ch =>
                invalid.Contains(ch) || windowsReserved.Contains(ch) || char.IsControl(ch)
                    ? '_'
                    : ch).ToArray());
            text = string.Join("_", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }
    }
}
