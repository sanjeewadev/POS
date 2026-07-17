using POS.BackOffice.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace POS.BackOffice.UI.Views.Pages.Crm
{
    public partial class GiftVoucherAdminView : UserControl
    {
        public GiftVoucherAdminView()
        {
            InitializeComponent();
            Loaded += GiftVoucherAdminView_Loaded;
        }

        private async void GiftVoucherAdminView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is not GiftVoucherAdminViewModel viewModel)
            {
                MessageBox.Show(
                    "Gift Voucher Management is not available.",
                    "Gift Voucher Management",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            try
            {
                await viewModel.InitializeAsync();
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
