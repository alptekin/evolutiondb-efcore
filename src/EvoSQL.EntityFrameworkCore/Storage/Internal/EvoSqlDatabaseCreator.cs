using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;

namespace EvoSQL.EntityFrameworkCore.Storage.Internal;

public class EvoSqlDatabaseCreator : RelationalDatabaseCreator
{
    private readonly IRelationalConnection _connection;
    private readonly IRawSqlCommandBuilder _rawSqlCommandBuilder;

    public EvoSqlDatabaseCreator(
        RelationalDatabaseCreatorDependencies dependencies,
        IRelationalConnection connection,
        IRawSqlCommandBuilder rawSqlCommandBuilder)
        : base(dependencies)
    {
        _connection = connection;
        _rawSqlCommandBuilder = rawSqlCommandBuilder;
    }

    public override bool Exists()
    {
        try
        {
            _connection.Open();
            _connection.Close();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public override bool HasTables()
    {
        var command = _rawSqlCommandBuilder.Build(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog', 'information_schema')");

        var result = command.ExecuteScalar(
            new RelationalCommandParameterObject(_connection, null, null, null, null));

        return result != null && Convert.ToInt32(result) > 0;
    }

    public override void Create()
    {
        // EvoSQL creates databases via SQL
        using var conn = _connection.DbConnection;
        conn.Open();
        using var cmd = conn.CreateCommand();
        var dbName = conn.Database;
        cmd.CommandText = $"CREATE DATABASE {dbName}";
        try { cmd.ExecuteNonQuery(); } catch { /* may already exist */ }
        conn.Close();
    }

    public override void Delete()
    {
        using var conn = _connection.DbConnection;
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP DATABASE IF EXISTS {conn.Database}";
        try { cmd.ExecuteNonQuery(); } catch { }
        conn.Close();
    }
}
