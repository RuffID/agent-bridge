namespace AgentBridge.LiveAcceptance;

/// <summary>Безопасное состояние единственной приёмки; содержимое диалога и секреты сюда не записываются.</summary>
internal class AcceptanceEvidence
{
    public string Phase { get; set; } = "new";
    public Guid DialogId { get; set; }
    public Guid ProcessId { get; set; }
    public string ConfigurationHash { get; set; } = string.Empty;
    public string RuntimeHash { get; set; } = string.Empty;
    public int Generations { get; set; }
    public int Catalogs { get; set; }
    public int ProbeExecutions { get; set; }
    public int StreamUpdates { get; set; }
    public string FirstTurnHash { get; set; } = string.Empty;
    public string? FailureType { get; set; }
    public string? FailureCode { get; set; }
    public List<string> RequestHashes { get; set; } = [];
    public List<string> InputHashes { get; set; } = [];
    public List<int> HttpStatuses { get; set; } = [];
    public List<string> ResponseFormats { get; set; } = [];
    public Dictionary<string, bool> Checks { get; set; } = [];
    public List<int?> DeclaredInputWindows { get; set; } = [];
    public List<bool> RequiresServerValidation { get; set; } = [];
}
