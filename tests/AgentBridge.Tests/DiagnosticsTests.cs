using System.Diagnostics;
using AgentBridge.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace AgentBridge.Tests;

/// <summary>Проверки публичной диагностики через DI и принадлежащие приложению провайдеры.</summary>
public class DiagnosticsTests
{
    /// <summary>Повторная регистрация сохраняет фабрику приложения и не дублирует события.</summary>
    [Fact]
    public void RegistrationPreservesApplicationFactoryAndProviders()
    {
        CollectingProvider sink = new();
        using ILoggerFactory applicationFactory = LoggerFactory.Create(logging => logging.AddProvider(sink));
        ServiceCollection services = new();
        services.AddSingleton(applicationFactory);
        services.AddAgentBridgeDiagnostics();
        services.AddAgentBridgeDiagnostics();
        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.Same(applicationFactory, provider.GetRequiredService<ILoggerFactory>());
        Assert.Single(provider.GetServices<AgentBridgeDiagnostics>());
        provider.GetRequiredService<AgentBridgeDiagnostics>().BeginOperation(AgentBridgeOperation.ConfigurationValidation, Guid.NewGuid(), callerCancellation: TestContext.Current.CancellationToken).Complete();
        LogEntry entry = Assert.Single(sink.Entries);
        Assert.Equal(typeof(AgentBridgeDiagnostics).FullName, entry.Category);
        Assert.Equal(LogLevel.Information, entry.Level);
    }

    /// <summary>Провайдер и фильтр приложения работают независимо от порядка регистрации.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RegistrationPreservesApplicationFilter(bool applicationFirst)
    {
        CollectingProvider sink = new();
        ServiceCollection services = new();
        if (applicationFirst)
        {
            services.AddLogging(logging => logging.AddProvider(sink).SetMinimumLevel(LogLevel.Warning));
        }

        services.AddAgentBridgeDiagnostics();
        if (!applicationFirst)
        {
            services.AddLogging(logging => logging.AddProvider(sink).SetMinimumLevel(LogLevel.Warning));
        }

        using ServiceProvider provider = services.BuildServiceProvider();
        AgentBridgeDiagnostics diagnostics = provider.GetRequiredService<AgentBridgeDiagnostics>();
        diagnostics.BeginOperation(AgentBridgeOperation.ConfigurationValidation, Guid.NewGuid(), callerCancellation: TestContext.Current.CancellationToken).Complete();
        diagnostics.BeginOperation(AgentBridgeOperation.ConfigurationValidation, Guid.NewGuid(), callerCancellation: TestContext.Current.CancellationToken).Fail(new Exception("synthetic-secret"));
        Assert.Equal(LogLevel.Error, Assert.Single(sink.Entries).Level);
    }

    /// <summary>Без провайдера DI доступен, но библиотека не создаёт вывод самостоятельно.</summary>
    [Fact]
    public void RegistrationDoesNotInstallProviders()
    {
        ServiceCollection services = new();
        services.AddAgentBridgeDiagnostics();
        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        Assert.Empty(provider.GetServices<ILoggerProvider>());
        provider.GetRequiredService<AgentBridgeDiagnostics>().BeginOperation(AgentBridgeOperation.ConfigurationValidation, Guid.NewGuid(), callerCancellation: TestContext.Current.CancellationToken).Complete();
    }

    /// <summary>Длительность измерена, корреляция общая, идентификатор каждой операции отдельный.</summary>
    [Fact]
    public void CompletionContainsOnlyStructuredMetadataAndMeasuredDuration()
    {
        CollectingProvider sink = new();
        using ServiceProvider provider = CreateProvider(sink);
        AgentBridgeDiagnostics diagnostics = provider.GetRequiredService<AgentBridgeDiagnostics>();
        Guid correlationId = Guid.NewGuid();
        Stopwatch elapsed = Stopwatch.StartNew();
        diagnostics.BeginOperation(AgentBridgeOperation.ConfigurationValidation, correlationId, callerCancellation: TestContext.Current.CancellationToken).Complete();
        diagnostics.BeginOperation(AgentBridgeOperation.ConfigurationValidation, correlationId, callerCancellation: TestContext.Current.CancellationToken).Complete();
        elapsed.Stop();

        Assert.Equal(2, sink.Entries.Count);
        foreach (LogEntry entry in sink.Entries)
        {
            Assert.Equal(new EventId(4100, "AgentBridgeOperationCompleted"), entry.EventId);
            Assert.Null(entry.Exception);
            Assert.Equal("ConfigurationValidation", entry.State["Operation"]);
            Assert.Equal(correlationId, entry.State["CorrelationId"]);
            Assert.NotEqual(Guid.Empty, Assert.IsType<Guid>(entry.State["OperationId"]));
            Assert.Equal("Succeeded", entry.State["Status"]);
            Assert.Equal("None", entry.State["ErrorCode"]);
            Assert.InRange(Assert.IsType<double>(entry.State["DurationMs"]), 0, elapsed.Elapsed.TotalMilliseconds);
            Assert.Equal(new[] { "CorrelationId", "DurationMs", "ErrorCode", "Operation", "OperationId", "Status", "{OriginalFormat}" }, entry.State.Keys.Order(StringComparer.Ordinal));
        }

        Assert.NotEqual(sink.Entries[0].State["OperationId"], sink.Entries[1].State["OperationId"]);
    }

