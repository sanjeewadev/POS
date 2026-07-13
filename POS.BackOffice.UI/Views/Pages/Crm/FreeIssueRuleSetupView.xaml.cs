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
        }

        private async void FreeIssueRuleSetupView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ViewModel = DataContext as FreeIssueRuleSetupViewModel;
                if (ViewModel == null && App.Services != null)
                {
                    ViewModel = App.Services.GetRequiredService<FreeIssueRuleSetupViewModel>();
                    DataContext = ViewModel;
                }

                if (ViewModel == null)
                    throw new InvalidOperationException("Free Issue Rule Setup is not available.");

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
