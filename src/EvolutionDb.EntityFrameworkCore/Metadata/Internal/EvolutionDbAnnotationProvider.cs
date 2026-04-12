using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EvolutionDb.EntityFrameworkCore.Metadata.Internal;

/// <summary>
/// Relational annotation provider for EvolutionDB.
/// Supplies provider-specific annotations for the relational model (tables, columns, indexes, etc.).
/// This is required for EF Core to properly build column mappings in the relational model,
/// which the query pipeline depends on (ConcreteColumnExpression uses property.GetTableColumnMappings()).
/// </summary>
public class EvolutionDbAnnotationProvider : RelationalAnnotationProvider
{
    public EvolutionDbAnnotationProvider(RelationalAnnotationProviderDependencies dependencies)
        : base(dependencies)
    {
        //...
    }

    public override IEnumerable<IAnnotation> For(IColumn column, bool designTime)
    {
        var property = column.PropertyMappings.FirstOrDefault()?.Property;
        if (property == null)
        {
            yield break;
        }

        // AUTO_INCREMENT annotation for integer PKs with ValueGeneratedOnAdd
        if (property.ValueGenerated == ValueGenerated.OnAdd
            && property.ClrType.IsInteger()
            && column.Table.PrimaryKey?.Columns.Count == 1
            && column.Table.PrimaryKey.Columns[0] == column)
        {
            yield return new Annotation("EvolutionDb:Autoincrement", true);
        }
    }

    public override IEnumerable<IAnnotation> For(IRelationalModel model, bool designTime)
    {
        return Enumerable.Empty<IAnnotation>();
    }

    public override IEnumerable<IAnnotation> For(ITable table, bool designTime)
    {
        return Enumerable.Empty<IAnnotation>();
    }

    public override IEnumerable<IAnnotation> For(IUniqueConstraint constraint, bool designTime)
    {
        return Enumerable.Empty<IAnnotation>();
    }

    public override IEnumerable<IAnnotation> For(ITableIndex index, bool designTime)
    {
        return Enumerable.Empty<IAnnotation>();
    }

    public override IEnumerable<IAnnotation> For(IForeignKeyConstraint foreignKey, bool designTime)
    {
        return Enumerable.Empty<IAnnotation>();
    }
}

internal static class TypeExtensions
{
    public static bool IsInteger(this Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(int)
               || type == typeof(long)
               || type == typeof(short)
               || type == typeof(byte)
               || type == typeof(uint)
               || type == typeof(ulong)
               || type == typeof(ushort)
               || type == typeof(sbyte);
    }
}
