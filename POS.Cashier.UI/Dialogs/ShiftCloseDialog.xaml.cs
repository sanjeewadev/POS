using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace POS.Cashier.UI.Dialogs
{
    public partial class ShiftCloseDialog : Window
    {
        public decimal CountedCash { get; private set; }
        public string VarianceNote => (VarianceNoteBox.Text ?? string.Empty).Trim();

        public ShiftCloseDialog()
        {
            InitializeComponent();
            UpdateTotals();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Qty5000.Focus();
            Qty5000.SelectAll();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            DialogResult = false;
            e.Handled = true;
        }

        private void Count_TextChanged(object sender, TextChangedEventArgs e) => UpdateTotals();

        private void UpdateTotals()
        {
            if (!IsInitialized)
                return;

            decimal t5000 = Quantity(Qty5000) * 5000m;
            decimal t1000 = Quantity(Qty1000) * 1000m;
            decimal t500 = Quantity(Qty500) * 500m;
            decimal t100 = Quantity(Qty100) * 100m;
            decimal t50 = Quantity(Qty50) * 50m;
            decimal t20 = Quantity(Qty20) * 20m;
            decimal t10 = Quantity(Qty10) * 10m;
            decimal t5 = Quantity(Qty5) * 5m;
            decimal other = Money(OtherAmount);

            Total5000.Text = $"Rs. {t5000:N2}";
            Total1000.Text = $"Rs. {t1000:N2}";
            Total500.Text = $"Rs. {t500:N2}";
            Total100.Text = $"Rs. {t100:N2}";
            Total50.Text = $"Rs. {t50:N2}";
            Total20.Text = $"Rs. {t20:N2}";
            Total10.Text = $"Rs. {t10:N2}";
            Total5.Text = $"Rs. {t5:N2}";

            CountedCash = decimal.Round(t5000 + t1000 + t500 + t100 + t50 + t20 + t10 + t5 + other, 2, MidpointRounding.AwayFromZero);
            GrandTotalText.Text = $"Rs. {CountedCash:N2}";
        }

        private static int Quantity(TextBox box)
        {
            string value = (box.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return 0;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int quantity) && quantity >= 0
                ? quantity
                : 0;
        }

        private static decimal Money(TextBox box)
        {
            string value = (box.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return 0m;
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal amount) && amount >= 0m
                ? amount
                : 0m;
        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            var quantities = new[]
            {
                (Box: Qty5000, Label: "Rs. 5,000"),
                (Box: Qty1000, Label: "Rs. 1,000"),
                (Box: Qty500, Label: "Rs. 500"),
                (Box: Qty100, Label: "Rs. 100"),
                (Box: Qty50, Label: "Rs. 50"),
                (Box: Qty20, Label: "Rs. 20"),
                (Box: Qty10, Label: "Rs. 10"),
                (Box: Qty5, Label: "Rs. 5")
            };

            foreach (var entry in quantities)
            {
                string value = (entry.Box.Text ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(value) &&
                    (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int quantity) || quantity < 0))
                {
                    MessageBox.Show(
                        $"Enter a non-negative whole-number quantity for {entry.Label}.",
                        "Invalid Cash Count",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    entry.Box.Focus();
                    entry.Box.SelectAll();
                    return;
                }
            }

            string otherValue = (OtherAmount.Text ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(otherValue) &&
                (!decimal.TryParse(otherValue, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal otherAmount) || otherAmount < 0m))
            {
                MessageBox.Show(
                    "Enter a non-negative amount for Coins / other amount.",
                    "Invalid Cash Count",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                OtherAmount.Focus();
                OtherAmount.SelectAll();
                return;
            }

            UpdateTotals();
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
