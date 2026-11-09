namespace AgentBridge.Persistence.EfCore.Mapping;

/// <summary>Типы и ограничения PostgreSQL; SQL не исполняет.</summary>
internal class ProviderModelMapping
{
    /// <summary>Отклоняет контекст с неподдерживаемым провайдером до построения модели.</summary>
    public ProviderModelMapping(string providerName)
    {
        if (providerName != "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            throw new InvalidOperationException("Контекст AgentBridge требует явного PostgreSQL.");
        }
    }

    /// <summary>Текст без JSON-нормализации провайдером.</summary>
    public string TextType => "text";

    /// <summary>Ordinal collation текстового владельца.</summary>
    public string OwnerCollation => "C";

    /// <summary>Сохраняет доверенные PostgreSQL-выражения ограничений модели.</summary>
    public string Check(string expression)
    {
        return expression;
    }
}
