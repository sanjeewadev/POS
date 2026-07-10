using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.Admin
{
    public partial class StoreSettingsView :
        UserControl
    {
        private bool _loadedOnce;

        public StoreSettingsView()
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
                            StoreSettingsViewModel>();
            }

            Loaded +=
                StoreSettingsView_Loaded;
        }

        private async void
            StoreSettingsView_Loaded(
                object sender,
                RoutedEventArgs e)
        {
            if (_loadedOnce)
                return;

            _loadedOnce = true;

            if (DataContext is
                StoreSettingsViewModel
                    viewModel)
            {
                await viewModel.LoadCommand
                    .ExecuteAsync(null);
            }
        }
    }
}
