using System.Text.Json;

namespace POS.Database.Setup;

internal sealed class SetupReportWriter
{
    public async Task WriteAsync(
        string path,
        object report,
        CancellationToken cancellationToken = default)
    {
        string fullPath = Path.GetFullPath(path);
        string? folder = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        await File.WriteAllTextAsync(
            fullPath,
            JsonSerializer.Serialize(report, options),
            cancellationToken);
    }
}
