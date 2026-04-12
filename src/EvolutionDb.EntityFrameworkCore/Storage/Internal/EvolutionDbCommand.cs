using System.Data;
using System.Data.Common;

namespace EvolutionDb.EntityFrameworkCore.Storage.Internal;

/// <summary>
/// Wraps NpgsqlCommand to return EvolutionDbDataReader instead of NpgsqlDataReader.
/// This ensures typed reads (GetInt32, GetDecimal, etc.) work correctly with
/// EvolutionDB's text-mode responses.
/// </summary>
public class EvolutionDbCommand : DbCommand
{
    private readonly DbCommand _inner;

    public EvolutionDbCommand(DbCommand inner)
    {
        _inner = inner;
    }

    public override string CommandText { get => _inner.CommandText; set => _inner.CommandText = value; }
    public override int CommandTimeout { get => _inner.CommandTimeout; set => _inner.CommandTimeout = value; }
    public override CommandType CommandType { get => _inner.CommandType; set => _inner.CommandType = value; }
    public override bool DesignTimeVisible { get => _inner.DesignTimeVisible; set => _inner.DesignTimeVisible = value; }
    public override UpdateRowSource UpdatedRowSource { get => _inner.UpdatedRowSource; set => _inner.UpdatedRowSource = value; }
    protected override DbConnection? DbConnection { get => _inner.Connection; set => _inner.Connection = value; }
    protected override DbParameterCollection DbParameterCollection => _inner.Parameters;
    protected override DbTransaction? DbTransaction { get => _inner.Transaction; set => _inner.Transaction = value; }

    public override void Cancel() => _inner.Cancel();
    public override int ExecuteNonQuery() => _inner.ExecuteNonQuery();
    public override object? ExecuteScalar() => _inner.ExecuteScalar();
    public override void Prepare() => _inner.Prepare();

    protected override DbParameter CreateDbParameter() => _inner.CreateParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => new EvolutionDbDataReader(_inner.ExecuteReader(behavior));

    protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken) => new EvolutionDbDataReader(await _inner.ExecuteReaderAsync(behavior, cancellationToken).ConfigureAwait(false));

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
