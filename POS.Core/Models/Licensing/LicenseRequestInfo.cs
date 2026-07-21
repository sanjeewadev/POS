using System;
using System.IO;
using System.Text;

namespace POS.Core.Models.Licensing
{
    public sealed class LicenseRequestInfo
    {
        public LicenseType RequestedLicenseType { get; init; } =
            LicenseType.TerminalLicense;

        public string StoreId { get; init; } = string.Empty;

        public string StoreName { get; init; } = string.Empty;

        public string LegalName { get; init; } = string.Empty;

        public string TerminalNo { get; init; } = string.Empty;

        public string TerminalName { get; init; } = string.Empty;

        public string MachineName { get; init; } = string.Empty;

        public string MachineCode { get; init; } = string.Empty;

        public string ApplicationVersion { get; init; } = string.Empty;

        public LicenseStatus CurrentStatus { get; init; } = LicenseStatus.Missing;

        public DateTime? CurrentExpiryDate { get; init; }

        public bool IsStoreLicenseRequest =>
            RequestedLicenseType == LicenseType.StoreLicense;

        public bool IsTerminalLicenseRequest =>
            RequestedLicenseType == LicenseType.TerminalLicense;

        public string BuildRequestText()
        {
            var builder = new StringBuilder();

            builder.AppendLine(
                IsStoreLicenseRequest
                    ? "ADVANCED POS STORE LICENCE REQUEST"
                    : "ADVANCED POS TERMINAL LICENCE REQUEST");
            builder.AppendLine();
            builder.AppendLine(
                $"Request Type: {ToLicenseTypeText(RequestedLicenseType)}");
            builder.AppendLine($"Store ID: {Display(StoreId)}");
            builder.AppendLine($"Store Name: {Display(StoreName)}");
            builder.AppendLine($"Legal Name: {Display(LegalName)}");

            if (IsTerminalLicenseRequest)
            {
                builder.AppendLine($"Terminal Number: {Display(TerminalNo)}");
                builder.AppendLine($"Terminal Name: {Display(TerminalName)}");
                builder.AppendLine($"Machine Name: {Display(MachineName)}");
                builder.AppendLine($"Machine Code: {Display(MachineCode)}");
            }

            builder.AppendLine($"Application Version: {Display(ApplicationVersion)}");
            builder.AppendLine($"Current Licence Status: {ToStatusText(CurrentStatus)}");
            builder.AppendLine(
                $"Current Expiry Date: " +
                (CurrentExpiryDate.HasValue
                    ? CurrentExpiryDate.Value.ToString("yyyy-MM-dd")
                    : "-"));

            return builder.ToString().TrimEnd();
        }

        public string BuildSuggestedFileName()
        {
            string store = SafeFilePart(StoreName, "Store");

            if (IsStoreLicenseRequest)
                return $"POS_Store_Licence_Request_{store}.txt";

            string terminal = SafeFilePart(TerminalNo, "Terminal");
            string machine = SafeFilePart(MachineName, "Machine");

            return $"POS_Terminal_{terminal}_Licence_Request_{machine}.txt";
        }

        public static string ToStatusText(LicenseStatus status)
        {
            return status switch
            {
                LicenseStatus.Missing => "Missing",
                LicenseStatus.Active => "Active",
                LicenseStatus.ExpiringSoon => "Expiring Soon",
                LicenseStatus.GracePeriod => "Grace Period",
                LicenseStatus.ExpiredReadOnly => "Expired",
                LicenseStatus.Invalid => "Invalid",
                LicenseStatus.Revoked => "Revoked",
                _ => "Unknown"
            };
        }

        public static string ToLicenseTypeText(LicenseType licenseType)
        {
            return licenseType == LicenseType.StoreLicense
                ? "Store License"
                : "Terminal License";
        }

        private static string Display(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "-"
                : value.Trim();
        }

        private static string SafeFilePart(
            string? value,
            string fallback)
        {
            string safe = string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim();

            foreach (char invalid in Path.GetInvalidFileNameChars())
                safe = safe.Replace(invalid, '_');

            safe = safe
                .Replace(' ', '_')
                .Trim('_', '.');

            return string.IsNullOrWhiteSpace(safe)
                ? fallback
                : safe;
        }
    }
}
