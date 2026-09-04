using POS.Cashier.UI.Models;
using POS.Cashier.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace POS.Cashier.UI.Dialogs
{
    public partial class ProductSeekDialog : Window
    {
        public ProductSeekDialog(PluSearchViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += OnLoaded;

            // Listen for the specific Close signal from the ViewModel
            viewModel.ActionCompleted += (result) =>
            {
                if (result == null) Close();
            };

            // Refocus the search bar automatically after continuous scanning resets
            viewModel.SearchResetRequested += () =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    SearchTxt.Focus();
                }), System.Windows.Threading.DispatcherPriority.Background);
            };
        }

        private PluSearchViewModel? ViewModel => DataContext as PluSearchViewModel;

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                await ViewModel.InitializeAsync();
            }
            SearchTxt.Focus();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close(); // Only close on Escape or the explicit Close button
            }
            else if (e.Key == Key.Enter)
            {
                e.Handled = true;
                if (SearchTxt.IsFocused)
                {
                    ViewModel?.SearchCommand.Execute(null);
                }
                else if (ViewModel?.ConfirmSelectionCommand.CanExecute(null) == true)
                {
                    ViewModel.ConfirmSelectionCommand.Execute(null);
                }
            }
            else if (e.Key == Key.Up || e.Key == Key.Down)
            {
                if (!ResultsDataGrid.IsKeyboardFocusWithin)
                {
                    e.Handled = true;
                    ResultsDataGrid.Focus();
                    if (ResultsDataGrid.SelectedIndex < 0 && ResultsDataGrid.Items.Count > 0)
                    {
                        ResultsDataGrid.SelectedIndex = 0;
                    }
                }
            }
        }

        // The new Single-Click Handler!
        private void ResultsDataGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Notice the <DataGridRow> added here!
            if (e.OriginalSource is DependencyObject source && FindParent<DataGridRow>(source) != null)
            {
                e.Handled = true;

                // Push the code to the back of the UI queue so the mouse click can finish first!
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (ViewModel?.ConfirmSelectionCommand.CanExecute(null) == true)
                    {
                        ViewModel.ConfirmSelectionCommand.Execute(null);
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && ViewModel != null)
            {
                ViewModel.SearchCommand.Execute(null);
            }
        }

        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent) return parent;
                child = System.Windows.Media.VisualTreeHelper.GetParent(child);
            }
            return null;
        }
    }
}