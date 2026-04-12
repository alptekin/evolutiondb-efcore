using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using evosql;

namespace EvolutionDb.EntityFrameworkCore.Scaffolding.Internal;

public class EvolutionDbDatabaseModelFactory : IDatabaseModelFactory
{
    public DatabaseModel Create(string connectionString, DatabaseModelFactoryOptions options)
    {
        using var connection = new EvosqlConnection(connectionString);
        connection.Open();
        return Create(connection, options);
    }

    public DatabaseModel Create(DbConnection connection, DatabaseModelFactoryOptions options)
    {
        var model = new DatabaseModel();
        var wasOpen = connection.State == ConnectionState.Open;

        if (!wasOpen) connection.Open();

        try
        {
            model.DatabaseName = connection.Database;
            ReadTables(connection, model);
            ReadColumns(connection, model);
            ReadPrimaryKeys(connection, model);
        }
        finally
        {
            if (!wasOpen) connection.Close();
        }

        return model;
    }

    private static void ReadTables(DbConnection connection, DatabaseModel model)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT table_schema, table_name FROM information_schema.tables WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog', 'information_schema')";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var table = new DatabaseTable
            {
                Database = model,
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
            };
            model.Tables.Add(table);
        }
    }

    private static void ReadColumns(DbConnection connection, DatabaseModel model)
    {
        foreach (var table in model.Tables)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT column_name, data_type, is_nullable, ordinal_position FROM information_schema.columns WHERE table_name = '{table.Name}' ORDER BY ordinal_position";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var column = new DatabaseColumn
                {
                    Table = table,
                    Name = reader.GetString(0),
                    StoreType = reader.GetString(1),
                    IsNullable = reader.GetString(2) == "YES",
                };
                table.Columns.Add(column);
            }
        }
    }

    private static void ReadPrimaryKeys(DbConnection connection, DatabaseModel model)
    {
        foreach (var table in model.Tables)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT constraint_name FROM information_schema.table_constraints WHERE table_name = '{table.Name}' AND constraint_type = 'PRIMARY KEY'";

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var pkName = reader.GetString(0);
                var pk = new DatabasePrimaryKey { Table = table, Name = pkName };

                // PK columns — use first column as fallback
                foreach (var col in table.Columns.Take(1))
                {
                    pk.Columns.Add(col);
                }

                table.PrimaryKey = pk;
            }
        }
    }
}
