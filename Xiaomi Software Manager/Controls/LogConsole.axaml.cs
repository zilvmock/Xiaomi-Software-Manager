using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using xsm.Models;

namespace xsm.Controls;

public partial class LogConsole : UserControl
{
	private INotifyCollectionChanged _collectionWatcher;

	public static readonly StyledProperty<IEnumerable<LogEntry>> ItemsProperty =
		AvaloniaProperty.Register<LogConsole, IEnumerable<LogEntry>>(nameof(Items), Array.Empty<LogEntry>());

	public IEnumerable<LogEntry> Items
	{
		get => GetValue(ItemsProperty);
		set => SetValue(ItemsProperty, value);
	}

	public LogConsole()
	{
		InitializeComponent();
		ItemsProperty.Changed.AddClassHandler<LogConsole>((control, args) => control.OnItemsChanged(args));
	}

	private void OnItemsChanged(AvaloniaPropertyChangedEventArgs args)
	{
		if (args.OldValue is INotifyCollectionChanged oldCollection)
		{
			oldCollection.CollectionChanged -= OnRootCollectionChanged;
			DetachDetailWatchers(oldCollection);
		}

		if (args.NewValue is INotifyCollectionChanged newCollection)
		{
			_collectionWatcher = newCollection;
			newCollection.CollectionChanged += OnRootCollectionChanged;
			AttachDetailWatchers(newCollection);
		}

		ScrollToLatest();
	}

	private void OnRootCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.OldItems != null)
		{
			foreach (var entry in e.OldItems.OfType<LogEntry>())
			{
				entry.Details.CollectionChanged -= OnDetailCollectionChanged;
			}
		}

		if (e.NewItems != null)
		{
			foreach (var entry in e.NewItems.OfType<LogEntry>())
			{
				entry.Details.CollectionChanged += OnDetailCollectionChanged;
			}
		}

		ScrollToLatest();
	}

	private void OnDetailCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.NewItems != null && e.NewItems.Count > 0)
		{
			var detail = e.NewItems[e.NewItems.Count - 1];
			ScrollToItem(detail);
			return;
		}

		ScrollToLatest();
	}

	private void AttachDetailWatchers(INotifyCollectionChanged collection)
	{
		if (collection is IEnumerable<LogEntry> entries)
		{
			foreach (var entry in entries)
			{
				entry.Details.CollectionChanged += OnDetailCollectionChanged;
			}
		}
	}

	private void DetachDetailWatchers(INotifyCollectionChanged collection)
	{
		if (collection is IEnumerable<LogEntry> entries)
		{
			foreach (var entry in entries)
			{
				entry.Details.CollectionChanged -= OnDetailCollectionChanged;
			}
		}
	}

	private void ScrollToLatest()
	{
		var lastEntry = Items?.LastOrDefault();
		if (lastEntry == null)
		{
			return;
		}

		ScrollToItem(lastEntry);
	}

	private void ScrollToItem(object? item)
	{
		if (item == null)
		{
			return;
		}

		Dispatcher.UIThread.Post(() => LogTree.ScrollIntoView(item));
	}
}
