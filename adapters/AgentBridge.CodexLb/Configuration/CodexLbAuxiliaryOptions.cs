namespace AgentBridge.CodexLb.Configuration;

/// <summary>Отдельные явные сроки optional usage, upload и image операций.</summary>
public class CodexLbAuxiliaryOptions
{
    /// <summary>Срок чтения usage.</summary>
    public TimeSpan MetadataTimeout { get; set; }
    /// <summary>Срок регистрации файла.</summary>
    public TimeSpan FileCreateTimeout { get; set; }
    /// <summary>Срок загрузки байтов.</summary>
    public TimeSpan FileUploadTimeout { get; set; }
    /// <summary>Срок подтверждения файла.</summary>
    public TimeSpan FileFinalizeTimeout { get; set; }
    /// <summary>Срок генерации и редактирования изображения.</summary>
    public TimeSpan ImageTimeout { get; set; }
}

