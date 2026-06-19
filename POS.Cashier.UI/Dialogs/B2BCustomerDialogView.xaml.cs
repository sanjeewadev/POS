using Microsoft.Extensions.DependencyInjection;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace POS.Cashier.UI.Views.Dialogs
{
    public partial class B2BCustomerDialogView : Window
    {
        private readonly LoyaltyCustomerRepository _repository;

        public WholesaleCustomerDto? SelectedCustomer { get; private set; }

        public B2BCustomerDialogView()
        {
            InitializeComponent();
            _repository = App.Services!.GetRequiredService<LoyaltyCustomerRepository>();

            this.Loaded += Window_Loaded;
            SearchTxt.Focus();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCustomersAsync("");
        }

        private async Task LoadCustomersAsync(string searchTerm)
        {
            try
            {
                // We will add this method to your POS CustomerRepository next
                var customers = await _repository.SearchWholesaleCustomersAsync(searchTerm);
                CustomerDataGrid.ItemsSource = customers;
                ResetSelection();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SearchBtn_Click(object sender, RoutedEventArgs e)
        {
            await LoadCustomersAsync(SearchTxt.Text);
        }

        private void CustomerDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CustomerDataGrid.SelectedItem is WholesaleCustomerDto selected)
            {
                SelectedCustomer = selected;
                UpdateFinancialDashboard(selected);
            }
            else
            {
                ResetSelection();
            }
        }

        // ==========================================
        // SECURITY & FINANCIAL LOGIC
        // ==========================================
        private void UpdateFinancialDashboard(WholesaleCustomerDto customer)
        {
            NoSelectionTxt.Visibility = Visibility.Collapsed;
            FinancialPanel.Visibility = Visibility.Visible;

            DisplayCompanyTxt.Text = string.IsNullOrWhiteSpace(customer.CompanyName) ? customer.FullName : customer.CompanyName;
            DisplayLimitTxt.Text = $"Rs. {customer.CreditLimit:N2}";
            DisplayDebtTxt.Text = $"Rs. {customer.CurrentBalance:N2}";

            decimal available = customer.RemainingCredit;
            DisplayAvailableTxt.Text = $"Rs. {available:N2}";

            // Security Check
            if (customer.IsCreditLocked)
            {
                LockReasonTxt.Text = "❌ ACCOUNT LOCKED BY MANAGEMENT";
                LockWarningBox.Visibility = Visibility.Visible;
                DisplayAvailableTxt.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkRed);
                ApplyBtn.IsEnabled = false; // Physically stop the sale
            }
            else if (available <= 0 && customer.CreditLimit > 0)
            {
                LockReasonTxt.Text = "❌ CREDIT LIMIT EXCEEDED";
                LockWarningBox.Visibility = Visibility.Visible;
                DisplayAvailableTxt.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkRed);
                ApplyBtn.IsEnabled = false; // Physically stop the sale
            }
            else
            {
                LockWarningBox.Visibility = Visibility.Collapsed;
                DisplayAvailableTxt.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGreen);
                ApplyBtn.IsEnabled = true; // Safe to proceed
            }
        }

        private void ResetSelection()
        {
            SelectedCustomer = null;
            ApplyBtn.IsEnabled = false;
            FinancialPanel.Visibility = Visibility.Collapsed;
            NoSelectionTxt.Visibility = Visibility.Visible;
        }

        // ==========================================
        // NUMPAD & BUTTONS
        // ==========================================
        private void NumpadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            string input = btn.Content.ToString()!;

            if (input == "C")
            {
                if (SearchTxt.Text.Length > 0)
                    SearchTxt.Text = SearchTxt.Text.Substring(0, SearchTxt.Text.Length - 1);
            }
            else
            {
                SearchTxt.Text += input;
            }
            SearchTxt.CaretIndex = SearchTxt.Text.Length;
            SearchTxt.Focus();
        }

        private async void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
        {
            SearchTxt.Clear();
            SearchTxt.Focus();
            await LoadCustomersAsync("");
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