using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace POS.Core.Models.DTOs
{
    public class BarcodeRecentGrnDto
    {
        public int GrnHeaderId { get; set; }

        public string GrnNumber { get; set; } = string.Empty;

        public string SupplierInvoiceNo { get; set; } = string.Empty;

        public string SupplierName { get; set; } = string.Empty;

        public DateTime ReceivedDate { get; set; }

        public int BatchLabelLineCount { get; set; }

        public int TotalLabelsSuggested { get; set; }

        public string DisplayText
        {
            get
            {
                string supplier = string.IsNullOrWhiteSpace(SupplierName)
                    ? "Supplier"
                    : SupplierName.Trim();

                return $"{GrnNumber} | {supplier} | Inv: {SupplierInvoiceNo} | {ReceivedDate:yyyy-MM-dd}";
            }
        }
    }

    public partial class BarcodePrintQueueItemDto : ObservableObject
    {
        // =========================================================
        // SOURCE / IDENTITY
        // =========================================================

        public int ItemVariantId { get; set; }

        public int? ItemBatchId { get; set; }

        public int? GrnHeaderId { get; set; }

        public int? GrnLineId { get; set; }

        public string SourceDocument { get; set; } = string.Empty;

        public string SourceType { get; set; } = "MANUAL";

        public bool IsBatchLabel { get; set; }

        // =========================================================
        // ITEM SNAPSHOT
        // =========================================================

        public string ItemCode { get; set; } = string.Empty;

        public string SkuCode { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string VariantDescription { get; set; } = string.Empty;

        public string Uom { get; set; } = "PCS";

        // =========================================================
        // BATCH SNAPSHOT
        // =========================================================

        public string BatchNo { get; set; } = string.Empty;

        public DateTime? ExpiryDate { get; set; }

        public DateTime? ReceivedDate { get; set; }

        public decimal ReceivedQty { get; set; }

        public decimal AvailableQty { get; set; }

        // =========================================================
        // BARCODE / PRICE SNAPSHOT
        // =========================================================

        public string Barcode { get; set; } = string.Empty;

        public string InternalBatchBarcode { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public decimal CostPrice { get; set; }

        // =========================================================
        // PRINT AUDIT SNAPSHOT
        // =========================================================

        public int BarcodePrintedCount { get; set; }

        public DateTime? LastBarcodePrintedAt { get; set; }

        public string LastBarcodePrintedBy { get; set; } = string.Empty;

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isSelected = true;

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

        public string BatchDisplayText =>
            string.IsNullOrWhiteSpace(BatchNo)
                ? "-"
                : BatchNo.Trim();

        public string ExpiryDisplayText =>
            ExpiryDate.HasValue
                ? ExpiryDate.Value.ToString("yyyy-MM-dd")
                : "-";

        public string ReceivedQtyDisplayText =>
            ReceivedQty.ToString("0.###");

        public string AvailableQtyDisplayText =>
            AvailableQty.ToString("0.###");

        public string PrintedStatusText
        {
            get
            {
                if (BarcodePrintedCount <= 0)
                    return "Not Printed";

                if (LastBarcodePrintedAt.HasValue)
                    return $"Printed {BarcodePrintedCount} / {LastBarcodePrintedAt:yyyy-MM-dd HH:mm}";

                return $"Printed {BarcodePrintedCount}";
            }
        }

        public string LabelTypeText =>
            IsBatchLabel ? "GRN Batch" : "Item";

        public string EffectiveBarcode
        {
            get
            {
                if (IsBatchLabel && !string.IsNullOrWhiteSpace(InternalBatchBarcode))
                    return InternalBatchBarcode.Trim();

                return (Barcode ?? string.Empty).Trim();
            }
        }

        public bool HasValidBarcode =>
            !string.IsNullOrWhiteSpace(EffectiveBarcode);

        partial void OnPrintQuantityChanged(int value)
        {
            if (value < 0)
            {
                PrintQuantity = 0;
                return;
            }

            if (value > 5000)
            {
                PrintQuantity = 5000;
            }
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
        private string _storeName = string.Empty;

        [ObservableProperty]
        private bool _printItemName = true;

        [ObservableProperty]
        private bool _printPrice = true;

        [ObservableProperty]
        private bool _printItemCode = true;

        [ObservableProperty]
        private bool _printBarcodeText = true;

        [ObservableProperty]
        private bool _printBatchNo = true;

        [ObservableProperty]
        private bool _printExpiryDate = true;

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