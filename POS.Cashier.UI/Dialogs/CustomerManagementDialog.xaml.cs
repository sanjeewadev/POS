using System.Windows;

namespace POS.Cashier.UI.Views.Dialogs
{
    public partial class CustomerManagementDialog : Window
    {
        public CustomerManagementDialog()
        {
            InitializeComponent();
            this.DataContext = this;
        }
    }
}