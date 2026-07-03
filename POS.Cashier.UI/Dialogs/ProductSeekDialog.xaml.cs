using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.ViewModels;
using POS.Core.Repositories;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace POS.Cashier.UI.Dialogs
{
    public partial class ProductSeekDialog : Window
    {
        private PluSearchViewModel? _viewModel;

        private bool _isCompletingWindowAction;
        private bool _isProcessingRowAction;

        public ProductSeekDialog()
        {
            InitializeComponent();

            if (App.Services == null)
                throw new InvalidOperationException("Application services are not available.");

            _viewModel = App.Services.GetRequiredService<PluSearchViewModel>();
            DataContext = _viewModel;

            _viewModel.ActionCompleted += OnActionCompleted;

            Loaded += ProductSeekDialog_Loaded;
            Closed += ProductSeekDialog_Closed;
        }

        private void ProductSeekDialog_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SearchTxt.Focus();
                SearchTxt.SelectAll();
            }), DispatcherPriority.ApplicationIdle);
        }

        private void ProductSeekDialog_Closed(object? sender, EventArgs e)
        {
            if (_viewModel != null)
                _viewModel.ActionCompleted -= OnActionCompleted;

            Loaded -= ProductSeekDialog_Loaded;
            Closed -= ProductSeekDialog_Closed;
        }

        private void OnActionCompleted(bool success)
        {
            if (_isCompletingWindowAction)
                return;

            _isCompletingWindowAction = true;

            try
            {
                DialogResult = success;
            }
            catch
            {
                // DialogResult can throw if the window was not opened with ShowDialog.
            }

            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_viewModel == null)
                return;

            if (e.Key == Key.Escape)
            {
                ExecuteClose();
                e.Handled = true;
                return;
            }

            if (e.Key != Key.Enter)
                return;

            if (IsSourceInside(SearchTxt, e.OriginalSource as DependencyObject))
            {
                if (_viewModel.SearchCommand.CanExecute(null))
                    _viewModel.SearchCommand.Execute(null);

                e.Handled = true;
                return;
            }

            if (IsSourceInside(BatchDataGrid, e.OriginalSource as DependencyObject) &&
                _viewModel.SelectedBatch != null)
            {
                ExecuteBatchRowAction(_viewModel.SelectedBatch);
                e.Handled = true;
                return;
            }

            if (IsSourceInside(VariantDataGrid, e.OriginalSource as DependencyObject) &&
                _viewModel.SelectedVariant != null)
            {
                ExecuteVariantRowAction(_viewModel.SelectedVariant);
                e.Handled = true;
                return;
            }

            if (IsSourceInside(ParentDataGrid, e.OriginalSource as DependencyObject) &&
                _viewModel.SelectedParent != null)
            {
                FocusVariantGridSoon();
                e.Handled = true;
            }
        }

        private void ParentDataGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isCompletingWindowAction)
            {
                e.Handled = true;
                return;
            }

            DataGridRow? row = FindParent<DataGridRow>(e.OriginalSource as DependencyObject);

            if (row?.DataContext is not ParentSeekDto parent)
                return;

            SelectParent(parent);

            e.Handled = true;
        }

        private void ParentDataGrid_TouchUp(object sender, TouchEventArgs e)
        {
            if (_isCompletingWindowAction)
            {
                e.Handled = true;
                return;
            }

            DataGridRow? row = FindParent<DataGridRow>(e.OriginalSource as DependencyObject);

            if (row?.DataContext is not ParentSeekDto parent)
                return;

            SelectParent(parent);

            e.Handled = true;
        }

        private void VariantDataGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isProcessingRowAction || _isCompletingWindowAction)
            {
                e.Handled = true;
                return;
            }

            DataGridRow? row = FindParent<DataGridRow>(e.OriginalSource as DependencyObject);

            if (row?.DataContext is not VariantSeekDto variant)
                return;

            ExecuteVariantRowAction(variant);

            e.Handled = true;
        }

        private void VariantDataGrid_TouchUp(object sender, TouchEventArgs e)
        {
            if (_isProcessingRowAction || _isCompletingWindowAction)
            {
                e.Handled = true;
                return;
            }

            DataGridRow? row = FindParent<DataGridRow>(e.OriginalSource as DependencyObject);

            if (row?.DataContext is not VariantSeekDto variant)
                return;

            ExecuteVariantRowAction(variant);

            e.Handled = true;
        }

        private void BatchDataGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isProcessingRowAction || _isCompletingWindowAction)
            {
                e.Handled = true;
                return;
            }

            DataGridRow? row = FindParent<DataGridRow>(e.OriginalSource as DependencyObject);

            if (row?.DataContext is not BatchSeekDto batch)
                return;

            ExecuteBatchRowAction(batch);

            e.Handled = true;
        }

        private void BatchDataGrid_TouchUp(object sender, TouchEventArgs e)
        {
            if (_isProcessingRowAction || _isCompletingWindowAction)
            {
                e.Handled = true;
                return;
            }

            DataGridRow? row = FindParent<DataGridRow>(e.OriginalSource as DependencyObject);

            if (row?.DataContext is not BatchSeekDto batch)
                return;

            ExecuteBatchRowAction(batch);

            e.Handled = true;
        }

        private void SelectParent(ParentSeekDto parent)
        {
            if (_viewModel == null)
                return;

            _viewModel.SelectedParent = parent;
            ParentDataGrid.SelectedItem = parent;
            ParentDataGrid.ScrollIntoView(parent);

            FocusVariantGridSoon();
        }

        private async void ExecuteVariantRowAction(VariantSeekDto variant)
        {
            if (_viewModel == null)
                return;

            _isProcessingRowAction = true;

            try
            {
                _viewModel.SelectedVariant = variant;
                VariantDataGrid.SelectedItem = variant;
                VariantDataGrid.ScrollIntoView(variant);

                if (!variant.HasBatchTracking)
                {
                    _viewModel.AddToCart();
                    return;
                }

                await Task.Delay(250);

                BatchDataGrid.Focus();

                if (_viewModel.SelectedBatch != null)
                {
                    BatchDataGrid.SelectedItem = _viewModel.SelectedBatch;
                    BatchDataGrid.ScrollIntoView(_viewModel.SelectedBatch);
                }
            }
            finally
            {
                _isProcessingRowAction = false;
            }
        }

        private void ExecuteBatchRowAction(BatchSeekDto batch)
        {
            if (_viewModel == null)
                return;

            _isProcessingRowAction = true;

            _viewModel.SelectedBatch = batch;
            BatchDataGrid.SelectedItem = batch;
            BatchDataGrid.ScrollIntoView(batch);

            _viewModel.AddBatchToCart(batch);

            if (!_isCompletingWindowAction)
                _isProcessingRowAction = false;
        }

        private void FocusVariantGridSoon()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_viewModel == null)
                    return;

                if (_viewModel.VariantResults.Count == 0)
                {
                    SearchTxt.Focus();
                    return;
                }

                VariantDataGrid.Focus();

                if (_viewModel.SelectedVariant != null)
                {
                    VariantDataGrid.SelectedItem = _viewModel.SelectedVariant;
                    VariantDataGrid.ScrollIntoView(_viewModel.SelectedVariant);
                }
            }), DispatcherPriority.Background);
        }

        private void ExecuteClose()
        {
            if (_viewModel?.CloseCommand.CanExecute(null) == true)
            {
                _viewModel.CloseCommand.Execute(null);
                return;
            }

            OnActionCompleted(false);
        }

        private static bool IsSourceInside(DependencyObject parent, DependencyObject? source)
        {
            while (source != null)
            {
                if (ReferenceEquals(source, parent))
                    return true;

                source = GetParentObject(source);
            }

            return false;
        }

        private static T? FindParent<T>(DependencyObject? child)
            where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;

                child = GetParentObject(child);
            }

            return null;
        }

        private static DependencyObject? GetParentObject(DependencyObject? child)
        {
            if (child == null)
                return null;

            if (child is Visual || child is Visual3D)
                return VisualTreeHelper.GetParent(child);

            return LogicalTreeHelper.GetParent(child);
        }
    }
}