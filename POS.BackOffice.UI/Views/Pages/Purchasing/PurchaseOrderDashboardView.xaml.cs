using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.Purchasing
{
    public partial class PurchaseOrderDashboardView : UserControl
    {
        public PurchaseOrderDashboardView()
        {
            InitializeComponent();

            if (App.Services != null)
            {
                this.DataContext = App.Services.GetRequiredService<PurchaseOrderDashboardViewModel>();
            }
        }
    }
}