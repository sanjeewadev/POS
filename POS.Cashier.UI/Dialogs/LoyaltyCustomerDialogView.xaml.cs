using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using POS.Core.Repositories;

namespace POS.Cashier.UI.Views.Dialogs
{
    public partial class LoyaltyCustomerDialogView : Window
    {
        private readonly LoyaltyCustomerRepository _customerRepo;
        private TextBox _currentFocusedTextBox;

        // This is the property the main SalesView will read after the dialog closes
        public LoyaltyCustomerDto? SelectedCustomer { get; private set; }

        public LoyaltyCustomerDialogView()
        {
            InitializeComponent();

            // Resolve the repository from your Dependency Injection container
            _customerRepo = App.Services!.GetRequiredService<LoyaltyCustomerRepository>();

            // Setup UI defaults
            this.Loaded += Window_Loaded;

            // Track which textbox the cashier is currently touching for the Numpad
            SearchTxt.GotFocus += TextBox_GotFocus;
            RegPhoneTxt.GotFocus += TextBox_GotFocus;
            RegNameTxt.GotFocus += TextBox_GotFocus;

            // Default focus to search
            _currentFocusedTextBox = SearchTxt;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SearchTxt.Focus();
            // Load the initial list of customers into the grid
            await LoadCustomerDataAsync("");
        }

        // ==========================================
        // DATA GRID LOGIC
        // ==========================================
        private async Task LoadCustomerDataAsync(string searchTerm)
        {
            try
            {
                var customers = await _customerRepo.SearchLoyaltyCustomersAsync(searchTerm);
                CustomerDataGrid.ItemsSource = customers;

                // Clear selection state
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
            // Enable the Apply button only if a valid row is clicked
            if (CustomerDataGrid.SelectedItem is LoyaltyCustomerDto selected)
            {
                SelectedCustomer = selected;
                ApplyBtn.IsEnabled = true;
            }
            else
            {
                SelectedCustomer = null;
                ApplyBtn.IsEnabled = false;
            }
        }

        // ==========================================
        // REGISTRATION LOGIC
        // ==========================================
        private async void RegisterBtn_Click(object sender, RoutedEventArgs e)
        {
            string name = RegNameTxt.Text.Trim();
            string phone = RegPhoneTxt.Text.Trim();
            string email = RegEmailTxt.Text.Trim();
            DateTime? dob = RegBirthDate.SelectedDate;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(phone))
            {
                MessageBox.Show("Full Name and Phone Number are required fields.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                RegisterBtn.IsEnabled = false;

                var newCustomer = await _customerRepo.RegisterLoyaltyCustomerAsync(name, phone, email, dob);

                MessageBox.Show($"Successfully registered {newCustomer.FullName}!", "Registration Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                // Clear the form
                RegNameTxt.Clear();
                RegPhoneTxt.Clear();
                RegEmailTxt.Clear();
                RegBirthDate.SelectedDate = null;

                // Reload the grid and search for the new person
                SearchTxt.Text = newCustomer.Phone;
                await LoadCustomerDataAsync(newCustomer.Phone);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Duplicate Entry", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                // This digs one level deeper to get the actual SQL Server error
                string trueError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                MessageBox.Show($"Database Error:\n{trueError}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RegisterBtn.IsEnabled = true;
            }
        }

        // ==========================================
        // TOUCH NUMPAD ENGINE
        // ==========================================
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _currentFocusedTextBox = sender as TextBox;
        }

        private void NumpadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFocusedTextBox == null || sender is not Button btn) return;

            string input = btn.Content.ToString()!;

            if (input == "C")
            {
                if (_currentFocusedTextBox.Text.Length > 0)
                {
                    // Backspace functionality
                    _currentFocusedTextBox.Text = _currentFocusedTextBox.Text.Substring(0, _currentFocusedTextBox.Text.Length - 1);
                    _currentFocusedTextBox.CaretIndex = _currentFocusedTextBox.Text.Length;
                }
            }
            else
            {
                // Append number
                _currentFocusedTextBox.Text += input;
                _currentFocusedTextBox.CaretIndex = _currentFocusedTextBox.Text.Length;
            }

            // Keep focus so the blinking cursor stays active
            _currentFocusedTextBox.Focus();
        }

        private async void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
        {
            SearchTxt.Clear();
            RegNameTxt.Clear();
            RegPhoneTxt.Clear();
            RegEmailTxt.Clear();
            RegBirthDate.SelectedDate = null;

            SearchTxt.Focus();
            await LoadCustomerDataAsync("");
        }

        // ==========================================
        // WINDOW ACTIONS
        // ==========================================
        private void ApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCustomer != null)
            {
                this.DialogResult = true; // Signals to the main window that a selection was made
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