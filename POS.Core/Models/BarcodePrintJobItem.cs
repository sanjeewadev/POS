using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace POS.Core.Models
{
    // Represents a single row in the Print Queue
    public partial class BarcodePrintJobItem : ObservableObject
    {
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public decimal Price { get; set; } = 0m;

        // Observable because the warehouse operator can manually change this number in the DataGrid
        [ObservableProperty]
        private int _printQuantity = 1;
    }
}