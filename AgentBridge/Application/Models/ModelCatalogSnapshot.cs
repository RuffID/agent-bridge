namespace AgentBridge.Application.Models;

/// <summary>Независимый каталог конкретного чтения; не хранит ключ или общий кеш.</summary>
public class ModelCatalogSnapshot
{
    /// <summary>Копирует коллекцию; ID должны быть уникальны при точном сравнении.</summary>
    public ModelCatalogSnapshot(IEnumerable<ModelCapabilities> models)
    {
        ArgumentNullException.ThrowIfNull(models);
        ModelCapabilities[] copy = models.ToArray();
        if (copy.Any(model => model is null) || copy.Select(model => model.Id).Distinct(StringComparer.Ordinal).Count() != copy.Length)
        {
            throw new ArgumentException("Каталог должен содержать модели с уникальными ID.", nameof(models));
        }
        Models = Array.AsReadOnly(copy);
    }

    /// <summary>Модели в полученном серверном порядке; пустой список допустим.</summary>
    public IReadOnlyList<ModelCapabilities> Models { get; }
}
