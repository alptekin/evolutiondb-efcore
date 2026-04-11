using System.Text;
using Microsoft.EntityFrameworkCore.Update;

namespace EvolutionDb.EntityFrameworkCore.Storage.Internal;

public class EvolutionDbUpdateSqlGenerator : UpdateSqlGenerator
{
    public EvolutionDbUpdateSqlGenerator(UpdateSqlGeneratorDependencies dependencies)
        : base(dependencies)
    {
        //...
    }

    // EvolutionDB supports RETURNING clause on INSERT for auto-generated keys
    public override ResultSetMapping AppendInsertReturningOperation(StringBuilder commandStringBuilder, IReadOnlyModificationCommand command, int commandPosition, out bool requiresTransaction)
    {
        var operations = command.ColumnModifications;
        var writeOperations = operations.Where(o => o.IsWrite).ToList();
        var readOperations = operations.Where(o => o.IsRead).ToList();

        AppendInsertCommand(commandStringBuilder, command.TableName, command.Schema, writeOperations, readOperations);

        if (readOperations.Count > 0)
        {
            commandStringBuilder.Append(" RETURNING ");
            commandStringBuilder.AppendJoin(", ", readOperations.Select(o => SqlGenerationHelper.DelimitIdentifier(o.ColumnName)));
        }

        commandStringBuilder.AppendLine(SqlGenerationHelper.StatementTerminator);

        requiresTransaction = true;
        return readOperations.Count > 0 ? ResultSetMapping.LastInResultSet : ResultSetMapping.NoResults;
    }
}
