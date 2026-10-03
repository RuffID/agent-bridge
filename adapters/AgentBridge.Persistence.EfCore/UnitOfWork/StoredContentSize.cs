using System.Text;
using AgentBridge.Persistence.EfCore.Models;

namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <summary>Считает UTF-8 сохраняемых текстовых колонок; копии output и envelope учитываются по фактическому хранению.</summary>
internal static class StoredContentSize
{
    /// <summary>Размер полного отчёта без числовых метаданных и overhead провайдера.</summary>
    public static long Of(ModelResponseRecord response) => checked(
        Of(response.OutputJson) + Of(response.EnvelopeJson) + Of(response.ContinuationJson) + Of(response.ErrorMessage));
    /// <summary>Размер текстового содержимого либо ноль для отсутствующей колонки.</summary>
    public static long Of(string? value) => value is null ? 0 : Encoding.UTF8.GetByteCount(value);
}
