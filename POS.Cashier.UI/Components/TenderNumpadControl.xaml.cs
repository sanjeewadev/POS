using System;
using System.Windows;
using System.Windows.Controls;

namespace POS.Cashier.UI.Components
{
    public partial class TenderNumpadControl : UserControl
    {
        public static readonly DependencyProperty TargetTextProperty =
            DependencyProperty.Register(
                nameof(TargetText),
                typeof(string),
                typeof(TenderNumpadControl),
                new FrameworkPropertyMetadata(
                    string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string TargetText
        {
            get => (string)GetValue(TargetTextProperty);
            set => SetValue(TargetTextProperty, value);
        }

        public static readonly DependencyProperty HeaderTextProperty =
            DependencyProperty.Register(
                nameof(HeaderText),
                typeof(string),
                typeof(TenderNumpadControl),
                new PropertyMetadata("T E N D E R   E N T R Y"));

        public string HeaderText
        {
            get => (string)GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        public static readonly DependencyProperty ModeTextProperty =
            DependencyProperty.Register(
                nameof(ModeText),
                typeof(string),
                typeof(TenderNumpadControl),
                new PropertyMetadata("TOUCH"));

        public string ModeText
        {
            get => (string)GetValue(ModeTextProperty);
            set => SetValue(ModeTextProperty, value);
        }

        public static readonly DependencyProperty TargetLabelProperty =
            DependencyProperty.Register(
                nameof(TargetLabel),
                typeof(string),
                typeof(TenderNumpadControl),
                new PropertyMetadata("CURRENT INPUT"));

        public string TargetLabel
        {
            get => (string)GetValue(TargetLabelProperty);
            set => SetValue(TargetLabelProperty, value);
        }

        public static readonly DependencyProperty ShowDecimalProperty =
            DependencyProperty.Register(
                nameof(ShowDecimal),
                typeof(bool),
                typeof(TenderNumpadControl),
                new PropertyMetadata(true, OnVisualOptionChanged));

        public bool ShowDecimal
        {
            get => (bool)GetValue(ShowDecimalProperty);
            set => SetValue(ShowDecimalProperty, value);
        }

        public static readonly DependencyProperty ShowDoubleZeroProperty =
            DependencyProperty.Register(
                nameof(ShowDoubleZero),
                typeof(bool),
                typeof(TenderNumpadControl),
                new PropertyMetadata(true, OnVisualOptionChanged));

        public bool ShowDoubleZero
        {
            get => (bool)GetValue(ShowDoubleZeroProperty);
            set => SetValue(ShowDoubleZeroProperty, value);
        }

        public static readonly DependencyProperty MaxLengthProperty =
            DependencyProperty.Register(
                nameof(MaxLength),
                typeof(int),
                typeof(TenderNumpadControl),
                new PropertyMetadata(0));

        public int MaxLength
        {
            get => (int)GetValue(MaxLengthProperty);
            set => SetValue(MaxLengthProperty, value);
        }

        public static readonly DependencyProperty ClearValueProperty =
            DependencyProperty.Register(
                nameof(ClearValue),
                typeof(string),
                typeof(TenderNumpadControl),
                new PropertyMetadata(string.Empty));

        public string ClearValue
        {
            get => (string)GetValue(ClearValueProperty);
            set => SetValue(ClearValueProperty, value);
        }

        public static readonly DependencyProperty SelectAllOnFirstKeyProperty =
            DependencyProperty.Register(
                nameof(SelectAllOnFirstKey),
                typeof(bool),
                typeof(TenderNumpadControl),
                new PropertyMetadata(true));

        public bool SelectAllOnFirstKey
        {
            get => (bool)GetValue(SelectAllOnFirstKeyProperty);
            set => SetValue(SelectAllOnFirstKeyProperty, value);
        }

        private bool _isFirstKeyPress = true;

        public event EventHandler? EnterPressed;
        public event EventHandler? KeyPressed;

        public TenderNumpadControl()
        {
            InitializeComponent();

            Loaded += (_, _) => ApplyVisualOptions();
        }

        public void ResetFirstKey()
        {
            _isFirstKeyPress = true;
        }

        private static void OnVisualOptionChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is TenderNumpadControl control)
                control.ApplyVisualOptions();
        }

        private void ApplyVisualOptions()
        {
            if (DecimalButton != null)
                DecimalButton.Visibility = ShowDecimal ? Visibility.Visible : Visibility.Hidden;

            if (DoubleZeroButton != null)
                DoubleZeroButton.Visibility = ShowDoubleZero ? Visibility.Visible : Visibility.Hidden;
        }

        private void Key_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            string key = button.Tag?.ToString() ?? string.Empty;

            switch (key)
            {
                case "ENTER":
                    _isFirstKeyPress = true;
                    EnterPressed?.Invoke(this, EventArgs.Empty);
                    break;

                case "CLR":
                    TargetText = ClearValue;
                    _isFirstKeyPress = true;
                    KeyPressed?.Invoke(this, EventArgs.Empty);
                    break;

                case "BACK":
                    ApplyBackspace();
                    _isFirstKeyPress = false;
                    KeyPressed?.Invoke(this, EventArgs.Empty);
                    break;

                case ".":
                    ApplyDecimalPoint();
                    _isFirstKeyPress = false;
                    KeyPressed?.Invoke(this, EventArgs.Empty);
                    break;

                case "00":
                    ApplyDigits("00");
                    KeyPressed?.Invoke(this, EventArgs.Empty);
                    break;

                default:
                    ApplyDigits(key);
                    KeyPressed?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }

        private void ApplyDigits(string digits)
        {
            if (string.IsNullOrWhiteSpace(digits))
                return;

            string current = TargetText ?? string.Empty;

            if (SelectAllOnFirstKey && _isFirstKeyPress)
            {
                current = string.Empty;
                _isFirstKeyPress = false;
            }

            if (current == "0")
                current = string.Empty;

            string next = current + digits;

            if (MaxLength > 0 && next.Length > MaxLength)
                next = next[..MaxLength];

            TargetText = next;
        }

        private void ApplyDecimalPoint()
        {
            if (!ShowDecimal)
                return;

            string current = TargetText ?? string.Empty;

            if (SelectAllOnFirstKey && _isFirstKeyPress)
            {
                current = string.Empty;
                _isFirstKeyPress = false;
            }

            if (string.IsNullOrWhiteSpace(current))
            {
                TargetText = "0.";
                return;
            }

            if (current.Contains("."))
                return;

            if (MaxLength > 0 && current.Length + 1 > MaxLength)
                return;

            TargetText = current + ".";
        }

        private void ApplyBackspace()
        {
            string current = TargetText ?? string.Empty;

            if (string.IsNullOrEmpty(current))
            {
                TargetText = ClearValue;
                return;
            }

            if (current.Length <= 1)
            {
                TargetText = ClearValue;
                return;
            }

            TargetText = current[..^1];
        }
    }
}