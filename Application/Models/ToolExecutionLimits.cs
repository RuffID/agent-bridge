namespace AgentBridge.Application.Models;

/// <summary>Явные ограничения одной сессии; timeout кооперативен и не позволяет оставлять tasks без ожидания.</summary>
public class ToolExecutionLimits
{
    /// <summary>Фиксирует положительные bounds; MaxSteps соответствует числу модельных шагов с инструментами.</summary>
    public ToolExecutionLimits(int maxSteps, int maxCallsPerStep, int maxConcurrency, TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSteps);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCallsPerStep);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrency);
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > uint.MaxValue - 1)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
        MaxSteps = maxSteps;
        MaxCallsPerStep = maxCallsPerStep;
        MaxConcurrency = maxConcurrency;
        Timeout = timeout;
    }

    /// <summary>Максимум модельных шагов, содержащих pending вызовы.</summary>
    public int MaxSteps { get; }
    /// <summary>Максимум pending вызовов одного шага.</summary>
    public int MaxCallsPerStep { get; }
    /// <summary>Максимум параллельных scopes/handlers одной сессии.</summary>
    public int MaxConcurrency { get; }
    /// <summary>Общий monotonic бюджет от создания сессии, включая ожидания между шагами.</summary>
    public TimeSpan Timeout { get; }
}
