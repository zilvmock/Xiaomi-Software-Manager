using System.Windows;
using System.Windows.Controls;

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

		public bool areButtonsDisabled { get; set; }

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

		public void ToggleButtonsEnabled()
		{
			FirstButton.IsEnabled = !FirstButton.IsEnabled;
			SecondButton.IsEnabled = !SecondButton.IsEnabled;
			areButtonsDisabled = !FirstButton.IsEnabled && !SecondButton.IsEnabled;
		}

		public void DisableCloseBehaviourFirstButton()
		{
			FirstButton.Click -= FirstButton_Click;
		}

		public void DisableCloseBehaviourSecondButton()
		{
			SecondButton.Click -= SecondButton_Click;
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
