using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace POS.Core.Models.DTOs
{
    public class ExpressItemSearchDto
    {
        public int ItemVariantId { get; set; }

        public int ItemParentId { get; set; }

        public string ItemCode { get; set; } = string.Empty;

        public string SkuCode { get; set; } = string.Empty;

        public string Barcode { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string VariantDescription { get; set; } = string.Empty;

        public decimal RetailPrice { get; set; }

        public decimal StockOnHand { get; set; }

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
    }

    public partial class ExpressItemLayoutDto : ObservableObject
    {
        public int LayoutId { get; set; }

        public int ItemVariantId { get; set; }

        public string ItemCode { get; set; } = string.Empty;

        public string SkuCode { get; set; } = string.Empty;

        public string Barcode { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string VariantDescription { get; set; } = string.Empty;

        public decimal RetailPrice { get; set; }

        [ObservableProperty]
        private string _displayLabel = string.Empty;

        [ObservableProperty]
        private string _buttonColorHex = "#005555";

        [ObservableProperty]
        private string _textColorHex = "#FFFFFF";

        [ObservableProperty]
        private int _gridRow = 1;

        [ObservableProperty]
        private int _gridColumn = 1;

        [ObservableProperty]
        private bool _isActive = true;

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

        public string PositionText => $"[{GridRow}, {GridColumn}]";

        public bool HasItem => ItemVariantId > 0;

        public List<string> ValidateForSave()
        {
            var errors = new List<string>();

            if (ItemVariantId <= 0)
                errors.Add("Please select an item before saving the express button.");

            if (string.IsNullOrWhiteSpace(DisplayLabel))
                errors.Add("Button label is required.");

            if (!string.IsNullOrWhiteSpace(DisplayLabel) && DisplayLabel.Trim().Length > 50)
                errors.Add("Button label cannot be longer than 50 characters.");

            if (GridRow < 1 || GridRow > 20)
                errors.Add("Grid row must be between 1 and 20.");

            if (GridColumn < 1 || GridColumn > 5)
                errors.Add("Grid column must be between 1 and 5.");

            if (!IsValidHexColor(ButtonColorHex))
                errors.Add("Button color must be a valid hex color. Example: #005555.");

            if (!IsValidHexColor(TextColorHex))
                errors.Add("Text color must be a valid hex color. Example: #FFFFFF.");

            return errors;
        }

        public static ExpressItemLayoutDto FromSearchItem(
            ExpressItemSearchDto item,
            int row,
            int column)
        {
            string label = item.DisplayName;

            if (label.Length > 20)
                label = label.Substring(0, 20).Trim();

            return new ExpressItemLayoutDto
            {
                LayoutId = 0,
                ItemVariantId = item.ItemVariantId,
                ItemCode = item.ItemCode,
                SkuCode = item.SkuCode,
                Barcode = item.Barcode,
                ItemName = item.ItemName,
                VariantDescription = item.VariantDescription,
                RetailPrice = item.RetailPrice,
                DisplayLabel = label,
                ButtonColorHex = "#005555",
                TextColorHex = "#FFFFFF",
                GridRow = row,
                GridColumn = column,
                IsActive = true
            };
        }

        public ExpressItemLayoutDto Clone()
        {
            return new ExpressItemLayoutDto
            {
                LayoutId = LayoutId,
                ItemVariantId = ItemVariantId,
                ItemCode = ItemCode,
                SkuCode = SkuCode,
                Barcode = Barcode,
                ItemName = ItemName,
                VariantDescription = VariantDescription,
                RetailPrice = RetailPrice,
                DisplayLabel = DisplayLabel,
                ButtonColorHex = ButtonColorHex,
                TextColorHex = TextColorHex,
                GridRow = GridRow,
                GridColumn = GridColumn,
                IsActive = IsActive
            };
        }

        private static bool IsValidHexColor(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string color = value.Trim();

            if (!(color.Length == 7 || color.Length == 9))
                return false;

            if (!color.StartsWith("#", StringComparison.Ordinal))
                return false;

            for (int i = 1; i < color.Length; i++)
            {
                char c = color[i];

                bool isHex =
                    char.IsDigit(c) ||
                    (c >= 'a' && c <= 'f') ||
                    (c >= 'A' && c <= 'F');

                if (!isHex)
                    return false;
            }

            return true;
        }
    }

    public class ExpressItemButtonDto
    {
        public int LayoutId { get; set; }

        public int ItemVariantId { get; set; }

        public string ItemCode { get; set; } = string.Empty;

        public string SkuCode { get; set; } = string.Empty;

        public string Barcode { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string VariantDescription { get; set; } = string.Empty;

        public string DisplayLabel { get; set; } = string.Empty;

        public string ButtonColorHex { get; set; } = "#005555";

        public string TextColorHex { get; set; } = "#FFFFFF";

        public int GridRow { get; set; }

        public int GridColumn { get; set; }

        public decimal RetailPrice { get; set; }

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
    }
}