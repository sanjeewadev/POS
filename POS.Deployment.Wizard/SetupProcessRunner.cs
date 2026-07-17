using System.Diagnostics;
using System.IO;

namespace POS.Deployment.Wizard;

internal sealed class SetupProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        IReadOnlyDictionary<string, string>? environment,
        Action<string> output,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(executable))
            throw new FileNotFoundException("Setup executable was not found.", executable);

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(executable)
                ?? AppContext.BaseDirectory
        };

        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        if (environment != null)
        {
            foreach ((string key, string value) in environment)
                startInfo.Environment[key] = value;
        }

        using var process = new Process { StartInfo = startInfo };
        var lines = new List<string>();

        void Capture(string line)
        {
            lock (lines)
                lines.Add(line);

            output(line);
        }

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data != null)
                Capture(eventArgs.Data);
        };

        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data != null)
                Capture(eventArgs.Data);
        };

        if (!process.Start())
            throw new InvalidOperationException("The setup process could not be started.");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        string[] snapshot;
        lock (lines)
            snapshot = lines.ToArray();

        return new ProcessResult(process.ExitCode, snapshot);
    }
}

internal sealed record ProcessResult(
    int ExitCode,
    IReadOnlyList<string> OutputLines)
{
    public string? ErrorCode =>
        ReadTaggedValue("ERROR_CODE:");

    public string? UserMessage =>
        ReadTaggedValue("USER_MESSAGE:");

    public string? TechnicalLogPath =>
        ReadTaggedValue("TECHNICAL_LOG:");

    public void ThrowIfFailed(string operation)
    {
        if (ExitCode == 0)
            return;

        string message = string.IsNullOrWhiteSpace(UserMessage)
            ? $"{operation} could not be completed. Review the setup progress and technical log."
            : UserMessage!;

        if (!string.IsNullOrWhiteSpace(TechnicalLogPath))
        {
            message += Environment.NewLine + Environment.NewLine +
                       $"Technical log: {TechnicalLogPath}";
        }

        throw new InvalidOperationException(message);
    }

    private string? ReadTaggedValue(string tag)
    {
        string? line = OutputLines
            .LastOrDefault(
                item => item.StartsWith(
                    tag,
                    StringComparison.OrdinalIgnoreCase));

        if (line == null)
            return null;

        string value = line[tag.Length..].Trim();
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }
}
