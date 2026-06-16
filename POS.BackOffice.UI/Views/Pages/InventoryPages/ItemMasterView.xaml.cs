using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.InventoryPages
{
    public partial class ItemMasterView : UserControl
    {
        public ItemMasterView()
        {
            InitializeComponent();

            // INJECT THE BRAIN: Connect the UI to the ViewModel via the DI Container
            if (App.Services != null)
            {
                this.DataContext = App.Services.GetRequiredService<ItemMasterViewModel>();
            }
        }
    }
}