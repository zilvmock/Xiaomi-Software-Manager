using Avalonia.Controls;
using Avalonia.Interactivity;

namespace xsm.UI.Views.Dialogs
{
	public partial class ConfirmCloseDialog : Window
	{
		public ConfirmCloseDialog()
		{
			InitializeComponent();
		}

		private void Cancel_OnClick(object? sender, RoutedEventArgs e)
		{
			Close(false);
		}

		private void Quit_OnClick(object? sender, RoutedEventArgs e)
		{
			Close(true);
		}
	}
}
