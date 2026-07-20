using POS.Core.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace POS.Cashier.UI.Services
{
    internal static class CashierConfigurationLauncher
    {
        public static bool TryLaunch(out string message) =>
            TryLaunch(
                out _,
                out message);

        public static bool TryLaunch(
            out Process? process,
            out string message)
        {
            process = null;

            try
            {
                string baseDirectory =
                    Path.GetFullPath(AppContext.BaseDirectory);

                DirectoryInfo? cashierDirectory =
                    Directory.GetParent(baseDirectory.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar));

                string inferredInstallRoot =
                    cashierDirectory?.FullName ??
                    baseDirectory;

                var candidates =
                    new List<(string WizardPath, string InstallRoot)>
                    {
                        (
                            Path.Combine(
                                inferredInstallRoot,
                                "DeploymentWizard",
                                "POS.Deployment.Wizard.exe"),
                            inferredInstallRoot),
                        (
                            Path.Combine(
                                baseDirectory,
                                "POS.Deployment.Wizard.exe"),
                            inferredInstallRoot)
                    };

                AddStandardInstallationCandidates(
                    candidates);

                (string WizardPath, string InstallRoot)? selected = null;

                foreach (var candidate in candidates)
                {
                    if (File.Exists(candidate.WizardPath))
                    {
                        selected = candidate;
                        break;
                    }
                }

                if (selected == null)
                {
                    message =
                        "The Cashier configuration tool is not installed. " +
                        "Run the matching Advanced POS Cashier setup and choose " +
                        "the repair or configuration option.";
                    return false;
                }

                process = Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = selected.Value.WizardPath,
                        Arguments =
                            $"--mode cashier --install-root " +
                            $"\"{selected.Value.InstallRoot}\"",
                        WorkingDirectory =
                            selected.Value.InstallRoot,
                        UseShellExecute = true
                    });

                if (process == null)
                {
                    message =
                        "Windows did not start the Cashier configuration tool.";
                    return false;
                }

                message =
                    "Cashier configuration was opened. Complete and close the " +
                    "wizard, then choose Restart Cashier so the new encrypted " +
                    "connection profile is loaded.";
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

        private static void AddStandardInstallationCandidates(
            ICollection<(string WizardPath, string InstallRoot)> candidates)
        {
            string programFiles =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles);

            if (string.IsNullOrWhiteSpace(programFiles))
                return;

            foreach (string productFolder in new[]
                     {
                         "Cashier",
                         "Server"
                     })
            {
                string installRoot = Path.Combine(
                    programFiles,
                    "Advanced POS",
                    productFolder);

                candidates.Add((
                    Path.Combine(
                        installRoot,
                        "DeploymentWizard",
                        "POS.Deployment.Wizard.exe"),
                    installRoot));
            }
        }
    }
}
