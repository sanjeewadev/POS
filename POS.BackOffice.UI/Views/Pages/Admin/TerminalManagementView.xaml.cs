using System.ComponentModel;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.Admin
{
    public partial class TerminalManagementView : UserControl
    {
        public TerminalManagementView()
        {
            InitializeComponent();

            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            if (DataContext == null && App.Services != null)
            {
                DataContext = App.Services.GetRequiredService<TerminalManagementViewModel>();
            }
        }
    }
}