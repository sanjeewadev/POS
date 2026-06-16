using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace POS.BackOffice.UI.Views.Pages.InventoryOperations
{
    /// <summary>
    /// Interaction logic for StockBalanceView.xaml
    /// </summary>
    public partial class StockBalanceView : UserControl
    {
        public StockBalanceView()
        {
            InitializeComponent();
            if (App.Services != null)
            {
                this.DataContext = App.Services.GetRequiredService<StockBalanceViewModel>();
            }
        }
    }
}
