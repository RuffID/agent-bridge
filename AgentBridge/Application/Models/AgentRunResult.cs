using AgentBridge.Application.Results;

namespace AgentBridge.Application.Models;

/// <summary>Итог public run; наличие отчёта не означает успешный ответ или сохранение.</summary>
public class AgentRunResult
{
    internal AgentRunResult(AgentRunStatus status, bool terminalSaved, DialogWriteToken? token,
        ModelSettingsSnapshot? settings, StoredDialogTurn? turn, ModelResponse? lastResponse,
        ServiceError? error, ToolExecutionBatch? lastTools = null)
    {
        Status = status;
        TerminalSaved = terminalSaved;
        Token = token;
        Settings = settings;
        Turn = turn;
        LastResponse = lastResponse;
        Error = error;
        LastTools = lastTools;
    }

    /// <summary>Авторитетный итог run; Completed требует terminal save.</summary>
    public AgentRunStatus Status { get; }
    /// <summary>Подтверждена ли именно terminal запись этого run.</summary>
    public bool TerminalSaved { get; }
    /// <summary>Только исходный либо последний подтверждённый token; не permission на запись.</summary>
    public DialogWriteToken? Token { get; }
    /// <summary>Зафиксированные безопасные настройки run, без ModelAccess.</summary>
    public ModelSettingsSnapshot? Settings { get; }
    /// <summary>Подтверждённое состояние обращения, включая partial output и журнал.</summary>
    public StoredDialogTurn? Turn { get; }
    /// <summary>Последний полученный model report, в том числе не принятый storage.</summary>
    public ModelResponse? LastResponse { get; }
    /// <summary>Безопасный semantic отказ, без raw exceptions/headers/body.</summary>
    public ServiceError? Error { get; }
    /// <summary>Последний полный tool report, включая подтверждённые outputs при отказе их сохранения.</summary>
    public ToolExecutionBatch? LastTools { get; }
}
