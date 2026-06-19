using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.Crm
{
    public partial class LoyaltyDiscountAdminView : UserControl
    {
        public LoyaltyDiscountAdminView()
        {
            InitializeComponent();

            // Ask the application to provide the ViewModel and all its database dependencies
            this.DataContext = App.Services!.GetRequiredService<LoyaltyDiscountAdminViewModel>();
        }
    }
}