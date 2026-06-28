using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace POS.Core.Models.DTOs
{
    public partial class BarcodeManagementDto : ObservableObject
    {
        public int VariantId { get; set; }

        public int ItemParentId { get; set; }

        public int CategoryId { get; set; }

        public string ItemCode { get; set; } = string.Empty;

        public string SkuCode { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string VariantDescription { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;

        public bool IsItemDeactivated { get; set; }

        public bool IsVariantDeactivated { get; set; }

        public bool IsActive => !IsItemDeactivated && !IsVariantDeactivated;

        public string StatusText => IsActive ? "Active" : "Inactive";

        private string _barcode = string.Empty;

        public string Barcode
        {
            get => _barcode;
            set
            {
                string normalized = value?.Trim() ?? string.Empty;

                if (_barcode != normalized)
                {
                    _barcode = normalized;
                    IsDirty = true;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasBarcode));
                    OnPropertyChanged(nameof(BarcodeType));
                }
            }
        }

        public string OriginalBarcode { get; private set; } = string.Empty;

        public bool HasBarcode => !string.IsNullOrWhiteSpace(Barcode);

        public string BarcodeType
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Barcode))
                    return "Missing";

                if (Barcode.Length == 13 &&
                    Barcode.StartsWith("29", StringComparison.Ordinal) &&
                    BarcodeIsNumeric(Barcode))
                    return "Internal EAN-13";

                if (Barcode.Length == 13 && BarcodeIsNumeric(Barcode))
                    return "EAN-13";

                if (Barcode.Length == 12 && BarcodeIsNumeric(Barcode))
                    return "UPC-A";

                if (Barcode.Length == 8 && BarcodeIsNumeric(Barcode))
                    return "EAN-8";

                return "Custom";
            }
        }

        [ObservableProperty]
        private bool _isSelected;

        private bool _isDirty;

        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (_isDirty != value)
                {
                    _isDirty = value;
                    OnPropertyChanged();
                }
            }
        }

        public string DisplayName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(VariantDescription) ||
                    VariantDescription.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                {
                    return ItemName;
                }

                return $"{ItemName} - {VariantDescription}";
            }
        }

        public void AcceptChanges()
        {
            OriginalBarcode = Barcode;
            IsDirty = false;
        }

        public void RejectChanges()
        {
            Barcode = OriginalBarcode;
            IsDirty = false;
        }

        private static bool BarcodeIsNumeric(string value)
        {
            foreach (char c in value)
            {
                if (!char.IsDigit(c))
                    return false;
            }

            return true;
        }
    }

    public class BarcodeGenerationResultDto
    {
        public int RequestedCount { get; set; }

        public int GeneratedCount { get; set; }

        public int SkippedAlreadyHadBarcodeCount { get; set; }

        public int SkippedInactiveCount { get; set; }

        public string Message =>
            $"Requested: {RequestedCount}, Generated: {GeneratedCount}, Already Had Barcode: {SkippedAlreadyHadBarcodeCount}, Inactive Skipped: {SkippedInactiveCount}";
    }
}