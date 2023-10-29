using Microsoft.EntityFrameworkCore;

namespace nextorm.core.benchmark;
class EFDataContext : DbContext
{
    public EFDataContext(DbContextOptions<EFDataContext> options) : base(options)
    {
    }
    public DbSet<SimpleEntity> SimpleEntities { get; set; }
}