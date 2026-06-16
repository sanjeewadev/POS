using System.Windows;

namespace POS.Cashier.UI.Views.Dialogs
{
    public partial class SuspendedCartsDialog : Window
    {
        public SuspendedCartsDialog()
        {
            InitializeComponent();
            this.DataContext = this;
        }
    }
}