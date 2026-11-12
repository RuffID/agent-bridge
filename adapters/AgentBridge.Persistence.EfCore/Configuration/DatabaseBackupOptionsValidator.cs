using Microsoft.Extensions.Options;

namespace AgentBridge.Persistence.EfCore.Configuration;

/// <inheritdoc/>
/// <remarks>Проверяет форму настроек выбранного provider без файловой системы, подключения или запуска утилит.</remarks>
public class DatabaseBackupOptionsValidator(IOptions<DatabaseOptions> database) : IValidateOptions<DatabaseBackupOptions>
{
    /// <summary>Сохраняет прежний самостоятельный PostgreSQL validator; DI использует явные DatabaseOptions.</summary>
    public DatabaseBackupOptionsValidator()
        : this(Options.Create(new DatabaseOptions { Provider = DatabaseProvider.PostgreSql }))
    {
    }

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, DatabaseBackupOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        List<string> errors = [];
        if (database.Value.Provider == DatabaseProvider.SqlServer)
        {
            string? path = options.SqlServerBackupDirectory;
            if (!IsServerAbsolutePath(path))
            {
                errors.Add("Backup.SqlServerBackupDirectory должен быть абсолютным путём на сервере SQL Server.");
            }
        }
        else if (!IsAbsolutePath(options.BackupDirectory))
        {
            errors.Add("Backup.BackupDirectory должен быть корректным абсолютным путём.");
        }

        if (options.BackupRetentionPeriod is not { } retention || retention <= TimeSpan.Zero)
        {
            errors.Add("Backup.BackupRetentionPeriod обязателен и должен быть положительным; default отсутствует, retention выполняет приложение.");
        }

        if (database.Value.Provider == DatabaseProvider.PostgreSql)
        {
            if (!IsAbsolutePath(options.PostgreSqlDumpExecutablePath))
            {
                errors.Add("Backup.PostgreSqlDumpExecutablePath должен быть корректным абсолютным путём.");
            }

            if (options.PostgreSqlServerMajorVersion is not >= 10)
            {
                errors.Add("Backup.PostgreSqlServerMajorVersion обязателен и должен быть не меньше 10.");
            }

            if (options.PostgreSqlCleanupTimeout is not { } cleanup || cleanup <= TimeSpan.Zero
                || cleanup.TotalMilliseconds > uint.MaxValue - 1)
            {
                errors.Add("Backup.PostgreSqlCleanupTimeout обязателен и должен быть положительным в диапазоне таймера .NET.");
            }
        }

        if (errors.Count > 0)
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }

    /// <summary>Распознаёт серверные Unix, Windows и UNC пути независимо от ОС приложения.</summary>
    private static bool IsServerAbsolutePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Any(char.IsControl))
        {
            return false;
        }

        if (path.StartsWith('/') || path.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return true;
        }

        return path.Length > 2 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '\\';
    }

    /// <summary>Проверяет форму пути без файловой системы и без включения значения в ошибку.</summary>
    private static bool IsAbsolutePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Any(char.IsControl)
            || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || !Path.IsPathFullyQualified(path))
        {
            return false;
        }

        try
        {
            Path.GetFullPath(path);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
    }
}
