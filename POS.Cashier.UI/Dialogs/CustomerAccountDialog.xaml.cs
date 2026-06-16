using System.Windows;

namespace POS.Cashier.UI.Views.Dialogs
{
    public partial class CustomerAccountDialog : Window
    {
        public CustomerAccountDialog()
        {
            InitializeComponent();
            this.DataContext = this;
        }
    }
}