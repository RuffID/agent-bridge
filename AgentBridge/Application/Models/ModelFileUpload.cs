namespace AgentBridge.Application.Models;

/// <summary>Файл для регистрации и загрузки; секреты Telegram и signed URL сюда не передаются.</summary>
public class ModelFileUpload
{
    /// <summary>Имя файла без пути.</summary>
    public string FileName { get; init; } = string.Empty;
    /// <summary>MIME содержимого либо null для application/octet-stream.</summary>
    public string? MimeType { get; init; }
    /// <summary>Байты файла; адаптер фиксирует собственную копию до первого await.</summary>
    public byte[] Bytes { get; init; } = [];
}

