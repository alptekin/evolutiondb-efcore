using System.Data.Common;

namespace evosql;

public class EvosqlProviderFactory : DbProviderFactory
{
    public static readonly EvosqlProviderFactory Instance = new();

    private EvosqlProviderFactory()
    {
        //...
    }

    public override DbConnection CreateConnection() => new EvosqlConnection();

    public override DbCommand CreateCommand() => new EvosqlCommand();

    public override DbParameter CreateParameter() => new EvosqlParameter();

    public override DbConnectionStringBuilder CreateConnectionStringBuilder() => new EvosqlConnectionStringBuilder();
}
