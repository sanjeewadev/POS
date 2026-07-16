using Microsoft.EntityFrameworkCore;

namespace POS.Core.Data;

internal static class SqlServerTransactionLock
{
    public static async Task AcquireAsync(
        AppDbContext context,
        string resource,
        int timeoutMilliseconds = 15000)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Database.IsSqlServer())
            return;

        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "A SQL Server transaction lock requires an active database transaction.");
        }

        string normalizedResource = resource?.Trim() ?? string.Empty;
        if (normalizedResource.Length == 0)
            throw new ArgumentException("Transaction-lock resource is required.", nameof(resource));
        if (normalizedResource.Length > 255)
            throw new ArgumentOutOfRangeException(
                nameof(resource),
                "SQL Server application-lock resources cannot exceed 255 characters.");
        if (timeoutMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));

        await context.Database.ExecuteSqlInterpolatedAsync($@"
DECLARE @LockResult int;

EXEC @LockResult = sys.sp_getapplock
    @Resource = {normalizedResource},
    @LockMode = N'Exclusive',
    @LockOwner = N'Transaction',
    @LockTimeout = {timeoutMilliseconds};

IF @LockResult < 0
BEGIN
    THROW 51000, N'Could not acquire the required POS transaction lock.', 1;
END;");
    }
}
