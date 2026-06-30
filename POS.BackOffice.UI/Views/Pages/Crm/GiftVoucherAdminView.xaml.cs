using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace POS.BackOffice.UI.Views.Pages.Crm
{
    public partial class GiftVoucherAdminView : UserControl
    {
        public GiftVoucherAdminViewModel? ViewModel { get; private set; }

        public GiftVoucherAdminView()
        {
            InitializeComponent();

            Loaded += GiftVoucherAdminView_Loaded;

            if (App.Services != null)
            {
                ViewModel = App.Services.GetRequiredService<GiftVoucherAdminViewModel>();
                DataContext = ViewModel;
            }
            else
            {
                MessageBox.Show(
                    "Application services are not available.",
                    "Gift Voucher Management",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void GiftVoucherAdminView_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            try
            {
                await ViewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to initialize Gift Voucher Management page.\n\n{ex.Message}",
                    "Gift Voucher Management",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}