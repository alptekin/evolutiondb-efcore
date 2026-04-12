using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;
using evosql;

namespace EvolutionDb.EntityFrameworkCore.Storage.Internal;

public class EvolutionDbRelationalConnection : RelationalConnection
{
    public EvolutionDbRelationalConnection(RelationalConnectionDependencies dependencies)
        : base(dependencies)
    {
        //...
    }

    protected override DbConnection CreateDbConnection() => new EvosqlConnection(ConnectionString ?? "");
}
