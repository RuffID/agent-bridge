using Microsoft.Extensions.Logging;

namespace AgentBridge.RuntimeProbe;

/// <summary>App-owned logger в памяти, без файлов и скрытых sinks.</summary>
public class ProbeLoggerProvider : ILoggerProvider
{
    /// <summary>Количество маркеров, переданных приложением.</summary>
    public int Messages { get; private set; }

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) => new ProbeLogger(this);

    /// <inheritdoc/>
    public void Dispose() { }

    /// <summary>Принимает только структурный marker без сохранения содержимого сообщений.</summary>
    private class ProbeLogger(ProbeLoggerProvider provider) : ILogger
    {
        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => true;
        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter(state, exception) == "Runtime probe marker") provider.Messages++;
        }
    }
}
