using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace XiaomiSoftwareManager.UIComponents
{
	public class AppConsole : RichTextBox
	{
		private readonly double _fontSize = 12;
		private readonly double _lineHeight = 1;

		public AppConsole()
		{
			// To remove the empty line at the beginning
			//Dispatcher.Invoke(() =>
			//{
			//	if (Document.Blocks.Count == 1 &&
			//	Document.Blocks.FirstBlock is Paragraph firstParagraph &&
			//	firstParagraph.Inlines.Count == 0)
			//	{
			//		Document.Blocks.Remove(firstParagraph);
			//	}
			//});

			IsReadOnly = true;
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
			FontFamily = new FontFamily("Consolas");
			Document.FontSize = _fontSize;
			Document.LineHeight = _lineHeight;
			PrintLogoFromFile("XiaomiSoftwareManager.Themes.logo.txt");
		}

		private void PrintLogoFromFile(string resourceName)
		{
			try
			{
				Assembly assembly = Assembly.GetExecutingAssembly();
				using Stream? stream = assembly.GetManifestResourceStream(resourceName);
				if (stream == null) { return; }

				using StreamReader reader = new(stream);
				string line;
				while ((line = reader.ReadLine()!) != null)
				{
					Print(line);
				}
			}
			catch (Exception) { }
		}

		public void Write(string message)
		{
			WriteMessage(message, Colors.Black);
		}

		public void WriteInfo(string message)
		{
			WriteMessage(message, Colors.Gray);
		}

		public void WriteSuccess(string message)
		{
			WriteMessage(message, Colors.Green, true);
		}

		public void WriteError(string message)
		{
			WriteMessage(message, Colors.Red, true);
		}

		public void WriteWarning(string message)
		{
			WriteMessage(message, Colors.Orange, true);
		}

		private void WriteMessage(string message, Color color, bool isBold = false)
		{
			string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

			Dispatcher.Invoke(() =>
			{
				Paragraph paragraph = new();

				Run timestampRun = new($"[{timestamp}] ")
				{
					Foreground = new SolidColorBrush(Colors.DarkGray)
				};
				paragraph.Inlines.Add(timestampRun);

				Run messageRun = new(message)
				{
					Foreground = new SolidColorBrush(color)
				};

				if (isBold)
				{
					messageRun.FontWeight = FontWeights.Bold;
				}

				paragraph.Inlines.Add(messageRun);
				Document.Blocks.Add(paragraph);
				ScrollToEnd();
			});
		}

		private void Print(string message)
		{
			Dispatcher.Invoke(() =>
			{
				Paragraph paragraph = new()
				{
					LineHeight = 0.1,
					TextAlignment = TextAlignment.Center,
				};

				Run messageRun = new(message)
				{
					FontSize = 5,
					Foreground = new SolidColorBrush(Colors.Black)
				};

				paragraph.Inlines.Add(messageRun);
				Document.Blocks.Add(paragraph);
				ScrollToEnd();
			});
		}
	}
}
