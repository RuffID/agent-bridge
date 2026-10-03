using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Управляемый root CAS failure через публичную поверхность EF exception, без internal API.</summary>
internal class FakeRootConcurrencyException(EntityEntry entry) : DbUpdateConcurrencyException("synthetic root CAS")
{
    /// <inheritdoc/>
    public override IReadOnlyList<EntityEntry> Entries => [entry];
}
