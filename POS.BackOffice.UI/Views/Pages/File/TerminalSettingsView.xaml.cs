using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.File
{
    public partial class TerminalSettingsView :
        UserControl
    {
        private bool _loadedOnce;

        public TerminalSettingsView()
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
                            TerminalSettingsViewModel>();
            }

            Loaded +=
                TerminalSettingsView_Loaded;
        }

        private async void
            TerminalSettingsView_Loaded(
                object sender,
                RoutedEventArgs e)
        {
            if (_loadedOnce)
                return;

            _loadedOnce = true;

            if (DataContext is
                TerminalSettingsViewModel
                    viewModel)
            {
                await viewModel.LoadCommand
                    .ExecuteAsync(null);
            }
        }
    }
}
