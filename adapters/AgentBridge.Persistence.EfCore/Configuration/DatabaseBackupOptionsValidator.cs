using Microsoft.Extensions.Options;

namespace AgentBridge.Persistence.EfCore.Configuration;

/// <inheritdoc/>
/// <remarks>Проверяет форму настроек PostgreSQL без файловой системы, подключения или запуска pg_dump.</remarks>
public class DatabaseBackupOptionsValidator : IValidateOptions<DatabaseBackupOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, DatabaseBackupOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        List<string> errors = [];
        if (!IsAbsolutePath(options.BackupDirectory))
        {
            errors.Add("Backup.BackupDirectory должен быть корректным абсолютным путём.");
        }

        if (options.BackupRetentionPeriod is not { } retention || retention <= TimeSpan.Zero)
        {
            errors.Add("Backup.BackupRetentionPeriod обязателен и должен быть положительным; default отсутствует, retention выполняет приложение.");
        }

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

        if (errors.Count > 0)
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
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
