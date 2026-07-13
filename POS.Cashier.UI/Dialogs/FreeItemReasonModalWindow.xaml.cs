using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.Models;
using POS.Cashier.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace POS.Cashier.UI.Dialogs
{
    public partial class FreeItemReasonModalWindow : Window
    {
        private readonly CartItem _selectedItem;
        private readonly IReadOnlyCollection<CartItem> _cartItems;
        private readonly string _cashierName;

        public FreeItemReasonModalViewModel? ViewModel { get; private set; }
        public FreeItemApplyResult? Result => ViewModel?.Result;

        public FreeItemReasonModalWindow(
            CartItem selectedItem,
            IReadOnlyCollection<CartItem> cartItems,
            string cashierName)
        {
            InitializeComponent();
            _selectedItem = selectedItem ?? throw new ArgumentNullException(nameof(selectedItem));
            _cartItems = cartItems ?? Array.Empty<CartItem>();
            _cashierName = cashierName ?? string.Empty;

            if (App.Services == null)
                throw new InvalidOperationException("Application services are not available.");

            ViewModel = App.Services.GetRequiredService<FreeItemReasonModalViewModel>();
            DataContext = ViewModel;
            ViewModel.ActionCompleted += OnActionCompleted;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            try
            {
                await ViewModel.InitializeAsync(_selectedItem, _cartItems, _cashierName);
                QuantityTextBox.Focus();
                QuantityTextBox.SelectAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to initialize Free Issue dialog.\n\n{ex.Message}",
                    "Free Issue",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                DialogResult = false;
            }
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

            if (e.Key == Key.Enter && Keyboard.FocusedElement is not System.Windows.Controls.ComboBox)
            {
                ViewModel.ConfirmCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void OnActionCompleted(bool accepted)
        {
            DialogResult = accepted;
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
