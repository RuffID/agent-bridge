using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentBridge.BinaryConsumer;

/// <summary>Проверяемые фрагменты public сценариев; в compile-check ни один метод не исполняется.</summary>
public static class UsageFlow
{
    /// <summary>Создаёт новый диалог в отдельном коротком scope с фиксированным сроком из options.</summary>
    /// <remarks>DialogId и авторизованного owner предоставляет приложение. Conflict не разрешает повтор/пересоздание.</remarks>
    public static async Task<ServiceResult<DialogWriteToken>> CreateAsync(IServiceProvider root,
        DialogId dialogId, DialogOwnerId authorizedOwner, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = root.CreateAsyncScope();
        DateTimeOffset created = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
        DialogRetentionOptions retention = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<DialogRetentionOptions>>().Value;
        return await scope.ServiceProvider.GetRequiredService<IDialogCreator>().CreateAsync(
            dialogId, authorizedOwner, created, retention.CalculateExpiresAtUtc(created), cancellationToken);
    }

    /// <summary>Читает актуальный каталог именно выбранного per-call ключа без вывода ключа в UI.</summary>
    public static async Task<ServiceResult<ModelCatalogSnapshot>> ReadModelsAsync(IServiceProvider root,
        DialogOwnerId authorizedOwner, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = root.CreateAsyncScope();
        ServiceResult<ModelAccess> access = await scope.ServiceProvider.GetRequiredService<IModelAccessResolver>()
            .ResolveAsync(authorizedOwner, cancellationToken);
        if (!access.Success) return ServiceResult<ModelCatalogSnapshot>.Fail(access.Error!);
        return await scope.ServiceProvider.GetRequiredService<IModelCatalog>().ReadAsync(access.Data!, cancellationToken);
    }

    /// <summary>Сохраняет exact model/effort с обеими версиями ранее прочитанного снимка без refresh/retry.</summary>
    public static async Task<ServiceResult<DialogModelSelection>> SelectAsync(IServiceProvider root, ApplicationCallContext call,
        AgentSettingsSnapshot displayedSettings, string model, string effort, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = root.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AgentSettingsService>().SelectAsync(call,
            displayedSettings.Token, displayedSettings.SelectionVersion, model, effort, cancellationToken);
    }

    /// <summary>Читает безопасные настройки для UI и последующего SelectAsync.</summary>
    public static async Task<ServiceResult<AgentSettingsSnapshot>> ReadSettingsAsync(IServiceProvider root,
        ApplicationCallContext call, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = root.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AgentSettingsService>().ReadAsync(call, cancellationToken);
    }

    /// <summary>Выполняет только новый turn с новым user input; null overrides используют saved выбор/defaults.</summary>
    /// <remarks>Приложение авторизует call до вызова. Callback null выбирает JSON, non-null — SSE.
    /// Updates предварительны; callback соблюдает отмену, фильтрует видимый текст и не логирует raw Item.</remarks>
    public static async Task<AgentRunResult> RunAsync(IServiceProvider root, ApplicationCallContext call, string text,
        Func<ModelStreamUpdate, CancellationToken, ValueTask>? onUpdate, CancellationToken cancellationToken,
        string? modelOverride = null, string? effortOverride = null)
    {
        await using AsyncServiceScope scope = root.CreateAsyncScope();
        CanonicalModelItem input = new(JsonSerializer.SerializeToElement(new
        {
            type = "message", role = "user", content = new[] { new { type = "input_text", text } }
        }));
        AgentRunRequest request = new(call, [input], [AccountSummaryTool.ToolDefinition.Name],
            new ToolExecutionLimits(8, 16, 2, TimeSpan.FromMinutes(1)), model: modelOverride, effort: effortOverride);
        AgentRunResult result = await scope.ServiceProvider.GetRequiredService<AgentRunner>()
            .RunAsync(request, onUpdate, cancellationToken);
        return result; // UI проверяет Status и TerminalSaved; LastResponse не является подтверждением сохранения.
    }

    /// <summary>Читает expiry/bytes/model/context без раскрытия ключей и исходных reports.</summary>
    public static async Task<ServiceResult<DialogStatus>> ReadStatusAsync(IServiceProvider root,
        ApplicationCallContext call, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = root.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AgentSettingsService>().GetStatusAsync(call, cancellationToken);
    }

    /// <summary>Обрабатывает один явный пакет; приложение авторизует системную операцию и планирует следующий вызов.</summary>
    /// <remarks>Неожиданное исключение распространяется. Для обработки LastResult приложение делает прямой вызов в своём caller scope.
    /// Partial/Canceled/Interrupted и candidate Unknown не объявляются успешной очисткой.</remarks>
    public static async Task<ExpiredDialogCleanupResult> CleanupAsync(IServiceProvider root, int limit,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = root.CreateAsyncScope();
        ExpiredDialogCleanup cleanup = scope.ServiceProvider.GetRequiredService<ExpiredDialogCleanup>();
        return await cleanup.CleanupAsync(limit, cancellationToken);
    }
}
