using System;
using System.Globalization;
using System.Windows;
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

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is not PurchaseOrderViewModel viewModel)
                return;

            if (!viewModel.InitializeCommand.CanExecute(null))
                return;

            try
            {
                await viewModel.InitializeCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to initialize Purchase Order page:\n\n{ex.Message}",
                    "Purchase Order Page Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void DgPoLines_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (DataContext is not PurchaseOrderViewModel viewModel)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    viewModel.RecalculateTotals();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to recalculate Purchase Order totals:\n\n{ex.Message}",
                        "Purchase Order Calculation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }), DispatcherPriority.Background);
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
                    return new SolidColorBrush(Color.FromRgb(255, 230, 230));
            }

            return new SolidColorBrush(Color.FromRgb(255, 255, 224));
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