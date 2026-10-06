using System.Text.RegularExpressions;

namespace AgentBridge.Persistence.EfCore.Mapping;

/// <summary>Выбирает типы и синтаксис собственных статических constraints; SQL не исполняет.</summary>
internal class ProviderModelMapping(string providerName)
{
    /// <summary>Признак Microsoft SQL Server для бинарного owner token.</summary>
    public bool IsSqlServer { get; } = providerName == "Microsoft.EntityFrameworkCore.SqlServer";

    /// <summary>Неограниченный Unicode текст без provider JSON-нормализации.</summary>
    public string TextType => IsSqlServer ? "nvarchar(max)" : "text";

    /// <summary>Ordinal collation существующих текстовых owner; SQL Server хранит owner бинарно.</summary>
    public string? OwnerCollation { get; } = providerName switch
    {
        "Microsoft.EntityFrameworkCore.Sqlite" => "BINARY",
        "Npgsql.EntityFrameworkCore.PostgreSQL" => "C",
        "Microsoft.EntityFrameworkCore.SqlServer" => null,
        _ => throw new InvalidOperationException("Контекст AgentBridge требует явного SQLite/PostgreSQL/SQL Server.")
    };

    /// <summary>Преобразует только доверенные выражения модели: bracket identifiers и DATALENGTH учитывают завершающие пробелы.</summary>
    public string Check(string expression) => IsSqlServer
        ? Regex.Replace(expression.Replace("length(", "DATALENGTH(", StringComparison.Ordinal), "\"([A-Za-z]+)\"", "[$1]")
        : expression;
}
