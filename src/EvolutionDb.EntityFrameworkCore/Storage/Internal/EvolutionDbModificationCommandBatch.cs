using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace EvolutionDb.EntityFrameworkCore.Storage.Internal;

/// <summary>
/// Batches multiple DML commands into a single roundtrip via Npgsql batching.
/// EvolutionDB supports multiple statements per roundtrip.
/// Default batch size: 100 commands.
/// </summary>
public class EvolutionDbModificationCommandBatch : ReaderModificationCommandBatch
{
    private readonly int _maxBatchSize;

    public EvolutionDbModificationCommandBatch(ModificationCommandBatchFactoryDependencies dependencies, int maxBatchSize)
        : base(dependencies)
    {
        _maxBatchSize = maxBatchSize;
    }

    protected override int MaxBatchSize => _maxBatchSize;

    protected override void Consume(RelationalDataReader reader)
    {
        for (var commandIndex = 0; commandIndex < ModificationCommands.Count; commandIndex++)
        {
            var command = ModificationCommands[commandIndex];

            if (command.ColumnModifications.Any(o => o.IsRead))
            {
                command.PropagateResults(reader);
                reader.DbDataReader.NextResult();
            }
        }
    }

    protected override async Task ConsumeAsync(RelationalDataReader reader, CancellationToken cancellationToken = default)
    {
        for (var commandIndex = 0; commandIndex < ModificationCommands.Count; commandIndex++)
        {
            var command = ModificationCommands[commandIndex];

            if (command.ColumnModifications.Any(o => o.IsRead))
            {
                command.PropagateResults(reader);
                await reader.DbDataReader.NextResultAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
