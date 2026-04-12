using System.Data;
using System.Data.Common;

namespace EvolutionDb.EntityFrameworkCore.Storage.Internal;

/// <summary>
/// Wraps NpgsqlConnection to return EvolutionDbCommand from CreateCommand().
/// This ensures all command executions go through our type-converting DataReader.
/// </summary>
public class EvolutionDbConnectionWrapper : DbConnection
{
    private readonly DbConnection _inner;

    public EvolutionDbConnectionWrapper(DbConnection inner)
    {
        _inner = inner;
    }

    public DbConnection InnerConnection => _inner;

    public override string ConnectionString { get => _inner.ConnectionString!; set => _inner.ConnectionString = value; }
    public override string Database => _inner.Database;
    public override string DataSource => _inner.DataSource;
    public override string ServerVersion => _inner.ServerVersion;
    public override ConnectionState State => _inner.State;

    public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);
    public override void Open() => _inner.Open();
    public override Task OpenAsync(CancellationToken cancellationToken) => _inner.OpenAsync(cancellationToken);
    public override void Close() => _inner.Close();
    public override Task CloseAsync() => _inner.CloseAsync();

    protected override DbCommand CreateDbCommand() => new EvolutionDbCommand(_inner.CreateCommand());
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => _inner.BeginTransaction(isolationLevel);
    protected override async ValueTask<DbTransaction> BeginDbTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken) => await _inner.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