    /// <summary>Причина отмены определяется раздельными исходными токенами, caller имеет приоритет.</summary>
    [Theory]
    [InlineData(true, false, "Canceled", "CallerCanceled", LogLevel.Information)]
    [InlineData(false, true, "DeadlineExceeded", "DeadlineExceeded", LogLevel.Warning)]
    [InlineData(true, true, "Canceled", "CallerCanceled", LogLevel.Information)]
    [InlineData(false, false, "Failed", "UnattributedCancellation", LogLevel.Error)]
    public void CancellationUsesExplicitSources(bool cancelCaller, bool cancelDeadline, string status, string code, LogLevel level)
    {
        CollectingProvider sink = new();
        using ServiceProvider provider = CreateProvider(sink);
        using CancellationTokenSource caller = new();
        using CancellationTokenSource deadline = new();
        AgentBridgeDiagnosticOperation operation = provider.GetRequiredService<AgentBridgeDiagnostics>()
            .BeginOperation(AgentBridgeOperation.ConfigurationValidation, Guid.NewGuid(), caller.Token, deadline.Token);
        if (cancelCaller)
        {
            caller.Cancel();
        }

        if (cancelDeadline)
        {
            deadline.Cancel();
        }

        operation.Fail(new OperationCanceledException("synthetic-secret"));
        LogEntry entry = Assert.Single(sink.Entries);
        Assert.Equal(status, entry.State["Status"]);
        Assert.Equal(code, entry.State["ErrorCode"]);
        Assert.Equal(level, entry.Level);
        Assert.Null(entry.Exception);
        Assert.DoesNotContain("synthetic-secret", entry.Message);
    }

    /// <summary>Наличие токена в исключении не заменяет явные источники причины отмены.</summary>
    [Fact]
    public void UnattributedCancellationIsNotAssumedToBeTimeout()
    {
        CollectingProvider sink = new();
        using ServiceProvider provider = CreateProvider(sink);
        using CancellationTokenSource unrelated = new();
        unrelated.Cancel();
        provider.GetRequiredService<AgentBridgeDiagnostics>().BeginOperation(AgentBridgeOperation.ConfigurationValidation, Guid.NewGuid(), callerCancellation: TestContext.Current.CancellationToken)
            .Fail(new OperationCanceledException("synthetic-secret", unrelated.Token));
        LogEntry entry = Assert.Single(sink.Entries);
        Assert.Equal("Failed", entry.State["Status"]);
        Assert.Equal("UnattributedCancellation", entry.State["ErrorCode"]);
    }

    /// <summary>Обычная ошибка не превращается в отмену или deadline только из-за сработавших токенов.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailureIsNotReclassifiedByTokensOrExceptionName(bool timeoutException)
    {
        CollectingProvider sink = new();
        using ServiceProvider provider = CreateProvider(sink);
        using CancellationTokenSource caller = new();
        using CancellationTokenSource deadline = new();
        caller.Cancel();
        deadline.Cancel();
        Exception error = timeoutException ? new TimeoutException("synthetic-secret") : new InvalidOperationException("synthetic-secret");
        provider.GetRequiredService<AgentBridgeDiagnostics>().BeginOperation(AgentBridgeOperation.ConfigurationValidation, Guid.NewGuid(), caller.Token, deadline.Token).Fail(error);
        LogEntry entry = Assert.Single(sink.Entries);
        Assert.Equal("Failed", entry.State["Status"]);
        Assert.Equal("UnexpectedFailure", entry.State["ErrorCode"]);
        Assert.Equal(LogLevel.Error, entry.Level);
    }

    /// <summary>Подтверждённый успех не переименовывается из-за поздней отмены.</summary>
    [Fact]
    public void SuccessfulResultIsNotChangedByCancellation()
    {
        CollectingProvider sink = new();
        using ServiceProvider provider = CreateProvider(sink);
        using CancellationTokenSource caller = new();
        AgentBridgeDiagnosticOperation operation = provider.GetRequiredService<AgentBridgeDiagnostics>()
            .BeginOperation(AgentBridgeOperation.ConfigurationValidation, Guid.NewGuid(), caller.Token);
        caller.Cancel();
        operation.Complete();
        Assert.Equal("Succeeded", Assert.Single(sink.Entries).State["Status"]);
    }

