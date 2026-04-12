using System.Data;
using System.Data.Common;
using evosql.Internal;

namespace evosql;

public class EvosqlConnection : DbConnection
{
    private string _connectionString = "";
    private ConnectionState _state = ConnectionState.Closed;
    private EvoProtocolClient? _client;
    private EvosqlConnectionStringBuilder? _csb;

    internal EvoProtocolClient Client => _client ?? throw new InvalidOperationException("Connection is not open.");

    public EvosqlConnection()
    {
        //...
    }

    public EvosqlConnection(string connectionString)
    {
        _connectionString = connectionString;
    }

    public override string ConnectionString { get => _connectionString; set { _connectionString = value; _csb = null; } }
    public override string Database => Csb.Database;
    public override string DataSource => $"{Csb.Host}:{Csb.Port}";
    public override string ServerVersion => "EvolutionDB 2.0";
    public override ConnectionState State => _state;

    private EvosqlConnectionStringBuilder Csb => _csb ??= new EvosqlConnectionStringBuilder(_connectionString);

    public override void Open()
    {
        if (_state == ConnectionState.Open) return;

        _state = ConnectionState.Connecting;
        _client = new EvoProtocolClient();

        try
        {
            _client.Connect(Csb.Host, Csb.Port, Csb.Timeout * 1000);
            _client.Authenticate(Csb.Username, Csb.Password);
            _state = ConnectionState.Open;
        }
        catch
        {
            _client.Dispose();
            _client = null;
            _state = ConnectionState.Closed;
            throw;
        }
    }

    public override Task OpenAsync(CancellationToken cancellationToken)
    {
        Open();
        return Task.CompletedTask;
    }

    public override void Close()
    {
        if (_state == ConnectionState.Closed) return;
        _client?.Dispose();
        _client = null;
        _state = ConnectionState.Closed;
    }

    public override Task CloseAsync()
    {
        Close();
        return Task.CompletedTask;
    }

    public override void ChangeDatabase(string databaseName) => throw new NotSupportedException("Use a new connection to change database.");

    protected override DbCommand CreateDbCommand() => new EvosqlCommand { Connection = this };
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new EvosqlTransaction(this, isolationLevel);

    protected override void Dispose(bool disposing)
    {
        if (disposing) Close();
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        Close();
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
