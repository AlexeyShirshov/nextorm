using Microsoft.EntityFrameworkCore;

namespace nextorm.core.benchmark;
class EFDataContext : Microsoft.EntityFrameworkCore.DbContext
{
    public EFDataContext(DbContextOptions<EFDataContext> options) : base(options)
    {
    }
    public DbSet<SimpleEntity> SimpleEntities { get; set; }
    public DbSet<LargeEntity> LargeEntities { get; set; }
}