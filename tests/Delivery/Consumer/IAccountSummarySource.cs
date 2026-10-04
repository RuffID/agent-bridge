using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.BinaryConsumer;

/// <summary>Иллюстративный порт приложения: чтение разрешённой сводки без ключей, PII и служебных деталей.</summary>
/// <remarks>Это не API AgentBridge и не готовая реализация. Приложение реализует проверку owner/agent
/// и повторную авторизацию внутри чтения. Прикладной доступ к данным остаётся в его собственном scope/UoW.</remarks>
public interface IAccountSummarySource
{
    /// <summary>Проверяет актуальные права без побочных действий; возвращает безопасный semantic отказ.</summary>
    Task<ServiceResult> AuthorizeAsync(ApplicationCallContext call, CancellationToken cancellationToken);

    /// <summary>Повторно проверяет права и возвращает только разрешённую сводку; неоднозначный исход не выдаётся за успех.</summary>
    Task<ServiceResult<ToolOutput>> ReadAuthorizedAsync(ApplicationCallContext call, CancellationToken cancellationToken);
}
