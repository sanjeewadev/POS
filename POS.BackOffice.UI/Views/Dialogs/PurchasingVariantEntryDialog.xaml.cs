using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Dialogs
{
    public partial class PurchasingVariantEntryDialog : Window
    {
        private readonly PurchasingVariantEntryDialogViewModel _viewModel;
        private bool _isCompleting;

        public PurchasingVariantEntryDialog(
            PurchasingVariantEntryDialogViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            DataContext = _viewModel;
        }

        public IReadOnlyList<PurchasingVariantEntryRow> AcceptedRows { get; private set; } =
            Array.Empty<PurchasingVariantEntryRow>();

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await _viewModel.InitializeAsync();
                ItemSelector.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"The variant-entry window could not be initialized.\n\n{ex.Message}",
                    "Variant Entry Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (_isCompleting)
                return;

            _isCompleting = true;

            try
            {
                Keyboard.ClearFocus();

                bool cellCommitted = VariantGrid.CommitEdit(
                    DataGridEditingUnit.Cell,
                    true);
                bool rowCommitted = VariantGrid.CommitEdit(
                    DataGridEditingUnit.Row,
                    true);

                if (!cellCommitted || !rowCommitted)
                {
                    MessageBox.Show(
                        this,
                        "The active variant entry could not be committed. Correct the current value and try again.",
                        "Variant Entry Not Ready",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!_viewModel.TryCollectAcceptedRows(
                        out IReadOnlyList<PurchasingVariantEntryRow> acceptedRows,
                        out string validationMessage))
                {
                    MessageBox.Show(
                        this,
                        validationMessage,
                        "Variant Entry Validation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                AcceptedRows = acceptedRows;
                DialogResult = true;
            }
            finally
            {
                _isCompleting = false;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (_isCompleting)
                return;

            DialogResult = false;
        }
    }
}
