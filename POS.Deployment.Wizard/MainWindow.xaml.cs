using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using POS.Core.Configuration;
using POS.Core.Data.Configuration;

namespace POS.Deployment.Wizard;

public partial class MainWindow : Window
{
    private readonly DeploymentRole _role;
    private readonly string _installRoot;
    private readonly bool _serverCashierSelected;
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
        string installRoot,
        bool serverCashierSelected)
    {
        InitializeComponent();

        _role = role;
        _installRoot = installRoot;
        _serverCashierSelected = serverCashierSelected;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        bool existingProfileLoaded =
            LoadExistingConnectionProfile();

        LoadExistingTerminalIdentity();

        InstallModeComboBox.SelectedIndex = 0;

        if (_role == DeploymentRole.Server)
        {
            ServerHostTextBox.Text = "localhost";
            ServerHostTextBox.IsReadOnly = true;
            ServerHostLabel.Text = "Local server connection";

            string lanAddress =
                NetworkAddressHelper.GetPreferredIpv4Address();

            ServerAddressHintTextBlock.Text =
                $"BackOffice and a Cashier installed on this same computer use " +
                $"localhost, so router or LAN IP changes do not break them. " +
                $"Remote Cashiers should use server computer name " +
                $"'{Environment.MachineName}' where possible. Current LAN IP: " +
                $"{lanAddress}.";

            RoleTextBlock.Text = _serverCashierSelected
                ? "Server, BackOffice, and Cashier production installation"
                : "Server and BackOffice production installation";

            InstallCashierOnServerCheckBox.Visibility =
                _serverCashierSelected
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            InstallCashierOnServerCheckBox.IsChecked = _serverCashierSelected;
            InstallCashierOnServerCheckBox.IsEnabled = false;

            TerminalPanel.Visibility =
                _serverCashierSelected
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            StatusTextBlock.Text = _serverCashierSelected
                ? "Choose the production database mode and confirm the server Cashier identity. The local connection is protected from router IP changes."
                : "This is a Server and BackOffice-only installation. No Cashier terminal will be reserved or assigned. The local connection is protected from router IP changes.";
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
            ServerHostLabel.Text = "POS Server computer name or IP address";
            ServerAddressHintTextBlock.Text =
                "Prefer the Server PC computer name, for example POS-SERVER. " +
                "The name normally remains usable when a router assigns a new " +
                "IP address. Use an IP address only when computer-name " +
                "resolution is unavailable.";
            RunButton.Content = existingProfileLoaded
                ? "Repair Cashier Connection"
                : "Configure Cashier";
            StatusTextBlock.Text = existingProfileLoaded
                ? "The existing encrypted connection values were loaded. Change only the server name or IP when repairing a network change, then verify the terminal assignment."
                : "Enter the server connection and this terminal's unique identity.";
        }
    }

    private bool LoadExistingConnectionProfile()
    {
        if (!File.Exists(ProfilePath))
            return false;

        try
        {
            DatabaseConnectionSettings settings =
                new DatabaseConnectionSettingsStore(ProfilePath)
                    .LoadOrDefault();

            if (!settings.IsCentralSqlServer)
                return false;

            ServerHostTextBox.Text = settings.ServerHost;
            PortTextBox.Text = settings.ServerPort.ToString();
            DatabaseNameTextBox.Text = settings.DatabaseName;
            ApplicationLoginTextBox.Text = settings.UserName;
            PasswordBox.Password = settings.Password;
            ConfirmPasswordBox.Password = settings.Password;
            return true;
        }
        catch (Exception ex)
        {
            AppendOutput(
                "The existing encrypted database profile could not be loaded: " +
                ex.Message);
            return false;
        }
    }

    private void LoadExistingTerminalIdentity()
    {
        string settingsFileName =
            _role == DeploymentRole.Server
                ? "deployment.server.json"
                : "deployment.terminal.json";

        string settingsPath = Path.Combine(
            ProgramDataRoot,
            settingsFileName);

        if (!File.Exists(settingsPath))
            return;

        try
        {
            using JsonDocument document =
                JsonDocument.Parse(
                    File.ReadAllText(settingsPath));

            if (document.RootElement.TryGetProperty(
                    "TerminalNo",
                    out JsonElement terminalNoValue))
            {
                string terminalNo =
                    terminalNoValue.GetString() ??
                    string.Empty;

                if (!string.IsNullOrWhiteSpace(terminalNo))
                    TerminalNoTextBox.Text = terminalNo.Trim();
            }

            if (document.RootElement.TryGetProperty(
                    "TerminalName",
                    out JsonElement terminalNameValue))
            {
                string terminalName =
                    terminalNameValue.GetString() ??
                    string.Empty;

                if (!string.IsNullOrWhiteSpace(terminalName))
                    TerminalNameTextBox.Text = terminalName.Trim();
            }
        }
        catch (Exception ex)
        {
            AppendOutput(
                "The previous terminal deployment record could not be loaded: " +
                ex.Message);
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
            _serverCashierSelected
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
        catch (OperationCanceledException ex)
        {
            AppendOutput(string.Empty);
            AppendOutput("SETUP CANCELLED:");
            AppendOutput(ex.Message);
            StatusTextBlock.Text = "Setup was cancelled. No further setup steps were run.";
        }
        catch (Exception ex)
        {
            string technicalLog = WriteWizardFailureLog(ex);
            string message = BuildFriendlyFailureMessage(ex, technicalLog);

            AppendOutput(string.Empty);
            AppendOutput("SETUP FAILED:");
            AppendOutput(message);

            StatusTextBlock.Text =
                "Setup could not be completed. Correct the reported issue and run configuration again.";

            MessageBox.Show(
                this,
                message,
                "Advanced POS Setup Could Not Complete",
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

        if (_serverCashierSelected)
        {
            string terminalReport = Path.Combine(
                reportsFolder,
                $"Server_Terminal_{timestamp}.json");

            ProcessResult terminalResult = await RunDatabaseSetupAsync(
                BuildTerminalArguments(terminalReport));

            terminalResult.ThrowIfFailed("Server Cashier terminal configuration");
            TerminalSetupReport terminalSetup =
                await ReadTerminalSetupReportAsync(terminalReport);
            machineCode = terminalSetup.MachineCode;
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
                ServerComputerName = Environment.MachineName,
                ServerLanAddress = NetworkAddressHelper.GetPreferredIpv4Address(),
                RecommendedCashierHost = Environment.MachineName,
                Port = ParsePort(),
                DatabaseName = DatabaseNameTextBox.Text.Trim(),
                ApplicationLogin = ApplicationLoginTextBox.Text.Trim(),
                ProfilePath,
                InstallRoot = _installRoot,
                NetworkSettingsBackup = networkBackup,
                PreviousProfileBackup = previousProfileBackup,
                ServerReport = reportPath,
                CashierConfigured = _serverCashierSelected,
                TerminalNo = _serverCashierSelected
                    ? TerminalNoTextBox.Text.Trim()
                    : string.Empty,
                TerminalName = _serverCashierSelected
                    ? TerminalNameTextBox.Text.Trim()
                    : string.Empty,
                MachineCode = machineCode ?? string.Empty
            });

        StatusTextBlock.Text = "Server production setup completed successfully.";
        AppendOutput(string.Empty);
        AppendOutput("SERVER PRODUCTION SETUP PASSED.");
        AppendOutput($"BackOffice profile: {ProfilePath}");
        AppendOutput($"Remote Cashier server name: {Environment.MachineName}");
        AppendOutput($"Current server LAN IP: {NetworkAddressHelper.GetPreferredIpv4Address()}");
        AppendOutput($"Installation report: {reportPath}");

        MessageBox.Show(
            this,
            "The production server and BackOffice database connection were configured successfully.\n\n" +
            $"Remote Cashiers should use server computer name: {Environment.MachineName}\n" +
            $"Current LAN IP: {NetworkAddressHelper.GetPreferredIpv4Address()}\n\n" +
            "Keep the server computer name stable. A router DHCP reservation remains recommended as a backup.",
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

        TerminalSetupReport terminalSetup =
            await ReadTerminalSetupReportAsync(terminalReport);

        string machineCode =
            string.IsNullOrWhiteSpace(terminalSetup.MachineCode)
                ? "Unavailable"
                : terminalSetup.MachineCode;

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

        StatusTextBlock.Text = terminalSetup.WasAlreadyConfigured
            ? "Cashier terminal configuration was already complete and has been verified."
            : "Cashier terminal setup completed successfully.";
        AppendOutput(string.Empty);
        AppendOutput(
            terminalSetup.WasAlreadyConfigured
                ? "CASHIER TERMINAL CONFIGURATION ALREADY COMPLETE."
                : "CASHIER TERMINAL SETUP PASSED.");
        AppendOutput($"Terminal: {TerminalNoTextBox.Text.Trim()}");
        AppendOutput($"Machine code: {machineCode}");
        AppendOutput($"Profile: {ProfilePath}");

        MessageBox.Show(
            this,
            (terminalSetup.WasAlreadyConfigured
                ? "This computer was already configured for the selected terminal. The connection and machine assignment were verified again.\n\n"
                : "Cashier setup completed.\n\n") +
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

    private async Task<TerminalSetupReport> ReadTerminalSetupReportAsync(
        string reportPath)
    {
        if (!File.Exists(reportPath))
            return new TerminalSetupReport(string.Empty, false);

        await using FileStream stream = File.OpenRead(reportPath);
        using JsonDocument document = await JsonDocument.ParseAsync(stream);

        string machineCode = document.RootElement.TryGetProperty(
            "MachineCode",
            out JsonElement machineCodeValue)
            ? machineCodeValue.GetString() ?? string.Empty
            : string.Empty;

        bool wasAlreadyConfigured =
            document.RootElement.TryGetProperty(
                "WasAlreadyConfigured",
                out JsonElement configuredValue) &&
            configuredValue.ValueKind == JsonValueKind.True;

        return new TerminalSetupReport(
            machineCode,
            wasAlreadyConfigured);
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

    private string WriteWizardFailureLog(Exception exception)
    {
        try
        {
            string folder = Path.Combine(
                ProgramDataRoot,
                "Logs",
                "Setup");

            Directory.CreateDirectory(folder);

            string path = Path.Combine(
                folder,
                $"DeploymentWizard_{Environment.MachineName}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log");

            var content = new StringBuilder();
            content.AppendLine("Advanced POS deployment wizard failure");
            content.AppendLine($"Generated: {DateTimeOffset.Now:O}");
            content.AppendLine($"Computer:  {Environment.MachineName}");
            content.AppendLine($"Role:      {_role}");
            content.AppendLine($"Version:   {ProductReleaseInfo.ProductVersion}");
            content.AppendLine();
            content.AppendLine(exception.ToString());

            File.WriteAllText(path, content.ToString(), Encoding.UTF8);
            return path;
        }
        catch
        {
            return "Technical setup log could not be written.";
        }
    }

    private static string BuildFriendlyFailureMessage(
        Exception exception,
        string technicalLog)
    {
        string message = string.IsNullOrWhiteSpace(exception.Message)
            ? "Advanced POS setup could not be completed."
            : exception.Message.Trim();

        if (!message.Contains(
                "Technical log:",
                StringComparison.OrdinalIgnoreCase))
        {
            message += Environment.NewLine + Environment.NewLine +
                       $"Technical log: {technicalLog}";
        }

        return message;
    }

    private void ValidateCommonInputs()
    {
        if (string.IsNullOrWhiteSpace(ServerHostTextBox.Text))
            throw new InvalidOperationException(
                "The POS Server computer name or IP address is required.");

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
            _serverCashierSelected)
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
        InstallCashierOnServerCheckBox.IsEnabled = false;
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

internal sealed record TerminalSetupReport(
    string MachineCode,
    bool WasAlreadyConfigured);
