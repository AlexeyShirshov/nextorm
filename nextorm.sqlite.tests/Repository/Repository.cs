using nextorm.core;

namespace nextorm.sqlite.tests;

public abstract class Repository
{
    public abstract Entity<SimpleEntity> SimpleEntity { get; }
}
