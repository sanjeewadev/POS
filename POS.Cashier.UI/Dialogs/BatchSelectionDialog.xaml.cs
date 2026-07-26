using POS.Cashier.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace POS.Cashier.UI.Dialogs
{
    public partial class BatchSelectionDialog : Window
    {
        private readonly BatchSelectionViewModel _viewModel;
        private bool _isCompleting;

        public BatchSelectionDialog(BatchSelectionViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            DataContext = _viewModel;
            _viewModel.ActionCompleted += OnActionCompleted;
            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
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

        private void OnClosed(object? sender, EventArgs e)
        {
            _viewModel.ActionCompleted -= OnActionCompleted;
            Loaded -= OnLoaded;
            Closed -= OnClosed;
        }

        private void OnActionCompleted(bool accepted)
        {
            if (_isCompleting)
                return;

            _isCompleting = true;
            try
            {
                DialogResult = accepted;
            }
            catch
            {
                // ShowDialog is expected; closing remains safe if ownership changes.
            }
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_viewModel.CancelCommand.CanExecute(null))
                    _viewModel.CancelCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter && _viewModel.ConfirmCommand.CanExecute(null))
            {
                _viewModel.ConfirmCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void BatchDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left ||
                _viewModel.ConfirmCommand.CanExecute(null) == false)
            {
                return;
            }

            if (e.OriginalSource is DependencyObject source &&
                FindParent<DataGridRow>(source) == null)
            {
                return;
            }

            _viewModel.ConfirmCommand.Execute(null);
            e.Handled = true;
        }

        private static T? FindParent<T>(DependencyObject? child)
            where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;
                child = System.Windows.Media.VisualTreeHelper.GetParent(child);
            }
            return null;
        }
    }
}
