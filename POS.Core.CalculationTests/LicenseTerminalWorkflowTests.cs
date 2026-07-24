using POS.Core.Models.Licensing;
using POS.Core.Services.Licensing;

namespace POS.Core.CalculationTests;

internal static class LicenseTerminalWorkflowTests
{
    public static void BackOfficeOnlyUsesStoreStatus()
    {
        LicenseStatus status = LicenseTerminalWorkflowPolicy.CalculateOverallStatus(
            LicenseStatus.Active,
            LicenseStatus.Missing,
            terminalLicenseRequired: false);

        AssertEqual(LicenseStatus.Active, status, "BackOffice-only overall status");
        AssertFalse(
            LicenseTerminalWorkflowPolicy.CanRunCashier(
                LicenseStatus.Active,
                LicenseStatus.Active,
                terminalLicenseRequired: false,
                terminalEnabled: true),
            "BackOffice-only Cashier access");
    }

    public static void CashierRequiresStoreAndTerminalLicences()
    {
        LicenseStatus status = LicenseTerminalWorkflowPolicy.CalculateOverallStatus(
            LicenseStatus.Active,
            LicenseStatus.Missing,
            terminalLicenseRequired: true);

        AssertEqual(LicenseStatus.Missing, status, "Cashier missing terminal licence");
        AssertFalse(
            LicenseTerminalWorkflowPolicy.CanRunCashier(
                LicenseStatus.Active,
                LicenseStatus.Missing,
                terminalLicenseRequired: true,
                terminalEnabled: true),
            "Cashier missing terminal licence access");
    }

    public static void LegacyGraceStatusDoesNotBecomeActive()
    {
        LicenseStatus status = LicenseTerminalWorkflowPolicy.CalculateOverallStatus(
            LicenseStatus.GracePeriod,
            LicenseStatus.Active,
            terminalLicenseRequired: true);

        AssertEqual(
            LicenseStatus.GracePeriod,
            status,
            "Legacy grace overall status");

        AssertFalse(
            LicenseTerminalWorkflowPolicy.CanRunCashier(
                LicenseStatus.GracePeriod,
                LicenseStatus.Active,
                terminalLicenseRequired: true,
                terminalEnabled: true),
            "Legacy grace Cashier access");
    }

    public static void DisabledTerminalCannotRunCashier()
    {
        AssertFalse(
            LicenseTerminalWorkflowPolicy.CanRunCashier(
                LicenseStatus.Active,
                LicenseStatus.Active,
                terminalLicenseRequired: true,
                terminalEnabled: false),
            "Disabled terminal Cashier access");
    }

    public static void CurrentComputerRoleIsExplicit()
    {
        AssertEqual(
            "BackOffice only",
            LicenseTerminalWorkflowPolicy.BuildCurrentComputerRole(false, "", ""),
            "BackOffice role text");

        AssertEqual(
            "Cashier terminal 01 — Front Counter",
            LicenseTerminalWorkflowPolicy.BuildCurrentComputerRole(true, "01", "Front Counter"),
            "Cashier role text");
    }

    public static void FleetSummaryIsCompact()
    {
        AssertEqual(
            "2 ready, 1 require attention, 1 disabled",
            LicenseTerminalWorkflowPolicy.BuildFleetSummary(4, 2, 1, 1),
            "Fleet summary");
    }

    private static void AssertEqual<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{label}: expected {expected}, actual {actual}.");
        }
    }

    private static void AssertFalse(bool value, string label)
    {
        if (value)
            throw new InvalidOperationException($"{label}: expected false.");
    }
}
