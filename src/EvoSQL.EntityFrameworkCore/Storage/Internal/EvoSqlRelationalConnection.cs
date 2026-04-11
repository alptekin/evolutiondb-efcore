using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace EvoSQL.EntityFrameworkCore.Storage.Internal;

public class EvoSqlRelationalConnection : RelationalConnection
{
    public EvoSqlRelationalConnection(RelationalConnectionDependencies dependencies)
        : base(dependencies)
    {
    }

    protected override DbConnection CreateDbConnection()
    {
        var connStr = ConnectionString ?? "";
        var builder = new NpgsqlConnectionStringBuilder(connStr);

        // Default to EvoSQL port
        if (!connStr.Contains("Port=", StringComparison.OrdinalIgnoreCase))
            builder.Port = 5433;

        // Skip Npgsql's type-loading queries (they use subqueries against pg_type)
        builder.ServerCompatibilityMode = ServerCompatibilityMode.NoTypeLoading;

        return new NpgsqlConnection(builder.ConnectionString);
    }
}
