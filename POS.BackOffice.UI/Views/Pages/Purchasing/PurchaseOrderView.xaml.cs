using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.Purchasing
{
    public partial class PurchaseOrderView : UserControl
    {
        public PurchaseOrderView()
        {
            InitializeComponent();

            if (App.Services != null)
            {
                this.DataContext = App.Services.GetRequiredService<PurchaseOrderViewModel>();
            }
        }

        // Triggered the moment a user finishes editing a cell in the main DataGrid
        private void DgPoLines_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (this.DataContext is PurchaseOrderViewModel viewModel)
            {
                // Delay by a fraction of a millisecond so the new text has time to bind to the PO Line model
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    viewModel.RecalculateTotals();
                }), DispatcherPriority.Background);
            }
        }
    }

    // ==============================================================================
    // ✅ NEW: UI LOGIC FOR MOQ WARNINGS (Turns cell RED if OrderQty < MOQ)
    // ==============================================================================
    public class MoqColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] is decimal qty && values[1] is int moq)
            {
                // If they ordered something (qty > 0) but it's less than the required minimum
                if (qty > 0 && qty < moq)
                {
                    return new SolidColorBrush(Color.FromRgb(255, 204, 204)); // Warning Red/Pink
                }
            }

            // Standard background color for the entry cell (#E0FFFF)
            return new SolidColorBrush(Color.FromRgb(224, 255, 255));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}