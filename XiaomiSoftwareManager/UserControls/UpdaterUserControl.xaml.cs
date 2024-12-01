using System.Windows;
using System.Windows.Controls;

namespace XiaomiSoftwareManager.UserControls
{
    public partial class UpdaterUserControl : UserControl
    {
        public UpdaterUserControl()
        {
            InitializeComponent();
            Results.Visibility = Visibility.Collapsed;
        }

        public void ShowResults(string results, string updateMessage = "")
        {
            Spinner.Visibility = Visibility.Collapsed;
            Status.Text = results;

            if (!string.IsNullOrEmpty(updateMessage))
            {
                Results.Visibility = Visibility.Visible;
                Results.Text = updateMessage;
            }
        }
    }
}
