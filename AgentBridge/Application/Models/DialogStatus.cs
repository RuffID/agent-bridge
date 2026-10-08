using AgentBridge.Domain.Dialogs;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Models;

/// <summary>Безопасный read-only статус; размер относится к сохраняемому рабочему окну без transient providers/new input/tools.</summary>
public class DialogStatus(DialogWriteToken token, DateTimeOffset createdAtUtc, DateTimeOffset? expiresAtUtc, bool isExpired,
    long contentBytes, long softContentLimitBytes, long compactionCount, AgentSettingsSnapshot? settings,
    string? serverModel, ContextTokenCount? contextSize, ServiceError? contextError, DialogModelSelection? savedSelection = null)
{
    /// <summary>Идентичность диалога.</summary>
    public DialogId DialogId => Token.DialogId;
    /// <summary>Безопасный token истории того же read snapshot.</summary>
    public DialogWriteToken Token { get; } = token;
    /// <summary>Фиксированное создание.</summary>
    public DateTimeOffset CreatedAtUtc { get; } = createdAtUtc;
    /// <summary>Текущий срок либо null для бессрочного хранения.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; } = expiresAtUtc;
    /// <summary>Истечение на fresh UTC момента возврата.</summary>
    public bool IsExpired { get; } = isExpired;
    /// <summary>Объём сохраняемого содержимого в байтах, не физический размер БД.</summary>
    public long ContentBytes { get; } = contentBytes;
    /// <summary>Мягкий порог конфигурации.</summary>
    public long SoftContentLimitBytes { get; } = softContentLimitBytes;
    /// <summary>Достигнут мягкий порог; не запрет записи и не команда удаления.</summary>
    public bool SoftContentLimitReached => ContentBytes >= SoftContentLimitBytes;
    /// <summary>Число принятых версий compact.</summary>
    public long CompactionCount { get; } = compactionCount;
    /// <summary>Эффективный проверенный выбор; null при отказе каталога.</summary>
    public AgentSettingsSnapshot? Settings { get; } = settings;
    /// <summary>Сохранённый выбор доступен даже при отказе каталога; не является подтверждением доступности модели.</summary>
    public DialogModelSelection? SavedSelection { get; } = savedSelection;
    /// <summary>Эффективный проверенный либо сохранённый выбранный ID, отдельно от server model.</summary>
    public string? SelectedModel => Settings?.Model.Model.Id ?? SavedSelection?.Model;
    /// <summary>Эффективный проверенный либо сохранённый effort.</summary>
    public string? SelectedEffort => Settings?.Model.ReasoningEffort ?? SavedSelection?.Effort;
    /// <summary>Фактическая модель последнего report генерации; никогда не подменяется выбранным именем.</summary>
    public string? ServerModel { get; } = serverModel;
    /// <summary>Known/nullable estimate рабочего сохранённого input; не полный будущий запрос.</summary>
    public ContextTokenCount? ContextSize { get; } = contextSize;
    /// <summary>Явная причина недоступности контекста/выбора либо отсутствие отказа.</summary>
    public ServiceError? ContextError { get; } = contextError;
    /// <summary>Мягкий порог compact достигнут по доступной оценке; null при unknown.</summary>
    public bool? CompactionThresholdReached => ContextSize?.EstimatedInputTokens is long estimate && Settings is not null
        ? estimate >= Settings.Model.TokenThreshold : null;
    /// <summary>Можно готовить следующее обращение; полный budget с transient вкладами проверяет runner отдельно.</summary>
    public bool CanContinue => !IsExpired && ContextError is null;
}
