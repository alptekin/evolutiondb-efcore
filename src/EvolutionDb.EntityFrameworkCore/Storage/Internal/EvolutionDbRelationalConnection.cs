using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace EvolutionDb.EntityFrameworkCore.Storage.Internal;

public class EvolutionDbRelationalConnection : RelationalConnection
{
    public EvolutionDbRelationalConnection(RelationalConnectionDependencies dependencies)
        : base(dependencies)
    {
        //...
    }

    protected override DbConnection CreateDbConnection()
    {
        var connStr = ConnectionString ?? "";
        var builder = new NpgsqlConnectionStringBuilder(connStr);

        if (!connStr.Contains("Port=", StringComparison.OrdinalIgnoreCase))
            builder.Port = 5433;

        var npgsqlConn = new NpgsqlConnection(builder.ConnectionString);
        return new EvolutionDbConnectionWrapper(npgsqlConn);
    }
}
