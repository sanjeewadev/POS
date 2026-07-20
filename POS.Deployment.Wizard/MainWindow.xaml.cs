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
    private ServerInspectionReport? _latestServerInspection;

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

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        FitWindowToWorkArea();

        bool existingProfileLoaded =
            LoadExistingConnectionProfile();

        LoadExistingTerminalIdentity();

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

            SelectInstallMode(
                existingProfileLoaded
                    ? ServerInstallMode.UpgradeRepair
                    : ServerInstallMode.NewStore);

            StatusTextBlock.Text = existingProfileLoaded
                ? "Existing encrypted Server settings were loaded. Inspecting the SQL installation before selecting Upgrade or Repair."
                : "Inspecting this computer for an existing Advanced POS production database.";

            await DetectExistingServerInstallationAsync();
        }
        else
        {
            InstallModeComboBox.SelectedIndex = 0;
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

    private void FitWindowToWorkArea()
    {
        Rect workArea = SystemParameters.WorkArea;
        const double workAreaMargin = 20;

        double availableWidth = Math.Max(
            640,
            workArea.Width - workAreaMargin);

        double availableHeight = Math.Max(
            520,
            workArea.Height - workAreaMargin);

        MinWidth = Math.Min(MinWidth, availableWidth);
        MinHeight = Math.Min(MinHeight, availableHeight);
        MaxWidth = availableWidth;
        MaxHeight = availableHeight;

        Width = Math.Min(1040, availableWidth);
        Height = Math.Min(800, availableHeight);

        Left = workArea.Left + Math.Max(0, (workArea.Width - Width) / 2);
        Top = workArea.Top + Math.Max(0, (workArea.Height - Height) / 2);
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

    private async Task DetectExistingServerInstallationAsync()
    {
        if (_role != DeploymentRole.Server)
            return;

        RunButton.IsEnabled = false;

        try
        {
            _latestServerInspection =
                await InspectServerInstallationAsync(
                    writeOutput: true);

            ApplyServerInspectionToUi(_latestServerInspection);
        }
        catch (Exception ex)
        {
            AppendOutput(
                "Automatic existing-installation inspection could not be completed: " +
                ex.Message);

            StatusTextBlock.Text =
                "Automatic inspection was unavailable. Confirm the correct installation mode before starting setup.";
        }
        finally
        {
            RunButton.IsEnabled = true;
        }
    }

    private async Task<ServerInspectionReport> InspectServerInstallationAsync(
        bool writeOutput)
    {
        string reportsFolder = Path.Combine(
            ProgramDataRoot,
            "Reports");

        Directory.CreateDirectory(reportsFolder);

        string reportPath = Path.Combine(
            reportsFolder,
            $"Server_Inspection_{DateTime.Now:yyyyMMdd_HHmmss_fff}.json");

        if (writeOutput)
            AppendOutput("Inspecting the existing SQL Server database and application login...");

        ProcessResult result = await RunDatabaseSetupAsync(
            new[]
            {
                "inspect-server",
                "--instance",
                SqlInstanceTextBox.Text.Trim(),
                "--database",
                DatabaseNameTextBox.Text.Trim(),
                "--app-login",
                ApplicationLoginTextBox.Text.Trim(),
                "--report",
                reportPath
            });

        result.ThrowIfFailed("Existing installation inspection");

        ServerInspectionReport inspection =
            await ReadServerInspectionReportAsync(reportPath);

        if (writeOutput)
        {
            AppendOutput(
                $"Inspection result: DatabaseExists={inspection.DatabaseExists}, " +
                $"LoginExists={inspection.LoginExists}, " +
                $"AdvancedPOS={inspection.IsAdvancedPosDatabase}, " +
                $"LatestMigration={inspection.LatestMigration}.");
        }

        return inspection;
    }

    private void ApplyServerInspectionToUi(
        ServerInspectionReport inspection)
    {
        if (inspection.DatabaseExists)
        {
            SelectInstallMode(ServerInstallMode.UpgradeRepair);

            if (inspection.DatabaseIsNewerThanApplication)
            {
                StatusTextBlock.Text =
                    "A newer Advanced POS database was detected. This setup version will refuse to downgrade it.";
            }
            else if (!inspection.IsAdvancedPosDatabase)
            {
                StatusTextBlock.Text =
                    "A database with the selected name exists, but its Advanced POS identity could not be verified. It will not be modified.";
            }
            else if (inspection.LoginExists)
            {
                StatusTextBlock.Text =
                    "Existing Advanced POS store detected. Upgrade / Repair was selected automatically; data and licences will be preserved.";
            }
            else
            {
                StatusTextBlock.Text =
                    "Existing Advanced POS database detected without its restricted login. Upgrade / Repair will preserve data and recreate the login safely.";
            }

            return;
        }

        if (inspection.LoginExists)
        {
            SelectInstallMode(ServerInstallMode.UpgradeRepair);
            StatusTextBlock.Text =
                "A partial setup was detected: the SQL login exists but the production database is missing. Setup will stop safely and require backup recovery or technician review.";
            return;
        }

        SelectInstallMode(ServerInstallMode.NewStore);
        StatusTextBlock.Text =
            "No existing Advanced POS database or login was found. New Store was selected automatically.";
    }

    private static void ValidateServerModeAgainstInspection(
        ServerInstallMode mode,
        ServerInspectionReport inspection)
    {
        if (mode == ServerInstallMode.NewStore &&
            (inspection.DatabaseExists || inspection.LoginExists))
        {
            throw new InvalidOperationException(
                inspection.DatabaseExists
                    ? "An existing database was detected. Choose 'Upgrade or repair an existing Advanced POS store'. The database was not changed."
                    : "The application login already exists but the database is missing. This partial setup requires technician review or backup recovery before New Store can continue.");
        }

        if (mode == ServerInstallMode.UpgradeRepair)
        {
            if (!inspection.DatabaseExists)
            {
                throw new InvalidOperationException(
                    inspection.LoginExists
                        ? "The application login exists, but the production database is missing. Restore a verified backup or contact support; setup will not create an empty replacement."
                        : "No existing Advanced POS store was found. Choose New Store or Restore Backup instead.");
            }

            if (inspection.IsEmptyDatabase)
            {
                throw new InvalidOperationException(
                    "The selected database exists but contains no application tables. It was not modified. Confirm whether it is an incomplete setup before removing it or restore a verified backup.");
            }

            if (!inspection.IsAdvancedPosDatabase)
            {
                throw new InvalidOperationException(
                    "The selected database does not contain the expected Advanced POS identity tables and migration history. Verify the database name or restore the correct backup.");
            }

            if (inspection.DatabaseIsNewerThanApplication)
            {
                throw new InvalidOperationException(
                    "The selected database was created by a newer Advanced POS version. Install the matching or newer Server version; database downgrade is not allowed.");
            }
        }

        if (mode == ServerInstallMode.MigrateSqlite &&
            (inspection.DatabaseExists || inspection.LoginExists))
        {
            throw new InvalidOperationException(
                "SQLite migration requires unused database and login names. Existing SQL resources were detected and were not changed. Choose Upgrade / Repair, Restore Backup, or different safe names.");
        }
    }

    private static async Task<ServerInspectionReport> ReadServerInspectionReportAsync(
        string reportPath)
    {
        if (!File.Exists(reportPath))
        {
            throw new FileNotFoundException(
                "The Server inspection report was not created.",
                reportPath);
        }

        await using FileStream stream = File.OpenRead(reportPath);
        using JsonDocument document = await JsonDocument.ParseAsync(stream);
        JsonElement root = document.RootElement;

        return new ServerInspectionReport(
            ReadBoolean(root, "DatabaseExists"),
            ReadBoolean(root, "LoginExists"),
            ReadBoolean(root, "IsEmptyDatabase"),
            ReadBoolean(root, "IsAdvancedPosDatabase"),
            ReadBoolean(root, "IsRequiredMigrationApplied"),
            ReadBoolean(root, "DatabaseIsNewerThanApplication"),
            root.TryGetProperty("LatestMigration", out JsonElement migration)
                ? migration.GetString() ?? string.Empty
                : string.Empty,
            root.TryGetProperty("UserTableCount", out JsonElement tableCount) &&
            tableCount.TryGetInt64(out long count)
                ? count
                : 0);
    }

    private static bool ReadBoolean(
        JsonElement root,
        string propertyName) =>
        root.TryGetProperty(propertyName, out JsonElement value) &&
        value.ValueKind == JsonValueKind.True;

    private void InstallModeComboBox_OnSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _role != DeploymentRole.Server)
            return;

        UpdateInstallModeUi();
    }

    private void UpdateInstallModeUi()
    {
        ServerInstallMode mode = GetServerInstallMode();

        SourceFilePanel.Visibility = Visibility.Collapsed;

        switch (mode)
        {
            case ServerInstallMode.NewStore:
                InstallModeHintTextBlock.Text =
                    "Use only when no Advanced POS production database exists. " +
                    "Setup will refuse to overwrite an existing database or login.";
                RunButton.Content = "Start New Store Setup";
                break;

            case ServerInstallMode.UpgradeRepair:
                InstallModeHintTextBlock.Text =
                    "Preserves the existing database, users, stock, sales, licences, " +
                    "terminal assignments, and settings. Setup creates a verified backup, " +
                    "applies pending migrations, and repairs the restricted SQL login and local profile.";
                RunButton.Content = "Upgrade / Repair Existing Store";
                break;

            case ServerInstallMode.MigrateSqlite:
                SourceFilePanel.Visibility = Visibility.Visible;
                SourceFileLabel.Text = "Existing standalone SQLite database";
                InstallModeHintTextBlock.Text =
                    "Creates a new SQL Server production database and transfers the selected standalone store. " +
                    "Existing SQL resources with the same names are never overwritten.";
                RunButton.Content = "Migrate Existing Store";
                break;

            case ServerInstallMode.RestoreBackup:
                SourceFilePanel.Visibility = Visibility.Visible;
                SourceFileLabel.Text = "Existing SQL Server backup (.bak)";
                InstallModeHintTextBlock.Text =
                    "Restores a verified SQL Server backup. When a database already exists, " +
                    "setup creates a safety backup before replacing it.";
                RunButton.Content = "Restore Store Backup";
                break;
        }
    }

    private void SelectInstallMode(ServerInstallMode mode)
    {
        foreach (object item in InstallModeComboBox.Items)
        {
            if (item is ComboBoxItem comboItem &&
                comboItem.Tag is string value &&
                Enum.TryParse(value, out ServerInstallMode itemMode) &&
                itemMode == mode)
            {
                InstallModeComboBox.SelectedItem = comboItem;
                UpdateInstallModeUi();
                return;
            }
        }

        InstallModeComboBox.SelectedIndex = 0;
        UpdateInstallModeUi();
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

        if (mode != ServerInstallMode.RestoreBackup)
        {
            _latestServerInspection =
                await InspectServerInstallationAsync(
                    writeOutput: true);

            ValidateServerModeAgainstInspection(
                mode,
                _latestServerInspection);
        }

        if ((mode == ServerInstallMode.MigrateSqlite ||
             mode == ServerInstallMode.RestoreBackup) &&
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
            ServerInstallMode.UpgradeRepair => "upgrade-existing",
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

        AppendOutput(
            mode == ServerInstallMode.UpgradeRepair
                ? "Backing up, upgrading, and repairing the existing production database..."
                : "Provisioning the production database...");

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

        StatusTextBlock.Text = mode == ServerInstallMode.UpgradeRepair
            ? "Existing Advanced POS store upgraded or repaired successfully."
            : "Server production setup completed successfully.";
        AppendOutput(string.Empty);
        AppendOutput(
            mode == ServerInstallMode.UpgradeRepair
                ? "EXISTING STORE UPGRADE OR REPAIR PASSED."
                : "SERVER PRODUCTION SETUP PASSED.");
        AppendOutput($"BackOffice profile: {ProfilePath}");
        AppendOutput($"Remote Cashier server name: {Environment.MachineName}");
        AppendOutput($"Current server LAN IP: {NetworkAddressHelper.GetPreferredIpv4Address()}");
        AppendOutput($"Installation report: {reportPath}");

        MessageBox.Show(
            this,
            (mode == ServerInstallMode.UpgradeRepair
                ? "The existing Advanced POS store was backed up, verified, upgraded, and repaired successfully. Existing business data and licences were preserved.\n\n"
                : "The production server and BackOffice database connection were configured successfully.\n\n") +
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

internal sealed record ServerInspectionReport(
    bool DatabaseExists,
    bool LoginExists,
    bool IsEmptyDatabase,
    bool IsAdvancedPosDatabase,
    bool IsRequiredMigrationApplied,
    bool DatabaseIsNewerThanApplication,
    string LatestMigration,
    long UserTableCount);

internal sealed record TerminalSetupReport(
    string MachineCode,
    bool WasAlreadyConfigured);
