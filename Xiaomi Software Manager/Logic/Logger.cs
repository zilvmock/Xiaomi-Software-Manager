using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using xsm.Models;

namespace XiaomiSoftwareManager.Logic.Logger
{
	public sealed class Logger
	{
		private static readonly Lazy<Logger> LazyInstance = new(() => new Logger());
		private readonly object _writeLock = new();
		private readonly List<LogEntry> _entries = new();
		private int _shutdownRequested;
		private int _stopped;

		public static Logger Instance => LazyInstance.Value;

		public string LOG_DIRECTORY { get; }

		public event Action<LogEntry>? EntryLogged;
		public event Action<LogEntry, LogEntry>? DetailLogged;

		private Logger()
		{
			LOG_DIRECTORY = Path.Combine(AppContext.BaseDirectory, "logs");
			Directory.CreateDirectory(LOG_DIRECTORY);
		}

		public static void Initialize()
		{
			_ = Instance;
		}

		public void Shutdown()
		{
			if (Interlocked.Exchange(ref _shutdownRequested, 1) != 0)
			{
				return;
			}

			Log("Logger shutting down.", LogLevel.Info);
			EntryLogged = null;
			DetailLogged = null;
			Interlocked.Exchange(ref _stopped, 1);
		}

		public LogHandle Log(string message, LogLevel level = LogLevel.Info, string? description = null,
			IEnumerable<LogEntry>? details = null)
		{
			var entry = new LogEntry(message, description, details, level);
			WriteEntry(entry);
			return new LogHandle(this, entry);
		}

		public LogHandle Log(LogEntry entry)
		{
			WriteEntry(entry);
			return new LogHandle(this, entry);
		}

		public IReadOnlyList<LogEntry> GetEntriesSnapshot()
		{
			lock (_writeLock)
			{
				return _entries.ToArray();
			}
		}

		public void LogException(Exception exception, string? message = null, LogLevel level = LogLevel.Error)
		{
			var details = BuildExceptionDetails(exception, level);
			var entry = new LogEntry(message ?? "Unhandled exception", exception.Message, details, level);
			WriteEntry(entry);
		}

		internal void AddDetail(LogEntry parent, LogEntry detail)
		{
			if (Volatile.Read(ref _stopped) != 0)
			{
				return;
			}

			var logPath = Path.Combine(LOG_DIRECTORY, $"{detail.Timestamp:yyyy-MM-dd}.log");
			var lines = new List<string>();
			AppendEntryLines(detail, lines, 1, parent.Id, includeId: false);

			try
			{
				lock (_writeLock)
				{
					File.AppendAllLines(logPath, lines, Encoding.UTF8);
				}
			}
			catch
			{
				// Swallow logging failures to avoid crashing the app during error handling.
			}

			DetailLogged?.Invoke(parent, detail);
		}

		private void WriteEntry(LogEntry entry)
		{
			if (Volatile.Read(ref _stopped) != 0)
			{
				return;
			}

			var logPath = Path.Combine(LOG_DIRECTORY, $"{entry.Timestamp:yyyy-MM-dd}.log");
			var lines = new List<string>();
			AppendEntryLines(entry, lines, 0, null, includeId: true);

			try
			{
				lock (_writeLock)
				{
					File.AppendAllLines(logPath, lines, Encoding.UTF8);
					_entries.Add(entry);
				}
			}
			catch
			{
				// Swallow logging failures to avoid crashing the app during error handling.
			}

			EntryLogged?.Invoke(entry);
		}

		private static void AppendEntryLines(LogEntry entry, List<string> lines, int indent, Guid? referenceId, bool includeId)
		{
			var prefix = indent == 0 ? string.Empty : new string(' ', indent * 2) + "- ";
			var formatted = FormatEntry(entry, includeId ? entry.Id : null, referenceId);
			var formattedLines = formatted.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

			foreach (var line in formattedLines)
			{
				lines.Add($"{prefix}{line}");
			}

			foreach (var detail in entry.Details)
			{
				AppendEntryLines(detail, lines, indent + 1, referenceId ?? entry.Id, includeId: false);
			}
		}

		private static string FormatEntry(LogEntry entry, Guid? id, Guid? referenceId)
		{
			var description = string.IsNullOrWhiteSpace(entry.Description) ? string.Empty : $" - {entry.Description}";
			var suffix = string.Empty;

			if (id.HasValue)
			{
				suffix = $" (id:{ToShortId(id.Value)})";
			}
			else if (referenceId.HasValue)
			{
				suffix = $" (ref:{ToShortId(referenceId.Value)})";
			}

			return string.Create(CultureInfo.InvariantCulture,
				$"[{entry.Timestamp:HH:mm:ss}] [{entry.Level.ToString().ToUpperInvariant()}] {entry.Message}{description}{suffix}");
		}

		private static string ToShortId(Guid id)
			=> id.ToString("N")[..8];

		private static IEnumerable<LogEntry> BuildExceptionDetails(Exception exception, LogLevel level)
		{
			var details = new List<LogEntry>();
			var current = exception;
			var depth = 0;

			while (current != null)
			{
				var prefix = depth == 0 ? "Exception" : $"Inner Exception {depth}";
				details.Add(new LogEntry(prefix, current.GetType().FullName, level: level));
				details.Add(new LogEntry("Message", current.Message, level: level));

				if (!string.IsNullOrWhiteSpace(current.StackTrace))
				{
					details.Add(new LogEntry("Stack Trace", current.StackTrace, level: level));
				}

				current = current.InnerException;
				depth++;
			}

			return details;
		}

		public sealed class LogHandle
		{
			private readonly Logger _logger;

			internal LogHandle(Logger logger, LogEntry entry)
			{
				_logger = logger;
				Entry = entry;
			}

			public LogEntry Entry { get; }

			public void AddDetail(string message, string? description = null, LogLevel level = LogLevel.Info,
				IEnumerable<LogEntry>? details = null)
			{
				var detail = new LogEntry(message, description, details, level);
				_logger.AddDetail(Entry, detail);
			}

			public void AddDetail(LogEntry detail)
			{
				_logger.AddDetail(Entry, detail);
			}
		}
	}
}
