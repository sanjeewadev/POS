using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace POS.Cashier.UI.Dialogs
{
    public enum ActiveCartRecoveryAction
    {
        Resume,
        CancelCart,
        LogOff
    }

    public partial class ActiveCartRecoveryDialog : Window
    {
        private IReadOnlyList<Button> ChoiceButtons =>
            new[] { LogOffButton, CancelCartButton, ResumeButton };

        public ActiveCartRecoveryAction SelectedAction { get; private set; } =
            ActiveCartRecoveryAction.Resume;

        public ActiveCartRecoveryDialog(
            string referenceNo,
            int itemCount,
            decimal netTotal)
        {
            InitializeComponent();

            ReferenceTextBlock.Text = string.IsNullOrWhiteSpace(referenceNo)
                ? "-"
                : referenceNo.Trim();
            ItemCountTextBlock.Text = itemCount.ToString("N0");
            NetTotalTextBlock.Text = $"Rs. {netTotal:N2}";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ResumeButton.Focus();
            Keyboard.Focus(ResumeButton);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Complete(ActiveCartRecoveryAction.LogOff);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                if (e.IsRepeat)
                {
                    e.Handled = true;
                    return;
                }

                ActiveCartRecoveryAction action =
                    ReferenceEquals(Keyboard.FocusedElement, LogOffButton)
                        ? ActiveCartRecoveryAction.LogOff
                        : ReferenceEquals(Keyboard.FocusedElement, CancelCartButton)
                            ? ActiveCartRecoveryAction.CancelCart
                            : ActiveCartRecoveryAction.Resume;

                Complete(action);
                e.Handled = true;
                return;
            }

            if (e.Key != Key.Left && e.Key != Key.Right)
                return;

            IReadOnlyList<Button> buttons = ChoiceButtons;
            int currentIndex = 0;

            for (int index = 0; index < buttons.Count; index++)
            {
                if (ReferenceEquals(Keyboard.FocusedElement, buttons[index]))
                {
                    currentIndex = index;
                    break;
                }
            }

            int direction = e.Key == Key.Left ? -1 : 1;
            int nextIndex = (currentIndex + direction + buttons.Count) % buttons.Count;
            buttons[nextIndex].Focus();
            Keyboard.Focus(buttons[nextIndex]);
            e.Handled = true;
        }

        private void ResumeButton_Click(object sender, RoutedEventArgs e)
        {
            Complete(ActiveCartRecoveryAction.Resume);
        }

        private void CancelCartButton_Click(object sender, RoutedEventArgs e)
        {
            Complete(ActiveCartRecoveryAction.CancelCart);
        }

        private void LogOffButton_Click(object sender, RoutedEventArgs e)
        {
            Complete(ActiveCartRecoveryAction.LogOff);
        }

        private void Complete(ActiveCartRecoveryAction action)
        {
            SelectedAction = action;
            DialogResult = true;
        }
    }
}
