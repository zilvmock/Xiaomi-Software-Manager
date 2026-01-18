using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using xsm.Models;

namespace xsm.ViewModels.Dialogs;

public sealed class ScrapeIssuesViewModel : INotifyPropertyChanged
{
	private ScrapeIssue? _selectedIssue;

	public ScrapeIssuesViewModel(IEnumerable<ScrapeIssue> issues)
	{
		foreach (var issue in issues)
		{
			Issues.Add(issue);
		}

		SelectedIssue = Issues.FirstOrDefault();
	}

	public ObservableCollection<ScrapeIssue> Issues { get; } = new();

	public ScrapeIssue? SelectedIssue
	{
		get => _selectedIssue;
		set => SetProperty(ref _selectedIssue, value);
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (Equals(field, value))
		{
			return false;
		}

		field = value;
		OnPropertyChanged(propertyName);
		return true;
	}

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
