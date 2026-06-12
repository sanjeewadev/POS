using System.Windows.Controls;

namespace POS.BackOffice.UI.Views.Pages.InventoryPages
{
    public partial class ItemMasterView : UserControl
    {
        public ItemMasterView()
        {
            InitializeComponent();

            // NO DataContext = new ... here! 
            // App.xaml and the Dependency Injection Factory handle it automatically.
        }
    }
}