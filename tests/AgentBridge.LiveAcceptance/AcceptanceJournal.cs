using System.Security.Cryptography;
using System.Text.Json;

namespace AgentBridge.LiveAcceptance;

/// <summary>Эксклюзивно удерживает журнал; неизвестный исход и начатая фаза не разрешают повтор.</summary>
internal class AcceptanceJournal : IDisposable
{
    private readonly FileStream stream;

    /// <summary>Открывает новый журнал либо единственное продолжение завершённой JSON-фазы.</summary>
    public AcceptanceJournal(string path, bool streaming, string configurationHash)
    {
        FileMode mode = FileMode.CreateNew;
        if (streaming)
        {
            mode = FileMode.Open;
        }

        stream = new FileStream(path, mode, FileAccess.ReadWrite, FileShare.None);
        try
        {
            Evidence = new AcceptanceEvidence();
            if (streaming)
            {
                Evidence = JsonSerializer.Deserialize<AcceptanceEvidence>(stream)
                    ?? throw new InvalidOperationException("journal_invalid");
                Require(Evidence.Phase == "json-complete", "json_phase_required");
                Require(Evidence.ConfigurationHash == configurationHash, "configuration_changed");
            }

            Evidence.ConfigurationHash = configurationHash;
            Evidence.ProcessId = Guid.NewGuid();
            Evidence.Phase = "json-started";
            if (streaming)
            {
                Evidence.Phase = "sse-started";
            }

            Save();
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public AcceptanceEvidence Evidence { get; }

    /// <summary>Синхронно фиксирует резерв до HTTP/tool действия, включая flush на диск.</summary>
    public void Save()
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(Evidence, new JsonSerializerOptions { WriteIndented = true });
        stream.Position = 0;
        stream.Write(bytes);
        stream.SetLength(bytes.Length);
        stream.Flush(flushToDisk: true);
    }

    /// <summary>Хеширует canonical элементы без записи их содержимого.</summary>
    public static string Hash(IEnumerable<JsonElement> items)
    {
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(items.ToArray())));
    }

    /// <summary>Останавливает приёмку при первой нарушенной проверке.</summary>
    public static void Require(bool condition, string code)
    {
        if (!condition)
        {
            throw new AcceptanceCheckException(code);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        stream.Dispose();
    }
}
