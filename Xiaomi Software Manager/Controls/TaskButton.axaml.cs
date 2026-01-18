using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentIconSymbol = FluentIcons.Common.Icon;

namespace xsm.Controls;

public partial class TaskButton : UserControl
{
	public static readonly StyledProperty<string> TextProperty =
		AvaloniaProperty.Register<TaskButton, string>(nameof(Text), string.Empty);

	public static readonly StyledProperty<string> RunningTextProperty =
		AvaloniaProperty.Register<TaskButton, string>(nameof(RunningText), string.Empty);

	public static readonly StyledProperty<bool> IsRunningProperty =
		AvaloniaProperty.Register<TaskButton, bool>(nameof(IsRunning));

	public static readonly StyledProperty<FluentIconSymbol> IconProperty =
		AvaloniaProperty.Register<TaskButton, FluentIconSymbol>(nameof(Icon), FluentIconSymbol.ArrowSync);

	public static readonly StyledProperty<FluentIconSymbol> SpinnerIconProperty =
		AvaloniaProperty.Register<TaskButton, FluentIconSymbol>(nameof(SpinnerIcon), FluentIconSymbol.ArrowSync);

	public TaskButton()
	{
		InitializeComponent();

		UpdateState();
	}

	public string Text
	{
		get => GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	public string RunningText
	{
		get => GetValue(RunningTextProperty);
		set => SetValue(RunningTextProperty, value);
	}

	public bool IsRunning
	{
		get => GetValue(IsRunningProperty);
		set => SetValue(IsRunningProperty, value);
	}

	public FluentIconSymbol Icon
	{
		get => GetValue(IconProperty);
		set => SetValue(IconProperty, value);
	}

	public FluentIconSymbol SpinnerIcon
	{
		get => GetValue(SpinnerIconProperty);
		set => SetValue(SpinnerIconProperty, value);
	}

	public event EventHandler<RoutedEventArgs>? Click;

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == IsRunningProperty || change.Property == IsEnabledProperty)
		{
			UpdateState();
		}
	}

	private void UpdateState()
	{
		Classes.Set("running", IsRunning);
		RootButton.IsEnabled = IsEnabled && !IsRunning;

		if (IsRunning)
		{
		}
		else
		{
		}
	}

	private void RootButton_OnClick(object? sender, RoutedEventArgs e)
	{
		Click?.Invoke(this, e);
	}
}
