using Newtonsoft.Json;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using XiaomiSoftwareManager.Dialogs;
using XiaomiSoftwareManager.Models;

namespace XiaomiSoftwareManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Constants and P/Invoke for resizing
        private const int WM_SYSCOMMAND = 0x112;
        private const int SC_SIZE = 0xF000;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public enum WindowResizeEdge // Indicates which edge of the window is being resized.
        {
            Left = 1,
            Right = 2,
            Top = 3,
            TopLeft = 4,
            TopRight = 5,
            Bottom = 6,
            BottomLeft = 7,
            BottomRight = 8
        }

        private AppInfo appInfo;

        public MainWindow()
        {
            // TODO: Kai uzsirenderina consolej parodyt asci art XiaomiSoftwareManager ar kazka tokio

            InitializeComponent();
            LoadInfo();
        }

        private void LoadInfo()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = "XiaomiSoftwareManager.info.json";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new(stream))
            {
                string json = reader.ReadToEnd();
                appInfo = JsonConvert.DeserializeObject<AppInfo>(json);
            }
        }
        private void ResizeGrip_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragResize(WindowResizeEdge.BottomRight);
            }
        }

        private void DragResize(WindowResizeEdge edge)
        {
            // Call the Windows API function to initiate resizing.
            SendMessage(new WindowInteropHelper(this).Handle, WM_SYSCOMMAND, (IntPtr)(SC_SIZE + edge), IntPtr.Zero);
        }

        private void AboutMenu_Click(object sender, RoutedEventArgs e)
        {
            new CustomDialog($"{appInfo.Title}\n{appInfo.Version}\n\nDeveloped by {appInfo.Author}", "About", CustomDialog.DialogType.OK).ShowDialog();
        }

        private void UpdateMenu_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}