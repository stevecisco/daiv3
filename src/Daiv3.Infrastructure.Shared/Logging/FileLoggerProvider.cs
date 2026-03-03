using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Daiv3.Infrastructure.Shared.Logging;

/// <summary>
/// Simple file logger provider that writes logs to a rolling file.
/// Thread-safe and supports all log levels with timestamps.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
	private readonly string _logDirectory;
	private readonly string _logPrefix;
	private readonly LogLevel _minLevel;
	private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
	private readonly object _fileLock = new();
	private string? _currentLogFile;
	private StreamWriter? _writer;

	public FileLoggerProvider(string logDirectory, string logPrefix = "app", LogLevel minLevel = LogLevel.Information)
	{
		_logDirectory = logDirectory;
		_logPrefix = logPrefix;
		_minLevel = minLevel;

		// Ensure log directory exists - use try/catch to handle access issues
		try
		{
			if (!Directory.Exists(_logDirectory))
			{
				Directory.CreateDirectory(_logDirectory);
			}
		}
		catch (Exception)
		{
			// If we can't create the log directory, fall back to temp directory
			_logDirectory = Path.Combine(Path.GetTempPath(), "daiv3_logs");
			try
			{
				if (!Directory.Exists(_logDirectory))
				{
					Directory.CreateDirectory(_logDirectory);
				}
			}
			catch
			{
				// If even temp fails, log to current directory as last resort
				_logDirectory = Directory.GetCurrentDirectory();
			}
		}

		// Initialize log file
		EnsureLogFile();
	}

	public ILogger CreateLogger(string categoryName)
	{
		return _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this, _minLevel));
	}

	internal void WriteLog(string categoryName, LogLevel logLevel, EventId eventId, string message, Exception? exception)
	{
		lock (_fileLock)
		{
			try
			{
				EnsureLogFile();

				var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
				var levelString = GetLogLevelString(logLevel);
				var logEntry = $"[{timestamp}] [{levelString}] {categoryName}: {message}";

				_writer?.WriteLine(logEntry);

				if (exception != null)
				{
					_writer?.WriteLine($"Exception: {exception}");
				}

				_writer?.Flush();
			}
			catch
			{
				// Don't throw exceptions from logging
			}
		}
	}

	private void EnsureLogFile()
	{
		var today = DateTime.Now.ToString("yyyy-MM-dd");
		var logFileName = $"{_logPrefix}-{today}.log";
		var logFilePath = Path.Combine(_logDirectory, logFileName);

		if (_currentLogFile == logFilePath && _writer != null)
		{
			return;
		}

		_writer?.Dispose();
		_currentLogFile = logFilePath;
		
		// Use FileShare.ReadWrite to allow concurrent access from multiple test instances
		var fileStream = new FileStream(
			logFilePath,
			FileMode.Append,
			FileAccess.Write,
			FileShare.ReadWrite | FileShare.Delete,
			bufferSize: 4096,
			useAsync: false);
		
		_writer = new StreamWriter(fileStream) { AutoFlush = true };
	}

	private static string GetLogLevelString(LogLevel logLevel) => logLevel switch
	{
		LogLevel.Trace => "TRACE",
		LogLevel.Debug => "DEBUG",
		LogLevel.Information => "INFO ",
		LogLevel.Warning => "WARN ",
		LogLevel.Error => "ERROR",
		LogLevel.Critical => "CRIT ",
		_ => "INFO "
	};

	public void Dispose()
	{
		_writer?.Dispose();
	}

	private sealed class FileLogger : ILogger
	{
		private readonly string _categoryName;
		private readonly FileLoggerProvider _provider;
		private readonly LogLevel _minLevel;

		public FileLogger(string categoryName, FileLoggerProvider provider, LogLevel minLevel)
		{
			_categoryName = categoryName;
			_provider = provider;
			_minLevel = minLevel;
		}

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (!IsEnabled(logLevel))
			{
				return;
			}

			var message = formatter(state, exception);
			_provider.WriteLog(_categoryName, logLevel, eventId, message, exception);
		}
	}
}
