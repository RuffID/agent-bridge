namespace AgentBridge.RuntimeProbe;

/// <summary>Запрещает любой HTTP даже при ошибочном добавлении операции в probe.</summary>
public class BlockingHandler : HttpMessageHandler
{
    /// <summary>Количество попыток отправки.</summary>
    public int Calls { get; private set; }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        throw new InvalidOperationException("HTTP is forbidden in the delivery probe.");
    }
}
