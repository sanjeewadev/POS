using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.Dialogs;
using POS.BackOffice.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace POS.BackOffice.UI.Views.Pages.Finance
{
    public partial class SupplierClaimsView : UserControl
    {
        public SupplierClaimsViewModel? ViewModel { get; private set; }

        public SupplierClaimsView()
        {
            InitializeComponent();
            Loaded += SupplierClaimsView_Loaded;
        }

        private async void SupplierClaimsView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ViewModel = DataContext as SupplierClaimsViewModel;
                if (ViewModel == null && App.Services != null)
                {
                    ViewModel = App.Services.GetRequiredService<SupplierClaimsViewModel>();
                    DataContext = ViewModel;
                }

                if (ViewModel == null)
                    throw new InvalidOperationException("Supplier Claims is not available.");

                await ViewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Supplier Claims", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Settle_Click(object sender, RoutedEventArgs e)
        {
            SupplierClaimsViewModel? viewModel = DataContext as SupplierClaimsViewModel ?? ViewModel;
            if (viewModel?.CanSettle != true)
                return;

            SupplierClaimActionDialog dialog = SupplierClaimActionDialog.ForSettlement();
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
                await viewModel.SettleSelectedAsync(dialog.PrimaryValue, dialog.ReferenceValue, dialog.Remarks);
        }

        private async void Reject_Click(object sender, RoutedEventArgs e)
        {
            SupplierClaimsViewModel? viewModel = DataContext as SupplierClaimsViewModel ?? ViewModel;
            if (viewModel?.CanReject != true)
                return;

            SupplierClaimActionDialog dialog = SupplierClaimActionDialog.ForRejection();
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
                await viewModel.RejectSelectedAsync(dialog.PrimaryValue, dialog.Remarks);
        }
    }
}
