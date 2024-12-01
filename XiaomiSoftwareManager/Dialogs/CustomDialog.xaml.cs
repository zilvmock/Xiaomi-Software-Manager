using System.Windows;

namespace XiaomiSoftwareManager.Dialogs
{
    public partial class CustomDialog : Window
    {
        private readonly bool canBeClosed;
        public enum DialogType
        {
            OK = 0,
            OKCancel = 1,
            YesNo = 2,
        }

        public CustomDialog(string message, string title = "Custom Dialog", DialogType dialogType = DialogType.OK)
        {
            InitializeComponent();
            this.Title = title;
            MessageText.Text = message;

            switch (dialogType)
            {
                case DialogType.OKCancel:
                    FirstButton.Content = "OK";
                    SecondButton.Content = "Cancel";
                    SecondButton.Visibility = Visibility.Visible;
                    break;

                case DialogType.YesNo:
                    FirstButton.Content = "Yes";
                    SecondButton.Content = "No";
                    SecondButton.Visibility = Visibility.Visible;
                    break;

                default:
                    FirstButton.Content = "OK";
                    SecondButton.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void FirstButton_Click(object sender, RoutedEventArgs e)
        {
            if (canBeClosed)
            {
                Close();
                return;
            }
            this.Hide();
        }

        private void SecondButton_Click(object sender, RoutedEventArgs e)
        {
            if (canBeClosed)
            {
                Close();
                return;
            }
            this.Hide();
        }
    }
}
