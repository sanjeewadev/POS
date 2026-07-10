using System;
using System.IO;
using System.Text;

namespace POS.Core.Services
{
    public static class LocalLogService
    {
        private static readonly object SyncRoot = new();

        public static string LogFolderPath =>
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "POS",
                "Logs");

        public static void WriteInformation(
            string applicationName,
            string context,
            string message)
        {
            WriteEntry(
                applicationName,
                "INFORMATION",
                context,
                message);
        }

        public static void WriteException(
            string applicationName,
            string context,
            Exception exception)
        {
            WriteEntry(
                applicationName,
                "ERROR",
                context,
                exception?.ToString()
                    ?? "No exception information was supplied.");
        }

        private static void WriteEntry(
            string applicationName,
            string level,
            string context,
            string details)
        {
            try
            {
                lock (SyncRoot)
                {
                    Directory.CreateDirectory(
                        LogFolderPath);

                    string safeApplicationName =
                        MakeSafeFileName(
                            applicationName);

                    string logFilePath =
                        Path.Combine(
                            LogFolderPath,
                            $"{safeApplicationName}_" +
                            $"{DateTime.Now:yyyy-MM-dd}.log");

                    var builder =
                        new StringBuilder();

                    builder.AppendLine(
                        new string('=', 78));

                    builder.AppendLine(
                        $"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");

                    builder.AppendLine(
                        $"Application: {Normalize(applicationName)}");

                    builder.AppendLine(
                        $"Level: {Normalize(level)}");

                    builder.AppendLine(
                        $"Context: {Normalize(context)}");

                    builder.AppendLine(
                        $"Machine: {Environment.MachineName}");

                    builder.AppendLine(
                        $"User: {Environment.UserName}");

                    builder.AppendLine("Details:");
                    builder.AppendLine(
                        string.IsNullOrWhiteSpace(details)
                            ? "-"
                            : details.Trim());

                    builder.AppendLine();

                    File.AppendAllText(
                        logFilePath,
                        builder.ToString(),
                        new UTF8Encoding(false));
                }
            }
            catch
            {
                // Logging must never cause a second application failure.
            }
        }

        private static string MakeSafeFileName(
            string? value)
        {
            string safeValue =
                string.IsNullOrWhiteSpace(value)
                    ? "POS"
                    : value.Trim();

            foreach (char invalidCharacter in
                     Path.GetInvalidFileNameChars())
            {
                safeValue =
                    safeValue.Replace(
                        invalidCharacter,
                        '_');
            }

            return safeValue.Replace(' ', '_');
        }

        private static string Normalize(
            string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "-"
                : value.Trim();
        }
    }
}
