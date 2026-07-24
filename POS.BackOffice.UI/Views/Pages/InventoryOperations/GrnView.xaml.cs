using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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