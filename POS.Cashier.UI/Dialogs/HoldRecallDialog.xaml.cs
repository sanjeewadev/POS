using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using POS.Core.Models.DTOs;

namespace POS.Cashier.UI.Dialogs
{
    public partial class HoldRecallDialog : Window
    {
        public ObservableCollection<CashierCartSessionDto> SuspendedCarts { get; } = new();

        public CashierCartSessionDto? SelectedCart { get; set; }

        public HoldRecallAction RequestedAction { get; private set; } = HoldRecallAction.Close;

        public HoldRecallDialog(IEnumerable<CashierCartSessionDto> carts)
        {
            InitializeComponent();

            foreach (CashierCartSessionDto cart in carts ?? Enumerable.Empty<CashierCartSessionDto>())
                SuspendedCarts.Add(cart);

            SelectedCart = SuspendedCarts.FirstOrDefault();
            DataContext = this;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            dgSuspended.Focus();

            if (SelectedCart != null)
                dgSuspended.ScrollIntoView(SelectedCart);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            RequestedAction = HoldRecallAction.Close;
            DialogResult = false;
            e.Handled = true;
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            RequestedAction = HoldRecallAction.Close;
            DialogResult = false;
        }

        private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCart == null)
            {
                MessageBox.Show(
                    "Select a held cart to recall.",
                    "Recall Cart",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                dgSuspended.Focus();
                return;
            }

            RequestedAction = HoldRecallAction.Recall;
            DialogResult = true;
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCart == null)
            {
                MessageBox.Show(
                    "Select a held cart to cancel.",
                    "Cancel Held Cart",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                dgSuspended.Focus();
                return;
            }

            RequestedAction = HoldRecallAction.Cancel;
            DialogResult = true;
        }
    }

    public enum HoldRecallAction
    {
        Close,
        Recall,
        Cancel
    }
}
