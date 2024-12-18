using System.Windows.Controls;

namespace XiaomiSoftwareManager.UserControls
{
	public partial class AboutUserControl : UserControl
	{
		public AboutUserControl(string content)
		{
			InitializeComponent();
			MessageText.Text = content;
		}
	}
}
