using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using POS.Core.Models;

namespace POS.BackOffice.UI.Views.Dialogs
{
    public partial class AssignVariantSuppliersDialog : Window
    {
        public Supplier? SelectedSupplier { get; private set; }
        public int MinimumOrderQuantity { get; private set; }

        public AssignVariantSuppliersDialog(
            IReadOnlyList<Supplier> suppliers,
            int defaultMoq,
            int selectedVariantCount)
        {
            InitializeComponent();

            var orderedSuppliers = suppliers
                .Where(s => s != null && !s.IsDeactivated)
                .OrderBy(s => s.SupplierCode)
                .ThenBy(s => s.SupplierName)
                .ToList();

            cmbSupplier.ItemsSource = orderedSuppliers;

            if (orderedSuppliers.Any())
                cmbSupplier.SelectedIndex = 0;

            txtMinimumOrderQty.Text = Math.Max(1, defaultMoq).ToString(CultureInfo.CurrentCulture);
            txtSelectedVariantCount.Text = selectedVariantCount.ToString(CultureInfo.CurrentCulture);

            Loaded += (_, _) =>
            {
                cmbSupplier.Focus();

                if (cmbSupplier.SelectedItem == null && orderedSuppliers.Any())
                    cmbSupplier.SelectedIndex = 0;
            };
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm())
                return;

            SelectedSupplier = cmbSupplier.SelectedItem as Supplier;
            MinimumOrderQuantity = ParseInt(txtMinimumOrderQty.Text);

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool ValidateForm()
        {
            if (cmbSupplier.SelectedItem is not Supplier)
            {
                MessageBox.Show(
                    "Please select a supplier.",
                    "Assign Supplier",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                cmbSupplier.Focus();
                return false;
            }

            if (!TryParseInt(txtMinimumOrderQty.Text, out int moq))
            {
                MessageBox.Show(
                    "MOQ must be a valid whole number.",
                    "Assign Supplier",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                txtMinimumOrderQty.Focus();
                txtMinimumOrderQty.SelectAll();
                return false;
            }

            if (moq <= 0)
            {
                MessageBox.Show(
                    "MOQ must be greater than zero.",
                    "Assign Supplier",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                txtMinimumOrderQty.Focus();
                txtMinimumOrderQty.SelectAll();
                return false;
            }

            return true;
        }

        private static bool TryParseInt(string? value, out int result)
        {
            string text = (value ?? string.Empty).Trim();

            if (int.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.CurrentCulture,
                    out result))
            {
                return true;
            }

            return int.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out result);
        }

        private static int ParseInt(string? value)
        {
            TryParseInt(value, out int result);
            return result;
        }
    }
}