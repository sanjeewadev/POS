namespace POS.Cashier.AuditTests;

internal static class LicenseTerminalWorkflowSourcePolicyAuditTests
{
    public static Task LicenceAndTerminalResponsibilitiesAreSeparatedAsync()
    {
        VerifyRoleAwareLicenceStatus();
        VerifySelectedTerminalLicenceWorkflow();
        VerifyTerminalManagementIsCentralAndSafe();
        VerifyCurrentComputerSetupIsDeliberate();

        return Task.CompletedTask;
    }

    private static void VerifyRoleAwareLicenceStatus()
    {
        string policy = Read(
            "POS.Core",
            "Services",
            "Licensing",
            "LicenseTerminalWorkflowPolicy.cs");
        string manager = Read(
            "POS.Core",
            "Services",
            "Licensing",
            "LicenseManagerService.cs");
        string summary = Read(
            "POS.Core",
            "Models",
            "Licensing",
            "LicenseSummary.cs");

        AuditAssert.Contains(
            policy,
            "terminalLicenseRequired",
            "Role-aware terminal licence policy");
        AuditAssert.Contains(
            policy,
            "BuildCurrentComputerRole",
            "Current-computer role formatter");
        AuditAssert.Contains(
            policy,
            "BuildFleetSummary",
            "Cashier fleet summary formatter");
        AuditAssert.Contains(
            manager,
            "currentTerminalSettings != null",
            "Current computer assignment determines terminal requirement");
        AuditAssert.Contains(
            manager,
            "This computer is BackOffice-only; a terminal licence is not required here.",
            "BackOffice-only licence guidance");
        AuditAssert.Contains(
            summary,
            "public bool TerminalLicenseRequired",
            "Terminal licence requirement in summary");
        AuditAssert.Contains(
            summary,
            "public string CurrentComputerRole",
            "Current computer role in summary");
    }

    private static void VerifySelectedTerminalLicenceWorkflow()
    {
        string xaml = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "Admin",
            "LicenseManagementView.xaml");
        string viewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "LicenseManagementViewModel.cs");
        string manager = Read(
            "POS.Core",
            "Services",
            "Licensing",
            "LicenseManagerService.cs");

        AuditAssert.Contains(
            xaml,
            "SelectedItem=\"{Binding SelectedTerminal, Mode=TwoWay}\"",
            "Selected terminal licence selector");
        AuditAssert.Contains(
            xaml,
            "Content=\"IMPORT STORE LICENCE\"",
            "Separate Store licence import");
        AuditAssert.Contains(
            xaml,
            "Content=\"IMPORT TERMINAL LICENCE\"",
            "Separate terminal licence import");
        AuditAssert.Contains(
            xaml,
            "Command=\"{Binding CopySelectedTerminalLicenseRequestCommand}\"",
            "Selected-terminal licence request action");
        AuditAssert.Contains(
            viewModel,
            "ImportTerminalLicenseForRegisteredTerminalAsync",
            "Selected-terminal import service call");
        AuditAssert.Contains(
            manager,
            "expectedTerminalNo",
            "Terminal number import validation");
        AuditAssert.Contains(
            manager,
            "expectedMachineCode",
            "Machine code import validation");
        AuditAssert.False(
            xaml.Contains(
                "Content=\"IMPORT LICENCE\"",
                StringComparison.Ordinal),
            "Generic licence import action remains on License Management.");
    }

    private static void VerifyTerminalManagementIsCentralAndSafe()
    {
        string xaml = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "Admin",
            "TerminalManagementView.xaml");
        string viewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "TerminalManagementViewModel.cs");

        AuditAssert.Contains(
            xaml,
            "Administrator overview of Cashier terminals registered to this store.",
            "Central Terminal Management guidance");
        AuditAssert.Contains(
            xaml,
            "Content=\"ENABLE\"",
            "Terminal enable action");
        AuditAssert.Contains(
            xaml,
            "Content=\"DISABLE\"",
            "Terminal disable action");
        AuditAssert.Contains(
            xaml,
            "Content=\"RELEASE COMPUTER\"",
            "Terminal machine-release action");
        AuditAssert.Contains(
            xaml,
            "Text=\"{Binding LastSeenText}\"",
            "Terminal last-seen display");
        AuditAssert.Contains(
            viewModel,
            "EnableTerminalAsync",
            "Unambiguous terminal enable command");
        AuditAssert.False(
            xaml.Contains(
                "REGISTER / UPDATE THIS PC",
                StringComparison.OrdinalIgnoreCase),
            "Terminal Management still registers the current computer.");
        AuditAssert.False(
            viewModel.Contains(
                "RegisterCurrentMachineAsync",
                StringComparison.Ordinal),
            "Terminal Management ViewModel still registers the current computer.");
    }

    private static void VerifyCurrentComputerSetupIsDeliberate()
    {
        string xaml = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "File",
            "TerminalSettingsView.xaml");
        string viewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "TerminalSettingsViewModel.cs");
        string repository = Read(
            "POS.Core",
            "Repositories",
            "TerminalManagementRepository.cs");

        AuditAssert.Contains(
            xaml,
            "Text=\"1. CURRENT COMPUTER ROLE\"",
            "Current-computer role section");
        AuditAssert.Contains(
            xaml,
            "Content=\"ENABLE CASHIER ON THIS COMPUTER\"",
            "Deliberate current-computer Cashier action");
        AuditAssert.Contains(
            xaml,
            "IsEnabled=\"{Binding CanEditHardware}\"",
            "Hardware settings assignment gate");
        AuditAssert.Contains(
            viewModel,
            "EnableCashierOnThisComputerAsync",
            "Current-computer assignment command");
        AuditAssert.Contains(
            viewModel,
            "AssignCurrentMachineAsync",
            "Authoritative assignment repository call");
        AuditAssert.Contains(
            viewModel,
            "A terminal licence is not required on this computer.",
            "BackOffice-only current-computer guidance");
        AuditAssert.Contains(
            repository,
            "AssignCurrentMachineAsync",
            "Explicit current-computer assignment repository operation");
        AuditAssert.Contains(
            repository,
            "assignment before selecting another terminal number.",
            "Duplicate current-computer assignment protection");
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(
            Path.Combine(
                new[] { AuditPaths.RepositoryRoot }
                    .Concat(segments)
                    .ToArray()));
}
