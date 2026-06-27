using System;
using System.Windows.Input;

namespace POS.BackOffice.UI.ViewModels
{
    // Restored this class so your older ViewModels can compile successfully!
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;

        public RelayCommand(Action<object?> execute)
        {
            _execute = execute;
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}