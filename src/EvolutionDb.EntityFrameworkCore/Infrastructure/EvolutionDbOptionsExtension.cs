using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using EvolutionDb.EntityFrameworkCore.Extensions;

namespace EvolutionDb.EntityFrameworkCore.Infrastructure;

public class EvolutionDbOptionsExtension : RelationalOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;

    public EvolutionDbOptionsExtension()
    {
        //...
    }

    protected EvolutionDbOptionsExtension(EvolutionDbOptionsExtension copyFrom)
        : base(copyFrom)
    {
        //...
    }

    public override DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

    protected override RelationalOptionsExtension Clone() => new EvolutionDbOptionsExtension(this);

    public override void ApplyServices(IServiceCollection services) => services.AddEntityFrameworkEvolutionDb();

    private sealed class ExtensionInfo : RelationalExtensionInfo
    {
        public ExtensionInfo(IDbContextOptionsExtension extension)
            : base(extension)
        {
            //...
        }

        private new EvolutionDbOptionsExtension Extension => (EvolutionDbOptionsExtension)base.Extension;

        public override bool IsDatabaseProvider => true;

        public override string LogFragment => "Using EvolutionDB ";

        public override int GetServiceProviderHashCode() => base.GetServiceProviderHashCode();

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is ExtensionInfo;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) => debugInfo["EvolutionDB"] = "1";
    }
}
