using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.ViewModels;
using POS.Core.Models.DTOs;

namespace POS.Cashier.UI.Dialogs
{
    public partial class LoyaltyCustomerDialogView : Window
    {
        public B2BCustomerViewModel? ViewModel { get; private set; }

        public CustomerSearchDto? SelectedCustomer => ViewModel?.SelectedCustomer;

        public LoyaltyCustomerDialogView()
        {
            InitializeComponent();

            if (App.Services != null)
            {
                ViewModel = App.Services.GetRequiredService<B2BCustomerViewModel>();
                ViewModel.ConfigureForRetailLoyaltyCustomers();

                DataContext = ViewModel;
                ViewModel.ActionCompleted += OnActionCompleted;
            }
            else
            {
                MessageBox.Show(
                    "Application services are not available.",
                    "Loyalty Customer Lookup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SearchTxt.Focus();
            SearchTxt.SelectAll();

            if (ViewModel != null)
                await ViewModel.ReloadAsync();
        }

        private void CustomerDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (ViewModel.SelectedCustomer == null)
                return;

            if (!ViewModel.CanAttachToInvoice)
                return;

            ViewModel.AttachCommand.Execute(null);
        }

        private void OnActionCompleted(bool success)
        {
            DialogResult = success;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (ViewModel != null)
                ViewModel.ActionCompleted -= OnActionCompleted;

            base.OnClosed(e);
        }

        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            await ViewModel.ReloadAsync();

            SearchTxt.Focus();
            SearchTxt.SelectAll();
        }
    }
}