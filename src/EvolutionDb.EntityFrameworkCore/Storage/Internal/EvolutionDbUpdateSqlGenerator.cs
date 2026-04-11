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

    /// <summary>
    /// INSERT INTO "table" ("col1", "col2") VALUES (@p0, @p1) RETURNING "id";
    /// </summary>
    public override ResultSetMapping AppendInsertReturningOperation(StringBuilder commandStringBuilder, IReadOnlyModificationCommand command, int commandPosition, out bool requiresTransaction)
    {
        var operations = command.ColumnModifications;
        var writeOperations = operations.Where(o => o.IsWrite).ToList();
        var readOperations = operations.Where(o => o.IsRead).ToList();

        AppendInsertCommand(commandStringBuilder, command.TableName, command.Schema, writeOperations, readOperations);
        AppendReturningClause(commandStringBuilder, readOperations);
        commandStringBuilder.AppendLine(SqlGenerationHelper.StatementTerminator);

        requiresTransaction = true;
        return readOperations.Count > 0 ? ResultSetMapping.LastInResultSet : ResultSetMapping.NoResults;
    }

    /// <summary>
    /// UPDATE "table" SET "col1" = @p0 WHERE "id" = @p1 RETURNING "computed_col";
    /// </summary>
    protected override ResultSetMapping AppendUpdateReturningOperation(StringBuilder commandStringBuilder, IReadOnlyModificationCommand command, int commandPosition, out bool requiresTransaction)
    {
        var operations = command.ColumnModifications;
        var writeOperations = operations.Where(o => o.IsWrite).ToList();
        var conditionOperations = operations.Where(o => o.IsCondition).ToList();
        var readOperations = operations.Where(o => o.IsRead).ToList();

        AppendUpdateCommand(commandStringBuilder, command.TableName, command.Schema, writeOperations, readOperations, conditionOperations);
        AppendReturningClause(commandStringBuilder, readOperations);
        commandStringBuilder.AppendLine(SqlGenerationHelper.StatementTerminator);

        requiresTransaction = true;
        return readOperations.Count > 0 ? ResultSetMapping.LastInResultSet : ResultSetMapping.NoResults;
    }

    /// <summary>
    /// DELETE FROM "table" WHERE "id" = @p0;
    /// </summary>
    protected override ResultSetMapping AppendDeleteReturningOperation(StringBuilder commandStringBuilder, IReadOnlyModificationCommand command, int commandPosition, out bool requiresTransaction)
    {
        var operations = command.ColumnModifications;
        var conditionOperations = operations.Where(o => o.IsCondition).ToList();
        var readOperations = operations.Where(o => o.IsRead).ToList();

        AppendDeleteCommand(commandStringBuilder, command.TableName, command.Schema, readOperations, conditionOperations);
        AppendReturningClause(commandStringBuilder, readOperations);
        commandStringBuilder.AppendLine(SqlGenerationHelper.StatementTerminator);

        requiresTransaction = true;
        return readOperations.Count > 0 ? ResultSetMapping.LastInResultSet : ResultSetMapping.NoResults;
    }

    private void AppendReturningClause(StringBuilder commandStringBuilder, List<IColumnModification> readOperations)
    {
        if (readOperations.Count == 0) return;

        commandStringBuilder.Append(" RETURNING ");
        commandStringBuilder.AppendJoin(", ", readOperations.Select(o => SqlGenerationHelper.DelimitIdentifier(o.ColumnName)));
    }
}
