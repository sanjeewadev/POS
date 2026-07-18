using System;
using System.Diagnostics;
using System.IO;
using POS.Core.Services;

namespace POS.Cashier.UI.Services
{
    internal static class CashierConfigurationLauncher
    {
        public static bool TryLaunch(out string message)
        {
            try
            {
                string baseDirectory =
                    Path.GetFullPath(AppContext.BaseDirectory);

                DirectoryInfo? cashierDirectory =
                    Directory.GetParent(baseDirectory.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar));

                string installRoot =
                    cashierDirectory?.FullName ??
                    baseDirectory;

                string[] candidates =
                {
                    Path.Combine(
                        installRoot,
                        "DeploymentWizard",
                        "POS.Deployment.Wizard.exe"),
                    Path.Combine(
                        baseDirectory,
                        "POS.Deployment.Wizard.exe")
                };

                string? wizardPath = null;

                foreach (string candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        wizardPath = candidate;
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(wizardPath))
                {
                    message =
                        "The Cashier configuration tool is not installed. " +
                        "Run the matching Advanced POS Cashier setup and choose " +
                        "the configuration option.";
                    return false;
                }

                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = wizardPath,
                        Arguments =
                            $"--mode cashier --install-root \"{installRoot}\"",
                        WorkingDirectory = installRoot,
                        UseShellExecute = true
                    });

                message =
                    "Cashier configuration was opened. Close this activation " +
                    "window after the connection or terminal assignment is changed.";
                return true;
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "Cashier",
                    "Open Cashier configuration",
                    ex);

                message =
                    "Cashier configuration could not be opened. Technical " +
                    "details were saved in the local POS Logs folder.";
                return false;
            }
        }
    }
}
