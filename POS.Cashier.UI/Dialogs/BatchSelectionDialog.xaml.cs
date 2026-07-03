using POS.Cashier.UI.ViewModels;
using POS.Core.Models.DTOs;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace POS.Cashier.UI.Dialogs
{
    public partial class BatchSelectionDialog : Window
    {
        private readonly BatchSelectionViewModel _viewModel;

        private bool _isCompletingAction;
        private bool _isProcessingRowAction;

        public CashierBatchDto? SelectedBatch => _viewModel.SelectedBatch;

        public BatchSelectionDialog(BatchSelectionViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            _viewModel.ActionCompleted += ViewModel_ActionCompleted;

            Loaded += BatchSelectionDialog_Loaded;
            Closed += BatchSelectionDialog_Closed;
        }

        private void BatchSelectionDialog_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                BatchDataGrid.Focus();

                if (_viewModel.SelectedBatch != null)
                {
                    BatchDataGrid.SelectedItem = _viewModel.SelectedBatch;
                    BatchDataGrid.ScrollIntoView(_viewModel.SelectedBatch);
                }
            }), DispatcherPriority.ApplicationIdle);
        }

        private void BatchSelectionDialog_Closed(object? sender, EventArgs e)
        {
            _viewModel.ActionCompleted -= ViewModel_ActionCompleted;

            Loaded -= BatchSelectionDialog_Loaded;
            Closed -= BatchSelectionDialog_Closed;
        }

        private void ViewModel_ActionCompleted(bool confirmed)
        {
            if (_isCompletingAction)
                return;

            _isCompletingAction = true;

            try
            {
                DialogResult = confirmed;
            }
            catch
            {
                // DialogResult can throw if the window was not opened with ShowDialog.
                // Keep closing safe.
            }

            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ExecuteCancel();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                ExecuteConfirmSelectedBatch();
                e.Handled = true;
            }
        }

        // Compatibility only:
        // If old XAML still has KeyDown="Window_KeyDown", this prevents compile errors.
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape)
                e.Handled = true;
        }

        // Compatibility only:
        // If old XAML still has KeyDown="BatchDataGrid_KeyDown", this prevents compile errors.
        private void BatchDataGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape)
                e.Handled = true;
        }

        private void BatchDataGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isProcessingRowAction || _isCompletingAction)
            {
                e.Handled = true;
                return;
            }

            DataGridRow? row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);

            if (row?.DataContext is not CashierBatchDto batch)
                return;

            SelectAndConfirmBatch(batch);

            e.Handled = true;
        }

        private void BatchDataGrid_TouchUp(object sender, TouchEventArgs e)
        {
            if (_isProcessingRowAction || _isCompletingAction)
            {
                e.Handled = true;
                return;
            }

            DataGridRow? row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);

            if (row?.DataContext is not CashierBatchDto batch)
                return;

            SelectAndConfirmBatch(batch);

            e.Handled = true;
        }

        private void SelectAndConfirmBatch(CashierBatchDto batch)
        {
            _isProcessingRowAction = true;

            _viewModel.SelectedBatch = batch;
            BatchDataGrid.SelectedItem = batch;
            BatchDataGrid.ScrollIntoView(batch);
            BatchDataGrid.Focus();

            ExecuteConfirmSelectedBatch();
        }

        private void ExecuteConfirmSelectedBatch()
        {
            if (_viewModel.ConfirmCommand.CanExecute(null))
            {
                _viewModel.ConfirmCommand.Execute(null);
                return;
            }

            // Invalid/expired/insufficient batch should not close the window.
            // Allow user to select another row.
            _isProcessingRowAction = false;
        }

        private void ExecuteCancel()
        {
            if (_viewModel.CancelCommand.CanExecute(null))
            {
                _viewModel.CancelCommand.Execute(null);
                return;
            }

            Close();
        }

        private static T? FindVisualParent<T>(DependencyObject? child)
            where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;

                child = VisualTreeHelper.GetParent(child);
            }

            return null;
        }
    }
}