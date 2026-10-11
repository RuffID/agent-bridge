using AgentBridge.Domain.Dialogs;

namespace AgentBridge.Application.Models;

/// <summary>Компактные authoritative metadata, без истории/модельных отчётов; профиль не предназначен для UI.</summary>
/// <param name="Token">Root CAS.</param>
/// <param name="Scope">Immutable namespace.</param>
/// <param name="Profile">Полный immutable профиль.</param>
/// <param name="Policy">Immutable policy.</param>
/// <param name="Text">Разрешённый saved текст.</param>
/// <param name="CreatedAtUtc">Создание.</param>
/// <param name="LastSavedMessageAtUtc">Последнее saved user/assistant сообщение.</param>
/// <param name="ExpiresAtUtc">Фиксированный срок.</param>
/// <param name="Deleted">Durable tombstone.</param>
/// <param name="Readiness">Разрешение продолжения.</param>
/// <param name="ProjectionRevision">Версия projection/change.</param>
/// <param name="RecoveryRevision">Версия append-only recovery.</param>
/// <param name="FencingEpoch">Epoch.</param>
/// <param name="FirstQuestionPosition">Saved turnSequence/itemPosition первого вопроса.</param>
/// <param name="LastMessagePosition">Saved turnSequence/itemPosition последнего сообщения.</param>
/// <param name="LastTurnId">Последний turn.</param>
/// <param name="LastTurnStatus">Original terminal либо InProgress после fence.</param>
public record DialogCatalogState(DialogWriteToken Token, DialogCatalogScope Scope, CatalogAccessProfile Profile,
    DialogProjectionPolicy Policy, DialogCatalogTextState Text, DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastSavedMessageAtUtc, DateTimeOffset? ExpiresAtUtc, bool Deleted,
    DialogReadiness Readiness, long ProjectionRevision, long RecoveryRevision, long FencingEpoch,
    string? FirstQuestionPosition = null, string? LastMessagePosition = null, Guid? LastTurnId = null, DialogTurnStatus? LastTurnStatus = null)
{
    /// <summary>SortTime descending; при равенстве применяется UUID big-endian tie-break.</summary>
    public DateTimeOffset SortTimeUtc => LastSavedMessageAtUtc ?? CreatedAtUtc;
}
