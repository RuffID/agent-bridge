namespace AgentBridge.Sub2Api.Configuration;

/// <summary>Проверяет локальный формат gateway Bearer без проверки upstream credential.</summary>
internal static class Sub2ApiCredential
{
    /// <summary>Отсутствие или непригодный ключ не разрешают сетевой fallback.</summary>
    internal static bool IsValid(string key)
    {
        return !string.IsNullOrWhiteSpace(key) && !key.Any(char.IsWhiteSpace) && !key.Any(char.IsControl);
    }
}
