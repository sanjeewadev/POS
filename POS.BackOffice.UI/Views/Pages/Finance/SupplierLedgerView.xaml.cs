using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.Finance
{
    public partial class SupplierLedgerView : UserControl
    {
        public SupplierLedgerView()
        {
            InitializeComponent();

            if (App.Services != null)
            {
                this.DataContext = App.Services.GetRequiredService<SupplierLedgerViewModel>();
            }
        }
    }
}