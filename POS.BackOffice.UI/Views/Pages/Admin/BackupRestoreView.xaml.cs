using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.Admin
{
    public partial class BackupRestoreView : UserControl
    {
        private bool _loadedOnce;

        public BackupRestoreView()
        {
            InitializeComponent();

            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            if (DataContext == null &&
                App.Services != null)
            {
                DataContext =
                    App.Services.GetRequiredService<
                        BackupRestoreViewModel>();
            }

            Loaded += BackupRestoreView_Loaded;
        }

        private async void BackupRestoreView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            if (_loadedOnce)
                return;

            _loadedOnce = true;

            if (DataContext is
                BackupRestoreViewModel viewModel)
            {
                await viewModel.LoadCommand
                    .ExecuteAsync(null);
            }
        }
    }
}
