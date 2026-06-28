using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.ViewModels;

namespace POS.Cashier.UI.Dialogs
{
    public partial class ExpressItemDialogView : Window
    {
        public ExpressItemDialogView()
        {
            InitializeComponent();

            if (App.Services != null)
            {
                DataContext = App.Services.GetRequiredService<ExpressMenuViewModel>();
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = true;
                Close();
                e.Handled = true;
            }
        }
    }
}