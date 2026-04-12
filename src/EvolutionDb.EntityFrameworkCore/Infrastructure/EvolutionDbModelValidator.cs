using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EvolutionDb.EntityFrameworkCore.Infrastructure;

/// <summary>
/// Model validator for EvolutionDB.
/// Validates the model for EvolutionDB-specific constraints and limitations.
/// </summary>
public class EvolutionDbModelValidator : RelationalModelValidator
{
    public EvolutionDbModelValidator(ModelValidatorDependencies dependencies, RelationalModelValidatorDependencies relationalDependencies)
        : base(dependencies, relationalDependencies)
    {
    }
}
