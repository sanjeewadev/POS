using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace POS.Core.Services.Licensing
{
    public class MachineFingerprintService
    {
        private const string ProductFolderName = "SVK_POS";
        private const string MachineSeedFileName = "machine.id";

        public string GetMachineCode()
        {
            string machineSeed = GetOrCreateMachineSeed();

            string rawFingerprint = string.Join("|",
                Environment.MachineName.Trim().ToUpperInvariant(),
                Environment.OSVersion.Platform.ToString(),
                Environment.Is64BitOperatingSystem ? "OS64" : "OS32",
                Environment.ProcessorCount.ToString(),
                machineSeed.Trim().ToUpperInvariant());

            string hash = ComputeSha256(rawFingerprint);

            return FormatMachineCode(hash);
        }

        public string GetMachineName()
        {
            return Environment.MachineName.Trim();
        }

        private static string GetOrCreateMachineSeed()
        {
            string folderPath = GetWritableProductFolder();
            string seedFilePath = Path.Combine(folderPath, MachineSeedFileName);

            if (File.Exists(seedFilePath))
            {
                string existingSeed = File.ReadAllText(seedFilePath).Trim();

                if (!string.IsNullOrWhiteSpace(existingSeed))
                    return existingSeed;
            }

            string newSeed = Guid.NewGuid().ToString("N").ToUpperInvariant();

            Directory.CreateDirectory(folderPath);
            File.WriteAllText(seedFilePath, newSeed, Encoding.UTF8);

            return newSeed;
        }

        private static string GetWritableProductFolder()
        {
            try
            {
                string commonFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

                if (!string.IsNullOrWhiteSpace(commonFolder))
                {
                    string targetFolder = Path.Combine(commonFolder, ProductFolderName);
                    Directory.CreateDirectory(targetFolder);
                    return targetFolder;
                }
            }
            catch
            {
                // Fallback below.
            }

            string localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (string.IsNullOrWhiteSpace(localFolder))
                localFolder = AppContext.BaseDirectory;

            string fallbackFolder = Path.Combine(localFolder, ProductFolderName);
            Directory.CreateDirectory(fallbackFolder);

            return fallbackFolder;
        }

        private static string ComputeSha256(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            byte[] hashBytes = SHA256.HashData(bytes);

            return Convert.ToHexString(hashBytes);
        }

        private static string FormatMachineCode(string hexHash)
        {
            string safeHash = (hexHash ?? string.Empty)
                .Replace("-", string.Empty)
                .Trim()
                .ToUpperInvariant();

            if (safeHash.Length < 16)
                safeHash = safeHash.PadRight(16, '0');

            return $"PC-{safeHash.Substring(0, 4)}-{safeHash.Substring(4, 4)}-{safeHash.Substring(8, 4)}-{safeHash.Substring(12, 4)}";
        }
    }
}