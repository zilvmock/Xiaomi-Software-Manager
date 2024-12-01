using System.Windows;
using System.Windows.Controls;
using static XiaomiSoftwareManager.Dialogs.CustomDialog;

namespace XiaomiSoftwareManager.Dialogs
{
    public partial class CustomDialog : Window
    {
        public enum DialogType
        {
            OK = 0,
            OKCancel = 1,
            YesNo = 2,
        }

        public CustomDialog(UserControl content, string title = "Custom Dialog", DialogType dialogType = DialogType.OK)
        {
            InitializeComponent();
            this.Title = title;
            ContentControl.Content = content;

            ChangeButtons(dialogType);
        }

        public void ChangeButtons(DialogType dialogType)
        {
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

        public void ToggleButtonsVisibility()
        {
            FirstButton.Visibility = FirstButton.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            SecondButton.Visibility = SecondButton.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }


        private void FirstButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SecondButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
