using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.Application;

/// <inheritdoc/>
public class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ToolRegistration> _registrations = new(StringComparer.Ordinal);
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Фиксирует список регистраций без создания бизнес-сервисов; duplicate exact names запрещены.</summary>
    public ToolRegistry(IEnumerable<ToolRegistration> registrations, IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
        foreach (ToolRegistration registration in registrations)
        {
            if (!_registrations.TryAdd(registration.Definition.Name, registration))
                throw new ArgumentException("Имена зарегистрированных инструментов повторяются.", nameof(registrations));
        }
        Definitions = Array.AsReadOnly(_registrations.Values.Select(registration => registration.Definition).ToArray());
    }

    /// <inheritdoc/>
    public IReadOnlyList<ModelToolDefinition> Definitions { get; }

    /// <inheritdoc/>
    public IToolHandlerScope? OpenScope(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _registrations.TryGetValue(name, out ToolRegistration? registration)
            ? new HandlerScope(_scopeFactory.CreateAsyncScope(), registration) : null;
    }

    private class HandlerScope(AsyncServiceScope scope, ToolRegistration registration) : IToolHandlerScope
    {
        // Разрешение ленивое: ошибки constructors также проходят awaited DisposeAsync executor.
        /// <inheritdoc/>
        public ModelToolDefinition Definition => registration.Definition;
        /// <inheritdoc/>
        public IToolInvocationValidator Validator => (IToolInvocationValidator)scope.ServiceProvider.GetRequiredService(registration.ValidatorType);
        /// <inheritdoc/>
        public IToolHandler Handler => (IToolHandler)scope.ServiceProvider.GetRequiredService(registration.HandlerType);
        /// <inheritdoc/>
        public ValueTask DisposeAsync() => scope.DisposeAsync();
    }
}
