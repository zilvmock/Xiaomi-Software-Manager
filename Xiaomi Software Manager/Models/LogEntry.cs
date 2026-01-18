using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace xsm.Models;

public enum LogLevel
{
	Debug,
	Info,
	Task,
	Warning,
	Error
}

public sealed class LogEntry : INotifyPropertyChanged
{
    private string _message;
    private string? _description;
    private string _descriptionSuffix;

    public LogEntry(
        string message,
        string? description = null,
        IEnumerable<LogEntry>? details = null,
        LogLevel level = LogLevel.Info,
        DateTimeOffset? timestamp = null,
        Guid? id = null)
    {
        _message = message;
        _description = description;
        _descriptionSuffix = string.IsNullOrWhiteSpace(description) ? string.Empty : $" - {description}";
        Level = level;
        Timestamp = timestamp ?? DateTimeOffset.Now;
        Id = id ?? Guid.NewGuid();
        LevelColor = GetLevelColor(level);
        TimestampText = $"[{Timestamp:HH:mm:ss}] ";
        LevelText = level.ToString().ToUpperInvariant();

        if (details is null)
        {
            return;
        }

        foreach (var detail in details)
        {
            Details.Add(detail);
        }
    }

    public string Message
    {
        get => _message;
        private set => SetField(ref _message, value);
    }

    public string? Description
    {
        get => _description;
        private set => SetField(ref _description, value);
    }

    public LogLevel Level { get; }

    public DateTimeOffset Timestamp { get; }

    public Guid Id { get; }

    public string LevelColor { get; }

    public string TimestampText { get; }

    public string LevelText { get; }

    public string DescriptionSuffix
    {
        get => _descriptionSuffix;
        private set => SetField(ref _descriptionSuffix, value);
    }

    public ObservableCollection<LogEntry> Details { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public void UpdateDescription(string? description)
    {
        Description = description;
        DescriptionSuffix = string.IsNullOrWhiteSpace(description) ? string.Empty : $" - {description}";
    }

    public void UpdateMessage(string message)
    {
        Message = message;
    }

	private static string GetLevelColor(LogLevel level)
	{
		return level switch
		{
			LogLevel.Debug => "#6B7280",
			LogLevel.Task => "#16A34A",
			LogLevel.Warning => "#B45309",
			LogLevel.Error => "#B91C1C",
			_ => "#1D4ED8"
		};
	}

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
