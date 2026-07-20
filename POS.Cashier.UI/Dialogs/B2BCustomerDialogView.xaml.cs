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

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || ViewModel == null)
                return;

            ViewModel.CancelCommand.Execute(null);
            e.Handled = true;
        }

        private async void SearchTxt_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (e.Key == Key.Enter)
            {
                await ViewModel.ReloadAsync();
                FocusCustomerGrid();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down || e.Key == Key.Up)
            {
                FocusCustomerGrid();
                e.Handled = true;
            }
        }

        private void CustomerDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (e.Key == Key.Enter)
            {
                if (ViewModel.SelectedCustomer != null && ViewModel.CanAttachToInvoice)
                    ViewModel.AttachCommand.Execute(null);

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                ViewModel.CancelCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void FocusCustomerGrid()
        {
            if (ViewModel?.SelectedCustomer == null)
            {
                SearchTxt.Focus();
                return;
            }

            CustomerDataGrid.SelectedItem = ViewModel.SelectedCustomer;
            CustomerDataGrid.ScrollIntoView(ViewModel.SelectedCustomer);
            CustomerDataGrid.Focus();
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