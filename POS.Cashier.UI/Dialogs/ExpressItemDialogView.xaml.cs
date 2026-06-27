using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.ViewModels;

namespace POS.Cashier.UI.Dialogs
{
    public partial class ExpressItemDialogView : Window
    {
        public ExpressItemDialogView()
        {
            InitializeComponent();

            // Wire up the ViewModel via Dependency Injection
            if (App.Services != null)
            {
                this.DataContext = App.Services.GetRequiredService<ExpressMenuViewModel>();
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            // Rapid-Fire mode: The window stays open until the cashier explicitly closes it
            this.DialogResult = true;
            this.Close();
        }
    }
}