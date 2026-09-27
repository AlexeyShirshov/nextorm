using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore;

/// <summary>
/// EF Core options extension that carries the optional nextorm <see cref="DataContextBuilder"/>
/// configuration stored by <c>UseNextOrm</c>.
/// </summary>
/// <remarks>
/// The extension contributes no services: the bridge builds its context lazily from the
/// <see cref="DbContext"/> connection, so <see cref="ApplyServices"/> is deliberately a no-op and the
/// delegate is read back by <c>GetNextOrmContext</c>.
/// </remarks>
internal sealed class NextOrmOptionsExtension : IDbContextOptionsExtension
{
    public NextOrmOptionsExtension(Action<DataContextBuilder>? configure)
    {
        Configure = configure;
    }

    public Action<DataContextBuilder>? Configure { get; }

    public DbContextOptionsExtensionInfo Info => new ExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
    {
    }

    public void Validate(IDbContextOptions options)
    {
    }

    private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
    {
        public ExtensionInfo(IDbContextOptionsExtension extension)
            : base(extension)
        {
        }

        public override bool IsDatabaseProvider => false;

        public override string LogFragment
            => ((NextOrmOptionsExtension)Extension).Configure is null ? "NextOrm " : "NextOrmConfigured ";

        public override int GetServiceProviderHashCode() => 0;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is ExtensionInfo;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["NextOrm"] = ((NextOrmOptionsExtension)Extension).Configure is null ? "0" : "1";
    }
}
