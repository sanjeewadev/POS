using System;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.InventoryOperations
{
    public partial class GrnView : UserControl
    {
        public GrnView()
        {
            InitializeComponent();

            if (App.Services != null)
            {
                this.DataContext = App.Services.GetRequiredService<GrnViewModel>();
            }
        }

        // Triggered the moment a user finishes editing an Excel-style cell in the main DataGrid
        private void DgGrnLines_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (this.DataContext is GrnViewModel viewModel)
            {
                // Delay the recalculation by a fraction of a millisecond so the new value has time to bind to the GrnLine model
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    viewModel.RecalculateTotals();
                }), DispatcherPriority.Background);
            }
        }
    }
}