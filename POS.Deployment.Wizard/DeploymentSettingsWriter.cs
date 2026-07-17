using System.IO;
using System.Text.Json;

namespace POS.Deployment.Wizard;

internal static class DeploymentSettingsWriter
{
    public static async Task WriteAsync(
        string path,
        object settings,
        CancellationToken cancellationToken = default)
    {
        string fullPath = Path.GetFullPath(path);
        string? folder = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);

        await File.WriteAllTextAsync(
            fullPath,
            JsonSerializer.Serialize(
                settings,
                new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }
}
