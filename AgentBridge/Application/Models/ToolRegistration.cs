using AgentBridge.Application.Ports;

namespace AgentBridge.Application.Models;

/// <summary>Программная связь immutable описания со scoped типами приложения.</summary>
public class ToolRegistration
{
    /// <summary>Проверяет совместимость типов; их создание выполняется только в scope вызова.</summary>
    public ToolRegistration(ModelToolDefinition definition, Type handlerType, Type validatorType)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(handlerType);
        ArgumentNullException.ThrowIfNull(validatorType);
        if (!typeof(IToolHandler).IsAssignableFrom(handlerType) || !typeof(IToolInvocationValidator).IsAssignableFrom(validatorType))
        {
            throw new ArgumentException("Регистрация требует обработчик и validator инструмента.");
        }
        Definition = new ModelToolDefinition(definition.Name, definition.Description, definition.Parameters, definition.Strict);
        HandlerType = handlerType;
        ValidatorType = validatorType;
    }

    /// <summary>Полное описание, передаваемое модели.</summary>
    public ModelToolDefinition Definition { get; }
    /// <summary>Тип обработчика приложения.</summary>
    public Type HandlerType { get; }
    /// <summary>Тип validator приложения.</summary>
    public Type ValidatorType { get; }
}
