namespace POS.Core.Data.Configuration;

public sealed class DatabaseConfigurationException : Exception
{
    public DatabaseConfigurationException(string message)
        : base(message)
    {
    }

    public DatabaseConfigurationException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
