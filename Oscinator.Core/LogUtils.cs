using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Oscinator.Core;

public static class LogUtils
{
    private static readonly ConcurrentDictionary<LogLevel, bool> EnabledLevels = new();
    public static ILogger<T> LoggerFor<T>() => new ClassedLogger<T>();
    public static ILogger LoggerFor(Type t) => new ClassedLogger(t);
    public static readonly ILoggerFactory Factory = new FactoryImpl();

    private static readonly HashSet<Action<LogLevel, string, string>> LogHandlers = new();
    public static void EnableLevel(LogLevel level) => EnabledLevels[level] = true;
    public static void DisableLevel(LogLevel level) => EnabledLevels.TryRemove(level, out _);

    static LogUtils()
    {
        EnabledLevels[LogLevel.Critical] = true;
        EnabledLevels[LogLevel.Error] = true;
        EnabledLevels[LogLevel.Warning] = true;
        EnabledLevels[LogLevel.Information] = true;

        OnLog += (level, category, message) => Console.WriteLine($"[{level}] {category} - {message}");
    }

    public static event Action<LogLevel, string, string> OnLog
    {
        add => LogHandlers.Add(value);
        remove => LogHandlers.Remove(value);
    }

    private class FactoryImpl : ILoggerFactory
    {
        public void Dispose() { }

        public ILogger CreateLogger(string categoryName)
        {
            return new ClassedLogger(categoryName.Split('.')[^1]);
        }

        public void AddProvider(ILoggerProvider provider)
        {
            throw new NotImplementedException();
        }
    }

    private class ClassedLogger<T>() : ClassedLogger(typeof(T)), ILogger<T>;

    private class ClassedLogger : ILogger
    {
        private readonly string myCategory;
        
        public ClassedLogger(Type loggingType)
        {
            myCategory = loggingType.Name;
        }

        public ClassedLogger(string category)
        {
            myCategory = category;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var message = formatter(state, exception);
            if (exception != null)
            {
                var builder = new StringBuilder(message);
                while (exception != null)
                {
                    builder.Append(' ');
                    builder.Append(exception.GetType().Name);
                    builder.Append(": ");
                    builder.AppendLine(exception.Message);
                    builder.AppendLine(exception.StackTrace);
                    exception = exception.InnerException;
                    if (exception != null)
                        builder.AppendLine("Inner:");
                }

                message = builder.ToString();
            }
            foreach (var handler in LogHandlers)
            {
                try
                {
                    handler(logLevel, myCategory, message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed handling log message: {ex}");
                }
            }
        }

        public bool IsEnabled(LogLevel logLevel) => EnabledLevels.ContainsKey(logLevel);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }
}