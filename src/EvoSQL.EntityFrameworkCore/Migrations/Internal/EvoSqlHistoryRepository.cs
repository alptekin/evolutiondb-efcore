using Microsoft.EntityFrameworkCore.Migrations;

namespace EvoSQL.EntityFrameworkCore.Migrations.Internal;

public class EvoSqlHistoryRepository : HistoryRepository
{
    public EvoSqlHistoryRepository(HistoryRepositoryDependencies dependencies)
        : base(dependencies)
    {
    }

    protected override string ExistsSql
        => $"SELECT COUNT(*) FROM information_schema.tables WHERE table_name = '{TableName}'";

    protected override bool InterpretExistsResult(object? value)
        => value != null && Convert.ToInt32(value) > 0;

    public override string GetBeginIfExistsScript(string migrationId)
        => $"-- Migration: {migrationId}";

    public override string GetBeginIfNotExistsScript(string migrationId)
        => $"-- Migration (if not exists): {migrationId}";

    public override string GetEndIfScript()
        => string.Empty;

    public override string GetCreateIfNotExistsScript()
    {
        var script = GetCreateScript();
        return script.Replace("CREATE TABLE", "CREATE TABLE IF NOT EXISTS");
    }
}
