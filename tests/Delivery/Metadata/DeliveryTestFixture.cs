using System.Text.Json;
using Xunit;

[assembly: AssemblyFixture(typeof(AgentBridge.Delivery.Metadata.Tests.DeliveryTestFixture))]

namespace AgentBridge.Delivery.Metadata.Tests;

/// <summary>Не допускает запуск против незавершённой поставки и её замену во время metadata-тестов.</summary>
public class DeliveryTestFixture : IDisposable
{
    private readonly FileStream preparationLock;

    /// <summary>Принимает только завершённую подготовку и удерживает общую read-блокировку до окончания тестов.</summary>
    public DeliveryTestFixture()
    {
        string lockPath = Path.Combine(DeliveryTestPaths.ArtifactsRoot, "preparation.lock");
        if (!File.Exists(lockPath))
            throw new InvalidOperationException("Поставка не подготовлена. Соберите delivery-тесты без --no-build и AgentBridgeSkipDeliveryPreparation.");

        preparationLock = new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            string readyPath = Path.Combine(DeliveryTestPaths.CurrentRoot, "ready.json");
            if (!File.Exists(readyPath))
                throw new InvalidOperationException("Подготовка поставки не завершена. Повторите сборку delivery-тестов.");

            using JsonDocument ready = JsonDocument.Parse(File.ReadAllText(readyPath));

            if (ready.RootElement.GetProperty("schemaVersion").GetInt32() != 1
                || string.IsNullOrWhiteSpace(ready.RootElement.GetProperty("fingerprint").GetString()))
                throw new InvalidDataException("Некорректный маркер подготовки delivery-тестов.");
        }
        catch
        {
            preparationLock.Dispose();
            throw;
        }
    }

    /// <summary>Разрешает следующей сборке обновить комплекты после завершения всех тестов сборки.</summary>
    public void Dispose() => preparationLock.Dispose();
}