    /// <summary>Исключение с секретами и небезопасными объектами никогда не передаётся провайдеру.</summary>
    [Fact]
    public void FailureDoesNotReadOrForwardExceptionContent()
    {
        CollectingProvider sink = new();
        using ServiceProvider provider = CreateProvider(sink);
        Exception error = new UnreadableException();
        error.Data["Url"] = "https://synthetic.invalid/path?api_key=synthetic-api-key";
        error.Data["Configuration"] = new SensitiveObject();
        AgentBridgeDiagnosticOperation operation = provider.GetRequiredService<AgentBridgeDiagnostics>()
            .BeginOperation(AgentBridgeOperation.ConfigurationValidation, Guid.NewGuid(), callerCancellation: TestContext.Current.CancellationToken);

        Action observeAndRethrow = () =>
        {
            try
            {
                throw error;
            }
            catch (Exception caught)
            {
                operation.Fail(caught);
                throw;
            }
        };
        Exception? rethrown = null;
        try
        {
            observeAndRethrow();
        }
        catch (Exception caught)
        {
            rethrown = caught;
        }

        Assert.Same(error, rethrown);
        AssertSafe(Assert.Single(sink.Entries));
    }

    /// <summary>Неполное наблюдение не выдаёт фиктивный успешный результат.</summary>
    [Fact]
    public void BeginningAloneDoesNotClaimCompletion()
    {
        CollectingProvider sink = new();
        using ServiceProvider provider = CreateProvider(sink);
        _ = provider.GetRequiredService<AgentBridgeDiagnostics>().BeginOperation(AgentBridgeOperation.ConfigurationValidation, Guid.NewGuid(), callerCancellation: TestContext.Current.CancellationToken);
        Assert.Empty(sink.Entries);
    }

    /// <summary>Повторное завершение отклоняется, null-ошибка не потребляет наблюдение.</summary>
    [Fact]
    public void CompletionIsExplicitAndCannotBeDuplicated()
    {
        CollectingProvider sink = new();
        using ServiceProvider provider = CreateProvider(sink);
        AgentBridgeDiagnosticOperation operation = provider.GetRequiredService<AgentBridgeDiagnostics>()
            .BeginOperation(AgentBridgeOperation.ConfigurationValidation, Guid.NewGuid(), callerCancellation: TestContext.Current.CancellationToken);
        Assert.Throws<ArgumentNullException>(() => operation.Fail(null!));
        operation.Complete();
        Assert.Throws<InvalidOperationException>(() => operation.Complete());
        Assert.Throws<InvalidOperationException>(() => operation.Fail(new Exception("synthetic-secret")));
        Assert.Single(sink.Entries);
    }

    /// <summary>Некорректные метаданные и один токен для двух причин отклоняются до записи события.</summary>
    [Fact]
    public void UnsafeOrAmbiguousMetadataFailsFast()
    {
        CollectingProvider sink = new();
        using ServiceProvider provider = CreateProvider(sink);
        AgentBridgeDiagnostics diagnostics = provider.GetRequiredService<AgentBridgeDiagnostics>();
        using CancellationTokenSource source = new();
        Assert.Throws<ArgumentOutOfRangeException>(() => diagnostics.BeginOperation((AgentBridgeOperation)int.MaxValue, Guid.NewGuid(), callerCancellation: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => diagnostics.BeginOperation(AgentBridgeOperation.ConfigurationValidation, Guid.Empty, callerCancellation: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => diagnostics.BeginOperation(AgentBridgeOperation.ConfigurationValidation, Guid.NewGuid(), source.Token, source.Token));
        Assert.Empty(sink.Entries);
    }

    /// <summary>Реальный Serilog provider приложения сохраняет структуру, global logger и владение sink.</summary>
    [Fact]
    public void ApplicationOwnedSerilogReceivesSafeEventsWithoutGlobalReplacement()
    {
        MemorySerilogSink sink = new();
        using Logger applicationLogger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
        Serilog.ILogger globalLogger = Log.Logger;
        ServiceCollection services = new();
        services.AddLogging(logging => logging.AddSerilog(applicationLogger, dispose: false));
        services.AddAgentBridgeDiagnostics();
        using (ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }))
        {
            AgentBridgeDiagnostics diagnostics = provider.GetRequiredService<AgentBridgeDiagnostics>();
            diagnostics.BeginOperation(AgentBridgeOperation.ConfigurationValidation, Guid.NewGuid(), callerCancellation: TestContext.Current.CancellationToken).Fail(new UnreadableException());
            using CancellationTokenSource caller = new();
            caller.Cancel();
            diagnostics.BeginOperation(AgentBridgeOperation.ConfigurationValidation, Guid.NewGuid(), caller.Token)
                .Fail(new OperationCanceledException("synthetic-api-key synthetic-connection synthetic-dialog"));
        }

        Assert.Same(globalLogger, Log.Logger);
        Assert.False(sink.Disposed);
        Assert.Equal(2, sink.Events.Count);
        Assert.Equal(LogEventLevel.Error, sink.Events[0].Level);
        Assert.Equal(LogEventLevel.Information, sink.Events[1].Level);
        Assert.Equal("Canceled", Assert.IsType<ScalarValue>(sink.Events[1].Properties["Status"]).Value);
        foreach (LogEvent entry in sink.Events)
        {
            Assert.Null(entry.Exception);
            Assert.Equal(typeof(AgentBridgeDiagnostics).FullName, Assert.IsType<ScalarValue>(entry.Properties["SourceContext"]).Value);
            Assert.IsType<double>(Assert.IsType<ScalarValue>(entry.Properties["DurationMs"]).Value);
            Assert.DoesNotContain("synthetic-", entry.RenderMessage());
            Assert.DoesNotContain("synthetic-", string.Join(" ", entry.Properties.Select(pair => pair.Value.ToString())));
        }

        applicationLogger.Information("Owner event after provider disposal");
        Assert.Equal(3, sink.Events.Count);
    }

