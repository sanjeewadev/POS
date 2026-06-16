using System.Windows;

namespace POS.Cashier.UI.Views.Dialogs
{
    public partial class DocumentTenderDialog : Window
    {
        public DocumentTenderDialog()
        {
            InitializeComponent();
            this.DataContext = this;
        }
    }
}