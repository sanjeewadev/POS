using System;
using System.Windows;
using System.Windows.Controls;
using POS.Cashier.UI.ViewModels;

namespace POS.Cashier.UI.Dialogs
{
    public partial class FreeItemReasonModalWindow : Window
    {
        public FreeItemReasonModalViewModel ViewModel { get; }

        // Public properties to read the window outputs after closing
        public string SelectedReasonType => (cmbReasons.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
        public string CustomNotes => txtCustomReason.Text.Trim();

        public FreeItemReasonModalWindow(FreeItemReasonModalViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;

            // ViewModel callbacks mapping
            ViewModel.OnReasonConfirmed = () =>
            {
                this.DialogResult = true;
                this.Close();
            };

            ViewModel.OnCancel = () =>
            {
                this.DialogResult = false;
                this.Close();
            };
        }

        private void CmbReasons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pnlCustomReason == null) return;

            // Show input note field only if 'Other' category item is selected
            if (cmbReasons.SelectedItem == cbiOther)
            {
                pnlCustomReason.Visibility = Visibility.Visible;
                txtCustomReason.Focus();
            }
            else
            {
                pnlCustomReason.Visibility = Visibility.Collapsed;
                txtCustomReason.Clear();
            }
        }

        private void ApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            // Fallback validation block if "Other" is chosen without typing a custom reason
            if (cmbReasons.SelectedItem == cbiOther && string.IsNullOrWhiteSpace(CustomNotes))
            {
                MessageBox.Show("Please specify the manual authorization reason.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtCustomReason.Focus();
                return;
            }

            // Trigger data persistence via your core ViewModel structure safely
            this.DialogResult = true;
            this.Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}