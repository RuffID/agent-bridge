namespace AgentBridge.Application.Models;

/// <summary>Один atomic Begin/input/profile gate/lease acquire.</summary>
public class DialogRunBegin
{
    /// <summary>Фиксирует новый turn, immutable input/settings и положительный lease period.</summary>
    public DialogRunBegin(DialogAccess access, DialogWriteToken expected, Guid turnId,
        IEnumerable<CanonicalModelItem> input, TurnModelSettings settings, TimeSpan leasePeriod,
        DialogCatalogScope scope, CatalogAccessProfile profile)
    {
        Access = access;
        Expected = expected;
        TurnId = turnId;
        Input = ContractSnapshot.Copy(input);
        Settings = settings;
        LeasePeriod = leasePeriod;
        Scope = scope;
        Profile = profile;
    }

    /// <summary>Access.</summary>
    public DialogAccess Access { get; }
    /// <summary>Исходный CAS.</summary>
    public DialogWriteToken Expected { get; }
    /// <summary>Новый явный turn.</summary>
    public Guid TurnId { get; }
    /// <summary>Новый input.</summary>
    public IReadOnlyList<CanonicalModelItem> Input { get; }
    /// <summary>Snapshot настроек.</summary>
    public TurnModelSettings Settings { get; }
    /// <summary>Явный lease period.</summary>
    public TimeSpan LeasePeriod { get; }
    /// <summary>Полный namespace gate.</summary>
    public DialogCatalogScope Scope { get; }
    /// <summary>Полный profile gate.</summary>
    public CatalogAccessProfile Profile { get; }
}
