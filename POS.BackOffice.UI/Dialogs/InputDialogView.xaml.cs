using System.Windows;

namespace POS.BackOffice.UI.Dialogs
{
    public partial class InputDialogView : Window
    {
        // This property holds the text the user typed so the ViewModel can read it
        public string InputText { get; private set; } = string.Empty;

        public InputDialogView(string title, string prompt)
        {
            InitializeComponent();

            // Set the dynamic text
            this.Title = title;
            lblPrompt.Text = prompt;

            // Auto-focus the textbox so the user can immediately start typing
            txtInput.Focus();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Security/Validation: Do not allow blank group names
            if (string.IsNullOrWhiteSpace(txtInput.Text))
            {
                lblError.Visibility = Visibility.Visible;
                return;
            }

            InputText = txtInput.Text.Trim();

            // Setting DialogResult to true automatically closes the window
            this.DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            // Setting DialogResult to false automatically closes the window
            this.DialogResult = false;
        }
    }
}