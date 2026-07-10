using System.ComponentModel;
using System.Windows;

namespace POS.BackOffice.UI.Views.Layout
{
    public partial class ManagementShellView : Window
    {
        public ManagementShellView()
        {
            InitializeComponent();
            Closing += ManagementShellView_Closing;
        }

        private void ManagementShellView_Closing(
            object? sender,
            CancelEventArgs e)
        {
            if (Application.Current is not App app)
                return;

            if (!app.TryApproveWindowClose())
                e.Cancel = true;
        }
    }
}
