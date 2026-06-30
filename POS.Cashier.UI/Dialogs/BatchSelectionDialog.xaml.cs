using System.Windows;
using System.Windows.Input;
using POS.Cashier.UI.ViewModels;
using POS.Core.Models.DTOs;

namespace POS.Cashier.UI.Dialogs
{
    public partial class BatchSelectionDialog : Window
    {
        private readonly BatchSelectionViewModel _viewModel;

        public CashierBatchDto? SelectedBatch => _viewModel.SelectedBatch;

        public BatchSelectionDialog(BatchSelectionViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            _viewModel.ActionCompleted += ViewModel_ActionCompleted;

            Loaded += (_, _) =>
            {
                BatchDataGrid.Focus();

                if (_viewModel.SelectedBatch != null)
                {
                    BatchDataGrid.SelectedItem = _viewModel.SelectedBatch;
                    BatchDataGrid.ScrollIntoView(_viewModel.SelectedBatch);
                }
            };

            Closed += (_, _) =>
            {
                _viewModel.ActionCompleted -= ViewModel_ActionCompleted;
            };
        }

        private void ViewModel_ActionCompleted(bool confirmed)
        {
            DialogResult = confirmed;
            Close();
        }

        private void BatchDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.ConfirmCommand.CanExecute(null))
                _viewModel.ConfirmCommand.Execute(null);
        }
    }
}