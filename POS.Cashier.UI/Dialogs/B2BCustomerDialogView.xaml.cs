using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.ViewModels;

namespace POS.Cashier.UI.Dialogs
{
    public partial class B2BCustomerDialogView : Window
    {
        public B2BCustomerViewModel? ViewModel { get; private set; }

        public B2BCustomerDialogView()
        {
            InitializeComponent();

            // Wire up the ViewModel via Dependency Injection
            if (App.Services != null)
            {
                ViewModel = App.Services.GetRequiredService<B2BCustomerViewModel>();
                this.DataContext = ViewModel;

                // Subscribe to the event so the ViewModel can tell the Window to close
                ViewModel.ActionCompleted += OnActionCompleted;
            }
        }

        private void OnActionCompleted(bool success)
        {
            // success == true if they clicked ATTACH, false if they clicked CANCEL
            this.DialogResult = success;
            this.Close();
        }

        // CRITICAL: Unsubscribe to prevent WPF Memory Leaks!
        protected override void OnClosed(EventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ActionCompleted -= OnActionCompleted;
            }
            base.OnClosed(e);
        }
    }
}