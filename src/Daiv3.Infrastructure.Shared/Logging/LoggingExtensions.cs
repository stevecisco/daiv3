using Microsoft.Extensions.Logging;

namespace Daiv3.Infrastructure.Shared.Logging;

/// <summary>
/// Extension methods for configuring file logging.
/// </summary>
public static class LoggingExtensions
{
	/// <summary>
	/// Adds file logging to the logging builder.
	/// Logs are written to %LOCALAPPDATA%\Daiv3\logs\ directory.
	/// </summary>
	/// <param name="builder">The logging builder</param>
	/// <param name="logPrefix">Prefix for log file names (e.g., "cli", "maui")</param>
	/// <param name="minLevel">Minimum log level to write (default: Information)</param>
	/// <returns>The logging builder for chaining</returns>
	public static ILoggingBuilder AddFileLogging(
		this ILoggingBuilder builder,
		string logPrefix,
		LogLevel minLevel = LogLevel.Information)
	{
		var logDirectory = GetDefaultLogDirectory();
		var provider = new FileLoggerProvider(logDirectory, logPrefix, minLevel);
		builder.AddProvider(provider);
		return builder;
	}

	/// <summary>
	/// Adds file logging with a custom log directory.
	/// </summary>
	/// <param name="builder">The logging builder</param>
	/// <param name="logDirectory">Directory path for log files</param>
	/// <param name="logPrefix">Prefix for log file names (e.g., "cli", "maui")</param>
	/// <param name="minLevel">Minimum log level to write (default: Information)</param>
	/// <returns>The logging builder for chaining</returns>
	public static ILoggingBuilder AddFileLogging(
		this ILoggingBuilder builder,
		string logDirectory,
		string logPrefix,
		LogLevel minLevel = LogLevel.Information)
	{
		var provider = new FileLoggerProvider(logDirectory, logPrefix, minLevel);
		builder.AddProvider(provider);
		return builder;
	}

	/// <summary>
	/// Gets the default log directory: %LOCALAPPDATA%\Daiv3\logs
	/// </summary>
	public static string GetDefaultLogDirectory()
	{
		var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		return Path.Combine(baseDir, "Daiv3", "logs");
	}
}
