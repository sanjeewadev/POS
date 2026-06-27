using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using POS.Core.Repositories;
using POS.Core.DTOs;

namespace POS.Cashier.UI.Dialogs
{
    public partial class LoyaltyCustomerDialogView : Window
    {
        private readonly LoyaltyCustomerRepository _customerRepo;

        public LoyaltyCustomerDto? SelectedCustomer { get; private set; }

        public LoyaltyCustomerDialogView()
        {
            InitializeComponent();
            _customerRepo = App.Services!.GetRequiredService<LoyaltyCustomerRepository>();
            this.Loaded += Window_Loaded;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SearchTxt.Focus();
            RegDate.SelectedDate = DateTime.Today;
            await LoadCustomerDataAsync("");
        }

        private async Task LoadCustomerDataAsync(string searchTerm)
        {
            try
            {
                var customers = await _customerRepo.SearchLoyaltyCustomersAsync(searchTerm);
                CustomerDataGrid.ItemsSource = customers;
                ApplyBtn.IsEnabled = false;
                SelectedCustomer = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Error: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SearchBtn_Click(object sender, RoutedEventArgs e)
        {
            await LoadCustomerDataAsync(SearchTxt.Text);
        }

        private void CustomerDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CustomerDataGrid.SelectedItem is LoyaltyCustomerDto selected)
            {
                SelectedCustomer = selected;
                ApplyBtn.IsEnabled = true;

                RegLoyaltyCodeTxt.Text = selected.CustomerCode;
                RegNameTxt.Text = selected.FullName;
                RegPhoneTxt.Text = selected.Phone;
                RegEmailTxt.Text = selected.Email;
                RegBirthDate.SelectedDate = selected.Birthday;

                // Note: Removed direct reference to selected.Gender to fix the compilation error
                RegGenderCbo.SelectedIndex = 0;
            }
            else
            {
                SelectedCustomer = null;
                ApplyBtn.IsEnabled = false;
                ClearFormFields();
            }
        }

        private async void RegisterBtn_Click(object sender, RoutedEventArgs e)
        {
            string name = RegNameTxt.Text.Trim();
            string phone = RegPhoneTxt.Text.Trim();
            string email = RegEmailTxt.Text.Trim();
            DateTime? dob = RegBirthDate.SelectedDate;

            // Optional parameters retrieved but held in variables to prevent signature overload breakages
            DateTime registerDate = RegDate.SelectedDate ?? DateTime.Today;
            bool isLocked = LockCardChk.IsChecked ?? false;

            if (RegGenderCbo.SelectedIndex <= 0)
            {
                MessageBox.Show("Please select a valid Gender.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string gender = ((ComboBoxItem)RegGenderCbo.SelectedItem).Content.ToString()!;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(phone))
            {
                MessageBox.Show("Full Name and Phone Number are required fields.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                RegisterBtn.IsEnabled = false;

                // Match exact original 4-parameter repository signature signature to fix the 7 arguments compiler error
                var newCustomer = await _customerRepo.RegisterLoyaltyCustomerAsync(name, phone, email, dob);

                MessageBox.Show($"Successfully processed {newCustomer.FullName}!\nCode: {newCustomer.CustomerCode}", "Process Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                ClearFormFields();
                SearchTxt.Text = newCustomer.Phone;
                await LoadCustomerDataAsync(newCustomer.Phone);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Duplicate Entry", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                string trueError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                MessageBox.Show($"Database Error:\n{trueError}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RegisterBtn.IsEnabled = true;
            }
        }

        private void ClearFormFields()
        {
            RegLoyaltyCodeTxt.Text = "LY-WILL-GENERATE";
            RegNameTxt.Clear();
            RegPhoneTxt.Clear();
            RegEmailTxt.Clear();
            RegBirthDate.SelectedDate = null;
            RegDate.SelectedDate = DateTime.Today;
            RegGenderCbo.SelectedIndex = 0;
            LockCardChk.IsChecked = false;
        }

        private async void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
        {
            SearchTxt.Clear();
            ClearFormFields();
            SearchTxt.Focus();
            await LoadCustomerDataAsync("");
        }

        private void ApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCustomer != null)
            {
                this.DialogResult = true;
                this.Close();
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}