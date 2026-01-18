using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using XiaomiSoftwareManager.Logic.Logger;
using xsm.Models;
using xsm.ViewModels.Dialogs;

namespace xsm.UI.Views.Dialogs;

public partial class ScrapeIssuesDialog : Window
{
	public ScrapeIssuesDialog(IReadOnlyList<ScrapeIssue> issues)
	{
		InitializeComponent();
		DataContext = new ScrapeIssuesViewModel(issues);
	}

	private void Close_OnClick(object? sender, RoutedEventArgs e)
	{
		Close();
	}

	private void SaveReport_OnClick(object? sender, RoutedEventArgs e)
	{
		if (DataContext is not ScrapeIssuesViewModel viewModel || viewModel.Issues.Count == 0)
		{
			return;
		}

		var logDirectory = Path.Combine(Logger.Instance.LOG_DIRECTORY, "Scraper");
		Directory.CreateDirectory(logDirectory);

		var filePath = Path.Combine(logDirectory, $"scrape_issues_{DateTime.Now:yyyyMMdd_HHmmss}.log");
		var builder = new StringBuilder();
		builder.AppendLine($"Scrape issues report - {DateTime.Now:O}");
		builder.AppendLine($"Total issues: {viewModel.Issues.Count}");
		builder.AppendLine();

		var index = 1;
		foreach (var issue in viewModel.Issues)
		{
			builder.AppendLine($"#{index}");
			builder.AppendLine($"Reason: {issue.Reason}");
			builder.AppendLine($"Link Text: {issue.LinkText}");
			builder.AppendLine($"Download Link: {issue.LinkHref}");
			builder.AppendLine("HTML:");
			builder.AppendLine(issue.Html ?? string.Empty);
			builder.AppendLine(new string('-', 80));
			index++;
		}

		File.WriteAllText(filePath, builder.ToString(), Encoding.UTF8);
		Logger.Instance.Log($"Scrape issues saved to {filePath}", LogLevel.Info);
	}
}
