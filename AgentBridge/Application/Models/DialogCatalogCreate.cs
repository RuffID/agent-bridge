using AgentBridge.Domain.Dialogs;

namespace AgentBridge.Application.Models;

/// <summary>Явная регистрация нового root и immutable scope/profile/policy одной transaction.</summary>
/// <param name="DialogId">Новый ID; tombstone запрещает повторное использование.</param>
/// <param name="Scope">Namespace.</param>
/// <param name="CreatedAtUtc">UTC создания.</param>
/// <param name="ExpiresAtUtc">Фиксированный nullable срок.</param>
/// <param name="Profile">Полный профиль.</param>
/// <param name="Policy">Зарегистрированная policy.</param>
public record DialogCatalogCreate(DialogId DialogId, DialogCatalogScope Scope, DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc, CatalogAccessProfile Profile, DialogProjectionPolicy Policy);
