using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace EvoSQL.EntityFrameworkCore.Storage.Internal;

public class EvoSqlModificationCommandBatchFactory : IModificationCommandBatchFactory
{
    private readonly ModificationCommandBatchFactoryDependencies _dependencies;
    private readonly IDbContextOptions _options;

    public EvoSqlModificationCommandBatchFactory(
        ModificationCommandBatchFactoryDependencies dependencies,
        IDbContextOptions options)
    {
        _dependencies = dependencies;
        _options = options;
    }

    public ModificationCommandBatch Create()
        => new SingularModificationCommandBatch(_dependencies);
}
