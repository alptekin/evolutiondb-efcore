using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace EvolutionDb.EntityFrameworkCore.Migrations.Internal;

public class EvolutionDbMigrationsSqlGenerator : MigrationsSqlGenerator
{
    public EvolutionDbMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, IRelationalAnnotationProvider migrationsAnnotations)
        : base(dependencies)
    {
        //...
    }

    protected override void Generate(CreateTableOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        builder.Append("CREATE TABLE ");
        builder.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name, operation.Schema));
        builder.AppendLine(" (");

        using (builder.Indent())
        {
            for (var i = 0; i < operation.Columns.Count; i++)
            {
                var column = operation.Columns[i];

                if (i > 0) 
                {
                    builder.AppendLine(",");
                }

                builder.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(column.Name));
                builder.Append(" ");
                builder.Append(column.ColumnType ?? GetColumnType(column));

                if (!column.IsNullable)
                {
                    builder.Append(" NOT NULL");
                }

                if (column.DefaultValueSql != null)
                {
                    builder.Append(" DEFAULT ");
                    builder.Append(column.DefaultValueSql);
                }
                else if (column.DefaultValue != null)
                {
                    builder.Append(" DEFAULT ");
                    builder.Append(GenerateSqlLiteral(column.DefaultValue));
                }

                // AUTO_INCREMENT for identity columns
                if (column[RelationalAnnotationNames.ColumnOrder] != null)
                {
                    // handled below via PK
                }
            }

            if (operation.PrimaryKey != null)
            {
                builder.AppendLine(",");
                builder.Append("PRIMARY KEY (");
                builder.Append(string.Join(", ", operation.PrimaryKey.Columns.Select(c => Dependencies.SqlGenerationHelper.DelimitIdentifier(c))));
                builder.Append(")");
            }

            foreach (var fk in operation.ForeignKeys)
            {
                builder.AppendLine(",");
                builder.Append("FOREIGN KEY (");
                builder.Append(string.Join(", ",fk.Columns.Select(c => Dependencies.SqlGenerationHelper.DelimitIdentifier(c))));
                builder.Append(") REFERENCES ");
                builder.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(fk.PrincipalTable, fk.PrincipalSchema));
                builder.Append(" (");
                builder.Append(string.Join(", ", (fk.PrincipalColumns ?? Array.Empty<string>()).Select(c => Dependencies.SqlGenerationHelper.DelimitIdentifier(c))));
                builder.Append(")");

                if (fk.OnDelete != ReferentialAction.NoAction)
                {
                    builder.Append(" ON DELETE ");
                    builder.Append(GetReferentialAction(fk.OnDelete));
                }
            }

            foreach (var unique in operation.UniqueConstraints)
            {
                builder.AppendLine(",");
                builder.Append("UNIQUE (");
                builder.Append(string.Join(", ", unique.Columns.Select(c => Dependencies.SqlGenerationHelper.DelimitIdentifier(c))));
                builder.Append(")");
            }

            foreach (var check in operation.CheckConstraints)
            {
                builder.AppendLine(",");
                builder.Append("CONSTRAINT ");
                builder.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(check.Name));
                builder.Append(" CHECK (");
                builder.Append(check.Sql);
                builder.Append(")");
            }
        }

        builder.AppendLine();
        builder.Append(")");

        if (terminate)
        {
            builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            builder.EndCommand();
        }
    }

    protected override void Generate(DropTableOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        builder.Append("DROP TABLE IF EXISTS ");
        builder.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name, operation.Schema));

        if (terminate)
        {
            builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            builder.EndCommand();
        }
    }

    protected override void Generate(AddColumnOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        builder.Append("ALTER TABLE ");
        builder.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema));
        builder.Append(" ADD COLUMN ");
        builder.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name));
        builder.Append(" ");
        builder.Append(operation.ColumnType ?? GetColumnType(operation));

        if (!operation.IsNullable)
        {
            builder.Append(" NOT NULL");
        }

        if (operation.DefaultValueSql != null)
        {
            builder.Append(" DEFAULT ");
            builder.Append(operation.DefaultValueSql);
        }
        else if (operation.DefaultValue != null)
        {
            builder.Append(" DEFAULT ");
            builder.Append(GenerateSqlLiteral(operation.DefaultValue));
        }

        if (terminate)
        {
            builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            builder.EndCommand();
        }
    }

    protected override void Generate(DropColumnOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        builder.Append("ALTER TABLE ");
        builder.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema));
        builder.Append(" DROP COLUMN ");
        builder.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name));

        if (terminate)
        {
            builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            builder.EndCommand();
        }
    }

    protected override void Generate(RenameColumnOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        builder.Append("ALTER TABLE ");
        builder.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema));
        builder.Append(" RENAME COLUMN ");
        builder.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name));
        builder.Append(" TO ");
        builder.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.NewName));
        builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
        builder.EndCommand();
    }

    protected override void Generate(CreateIndexOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        builder.Append("CREATE ");
        if (operation.IsUnique) builder.Append("UNIQUE ");
        builder.Append("INDEX ");
        builder.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name));
        builder.Append(" ON ");
        builder.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema));
        builder.Append(" (");
        builder.Append(string.Join(", ", operation.Columns.Select(c => Dependencies.SqlGenerationHelper.DelimitIdentifier(c))));
        builder.Append(")");

        if (terminate)
        {
            builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            builder.EndCommand();
        }
    }

    protected override void Generate(DropIndexOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        builder.Append("DROP INDEX ");
        builder.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name));

        if (terminate)
        {
            builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            builder.EndCommand();
        }
    }

    private static string GetColumnType(AddColumnOperation column)
    {
        if (column.ClrType == typeof(int) || column.ClrType == typeof(int?))
            return "INT";
        if (column.ClrType == typeof(long) || column.ClrType == typeof(long?))
            return "BIGINT";
        if (column.ClrType == typeof(short) || column.ClrType == typeof(short?))
            return "SMALLINT";
        if (column.ClrType == typeof(bool) || column.ClrType == typeof(bool?))
            return "BOOLEAN";
        if (column.ClrType == typeof(string))
            return column.MaxLength.HasValue ? $"VARCHAR({column.MaxLength.Value})" : "TEXT";
        if (column.ClrType == typeof(decimal) || column.ClrType == typeof(decimal?))
            return "DECIMAL";
        if (column.ClrType == typeof(float) || column.ClrType == typeof(float?))
            return "FLOAT";
        if (column.ClrType == typeof(double) || column.ClrType == typeof(double?))
            return "DOUBLE";
        if (column.ClrType == typeof(DateTime) || column.ClrType == typeof(DateTime?))
            return "TIMESTAMP";
        if (column.ClrType == typeof(DateOnly) || column.ClrType == typeof(DateOnly?))
            return "DATE";
        if (column.ClrType == typeof(Guid) || column.ClrType == typeof(Guid?))
            return "UUID";
        return "TEXT";
    }

    private static string GetReferentialAction(ReferentialAction action) => action switch
    {
        ReferentialAction.Cascade => "CASCADE",
        ReferentialAction.SetNull => "SET NULL",
        ReferentialAction.SetDefault => "SET DEFAULT",
        ReferentialAction.Restrict => "RESTRICT",
        _ => "NO ACTION"
    };

    private static string GenerateSqlLiteral(object value) => value switch
    {
        string s => $"'{s.Replace("'", "''")}'",
        bool b => b ? "TRUE" : "FALSE",
        DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
        _ => value.ToString() ?? "NULL"
    };
}
