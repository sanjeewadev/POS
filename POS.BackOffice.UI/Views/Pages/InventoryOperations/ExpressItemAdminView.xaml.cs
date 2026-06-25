using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.InventoryOperations
{
    public partial class ExpressItemAdminView : UserControl
    {
        public ExpressItemAdminView()
        {
            InitializeComponent();

            // Wire up the ViewModel via Dependency Injection
            if (App.Services != null)
            {
                this.DataContext = App.Services.GetRequiredService<ExpressItemAdminViewModel>();
            }
        }
    }
}