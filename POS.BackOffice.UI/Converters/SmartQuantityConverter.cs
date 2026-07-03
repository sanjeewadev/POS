using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace POS.BackOffice.UI.Converters
{
    public class SmartQuantityConverter : IValueConverter
    {
        public int MaxDecimalPlaces { get; set; } = 3;

        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            if (value == null || value == DependencyProperty.UnsetValue)
                return string.Empty;

            if (!TryGetDecimal(value, culture, out decimal quantity))
                return value.ToString() ?? string.Empty;

            int decimalPlaces = ResolveDecimalPlaces(parameter);

            string format = decimalPlaces <= 0
                ? "0"
                : "0." + new string('#', decimalPlaces);

            return quantity.ToString(format, culture);
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            if (value == null)
                return 0m;

            string text = value.ToString()?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(text))
                return 0m;

            if (decimal.TryParse(text, NumberStyles.Number, culture, out decimal result))
                return result;

            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out result))
                return result;

            return 0m;
        }

        private int ResolveDecimalPlaces(object parameter)
        {
            if (parameter == null)
                return MaxDecimalPlaces;

            string text = parameter.ToString()?.Trim() ?? string.Empty;

            if (int.TryParse(text, out int parsed) && parsed >= 0)
                return parsed;

            return MaxDecimalPlaces;
        }

        private static bool TryGetDecimal(
            object value,
            CultureInfo culture,
            out decimal result)
        {
            switch (value)
            {
                case decimal decimalValue:
                    result = decimalValue;
                    return true;

                case int intValue:
                    result = intValue;
                    return true;

                case long longValue:
                    result = longValue;
                    return true;

                case double doubleValue:
                    result = System.Convert.ToDecimal(doubleValue);
                    return true;

                case float floatValue:
                    result = System.Convert.ToDecimal(floatValue);
                    return true;

                case string stringValue:
                    if (decimal.TryParse(stringValue, NumberStyles.Number, culture, out result))
                        return true;

                    if (decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out result))
                        return true;

                    result = 0m;
                    return false;

                default:
                    result = 0m;
                    return false;
            }
        }
    }
}