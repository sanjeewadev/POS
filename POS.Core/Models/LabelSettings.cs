using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace POS.Core.Models
{
    // Represents the physical dimensions and visual toggles for the sticker hardware
    public partial class LabelSettings : ObservableObject
    {
        [ObservableProperty] private string _printerName = string.Empty;

        // Defaulting to standard 50mm x 25mm thermal retail stickers
        [ObservableProperty] private double _widthMm = 50.0;
        [ObservableProperty] private double _heightMm = 25.0;

        // Visual Toggles
        [ObservableProperty] private bool _printStoreName = true;
        [ObservableProperty] private string _storeName = "MY RETAIL STORE"; // TODO: Load from global app settings later

        [ObservableProperty] private bool _printItemName = true;
        [ObservableProperty] private bool _printPrice = true;
        [ObservableProperty] private bool _printItemCode = true;
    }
}