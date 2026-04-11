using Microsoft.EntityFrameworkCore.Query;

namespace EvolutionDb.EntityFrameworkCore.Query.Internal;

public class EvolutionDbQuerySqlGeneratorFactory : IQuerySqlGeneratorFactory
{
    private readonly QuerySqlGeneratorDependencies _dependencies;

    public EvolutionDbQuerySqlGeneratorFactory(QuerySqlGeneratorDependencies dependencies)
    {
        _dependencies = dependencies;
    }

    public QuerySqlGenerator Create() => new EvolutionDbQuerySqlGenerator(_dependencies);
}
