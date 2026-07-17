using System.IO;
using System.Diagnostics;

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

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data == null)
                return;

            lock (lines)
                lines.Add(eventArgs.Data);

            output(eventArgs.Data);
        };

        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data == null)
                return;

            lock (lines)
                lines.Add(eventArgs.Data);

            output(eventArgs.Data);
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
    public void ThrowIfFailed(string operation)
    {
        if (ExitCode == 0)
            return;

        string details = string.Join(
            Environment.NewLine,
            OutputLines.TakeLast(15));

        throw new InvalidOperationException(
            $"{operation} failed with exit code {ExitCode}." +
            (string.IsNullOrWhiteSpace(details)
                ? string.Empty
                : Environment.NewLine + details));
    }
}
