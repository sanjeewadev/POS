using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.Models;
using POS.Cashier.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;

namespace POS.Cashier.UI.Dialogs
{
    public partial class FreeItemReasonModalWindow : Window
    {
        private readonly CartItem _selectedItem;

        public FreeItemReasonModalViewModel? ViewModel { get; private set; }

        public FreeItemApplyResult? Result => ViewModel?.Result;

        public FreeItemReasonModalWindow(CartItem selectedItem)
        {
            InitializeComponent();

            _selectedItem = selectedItem ?? throw new ArgumentNullException(nameof(selectedItem));

            if (App.Services != null)
            {
                ViewModel = App.Services.GetRequiredService<FreeItemReasonModalViewModel>();
                DataContext = ViewModel;

                ViewModel.ActionCompleted += OnActionCompleted;
            }
            else
            {
                MessageBox.Show(
                    "Application services are not available.",
                    "Free Item",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            try
            {
                await ViewModel.InitializeAsync(_selectedItem);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to initialize Free Item dialog.\n\n{ex.Message}",
                    "Free Item",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

            if (e.Key == Key.Enter)
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