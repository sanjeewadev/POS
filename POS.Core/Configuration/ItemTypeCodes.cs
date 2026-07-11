using System;

namespace POS.Core.Configuration
{
    public static class ItemTypeCodes
    {
        public const string StockItem = "StockItem";
        public const string Service = "Service";

        public static bool IsValid(string? value)
        {
            return string.Equals(value, StockItem, StringComparison.Ordinal) ||
                   string.Equals(value, Service, StringComparison.Ordinal);
        }
    }
}
