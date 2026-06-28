using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace POS.Core.Models.DTOs
{
    public class BarcodeRecentGrnDto
    {
        public int GrnHeaderId { get; set; }

        public string GrnNumber { get; set; } = string.Empty;

        public string SupplierInvoiceNo { get; set; } = string.Empty;

        public DateTime ReceivedDate { get; set; }

        public string DisplayText => $"{GrnNumber} | {SupplierInvoiceNo} | {ReceivedDate:d}";
    }

    public partial class BarcodePrintQueueItemDto : ObservableObject
    {
        public int ItemVariantId { get; set; }

        public int? GrnHeaderId { get; set; }

        public int? GrnLineId { get; set; }

        public string SourceDocument { get; set; } = string.Empty;

        public string ItemCode { get; set; } = string.Empty;

        public string SkuCode { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string VariantDescription { get; set; } = string.Empty;

        public string Barcode { get; set; } = string.Empty;

        public decimal Price { get; set; }

        [ObservableProperty]
        private int _printQuantity = 1;

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

        public bool HasValidBarcode => !string.IsNullOrWhiteSpace(Barcode);

        partial void OnPrintQuantityChanged(int value)
        {
            if (value < 0)
                PrintQuantity = 0;
        }
    }

    public partial class BarcodeLabelSettingsDto : ObservableObject
    {
        [ObservableProperty]
        private string _printerName = string.Empty;

        [ObservableProperty]
        private decimal _widthMm = 38m;

        [ObservableProperty]
        private decimal _heightMm = 25m;

        [ObservableProperty]
        private bool _printStoreName = true;

        [ObservableProperty]
        private string _storeName = "MY STORE";

        [ObservableProperty]
        private bool _printItemName = true;

        [ObservableProperty]
        private bool _printPrice = true;

        [ObservableProperty]
        private bool _printItemCode = true;

        [ObservableProperty]
        private bool _printBarcodeText = true;

        public List<string> ValidateForPrint()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(PrinterName))
                errors.Add("Printer is required.");

            if (WidthMm < 20 || WidthMm > 100)
                errors.Add("Label width must be between 20mm and 100mm.");

            if (HeightMm < 10 || HeightMm > 80)
                errors.Add("Label height must be between 10mm and 80mm.");

            if (PrintStoreName && string.IsNullOrWhiteSpace(StoreName))
                errors.Add("Store name is required when store name printing is enabled.");

            return errors;
        }
    }

    public class BarcodePrintRequestDto
    {
        public List<BarcodePrintQueueItemDto> Items { get; set; } = new();

        public BarcodeLabelSettingsDto Settings { get; set; } = new();
    }
}