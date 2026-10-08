using System.Reflection;

namespace AgentBridge.Delivery.Metadata.Tests;

/// <summary>Определяет проектные каталоги поставки независимо от окружения и рабочего каталога test runner.</summary>
internal static class DeliveryTestPaths
{
    /// <summary>Корень подготовки комплектов, записанный MSBuild в metadata тестовой сборки.</summary>
    internal static string ArtifactsRoot { get; } = ReadArtifactsRoot();

    /// <summary>Каталог последней успешно подготовленной поставки.</summary>
    internal static string CurrentRoot => Path.Combine(ArtifactsRoot, "current");

    /// <summary>Матрица полных SDK-комплектов по провайдерам и RID.</summary>
    internal static string SdkRoot => Path.Combine(CurrentRoot, "sdk");

    /// <summary>Матрица поставок собственных DLL с обязательными NuGet-зависимостями.</summary>
    internal static string NuGetRoot => Path.Combine(CurrentRoot, "nuget");

    /// <summary>Общая поставка SQL Server для трёх RID.</summary>
    internal static string SharedRoot => Path.Combine(CurrentRoot, "shared", "SqlServer");

    /// <summary>Требует единственный абсолютный проектный путь без поиска старых комплектов.</summary>
    private static string ReadArtifactsRoot()
    {
        string? root = typeof(DeliveryTestPaths).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "AgentBridgeDeliveryTestsRoot").Value;
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            throw new InvalidOperationException("MSBuild должен указать абсолютный каталог подготовки delivery-тестов.");

        return root;
    }
}
