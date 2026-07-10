using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.Admin
{
    public partial class TerminalManagementView :
        UserControl
    {
        private bool _loadedOnce;

        public TerminalManagementView()
        {
            InitializeComponent();

            if (DesignerProperties
                .GetIsInDesignMode(this))
            {
                return;
            }

            if (DataContext == null &&
                App.Services != null)
            {
                DataContext =
                    App.Services
                        .GetRequiredService<
                            TerminalManagementViewModel>();
            }

            Loaded +=
                TerminalManagementView_Loaded;
        }

        private async void
            TerminalManagementView_Loaded(
                object sender,
                RoutedEventArgs e)
        {
            if (_loadedOnce)
                return;

            _loadedOnce = true;

            if (DataContext is
                TerminalManagementViewModel
                    viewModel)
            {
                await viewModel.LoadCommand
                    .ExecuteAsync(null);
            }
        }
    }
}
