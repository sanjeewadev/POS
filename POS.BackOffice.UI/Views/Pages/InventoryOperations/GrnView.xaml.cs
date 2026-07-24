using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using POS.BackOffice.UI.ViewModels;

namespace POS.BackOffice.UI.Views.Pages.InventoryOperations
{
    public partial class GrnView : UserControl
    {
        public GrnView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext == null)
                return;

            try
            {
                await TryExecuteInitializeCommandAsync(DataContext);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to initialize GRN page:\n\n{ex.Message}",
                    "GRN Page Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void AddMatrixRows_Click(
            object sender,
            RoutedEventArgs e)
        {
            Keyboard.ClearFocus();

            bool cellCommitted = MatrixVariantsGrid.CommitEdit(
                DataGridEditingUnit.Cell,
                true);
            bool rowCommitted = MatrixVariantsGrid.CommitEdit(
                DataGridEditingUnit.Row,
                true);

            if (!cellCommitted || !rowCommitted)
                return;

            if (DataContext is not GrnViewModel viewModel)
                return;

            if (viewModel.AddMatrixCommand.CanExecute(null))
                viewModel.AddMatrixCommand.Execute(null);
        }

        private static async Task TryExecuteInitializeCommandAsync(object viewModel)
        {
            PropertyInfo? initializeCommandProperty =
                viewModel.GetType().GetProperty("InitializeCommand");

            object? initializeCommand =
                initializeCommandProperty?.GetValue(viewModel);

            if (initializeCommand == null)
                return;

            MethodInfo? canExecuteMethod =
                initializeCommand.GetType().GetMethod("CanExecute");

            object? canExecuteResult =
                canExecuteMethod?.Invoke(initializeCommand, new object?[] { null });

            if (canExecuteResult is bool canExecute && !canExecute)
                return;

            MethodInfo? executeAsyncMethod =
                initializeCommand.GetType().GetMethod("ExecuteAsync", new[] { typeof(object) });

            if (executeAsyncMethod != null)
            {
                object? taskResult =
                    executeAsyncMethod.Invoke(initializeCommand, new object?[] { null });

                if (taskResult is Task task)
                    await task;

                return;
            }

            MethodInfo? executeMethod =
                initializeCommand.GetType().GetMethod("Execute", new[] { typeof(object) });

            executeMethod?.Invoke(initializeCommand, new object?[] { null });
        }
    }
}