using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace EvolutionDb.EntityFrameworkCore.Metadata.Conventions;

/// <summary>
/// Convention set builder for EvolutionDB.
/// Builds the convention set used during model building, which determines
/// how entity types, properties, keys, etc. map to relational concepts
/// (tables, columns, constraints).
/// </summary>
public class EvolutionDbConventionSetBuilder : RelationalConventionSetBuilder
{
    public EvolutionDbConventionSetBuilder(ProviderConventionSetBuilderDependencies dependencies, RelationalConventionSetBuilderDependencies relationalDependencies)
        : base(dependencies, relationalDependencies)
    {
    }

    public override ConventionSet CreateConventionSet()
    {
        var conventionSet = base.CreateConventionSet();

        // EvolutionDB uses the default relational conventions.
        // Provider-specific convention replacements can be added here in the future
        // (e.g., for custom runtime model conventions, shared table conventions, etc.)

        return conventionSet;
    }
}
