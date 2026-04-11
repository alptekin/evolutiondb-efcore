using System.Text;
using Microsoft.EntityFrameworkCore.Update;

namespace EvoSQL.EntityFrameworkCore.Storage.Internal;

public class EvoSqlUpdateSqlGenerator : UpdateSqlGenerator
{
    public EvoSqlUpdateSqlGenerator(UpdateSqlGeneratorDependencies dependencies)
        : base(dependencies)
    {
    }

    // EvoSQL supports RETURNING clause on INSERT for auto-generated keys
    public override ResultSetMapping AppendInsertReturningOperation(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        int commandPosition,
        out bool requiresTransaction)
    {
        var operations = command.ColumnModifications;
        var writeOperations = operations.Where(o => o.IsWrite).ToList();
        var readOperations = operations.Where(o => o.IsRead).ToList();

        AppendInsertCommand(
            commandStringBuilder, command.TableName, command.Schema,
            writeOperations, readOperations);

        if (readOperations.Count > 0)
        {
            commandStringBuilder.Append(" RETURNING ");
            commandStringBuilder.AppendJoin(", ",
                readOperations.Select(o =>
                    SqlGenerationHelper.DelimitIdentifier(o.ColumnName)));
        }

        commandStringBuilder.AppendLine(SqlGenerationHelper.StatementTerminator);

        requiresTransaction = true;
        return readOperations.Count > 0
            ? ResultSetMapping.LastInResultSet
            : ResultSetMapping.NoResults;
    }
}
