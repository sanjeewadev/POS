namespace POS.Database.Setup;

internal sealed class CommandLineArguments
{
    private readonly Dictionary<string, string> _values;
    private readonly HashSet<string> _flags;

    private CommandLineArguments(
        string command,
        Dictionary<string, string> values,
        HashSet<string> flags)
    {
        Command = command;
        _values = values;
        _flags = flags;
    }

    public string Command { get; }

    public static CommandLineArguments Parse(string[] args)
    {
        if (args.Length == 0)
            return new CommandLineArguments("help", new(), new());

        string command = args[0].Trim().ToLowerInvariant();
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 1; index < args.Length; index++)
        {
            string token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Unexpected argument: {token}");

            string key = token[2..].Trim();
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("An empty option name is not allowed.");

            if (index + 1 < args.Length &&
                !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                values[key] = args[++index];
            }
            else
            {
                flags.Add(key);
            }
        }

        return new CommandLineArguments(command, values, flags);
    }

    public string GetRequired(string name)
    {
        string value = GetOptional(name, string.Empty);
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Required option --{name} was not supplied.");

        return value.Trim();
    }

    public string GetOptional(string name, string defaultValue) =>
        _values.TryGetValue(name, out string? value)
            ? value.Trim()
            : defaultValue;

    public int GetInt(string name, int defaultValue)
    {
        if (!_values.TryGetValue(name, out string? value))
            return defaultValue;

        if (!int.TryParse(value, out int parsed))
            throw new ArgumentException($"Option --{name} must be an integer.");

        return parsed;
    }

    public bool HasFlag(string name) => _flags.Contains(name);

    public string GetRequiredSecret(string name, string environmentVariable)
    {
        if (_values.ContainsKey(name))
        {
            throw new ArgumentException(
                $"Secret option --{name} is not accepted on the command line. " +
                $"Set process environment variable {environmentVariable} instead.");
        }

        string value =
            Environment.GetEnvironmentVariable(environmentVariable)
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"Required secret environment variable {environmentVariable} was not supplied.");
        }

        return value;
    }
}
