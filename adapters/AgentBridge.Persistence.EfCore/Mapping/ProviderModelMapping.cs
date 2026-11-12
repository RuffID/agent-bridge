using System.Text.RegularExpressions;

namespace AgentBridge.Persistence.EfCore.Mapping;

/// <summary>Типы и ограничения PostgreSQL и SQL Server; SQL не исполняет.</summary>
internal class ProviderModelMapping
{
    /// <summary>Отклоняет контекст с неподдерживаемым провайдером до построения модели.</summary>
    public ProviderModelMapping(string providerName)
    {
        IsSqlServer = providerName == "Microsoft.EntityFrameworkCore.SqlServer";
        if (!IsSqlServer && providerName != "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            throw new InvalidOperationException("Контекст AgentBridge требует явного PostgreSQL или SQL Server.");
        }
    }

    /// <summary>Признак SQL Server с бинарным owner token и собственным синтаксисом constraints.</summary>
    public bool IsSqlServer { get; }

    /// <summary>Текст без JSON-нормализации провайдером.</summary>
    public string TextType
    {
        get
        {
            if (IsSqlServer)
            {
                return "nvarchar(max)";
            }

            return "text";
        }
    }

    /// <summary>Ordinal collation текстовых ключей каталога; SQL Server owner хранится бинарно.</summary>
    public string OwnerCollation
    {
        get
        {
            if (IsSqlServer)
            {
                return "Latin1_General_100_BIN2";
            }

            return "C";
        }
    }

    /// <summary>Преобразует доверенные constraints для SQL Server с учётом завершающих пробелов.</summary>
    public string Check(string expression)
    {
        if (IsSqlServer)
        {
            return Regex.Replace(expression.Replace("length(", "DATALENGTH(", StringComparison.Ordinal), "\"([A-Za-z]+)\"", "[$1]");
        }

        return expression;
    }
}
