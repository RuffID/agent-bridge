using System.Text.RegularExpressions;
using EFCoreLibrary.Maintenance.Coordination;
using EFCoreLibrary.Maintenance.Models;
using EFCoreLibrary.Maintenance.Processes;
using Microsoft.Win32;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Находит настоящие клиентские утилиты PostgreSQL и проверяет их major до запуска контейнера.</summary>
public class PostgreSqlIntegrationTools : IAsyncDisposable
{
    private readonly BackupProcessRunner runner = new(new SystemBackupProcessFactory());

    /// <summary>Major сервера контейнера и обеих клиентских утилит.</summary>
    public const int SERVER_MAJOR = 18;

    /// <summary>Проверяет абсолютные существующие пути; не запускает процессы.</summary>
    public PostgreSqlIntegrationTools(string dumpPath, string restorePath)
    {
        DumpPath = ValidatePath(dumpPath, "pg_dump");
        RestorePath = ValidatePath(restorePath, "pg_restore");
    }

    /// <summary>Абсолютный путь к настоящему pg_dump.</summary>
    public string DumpPath { get; }

    /// <summary>Абсолютный путь к настоящему pg_restore.</summary>
    public string RestorePath { get; }

    /// <summary>Принимает явные overrides либо ищет PostgreSQL 18 в реестре, Program Files и PATH.</summary>
    public static PostgreSqlIntegrationTools Discover()
    {
        string[] directories = GetDirectories().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new(
            ResolveExecutable(Environment.GetEnvironmentVariable("AGENTBRIDGE_PG_DUMP"), "pg_dump", directories),
            ResolveExecutable(Environment.GetEnvironmentVariable("AGENTBRIDGE_PG_RESTORE"), "pg_restore", directories));
    }

    /// <summary>Проверяет версии через настоящий bounded process runner EFCoreLibrary.</summary>
    public async Task VerifyAsync(CancellationToken cancellationToken)
    {
        foreach ((string executable, string name) in new[] { (DumpPath, "pg_dump"), (RestorePath, "pg_restore") })
        {
            using MaintenanceBudget budget = new(TimeSpan.FromSeconds(10), cancellationToken);
            ProcessResult result = await runner.RunAsync(new ProcessCommand(executable, ["--version"],
                new Dictionary<string, string> { ["LC_ALL"] = "C" }), budget, TimeSpan.FromSeconds(5));

            if (result.ExitCode != 0 || result.OutputTruncated)
            {
                throw new InvalidOperationException($"Не удалось проверить версию {name}.");
            }

            ValidateVersion(name, result.StandardOutput);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (!await runner.RetryCleanupAsync(TimeSpan.FromSeconds(15)))
        {
            throw new InvalidOperationException("Не подтверждена остановка процесса проверки версии PostgreSQL.");
        }
    }

    /// <summary>Отклоняет другую major и malformed output без раскрытия исходного stdout.</summary>
    internal static void ValidateVersion(string name, string output)
    {
        string pattern = @"\A" + Regex.Escape(name) + @" \(PostgreSQL\) " + SERVER_MAJOR + @"(?:\.\d+)+(?:[^\r\n]*)\r?\n?\z";
        if (!Regex.IsMatch(output, pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
        {
            throw new InvalidOperationException($"Требуется {name} PostgreSQL {SERVER_MAJOR}.");
        }
    }

    /// <summary>Явный ошибочный путь не заменяется результатом автоматического поиска.</summary>
    internal static string ResolveExecutable(string? configuredPath, string name, IEnumerable<string> directories)
    {
        if (configuredPath is not null)
        {
            return ValidatePath(configuredPath, name);
        }

        string filename = name + (OperatingSystem.IsWindows() ? ".exe" : "");
        foreach (string directory in directories)
        {
            if (!Path.IsPathFullyQualified(directory)) continue;

            string candidate = Path.Combine(directory, filename);
            if (File.Exists(candidate)) return candidate;
        }

        throw new InvalidOperationException($"Не найден {name} PostgreSQL {SERVER_MAJOR}. Установите клиентские утилиты либо задайте AGENTBRIDGE_PG_DUMP/AGENTBRIDGE_PG_RESTORE.");
    }

    /// <summary>Не допускает относительные, пустые и отсутствующие пути.</summary>
    private static string ValidatePath(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !File.Exists(path))
        {
            throw new InvalidOperationException($"Требуется существующий абсолютный путь к {name}.");
        }

        return path;
    }

    /// <summary>Перечисляет места установки; версии найденных программ проверяются отдельно до Docker.</summary>
    private static IEnumerable<string> GetDirectories()
    {
        if (OperatingSystem.IsWindows())
        {
            using RegistryKey? installation = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\PostgreSQL\Installations\postgresql-x64-" + SERVER_MAJOR);
            if (installation?.GetValue("Base Directory") is string directory)
            {
                yield return Path.Combine(directory, "bin");
            }

            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "PostgreSQL", SERVER_MAJOR.ToString(), "bin");
        }

        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            yield return directory;
        }
    }
}