    /// <summary>Создаёт обычный DI-контейнер с собирающим провайдером без host.</summary>
    private static ServiceProvider CreateProvider(CollectingProvider sink)
    {
        ServiceCollection services = new();
        services.AddLogging(logging => logging.AddProvider(sink));
        services.AddAgentBridgeDiagnostics();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    /// <summary>Проверяет отсутствие синтетического секретного содержимого во всех частях события.</summary>
    private static void AssertSafe(LogEntry entry)
    {
        Assert.Null(entry.Exception);
        Assert.DoesNotContain("synthetic-", entry.Message);
        foreach (object? value in entry.State.Values)
        {
            Assert.DoesNotContain("synthetic-", value?.ToString() ?? string.Empty);
            Assert.False(value is Exception or SensitiveObject);
        }
    }

    /// <summary>Сохранённое событие Microsoft.Extensions.Logging.</summary>
    private record LogEntry(string Category, LogLevel Level, EventId EventId, Dictionary<string, object?> State, Exception? Exception, string Message);

    /// <inheritdoc cref="ILoggerProvider"/>
    /// <remarks>Провайдер приложения, собирающий события в памяти.</remarks>
    private class CollectingProvider : ILoggerProvider
    {
        /// <summary>Принятые события.</summary>
        public List<LogEntry> Entries { get; } = [];

        /// <inheritdoc/>
        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new CollectingLogger(categoryName, Entries);

        /// <inheritdoc/>
        public void Dispose() { }
    }

    /// <inheritdoc cref="Microsoft.Extensions.Logging.ILogger"/>
    /// <remarks>Сохраняет state, исключение и отформатированный текст.</remarks>
    private class CollectingLogger(string category, List<LogEntry> entries) : Microsoft.Extensions.Logging.ILogger
    {
        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Dictionary<string, object?> properties = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(state).ToDictionary(pair => pair.Key, pair => pair.Value);
            entries.Add(new LogEntry(category, logLevel, eventId, properties, exception, formatter(state, exception)));
        }
    }

    /// <summary>Исключение, запрещающее чтение сообщения и преобразование в строку.</summary>
    private class UnreadableException : Exception
    {
        /// <summary>Содержит синтетические ключ, подключение и текст только внутри исключения.</summary>
        public UnreadableException() : base("synthetic-api-key synthetic-connection synthetic-dialog", new Exception("synthetic-inner")) { }

        /// <inheritdoc/>
        public override string Message => throw new InvalidOperationException("Message must not be read");

        /// <inheritdoc/>
        public override string ToString() => throw new InvalidOperationException("ToString must not be called");
    }

    /// <summary>Синтетический объект конфигурации, который нельзя форматировать.</summary>
    private class SensitiveObject
    {
        /// <inheritdoc/>
        public override string ToString() => throw new InvalidOperationException("Configuration must not be formatted");
    }

    /// <inheritdoc cref="ILogEventSink"/>
    /// <remarks>Принадлежащий приложению Serilog sink без файлов и внешних вызовов; реализует IDisposable для проверки владения.</remarks>
    private class MemorySerilogSink : ILogEventSink, IDisposable
    {
        /// <summary>События Serilog.</summary>
        public List<LogEvent> Events { get; } = [];

        /// <summary>Признак освобождения владельцем.</summary>
        public bool Disposed { get; private set; }

        /// <inheritdoc/>
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);

        /// <inheritdoc/>
        public void Dispose() => Disposed = true;
    }
}
