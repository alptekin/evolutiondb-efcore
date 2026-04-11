using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace EvolutionDb.EntityFrameworkCore.Storage.Internal;

public class EvolutionDbModificationCommandBatchFactory : IModificationCommandBatchFactory
{
    private const int DefaultMaxBatchSize = 100;

    private readonly ModificationCommandBatchFactoryDependencies _dependencies;
    private readonly int _maxBatchSize;

    public EvolutionDbModificationCommandBatchFactory(ModificationCommandBatchFactoryDependencies dependencies, IDbContextOptions options)
    {
        _dependencies = dependencies;

        var relationalOptions = RelationalOptionsExtension.Extract(options);
        _maxBatchSize = relationalOptions.MaxBatchSize ?? DefaultMaxBatchSize;
    }

    public ModificationCommandBatch Create() => new EvolutionDbModificationCommandBatch(_dependencies, _maxBatchSize);
}
