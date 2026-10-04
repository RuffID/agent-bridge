namespace AgentBridge.Tokenization;

/// <summary>Конечные exact ID, прямо подтверждённые OpenAI tiktoken0.12.0 model.py; не каталог доступности.</summary>
internal static class ModelEncodingMap
{
    /// <summary>Возвращает только проверенное соответствие без prefix, case folding или fallback.</summary>
    internal static string? Find(string model) => model switch
    {
        "gpt-5" or "gpt-4.1" or "gpt-4o" or "o1" or "o3" or "o4-mini" => "o200k_base",
        "gpt-4" or "gpt-3.5-turbo" => "cl100k_base",
        _ => null
    };
}
