using System.IO.Compression;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.ML.Tokenizers;

namespace AgentBridge.Tokenization;

/// <summary>Создаёт BPE из embedded словарей; пользовательские markers не становятся protocol tokens.</summary>
internal static class OrdinaryTokenizerFactory
{
    // Regex соответствует Microsoft.ML.Tokenizers2.0.0 (machinelearning v5.0.0), без special-token matching.
    private const string CL100K_PATTERN = @"'(?i:[sdmt]|ll|ve|re)|(?>[^\r\n\p{L}\p{N}]?)(?>\p{L}+)|(?>\p{N}{1,3})| ?(?>[^\s\p{L}\p{N}]+)(?>[\r\n]*)|(?>\s+)$|\s*[\r\n]|\s+(?!\S)|\s";
    private const string O200K_PATTERN = @"[^\r\n\p{L}\p{N}]?[\p{Lu}\p{Lt}\p{Lm}\p{Lo}\p{M}]*[\p{Ll}\p{Lm}\p{Lo}\p{M}]+(?i:'s|'t|'re|'ve|'m|'ll|'d)?|[^\r\n\p{L}\p{N}]?[\p{Lu}\p{Lt}\p{Lm}\p{Lo}\p{M}]+[\p{Ll}\p{Lm}\p{Lo}\p{M}]*(?i:'s|'t|'re|'ve|'m|'ll|'d)?|\p{N}{1,3}| ?[^\s\p{L}\p{N}]+[\r\n/]*|\s*[\r\n]+|\s+(?!\S)|\s+";
    private static readonly Lazy<TiktokenTokenizer> o200k = new(() => Create("O200kBase", "o200k_base", O200K_PATTERN));
    private static readonly Lazy<TiktokenTokenizer> cl100k = new(() => Create("Cl100kBase", "cl100k_base", CL100K_PATTERN));

    /// <summary>Использует только уже проверенное имя кодировки; нарушение внутреннего mapping fail-fast.</summary>
    internal static TiktokenTokenizer Get(string encoding) => encoding switch
    {
        "o200k_base" => o200k.Value,
        "cl100k_base" => cl100k.Value,
        _ => throw new InvalidOperationException("Внутренняя кодировка не поддерживается.")
    };

    /// <summary>Читает package resource без сети, нормализации или распознавания special tokens.</summary>
    private static TiktokenTokenizer Create(string packageSuffix, string encoding, string pattern)
    {
        Assembly data = Assembly.Load($"Microsoft.ML.Tokenizers.Data.{packageSuffix}");
        using Stream compressed = data.GetManifestResourceStream($"{encoding}.tiktoken.deflate")
            ?? throw new InvalidOperationException("Embedded словарь токенизатора отсутствует.");
        using DeflateStream vocabulary = new(compressed, CompressionMode.Decompress);
        return TiktokenTokenizer.Create(vocabulary,
            new RegexPreTokenizer(new Regex(pattern, RegexOptions.Compiled, TimeSpan.FromSeconds(1)), null),
            normalizer: null, specialTokens: null);
    }
}
