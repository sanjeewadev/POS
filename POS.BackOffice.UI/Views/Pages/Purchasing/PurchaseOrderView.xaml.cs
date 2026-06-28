using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.Purchasing
{
    public partial class PurchaseOrderView : UserControl
    {
        public PurchaseOrderView()
        {
            InitializeComponent();
        }

        private void DgPoLines_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (DataContext is PurchaseOrderViewModel viewModel)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    viewModel.RecalculateTotals();
                }), DispatcherPriority.Background);
            }
        }
    }

    public class MoqColorConverter : IMultiValueConverter
    {
        public object Convert(
            object[] values,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            if (values.Length == 2 &&
                values[0] is decimal qty &&
                values[1] is int moq)
            {
                if (qty > 0 && moq > 0 && qty < moq)
                {
                    return new SolidColorBrush(Color.FromRgb(255, 204, 204));
                }
            }

            return new SolidColorBrush(Color.FromRgb(224, 255, 255));
        }

        public object[] ConvertBack(
            object value,
            Type[] targetTypes,
            object parameter,
            CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}