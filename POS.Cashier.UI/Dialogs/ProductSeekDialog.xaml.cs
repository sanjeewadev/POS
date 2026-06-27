using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.ViewModels;

namespace POS.Cashier.UI.Dialogs
{
    public partial class ProductSeekDialog : Window
    {
        public ProductSeekDialog()
        {
            InitializeComponent();

            // Wire up the new MVVM Brain via Dependency Injection
            if (App.Services != null)
            {
                var viewModel = App.Services.GetRequiredService<PluSearchViewModel>();
                this.DataContext = viewModel;

                // Subscribe to the event so the ViewModel can tell this window when to safely close
                viewModel.ActionCompleted += OnActionCompleted;
            }

            // Put the blinking cursor right into the search bar automatically
            SearchTxt.Focus();
        }

        private void OnActionCompleted(bool success)
        {
            // success == true if they successfully pushed an item to the cart!
            this.DialogResult = success;
            this.Close();
        }

        // CRITICAL: Unsubscribe to prevent WPF Memory Leaks!
        protected override void OnClosed(EventArgs e)
        {
            if (this.DataContext is PluSearchViewModel viewModel)
            {
                viewModel.ActionCompleted -= OnActionCompleted;
            }
            base.OnClosed(e);
        }
    }
}