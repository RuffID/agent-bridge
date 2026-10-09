using AgentBridge.Tokenization;
using Microsoft.Extensions.Options;

namespace AgentBridge.Configuration;

/// <summary>Проверяет конечные offline кодировки без публикации исходных ID или значений конфигурации.</summary>
internal class TokenizationOptionsValidator : IValidateOptions<TokenizationOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, TokenizationOptions options)
    {
        if (options.UnknownModelEstimateEncoding is not (null or "o200k_base" or "cl100k_base"))
            return ValidateOptionsResult.Fail("Tokenization.UnknownModelEstimateEncoding: unsupported_encoding — допустимы null, o200k_base или cl100k_base.");

        if (options.ModelEncodings is null)
            return ValidateOptionsResult.Fail("Tokenization.ModelEncodings: required_object — ожидается словарь соответствий.");

        foreach (KeyValuePair<string, string> entry in options.ModelEncodings)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Key != entry.Key.Trim())
                return ValidateOptionsResult.Fail("Tokenization.ModelEncodings: invalid_model_id — требуется непустой точный ID без внешних пробелов.");

            if (entry.Value is not ("o200k_base" or "cl100k_base"))
                return ValidateOptionsResult.Fail("Tokenization.ModelEncodings: unsupported_encoding — допустимы только o200k_base и cl100k_base.");

            string? builtin = ModelEncodingMap.Find(entry.Key);
            if (builtin is not null && builtin != entry.Value)
                return ValidateOptionsResult.Fail("Tokenization.ModelEncodings: builtin_conflict — встроенная кодировка модели не может быть заменена другой.");
        }

        return ValidateOptionsResult.Success;
    }
}
