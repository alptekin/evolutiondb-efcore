using System.Text;
using Microsoft.EntityFrameworkCore.Storage;

namespace EvoSQL.EntityFrameworkCore.Storage.Internal;

public class EvoSqlSqlGenerationHelper : RelationalSqlGenerationHelper
{
    public EvoSqlSqlGenerationHelper(RelationalSqlGenerationHelperDependencies dependencies)
        : base(dependencies)
    {
    }

    public override string StatementTerminator => ";";
    public override string BatchTerminator => string.Empty;

    public override string DelimitIdentifier(string identifier)
        => $"\"{EscapeIdentifier(identifier)}\"";

    public override void DelimitIdentifier(StringBuilder builder, string identifier)
    {
        builder.Append('"');
        EscapeIdentifier(builder, identifier);
        builder.Append('"');
    }

    public override string DelimitIdentifier(string name, string? schema)
    {
        return schema != null
            ? $"{DelimitIdentifier(schema)}.{DelimitIdentifier(name)}"
            : DelimitIdentifier(name);
    }

    public override string EscapeIdentifier(string identifier)
        => identifier.Replace("\"", "\"\"");

    public override void EscapeIdentifier(StringBuilder builder, string identifier)
        => builder.Append(identifier.Replace("\"", "\"\""));

    public override string GenerateParameterName(string name)
        => $"@{name}";

    public override void GenerateParameterName(StringBuilder builder, string name)
    {
        builder.Append('@');
        builder.Append(name);
    }
}
