using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace POS.Core.Services.Exports
{
    public sealed class CsvExportService
    {
        public string Format(
            IReadOnlyList<string> headers,
            IEnumerable<IReadOnlyList<string?>> rows)
        {
            if (headers == null || headers.Count == 0)
                throw new ArgumentException("At least one CSV heading is required.", nameof(headers));
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));

            var text = new StringBuilder();
            text.AppendLine(string.Join(",", headers.Select(Escape)));

            foreach (IReadOnlyList<string?> row in rows)
            {
                if (row.Count != headers.Count)
                    throw new InvalidOperationException("CSV row column count does not match the heading count.");

                text.AppendLine(string.Join(",", row.Select(Escape)));
            }

            return text.ToString();
        }

        public async Task WriteUtf8BomAsync(string filePath, string csv)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Export file path is required.", nameof(filePath));
            if (string.IsNullOrWhiteSpace(csv))
                throw new InvalidOperationException("There is no CSV content to export.");

            string? folder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            await File.WriteAllTextAsync(
                filePath,
                csv,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        public static string Invariant(decimal value, string format = "0.00") =>
            value.ToString(format, CultureInfo.InvariantCulture);

        public static string Invariant(DateTime value, string format = "yyyy-MM-dd HH:mm") =>
            value.ToString(format, CultureInfo.InvariantCulture);

        private static string Escape(string? value)
        {
            string normalized = (value ?? string.Empty)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');

            return $"\"{normalized.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }
    }
}
