using System.Runtime.CompilerServices;
using System.Text;

namespace AgentBridge.CodexLb.Responses;

/// <summary>Разбирает SSE framing независимо от сетевых границ и JSON протокола Responses.</summary>
internal static class SseEventReader
{
    /// <summary>Объединяет data через LF; незакрытое событие на EOF не подтверждает terminal.</summary>
    internal static async IAsyncEnumerable<(string? Event, string Data)> ReadAsync(Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using StreamReader reader = new(stream, new UTF8Encoding(false, true), false, 4096, leaveOpen: true);
        StringBuilder data = new();
        string? eventName = null;
        bool hasData = false;
        bool firstLine = true;
        while (await reader.ReadLineAsync(cancellationToken) is string line)
        {
            if (firstLine)
            {
                firstLine = false;
                if (line.StartsWith('\uFEFF')) { line = line[1..]; }
            }
            if (line.Length == 0)
            {
                if (hasData) { yield return (eventName, data.ToString()); }
                data.Clear();
                eventName = null;
                hasData = false;
                continue;
            }
            if (line[0] == ':') { continue; }
            int colon = line.IndexOf(':');
            string field = colon < 0 ? line : line[..colon];
            string value = colon < 0 ? "" : line[(colon + 1)..];
            if (value.StartsWith(' ')) { value = value[1..]; }
            if (field == "event") { eventName = value; }
            if (field == "data")
            {
                if (hasData) { data.Append('\n'); }
                data.Append(value);
                hasData = true;
            }
        }
    }
}
