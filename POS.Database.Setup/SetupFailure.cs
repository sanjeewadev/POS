using System.IO;
using System.Text;
using POS.Core.Configuration;

namespace POS.Database.Setup;

internal sealed class SetupUserException : InvalidOperationException
{
    public SetupUserException(
        string code,
        string message)
        : base(message)
    {
        Code = string.IsNullOrWhiteSpace(code)
            ? "SETUP_ERROR"
            : code.Trim();
    }

    public string Code { get; }
}

internal static class SetupFailureLog
{
    public static string Write(Exception exception)
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                "Advanced POS",
                "Logs",
                "Setup");

            Directory.CreateDirectory(folder);

            string path = Path.Combine(
                folder,
                $"DatabaseSetup_{Environment.MachineName}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log");

            var content = new StringBuilder();
            content.AppendLine("Advanced POS database setup failure");
            content.AppendLine($"Generated: {DateTimeOffset.Now:O}");
            content.AppendLine($"Computer:  {Environment.MachineName}");
            content.AppendLine($"Version:   {ProductReleaseInfo.ProductVersion}");
            content.AppendLine();
            content.AppendLine(exception.ToString());

            File.WriteAllText(path, content.ToString(), Encoding.UTF8);
            return path;
        }
        catch
        {
            return "Technical setup log could not be written.";
        }
    }
}
