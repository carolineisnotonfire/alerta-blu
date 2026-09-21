using Microsoft.Extensions.Logging;

namespace AlertaBlu.Infrastructure;

/// <summary>
/// Minimal rolling-file logger so Warning/Error entries are not silently dropped in Release builds
/// (the built-in Debug provider only writes when a debugger is attached). Never throws: a failed
/// write is dropped rather than crashing the app that was trying to report a different failure.
/// </summary>
public sealed class FileLoggerProvider(string logFilePath) : ILoggerProvider
{
    /// <summary>Once the log exceeds this size, it is truncated before the next write.</summary>
    private const long MaxSizeBytes = 1 * 1024 * 1024;

    private readonly Lock _writeLock = new();

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

    public void Dispose()
    {
    }

    private void WriteLine(string line)
    {
        lock (_writeLock)
        {
            try
            {
                var directory = Path.GetDirectoryName(logFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(logFilePath) && new FileInfo(logFilePath).Length > MaxSizeBytes)
                {
                    File.Delete(logFilePath);
                }

                File.AppendAllText(logFilePath, line + Environment.NewLine);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed class FileLogger(string categoryName, FileLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} [{logLevel}] {categoryName}: {formatter(state, exception)}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            provider.WriteLine(line);
        }
    }
}
