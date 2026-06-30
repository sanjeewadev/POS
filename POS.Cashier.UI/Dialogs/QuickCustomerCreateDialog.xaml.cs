using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.ViewModels;
using POS.Core.Models.DTOs;

namespace POS.Cashier.UI.Dialogs
{
    public partial class QuickCustomerCreateDialog : Window
    {
        public QuickCustomerCreateViewModel? ViewModel { get; private set; }

        public CustomerSearchDto? CreatedCustomer => ViewModel?.CreatedCustomer;

        public QuickCustomerCreateDialog()
        {
            InitializeComponent();

            if (App.Services != null)
            {
                ViewModel = App.Services.GetRequiredService<QuickCustomerCreateViewModel>();
                DataContext = ViewModel;

                ViewModel.ActionCompleted += OnActionCompleted;
            }
            else
            {
                MessageBox.Show(
                    "Application services are not available.",
                    "Quick Customer Registration",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public QuickCustomerCreateDialog(QuickCustomerCreateViewModel viewModel)
        {
            InitializeComponent();

            ViewModel = viewModel;
            DataContext = ViewModel;

            ViewModel.ActionCompleted += OnActionCompleted;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FullNameTextBox.Focus();
            FullNameTextBox.SelectAll();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (e.Key == Key.Escape)
            {
                ViewModel.CancelCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                MoveFocusOrSave();
                e.Handled = true;
            }
        }

        private void MoveFocusOrSave()
        {
            if (ViewModel == null)
                return;

            if (Keyboard.FocusedElement is TextBox currentTextBox)
            {
                if (currentTextBox == FullNameTextBox)
                {
                    PhoneTextBox.Focus();
                    PhoneTextBox.SelectAll();
                    return;
                }

                if (currentTextBox == PhoneTextBox)
                {
                    EmailTextBox.Focus();
                    EmailTextBox.SelectAll();
                    return;
                }

                if (currentTextBox == EmailTextBox)
                {
                    AddressTextBox.Focus();
                    AddressTextBox.SelectAll();
                    return;
                }

                if (currentTextBox == AddressTextBox)
                {
                    if (ViewModel.IsRetail)
                    {
                        NicTextBox.Focus();
                        NicTextBox.SelectAll();
                        return;
                    }

                    CompanyTextBox.Focus();
                    CompanyTextBox.SelectAll();
                    return;
                }

                if (currentTextBox == NicTextBox)
                {
                    ViewModel.SaveCommand.Execute(null);
                    return;
                }

                if (currentTextBox == CompanyTextBox)
                {
                    BrTextBox.Focus();
                    BrTextBox.SelectAll();
                    return;
                }

                if (currentTextBox == BrTextBox)
                {
                    VatTextBox.Focus();
                    VatTextBox.SelectAll();
                    return;
                }

                if (currentTextBox == VatTextBox)
                {
                    ViewModel.SaveCommand.Execute(null);
                    return;
                }
            }

            ViewModel.SaveCommand.Execute(null);
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
    }
}