using System.Data;
using System.Data.Common;

namespace evosql;

public class EvosqlTransaction : DbTransaction
{
    private readonly EvosqlConnection _connection;
    private readonly IsolationLevel _isolationLevel;
    private bool _completed;

    internal EvosqlTransaction(EvosqlConnection connection, IsolationLevel isolationLevel)
    {
        _connection = connection;
        _isolationLevel = isolationLevel;
        _connection.Client.ExecuteQuery("BEGIN");
    }

    public override IsolationLevel IsolationLevel => _isolationLevel;

    protected override DbConnection DbConnection => _connection;

    public override void Commit()
    {
        if (_completed)
            throw new InvalidOperationException("Transaction has already been committed or rolled back.");

        _connection.Client.ExecuteQuery("COMMIT");
        _completed = true;
    }

    public override void Rollback()
    {
        if (_completed)
            throw new InvalidOperationException("Transaction has already been committed or rolled back.");

        _connection.Client.ExecuteQuery("ROLLBACK");
        _completed = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_completed)
        {
            try { Rollback(); }
            catch { /* best-effort rollback on dispose */ }
        }

        base.Dispose(disposing);
    }
}
