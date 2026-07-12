using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
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

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            RequestedAction = HoldRecallAction.Close;
            DialogResult = false;
            Close();
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
                return;
            }

            RequestedAction = HoldRecallAction.Recall;
            DialogResult = true;
            Close();
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
                return;
            }

            RequestedAction = HoldRecallAction.Cancel;
            DialogResult = true;
            Close();
        }
    }

    public enum HoldRecallAction
    {
        Close,
        Recall,
        Cancel
    }
}
