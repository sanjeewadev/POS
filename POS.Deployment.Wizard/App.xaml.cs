using System;
using System.Windows;

namespace POS.Deployment.Wizard;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DeploymentRole role = DeploymentRole.Cashier; // Default to Cashier if no argument is passed
        bool serverCashierSelected = false;
        string installRoot = AppDomain.CurrentDomain.BaseDirectory;

        for (int i = 0; i < e.Args.Length; i++)
        {
            string arg = e.Args[i].ToLowerInvariant();
            if (arg == "--server")
            {
                role = DeploymentRole.Server;
            }
            else if (arg == "--server-cashier")
            {
                serverCashierSelected = true;
            }
        }

        var mainWindow = new MainWindow(role, installRoot, serverCashierSelected);
        mainWindow.Show();
    }
}