using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AgentBridge.Hosting.Tests;

/// <summary>Borrowed app logger factory сохраняет настоящие host exceptions для проверки наблюдаемости.</summary>
internal class HostingLogger : ILoggerFactory
{
    internal readonly ConcurrentQueue<Exception> Errors = new();
    internal readonly TaskCompletionSource ErrorLogged = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal bool Disposed;

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName)
    {
        if (Disposed) throw new ObjectDisposedException(nameof(HostingLogger));
        return new Logger(this);
    }

    /// <inheritdoc/>
    public void AddProvider(ILoggerProvider provider) => throw new InvalidOperationException("Providers принадлежат приложению.");

    /// <inheritdoc/>
    public void Dispose() => Disposed = true;

    /// <inheritdoc/>
    private class Logger(HostingLogger owner) : ILogger
    {
        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => true;
        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (owner.Disposed) throw new ObjectDisposedException(nameof(HostingLogger));
            if (exception is not null)
            {
                owner.Errors.Enqueue(exception);
                owner.ErrorLogged.TrySetResult();
            }
        }
    }
}
