using Microsoft.Extensions.Logging;

namespace EchoForge.API.Logging;

public class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly string _filePath;

    public FileLogger(string categoryName, string filePath)
    {
        _categoryName = categoryName;
        _filePath = filePath;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception == null) return;

        var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{logLevel}] [{_categoryName}]\n{message}";
        if (exception != null)
        {
            logLine += $"\n{exception}";
        }
        logLine += "\n";

        lock (_filePath)
        {
            File.AppendAllText(_filePath, logLine);
        }
    }
}

public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;

    public FileLoggerProvider(string filePath)
    {
        _filePath = filePath;
        // Overwrite log file on startup as requested
        File.WriteAllText(_filePath, $"--- Application Started at {DateTime.Now} ---\n");
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName, _filePath);
    }

    public void Dispose() { }
}

public static class FileLoggerExtensions
{
    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string filePath)
    {
        builder.AddProvider(new FileLoggerProvider(filePath));
        return builder;
    }
}
