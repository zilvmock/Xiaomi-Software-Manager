using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace xsm.ViewModels;

public sealed class FilterOptionViewModel : INotifyPropertyChanged
{
	private bool _isSelected;

	public FilterOptionViewModel(string label, string value, bool isAllOption, bool isSelected)
	{
		Label = label;
		Value = value;
		IsAllOption = isAllOption;
		_isSelected = isSelected;
	}

	public string Label { get; }

	public string Value { get; }

	public bool IsAllOption { get; }

	public bool IsSelected
	{
		get => _isSelected;
		set => SetProperty(ref _isSelected, value);
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (Equals(field, value))
		{
			return;
		}

		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
