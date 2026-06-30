using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.ViewModels;
using POS.Core.Models.DTOs;

namespace POS.Cashier.UI.Dialogs
{
    public partial class B2BCustomerDialogView : Window
    {
        public B2BCustomerViewModel? ViewModel { get; private set; }

        public CustomerSearchDto? SelectedCustomer => ViewModel?.SelectedCustomer;

        public B2BCustomerDialogView()
            : this("All")
        {
        }

        public B2BCustomerDialogView(string lookupMode)
        {
            InitializeComponent();

            if (App.Services != null)
            {
                ViewModel = App.Services.GetRequiredService<B2BCustomerViewModel>();

                ConfigureLookupMode(lookupMode);

                DataContext = ViewModel;
                ViewModel.ActionCompleted += OnActionCompleted;
            }
            else
            {
                MessageBox.Show(
                    "Application services are not available.",
                    "Customer Lookup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ConfigureLookupMode(string lookupMode)
        {
            if (ViewModel == null)
                return;

            string mode = string.IsNullOrWhiteSpace(lookupMode)
                ? "All"
                : lookupMode.Trim();

            if (mode.Equals("Loyalty", StringComparison.OrdinalIgnoreCase))
            {
                ViewModel.ConfigureForRetailLoyaltyCustomers();
                return;
            }

            if (mode.Equals("Wholesale", StringComparison.OrdinalIgnoreCase) ||
                mode.Equals("B2B", StringComparison.OrdinalIgnoreCase))
            {
                ViewModel.ConfigureForWholesaleCustomers();
                return;
            }

            if (mode.Equals("Credit", StringComparison.OrdinalIgnoreCase))
            {
                ViewModel.ConfigureForCreditCustomers();
                return;
            }

            ViewModel.ConfigureForAllCustomers();
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