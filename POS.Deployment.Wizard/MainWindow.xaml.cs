using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using POS.Core.Configuration;

namespace POS.Deployment.Wizard;

public partial class MainWindow : Window
{
    private readonly DeploymentRole _role;
    private readonly string _installRoot;
    private readonly SetupProcessRunner _runner = new();
    private bool _running;

    private string DatabaseSetupExecutable =>
        Path.Combine(
            _installRoot,
            "DatabaseSetup",
            "POS.Database.Setup.exe");

    private string ToolsFolder =>
        Path.Combine(_installRoot, "Tools");

    private string ProgramDataRoot =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData),
            "Advanced POS");

    private string ProfilePath =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "POS",
            "database.connection.dat");

    public MainWindow(
        DeploymentRole role,
        string installRoot)
    {
        InitializeComponent();

        _role = role;
        _installRoot = installRoot;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ServerHostTextBox.Text =
            _role == DeploymentRole.Server
                ? NetworkAddressHelper.GetPreferredIpv4Address()
                : string.Empty;

        InstallModeComboBox.SelectedIndex = 0;

        if (_role == DeploymentRole.Server)
        {
            RoleTextBlock.Text =
                "Server and BackOffice production installation";

            bool cashierInstalled = File.Exists(
                Path.Combine(
                    _installRoot,
                    "Cashier",
                    "POS.Cashier.UI.exe"));

            InstallCashierOnServerCheckBox.Visibility =
                cashierInstalled
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            InstallCashierOnServerCheckBox.IsChecked = cashierInstalled;
            TerminalPanel.Visibility =
                cashierInstalled
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            StatusTextBlock.Text = cashierInstalled
                ? "Choose the production database mode and confirm the server Cashier identity."
                : "Choose the production database mode, then start setup.";
        }
        else
        {
            Title = "Advanced POS Cashier Setup";
            RoleTextBlock.Text =
                "Cashier terminal production installation";

            ServerModePanel.Visibility = Visibility.Collapsed;
            SqlInstancePanel.Visibility = Visibility.Collapsed;
            SourceFilePanel.Visibility = Visibility.Collapsed;
            TrustedNetworkCheckBox.Visibility = Visibility.Collapsed;
            InstallCashierOnServerCheckBox.Visibility = Visibility.Collapsed;
            TerminalPanel.Visibility = Visibility.Visible;
            ServerHostLabel.Text = "POS Server IP address";
            RunButton.Content = "Configure Cashier";
            StatusTextBlock.Text =
                "Enter the server connection and this terminal's unique identity.";
        }
    }

    private void InstallModeComboBox_OnSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _role != DeploymentRole.Server)
            return;

        ServerInstallMode mode = GetServerInstallMode();

        switch (mode)
        {
            case ServerInstallMode.NewStore:
                SourceFilePanel.Visibility = Visibility.Collapsed;
                break;

            case ServerInstallMode.MigrateSqlite:
                SourceFilePanel.Visibility = Visibility.Visible;
                SourceFileLabel.Text = "Existing standalone SQLite database";
                break;

            case ServerInstallMode.RestoreBackup:
                SourceFilePanel.Visibility = Visibility.Visible;
                SourceFileLabel.Text = "Existing SQL Server backup (.bak)";
                break;
        }
    }

    private void InstallCashierOnServerCheckBox_OnChanged(
        object sender,
        RoutedEventArgs e)
    {
        TerminalPanel.Visibility =
            InstallCashierOnServerCheckBox.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void BrowseSourceButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        ServerInstallMode mode = GetServerInstallMode();

        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Multiselect = false,
            Filter = mode == ServerInstallMode.MigrateSqlite
                ? "SQLite database (*.db)|*.db|All files (*.*)|*.*"
                : "SQL Server backup (*.bak)|*.bak|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
            SourceFileTextBox.Text = dialog.FileName;
    }

    private void BrowseLicenseButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Multiselect = false,
            Filter = "POS licence (*.poslic)|*.poslic|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
            LicenseFileTextBox.Text = dialog.FileName;
    }

    private async void RunButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_running)
            return;

        try
        {
            _running = true;
            SetControlsEnabled(false);
            OutputTextBox.Clear();
            StatusTextBlock.Text = "Setup is running...";

            ValidateCommonInputs();

            if (_role == DeploymentRole.Server)
                await RunServerSetupAsync();
            else
                await RunCashierSetupAsync();
        }
        catch (Exception ex)
        {
            AppendOutput(string.Empty);
            AppendOutput("SETUP FAILED:");
            AppendOutput(ex.Message);

            StatusTextBlock.Text = "Setup failed. Review the progress details.";

            MessageBox.Show(
                this,
                ex.Message,
                "Advanced POS Setup Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _running = false;
            SetControlsEnabled(true);
        }
    }

    private async Task RunServerSetupAsync()
    {
        if (TrustedNetworkCheckBox.IsChecked != true)
        {
            throw new InvalidOperationException(
                "Confirm that this computer is connected to the trusted private store network.");
        }

        ServerInstallMode mode = GetServerInstallMode();
        string sourceFile = SourceFileTextBox.Text.Trim();

        if (mode != ServerInstallMode.NewStore &&
            !File.Exists(sourceFile))
        {
            throw new FileNotFoundException(
                "The selected source database or backup file was not found.",
                sourceFile);
        }

        if (mode == ServerInstallMode.RestoreBackup)
        {
            MessageBoxResult confirmation = MessageBox.Show(
                this,
                "Restore will disconnect active POS users and replace the selected production database. Continue?",
                "Confirm Production Restore",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
                throw new OperationCanceledException("Production restore was cancelled.");
        }

        Directory.CreateDirectory(ProgramDataRoot);

        string reportsFolder = Path.Combine(ProgramDataRoot, "Reports");
        string recoveryFolder = Path.Combine(ProgramDataRoot, "Recovery");
        Directory.CreateDirectory(reportsFolder);
        Directory.CreateDirectory(recoveryFolder);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string previousProfileBackup = BackupExistingProfile(
            recoveryFolder,
            timestamp);

        string networkBackup = Path.Combine(
            recoveryFolder,
            $"SQLServer_Network_Before_Install_{timestamp}.json");

        string networkScript = Path.Combine(
            ToolsFolder,
            "Configure-POS-SqlServer-Network.ps1");

        if (!File.Exists(networkScript))
            throw new FileNotFoundException("SQL Server network setup script is missing.", networkScript);

        string instance = SqlInstanceTextBox.Text.Trim();
        if (!string.Equals(
                instance,
                @".\SQLEXPRESS",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Version 1.0 production setup supports the local .\\SQLEXPRESS instance only.");
        }

        string instanceName = "SQLEXPRESS";

        AppendOutput("Configuring private SQL Server network access...");

        var networkArguments = new List<string>
        {
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            networkScript,
            "-InstanceName",
            instanceName,
            "-Port",
            ParsePort().ToString(),
            "-BackupPath",
            networkBackup,
            "-SetActiveNetworkPrivate"
        };

        ProcessResult networkResult = await _runner.RunAsync(
            GetWindowsPowerShellPath(),
            networkArguments,
            environment: null,
            AppendOutput);

        networkResult.ThrowIfFailed("SQL Server network configuration");

        string reportPath = Path.Combine(
            reportsFolder,
            $"Server_Install_{timestamp}.json");

        string command = mode switch
        {
            ServerInstallMode.NewStore => "provision-empty",
            ServerInstallMode.MigrateSqlite => "provision",
            ServerInstallMode.RestoreBackup => "provision-restore",
            _ => throw new InvalidOperationException("Unsupported server installation mode.")
        };

        var arguments = new List<string>
        {
            command,
            "--instance",
            instance,
            "--host",
            ServerHostTextBox.Text.Trim(),
            "--port",
            ParsePort().ToString(),
            "--database",
            DatabaseNameTextBox.Text.Trim(),
            "--app-login",
            ApplicationLoginTextBox.Text.Trim(),
            "--profile",
            ProfilePath,
            "--report",
            reportPath
        };

        if (mode == ServerInstallMode.MigrateSqlite)
        {
            arguments.Add("--sqlite");
            arguments.Add(sourceFile);
        }
        else if (mode == ServerInstallMode.RestoreBackup)
        {
            arguments.Add("--file");
            arguments.Add(sourceFile);
            arguments.Add("--confirm-destructive-restore");
        }

        AppendOutput("Provisioning the production database...");

        ProcessResult setupResult = await RunDatabaseSetupAsync(arguments);
        setupResult.ThrowIfFailed("Production database provisioning");

        string? machineCode = null;

        if (InstallCashierOnServerCheckBox.IsChecked == true)
        {
            string terminalReport = Path.Combine(
                reportsFolder,
                $"Server_Terminal_{timestamp}.json");

            ProcessResult terminalResult = await RunDatabaseSetupAsync(
                BuildTerminalArguments(terminalReport));

            terminalResult.ThrowIfFailed("Server Cashier terminal configuration");
            machineCode = await ReadMachineCodeAsync(terminalReport);
        }

        await ImportOptionalLicenseAsync(
            Path.Combine(reportsFolder, $"Server_Licence_{timestamp}.json"));

        string settingsPath = Path.Combine(
            ProgramDataRoot,
            "deployment.server.json");

        await DeploymentSettingsWriter.WriteAsync(
            settingsPath,
            new
            {
                ProductVersion = ProductReleaseInfo.ProductVersion,
                InstalledAt = DateTimeOffset.Now,
                InstallMode = mode.ToString(),
                SqlServerInstance = instance,
                ServerHost = ServerHostTextBox.Text.Trim(),
                Port = ParsePort(),
                DatabaseName = DatabaseNameTextBox.Text.Trim(),
                ApplicationLogin = ApplicationLoginTextBox.Text.Trim(),
                ProfilePath,
                InstallRoot = _installRoot,
                NetworkSettingsBackup = networkBackup,
                PreviousProfileBackup = previousProfileBackup,
                ServerReport = reportPath,
                CashierConfigured = InstallCashierOnServerCheckBox.IsChecked == true,
                TerminalNo = InstallCashierOnServerCheckBox.IsChecked == true
                    ? TerminalNoTextBox.Text.Trim()
                    : string.Empty,
                MachineCode = machineCode ?? string.Empty
            });

        StatusTextBlock.Text = "Server production setup completed successfully.";
        AppendOutput(string.Empty);
        AppendOutput("SERVER PRODUCTION SETUP PASSED.");
        AppendOutput($"BackOffice profile: {ProfilePath}");
        AppendOutput($"Installation report: {reportPath}");

        MessageBox.Show(
            this,
            "The production server and BackOffice database connection were configured successfully.",
            "Advanced POS Server Setup",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async Task RunCashierSetupAsync()
    {
        await VerifyTcpEndpointAsync(
            ServerHostTextBox.Text.Trim(),
            ParsePort());

        string reportsFolder = Path.Combine(ProgramDataRoot, "Reports");
        string recoveryFolder = Path.Combine(ProgramDataRoot, "Recovery");
        Directory.CreateDirectory(reportsFolder);
        Directory.CreateDirectory(recoveryFolder);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string previousProfileBackup = BackupExistingProfile(
            recoveryFolder,
            timestamp);

        string terminalReport = Path.Combine(
            reportsFolder,
            $"Terminal_Install_{timestamp}.json");

        AppendOutput("Binding this computer to the selected terminal...");

        ProcessResult terminalResult = await RunDatabaseSetupAsync(
            BuildTerminalArguments(terminalReport));

        terminalResult.ThrowIfFailed("Cashier terminal configuration");

        string machineCode =
            await ReadMachineCodeAsync(terminalReport)
            ?? "Unavailable";

        await ImportOptionalLicenseAsync(
            Path.Combine(reportsFolder, $"Terminal_Licence_{timestamp}.json"));

        string settingsPath = Path.Combine(
            ProgramDataRoot,
            "deployment.terminal.json");

        await DeploymentSettingsWriter.WriteAsync(
            settingsPath,
            new
            {
                ProductVersion = ProductReleaseInfo.ProductVersion,
                InstalledAt = DateTimeOffset.Now,
                ServerHost = ServerHostTextBox.Text.Trim(),
                Port = ParsePort(),
                DatabaseName = DatabaseNameTextBox.Text.Trim(),
                ApplicationLogin = ApplicationLoginTextBox.Text.Trim(),
                TerminalNo = TerminalNoTextBox.Text.Trim(),
                TerminalName = TerminalNameTextBox.Text.Trim(),
                MachineName = Environment.MachineName,
                MachineCode = machineCode,
                ProfilePath,
                PreviousProfileBackup = previousProfileBackup,
                InstallRoot = _installRoot,
                TerminalReport = terminalReport
            });

        StatusTextBlock.Text = "Cashier terminal setup completed successfully.";
        AppendOutput(string.Empty);
        AppendOutput("CASHIER TERMINAL SETUP PASSED.");
        AppendOutput($"Terminal: {TerminalNoTextBox.Text.Trim()}");
        AppendOutput($"Machine code: {machineCode}");
        AppendOutput($"Profile: {ProfilePath}");

        MessageBox.Show(
            this,
            "Cashier setup completed.\n\n" +
            $"Terminal: {TerminalNoTextBox.Text.Trim()}\n" +
            $"Machine code: {machineCode}\n\n" +
            "When no terminal licence was selected, create one for this exact terminal number and machine code before opening Cashier.",
            "Advanced POS Cashier Setup",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private List<string> BuildTerminalArguments(string reportPath) =>
        new()
        {
            "configure-terminal",
            "--host",
            ServerHostTextBox.Text.Trim(),
            "--port",
            ParsePort().ToString(),
            "--database",
            DatabaseNameTextBox.Text.Trim(),
            "--app-login",
            ApplicationLoginTextBox.Text.Trim(),
            "--terminal-no",
            TerminalNoTextBox.Text.Trim(),
            "--terminal-name",
            TerminalNameTextBox.Text.Trim(),
            "--location",
            LocationTextBox.Text.Trim(),
            "--profile",
            ProfilePath,
            "--report",
            reportPath,
            "--updated-by",
            "Production deployment wizard"
        };

    private async Task<ProcessResult> RunDatabaseSetupAsync(
        IReadOnlyList<string> arguments)
    {
        if (!File.Exists(DatabaseSetupExecutable))
        {
            throw new FileNotFoundException(
                "The production database setup utility is missing.",
                DatabaseSetupExecutable);
        }

        var environment = new Dictionary<string, string>
        {
            ["POS_SETUP_APP_PASSWORD"] = PasswordBox.Password
        };

        return await _runner.RunAsync(
            DatabaseSetupExecutable,
            arguments,
            environment,
            AppendOutput);
    }

    private async Task ImportOptionalLicenseAsync(string reportPath)
    {
        string licenseFile = LicenseFileTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(licenseFile))
            return;

        if (!File.Exists(licenseFile))
        {
            throw new FileNotFoundException(
                "The selected POS licence file was not found.",
                licenseFile);
        }

        AppendOutput("Importing the signed POS licence...");

        ProcessResult result = await RunDatabaseSetupAsync(
            new[]
            {
                "import-license",
                "--profile",
                ProfilePath,
                "--file",
                licenseFile,
                "--report",
                reportPath,
                "--imported-by",
                "Production deployment wizard"
            });

        result.ThrowIfFailed("POS licence import");
    }

    private async Task<string?> ReadMachineCodeAsync(string reportPath)
    {
        if (!File.Exists(reportPath))
            return null;

        await using FileStream stream = File.OpenRead(reportPath);
        using JsonDocument document = await JsonDocument.ParseAsync(stream);

        return document.RootElement.TryGetProperty(
            "MachineCode",
            out JsonElement value)
            ? value.GetString()
            : null;
    }

    private async Task VerifyTcpEndpointAsync(string host, int port)
    {
        AppendOutput($"Checking SQL Server endpoint {host}:{port}...");

        using var client = new TcpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            await client.ConnectAsync(host, port, timeout.Token);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"The POS Server could not be reached at {host}:{port}. " +
                "Check that the server is on, SQL Server is running, and both computers are connected to the store LAN.",
                ex);
        }
    }

    private string BackupExistingProfile(
        string recoveryFolder,
        string timestamp)
    {
        if (!File.Exists(ProfilePath))
            return string.Empty;

        Directory.CreateDirectory(recoveryFolder);

        string backupPath = Path.Combine(
            recoveryFolder,
            $"DatabaseProfile_{Environment.MachineName}_{timestamp}.dat");

        File.Copy(ProfilePath, backupPath, overwrite: false);
        AppendOutput($"Previous encrypted database profile preserved: {backupPath}");
        return backupPath;
    }

    private void ValidateCommonInputs()
    {
        if (string.IsNullOrWhiteSpace(ServerHostTextBox.Text))
            throw new InvalidOperationException("The POS Server IP address is required.");

        _ = ParsePort();

        if (string.IsNullOrWhiteSpace(DatabaseNameTextBox.Text))
            throw new InvalidOperationException("The production database name is required.");

        if (string.IsNullOrWhiteSpace(ApplicationLoginTextBox.Text))
            throw new InvalidOperationException("The restricted application login is required.");

        if (PasswordBox.Password.Length < 16)
        {
            throw new InvalidOperationException(
                "The SQL application password must contain at least 16 characters.");
        }

        if (!PasswordBox.Password.Any(char.IsUpper) ||
            !PasswordBox.Password.Any(char.IsLower) ||
            !PasswordBox.Password.Any(char.IsDigit) ||
            PasswordBox.Password.All(char.IsLetterOrDigit))
        {
            throw new InvalidOperationException(
                "The SQL application password must include uppercase, lowercase, a number, and a symbol.");
        }

        if (!string.Equals(
                PasswordBox.Password,
                ConfirmPasswordBox.Password,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The two SQL passwords do not match.");
        }

        if (_role == DeploymentRole.Cashier ||
            InstallCashierOnServerCheckBox.IsChecked == true)
        {
            if (string.IsNullOrWhiteSpace(TerminalNoTextBox.Text))
                throw new InvalidOperationException("A unique terminal number is required.");

            if (string.IsNullOrWhiteSpace(TerminalNameTextBox.Text))
                throw new InvalidOperationException("A terminal name is required.");
        }
    }

    private int ParsePort()
    {
        if (!int.TryParse(PortTextBox.Text.Trim(), out int port) ||
            port is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                "The SQL TCP port must be between 1 and 65535.");
        }

        return port;
    }

    private ServerInstallMode GetServerInstallMode()
    {
        if (InstallModeComboBox.SelectedItem is not ComboBoxItem item ||
            item.Tag is not string value ||
            !Enum.TryParse(value, out ServerInstallMode result))
        {
            return ServerInstallMode.NewStore;
        }

        return result;
    }

    private static string GetWindowsPowerShellPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");

    private void AppendOutput(string line)
    {
        Dispatcher.Invoke(() =>
        {
            OutputTextBox.AppendText(line + Environment.NewLine);
            OutputTextBox.ScrollToEnd();
        });
    }

    private void SetControlsEnabled(bool enabled)
    {
        RunButton.IsEnabled = enabled;
        InstallModeComboBox.IsEnabled = enabled;
        ServerHostTextBox.IsEnabled = enabled;
        PortTextBox.IsEnabled = enabled;
        SqlInstanceTextBox.IsEnabled = enabled;
        DatabaseNameTextBox.IsEnabled = enabled;
        ApplicationLoginTextBox.IsEnabled = enabled;
        PasswordBox.IsEnabled = enabled;
        ConfirmPasswordBox.IsEnabled = enabled;
        SourceFileTextBox.IsEnabled = enabled;
        TrustedNetworkCheckBox.IsEnabled = enabled;
        InstallCashierOnServerCheckBox.IsEnabled = enabled;
        TerminalNoTextBox.IsEnabled = enabled;
        TerminalNameTextBox.IsEnabled = enabled;
        LocationTextBox.IsEnabled = enabled;
        LicenseFileTextBox.IsEnabled = enabled;
    }

    private void CloseButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_running)
        {
            MessageBox.Show(
                this,
                "Wait for the current setup operation to finish.",
                "Advanced POS Setup",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        Close();
    }
}
