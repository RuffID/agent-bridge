using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Подставное соединение для actual EF pin; команды и БД запрещены.</summary>
public class FakePinnedConnection : DbConnection
{
    private ConnectionState state;
    /// <summary>Включает secondary отказ закрытия.</summary>
    public bool FailClose { get; set; }
    /// <summary>Число вызовов actual CloseConnectionAsync.</summary>
    public int Closes { get; private set; }
    /// <inheritdoc/>
    [AllowNull] public override string ConnectionString { get; set; } = "";
    /// <inheritdoc/>
    public override string Database => "fake";
    /// <inheritdoc/>
    public override string DataSource => "fake";
    /// <inheritdoc/>
    public override string ServerVersion => "0";
    /// <inheritdoc/>
    public override ConnectionState State => state;
    /// <inheritdoc/>
    public override void Open() => state = ConnectionState.Open;
    /// <inheritdoc/>
    public override Task OpenAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); Open(); return Task.CompletedTask; }
    /// <inheritdoc/>
    public override void Close() { Closes++; if (FailClose) throw new IOException("synthetic-secret-close"); state = ConnectionState.Closed; }
    /// <inheritdoc/>
    public override Task CloseAsync() { Close(); return Task.CompletedTask; }
    /// <inheritdoc/>
    public override void ChangeDatabase(string databaseName) => throw new InvalidOperationException("БД запрещена.");
    /// <inheritdoc/>
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new InvalidOperationException("БД запрещена.");
    /// <inheritdoc/>
    protected override DbCommand CreateDbCommand() => throw new InvalidOperationException("SQL запрещён.");
}
