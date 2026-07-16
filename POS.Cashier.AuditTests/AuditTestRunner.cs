using Microsoft.Data.SqlClient;
using System.Diagnostics;

namespace POS.Cashier.AuditTests;

internal static class AuditGroups
{
    public const string Migration = "Migration";
    public const string Concurrency = "Concurrency";
    public const string Transaction = "Transaction";
    public const string Control = "Control";
    public const string SourcePolicy = "SourcePolicy";
}

internal sealed record AuditTestCase(
    string Name,
    string Group,
    Func<Task> RunAsync);

internal sealed record AuditRunSummary(
    int Total,
    int Passed,
    int Failed,
    int Skipped,
    TimeSpan Duration);

internal sealed class AuditSkippedException : Exception
{
    public AuditSkippedException(string message) : base(message)
    {
    }
}

internal sealed class AuditTestRunner
{
    private readonly TextWriter _writer;

    public AuditTestRunner(TextWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public async Task<AuditRunSummary> RunAsync(IReadOnlyList<AuditTestCase> tests)
    {
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        Stopwatch totalTimer = Stopwatch.StartNew();

        foreach (AuditTestCase test in tests)
        {
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                await test.RunAsync();
                timer.Stop();
                passed++;
                await _writer.WriteLineAsync($"PASS: [{test.Group}] {test.Name} ({timer.ElapsedMilliseconds} ms)");
            }
            catch (AuditSkippedException ex)
            {
                timer.Stop();
                skipped++;
                await _writer.WriteLineAsync($"SKIP: [{test.Group}] {test.Name} - {ex.Message}");
            }
            catch (Exception ex)
            {
                timer.Stop();
                failed++;
                Exception actual = Unwrap(ex);
                await _writer.WriteLineAsync($"FAIL: [{test.Group}] {test.Name} ({timer.ElapsedMilliseconds} ms)");
                await WriteExceptionChainAsync(actual);
                if (!string.IsNullOrWhiteSpace(actual.StackTrace))
                    await _writer.WriteLineAsync(actual.StackTrace);
            }
        }

        totalTimer.Stop();
        await _writer.WriteLineAsync(
            $"AUDIT SUMMARY: Total={tests.Count}; Passed={passed}; Failed={failed}; Skipped={skipped}; DurationMs={totalTimer.ElapsedMilliseconds}");

        return new AuditRunSummary(tests.Count, passed, failed, skipped, totalTimer.Elapsed);
    }

    private async Task WriteExceptionChainAsync(Exception exception)
    {
        int depth = 0;
        Exception? current = exception;

        while (current != null)
        {
            string prefix = depth == 0
                ? "  "
                : $"  Inner[{depth}]: ";

            if (current is SqlException sqlException)
            {
                await _writer.WriteLineAsync(
                    $"{prefix}{current.GetType().Name}: {current.Message} " +
                    $"(Number={sqlException.Number}, Class={sqlException.Class}, State={sqlException.State}, " +
                    $"Procedure={sqlException.Procedure ?? "<none>"}, Line={sqlException.LineNumber})");
            }
            else
            {
                await _writer.WriteLineAsync(
                    $"{prefix}{current.GetType().Name}: {current.Message}");
            }

            current = current.InnerException;
            depth++;
        }
    }

    private static Exception Unwrap(Exception exception)
    {
        if (exception is AggregateException aggregate && aggregate.InnerExceptions.Count == 1)
            return Unwrap(aggregate.InnerExceptions[0]);
        return exception.InnerException is not null && exception is System.Reflection.TargetInvocationException
            ? Unwrap(exception.InnerException)
            : exception;
    }
}

internal static class AuditAssert
{
    public static void True(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    public static void False(bool condition, string message) => True(!condition, message);

    public static void Equal<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
    }

    public static void Money(decimal expected, decimal actual, string label)
    {
        if (decimal.Round(expected, 2) != decimal.Round(actual, 2))
            throw new InvalidOperationException($"{label}: expected {expected:N2}, actual {actual:N2}.");
    }

    public static void Contains(string text, string expected, string label)
    {
        if (!text.Contains(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{label}: expected text '{expected}' was not found.");
    }

    public static async Task<Exception> ThrowsAsync(Func<Task> action, string? expectedMessage = null)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Exception actual = ex is AggregateException aggregate && aggregate.InnerException is not null
                ? aggregate.InnerException
                : ex;
            if (!string.IsNullOrWhiteSpace(expectedMessage) &&
                !actual.Message.Contains(expectedMessage, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Expected error containing '{expectedMessage}', actual: {actual.Message}", actual);
            }

            return actual;
        }

        throw new InvalidOperationException("Expected the operation to fail, but it succeeded.");
    }
}
