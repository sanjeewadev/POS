using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.Reports
{
    public partial class FloatCashLogView : UserControl
    {
        public FloatCashLogView()
        {
            InitializeComponent();

            // Wire up the ViewModel via Dependency Injection
            if (App.Services != null)
            {
                this.DataContext = App.Services.GetRequiredService<FloatCashLogViewModel>();
            }
        }
    }
}