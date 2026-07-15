namespace POS.Cashier.AuditTests;

internal static class LockRecoverySourcePolicyAuditTests
{
    public static Task ManagerRecoveryAndSafeExitAreControlledAsync()
    {
        string lockView = Read("POS.Cashier.UI", "Dialogs", "LockScreenView.xaml");
        string lockCode = Read("POS.Cashier.UI", "Dialogs", "LockScreenView.xaml.cs");
        string recoveryView = Read("POS.Cashier.UI", "Dialogs", "LockRecoveryActionDialog.xaml");
        string recoveryCode = Read("POS.Cashier.UI", "Dialogs", "LockRecoveryActionDialog.xaml.cs");
        string service = Read("POS.Cashier.UI", "Services", "CashierLockService.cs");
        string auth = Read("POS.Core", "Services", "AuthService.cs");

        AuditAssert.Contains(lockView, "MANAGER RECOVERY", "lock-screen manager recovery button");
        AuditAssert.Contains(lockView, "Terminal / Shift", "lock-screen shift context");
        AuditAssert.Contains(lockCode, "ManagerAuthDialogView", "manager credential authorization");
        AuditAssert.Contains(lockCode, "LockRecoveryActionDialog", "explicit recovery action choice");
        AuditAssert.Contains(lockCode, "LockRecoveryUnlock", "manager unlock audit event");
        AuditAssert.Contains(lockCode, "LockRecoveryExit", "safe-exit audit event");
        AuditAssert.Contains(lockCode, "SafeExitRequested", "safe-exit request flag");
        AuditAssert.Contains(recoveryView, "UNLOCK AND CONTINUE", "manager unlock action");
        AuditAssert.Contains(recoveryView, "CLOSE CASHIER APPLICATION", "safe application exit action");
        AuditAssert.Contains(recoveryCode, "LockRecoveryAction.CloseApplication", "safe-exit action result");
        AuditAssert.Contains(service, "FlushCartBeforeLogoffAsync", "cart save before manager exit");
        AuditAssert.Contains(service, "Application.Current.Shutdown", "approved application shutdown");
        AuditAssert.Contains(service, "CurrentShiftId", "shift context capture");
        AuditAssert.Contains(service, "TerminalNo", "terminal context capture");
        AuditAssert.Contains(auth, "RecordSecurityAuditAsync", "recovery action audit service");
        AuditAssert.False(lockView.Contains("LOG OUT", StringComparison.OrdinalIgnoreCase),
            "The locked screen must not offer an unaudited ordinary logout.");

        return Task.CompletedTask;
    }

    private static string Read(params string[] parts)
    {
        string path = Path.Combine(new[] { AuditPaths.RepositoryRoot }.Concat(parts).ToArray());
        if (!File.Exists(path))
            throw new InvalidOperationException($"Required source file was not found: {path}");
        return File.ReadAllText(path);
    }
}
