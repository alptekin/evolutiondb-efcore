using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using EvoSQL.EntityFrameworkCore.Extensions;

namespace EvoSQL.EntityFrameworkCore.Infrastructure;

public class EvoSqlOptionsExtension : RelationalOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;

    public EvoSqlOptionsExtension()
    {
    }

    protected EvoSqlOptionsExtension(EvoSqlOptionsExtension copyFrom)
        : base(copyFrom)
    {
    }

    public override DbContextOptionsExtensionInfo Info
        => _info ??= new ExtensionInfo(this);

    protected override RelationalOptionsExtension Clone()
        => new EvoSqlOptionsExtension(this);

    public override void ApplyServices(IServiceCollection services)
        => services.AddEntityFrameworkEvoSql();

    private sealed class ExtensionInfo : RelationalExtensionInfo
    {
        public ExtensionInfo(IDbContextOptionsExtension extension)
            : base(extension)
        {
        }

        private new EvoSqlOptionsExtension Extension
            => (EvoSqlOptionsExtension)base.Extension;

        public override bool IsDatabaseProvider => true;

        public override string LogFragment => "Using EvoSQL ";

        public override int GetServiceProviderHashCode()
            => base.GetServiceProviderHashCode();

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["EvoSQL"] = "1";
    }
}
