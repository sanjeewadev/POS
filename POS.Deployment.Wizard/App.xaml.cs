using System.IO;
using System.Windows;

namespace POS.Deployment.Wizard;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DeploymentRole role = DeploymentRole.Server;
        string installRoot = string.Empty;
        bool serverCashierSelected = false;

        for (int index = 0; index < e.Args.Length; index++)
        {
            string token = e.Args[index];

            if (string.Equals(token, "--mode", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < e.Args.Length)
            {
                string value = e.Args[++index];
                role = string.Equals(value, "cashier", StringComparison.OrdinalIgnoreCase)
                    ? DeploymentRole.Cashier
                    : DeploymentRole.Server;
            }
            else if (string.Equals(token, "--install-root", StringComparison.OrdinalIgnoreCase) &&
                     index + 1 < e.Args.Length)
            {
                installRoot = e.Args[++index];
            }
            else if (string.Equals(token, "--server-cashier", StringComparison.OrdinalIgnoreCase) &&
                     index + 1 < e.Args.Length)
            {
                string value = e.Args[++index];
                serverCashierSelected =
                    string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
            }
        }

        if (string.IsNullOrWhiteSpace(installRoot))
        {
            installRoot = Directory.GetParent(AppContext.BaseDirectory)?.FullName
                ?? AppContext.BaseDirectory;
        }

        var window = new MainWindow(
            role,
            Path.GetFullPath(installRoot),
            serverCashierSelected);
        MainWindow = window;
        window.Show();
    }
}
