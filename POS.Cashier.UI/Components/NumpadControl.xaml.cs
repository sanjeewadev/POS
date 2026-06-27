using System.Windows;
using System.Windows.Controls;

namespace POS.Cashier.UI.Components
{
    public partial class NumpadControl : UserControl
    {
        // ── Dependency property ─────────────────────────────────────────────
        // Bind this to whichever TextBox the host dialog wants to drive.
        // Two-way binding works automatically.
        public static readonly DependencyProperty TargetTextProperty =
            DependencyProperty.Register(
                nameof(TargetText),
                typeof(string),
                typeof(NumpadControl),
                new FrameworkPropertyMetadata(
                    "0",
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string TargetText
        {
            get => (string)GetValue(TargetTextProperty);
            set => SetValue(TargetTextProperty, value);
        }

        // ── Optional event so host can react to every key press ─────────────
        public event RoutedEventHandler? KeyPressed;

        public NumpadControl()
        {
            InitializeComponent();
        }

        private void Key_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            string key = btn.Tag?.ToString() ?? "";

            switch (key)
            {
                case "CLR":
                    // Clear everything
                    TargetText = "0";
                    break;

                case "C":
                    // Backspace one character
                    if (TargetText.Length > 1)
                        TargetText = TargetText[..^1];
                    else
                        TargetText = "0";
                    break;

                default:
                    // Digit press
                    if (TargetText == "0")
                        TargetText = key;
                    else
                        TargetText += key;
                    break;
            }

            KeyPressed?.Invoke(this, e);
        }
    }
}