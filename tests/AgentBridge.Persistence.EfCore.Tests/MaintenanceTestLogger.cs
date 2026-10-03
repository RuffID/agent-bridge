using Microsoft.Extensions.Logging;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <inheritdoc/>
/// <remarks>Собирает безопасные события библиотечного coordinator без вывода исходных исключений.</remarks>
public class MaintenanceTestLogger : ILoggerProvider
{
    /// <summary>Снимки state с закрытыми стадиями и кодами.</summary>
    public List<IReadOnlyDictionary<string, object?>> Events { get; } = [];
    /// <summary>Переданные logger исключения.</summary>
    public List<Exception?> Exceptions { get; } = [];
    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) => new Logger(this);
    /// <inheritdoc/>
    public void Dispose() { }

    /// <inheritdoc/>
    private class Logger(MaintenanceTestLogger owner) : ILogger
    {
        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => true;
        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            owner.Events.Add(((IEnumerable<KeyValuePair<string, object?>>)(object)state!).ToDictionary(pair => pair.Key, pair => pair.Value));
            owner.Exceptions.Add(exception);
        }
    }
}
