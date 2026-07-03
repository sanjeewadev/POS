using System.ComponentModel;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.File
{
    public partial class TerminalSettingsView : UserControl
    {
        public TerminalSettingsView()
        {
            InitializeComponent();

            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            if (DataContext == null && App.Services != null)
            {
                DataContext = App.Services.GetRequiredService<TerminalSettingsViewModel>();
            }
        }
    }
}