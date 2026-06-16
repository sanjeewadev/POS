using System;

namespace POS.Core.Models
{
    // Configuration for the physical label and design
    public class LabelSettings
    {
        public string PrinterName { get; set; } = string.Empty;

        // Standard Thermal Label sizes are often 50x25mm, 30x20mm, or 40x30mm
        public double WidthMm { get; set; } = 50.0;
        public double HeightMm { get; set; } = 25.0;

        // Display Toggles
        public bool PrintStoreName { get; set; } = true;
        public string StoreName { get; set; } = "MY STORE";

        public bool PrintItemName { get; set; } = true;
        public bool PrintPrice { get; set; } = true;
        public bool PrintItemCode { get; set; } = false;
    }

    // DTO representing exactly what needs to be printed and how many times
    public class BarcodePrintJobItem
    {
        public string Barcode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int PrintQuantity { get; set; } = 1;
    }
}