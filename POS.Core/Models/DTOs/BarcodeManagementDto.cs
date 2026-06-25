using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace POS.Core.Models.DTOs
{
    // Inherits ObservableObject so the DataGrid CheckBoxes and text inputs update the UI instantly
    public partial class BarcodeManagementDto : ObservableObject
    {
        public int VariantId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;

        [ObservableProperty] private string _barcode = string.Empty;
        [ObservableProperty] private bool _isSelected;

        partial void OnIsSelectedChanged(bool value)
        {
            // We handle the count updates up in the ViewModel
        }
    }
}