using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.InventoryOperations
{
    public partial class PriceManagementView : UserControl
    {
        public PriceManagementView()
        {
            InitializeComponent();

            // Dependency Injection Wiring
            if (App.Services != null)
            {
                this.DataContext = App.Services.GetRequiredService<PriceManagementViewModel>();
            }
        }
    }
}