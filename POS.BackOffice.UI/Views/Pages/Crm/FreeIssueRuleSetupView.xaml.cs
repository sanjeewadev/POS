using Microsoft.Extensions.DependencyInjection;
using POS.BackOffice.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace POS.BackOffice.UI.Views.Pages.Crm
{
    public partial class FreeIssueRuleSetupView : UserControl
    {
        public FreeIssueRuleSetupViewModel? ViewModel { get; private set; }

        public FreeIssueRuleSetupView()
        {
            InitializeComponent();

            Loaded += FreeIssueRuleSetupView_Loaded;

            if (App.Services != null)
            {
                ViewModel = App.Services.GetRequiredService<FreeIssueRuleSetupViewModel>();
                DataContext = ViewModel;
            }
            else
            {
                MessageBox.Show(
                    "Application services are not available.",
                    "Free Issue Rule Setup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void FreeIssueRuleSetupView_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            try
            {
                await ViewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to initialize Free Issue Rule Setup.\n\n{ex.Message}",
                    "Free Issue Rule Setup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}