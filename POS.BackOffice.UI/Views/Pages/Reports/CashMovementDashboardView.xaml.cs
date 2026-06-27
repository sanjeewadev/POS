using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.Reports
{
    public partial class CashMovementDashboardView : UserControl
    {
        public CashMovementDashboardView()
        {
            InitializeComponent();

            // Wire up the ViewModel via Dependency Injection
            if (App.Services != null)
            {
                this.DataContext = App.Services.GetRequiredService<CashMovementDashboardViewModel>();
            }
        }
    }
}