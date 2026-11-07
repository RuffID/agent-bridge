using System.Security.Cryptography;
using System.Text.Json;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Models;

/// <summary>Локальное происхождение контекста, отдельное от canonical данных и transport continuation.</summary>
/// <remarks>Отпечаток защищает от случайного смешивания trusted данных, не является авторизацией или upstream identity.</remarks>
public class ModelConnectorProvenance
{
    /// <summary>Фиксирует коннектор и отпечаток локального владения без сохранения секретов.</summary>
    public ModelConnectorProvenance(string connectorId, string binding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding);
        if (binding.Length != 64 || !binding.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Некорректный отпечаток происхождения.", nameof(binding));
        }

        ConnectorId = connectorId;
        Binding = binding;
    }

    /// <summary>Идентификатор технологии коннектора, не имя модели.</summary>
    public string ConnectorId { get; }
    /// <summary>Чувствительный локальный отпечаток; не предназначен для логов или upstream.</summary>
    public string Binding { get; }

    /// <summary>Связывает диалог, владельца, агента, endpoint и доступ; TurnId не ограничивает следующий turn.</summary>
    public static ModelConnectorProvenance Create(string connectorId, ApplicationCallContext call, ModelAccess access, string endpoint)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new[] { connectorId, call.DialogId.Value.ToString("D"),
            call.OwnerId.Value, call.AgentId, endpoint, access.RevealApiKey() });
        try
        {
            return new ModelConnectorProvenance(connectorId, Convert.ToHexString(SHA256.HashData(bytes)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>Проверяет отдельный источник; одинаковая модель не заменяет проверку происхождения.</summary>
    public ServiceError? ValidateSource(ModelConnectorProvenance? source)
    {
        if (source is null || source.ConnectorId != ConnectorId)
        {
            return new ServiceError(ServiceErrorType.Unsupported, "Происхождение непрозрачного контекста не подтверждено для коннектора.");
        }

        if (!string.Equals(source.Binding, Binding, StringComparison.Ordinal))
        {
            return new ServiceError(ServiceErrorType.Conflict, "Контекст принадлежит другому диалогу, endpoint или доступу.");
        }

        return null;
    }
}
