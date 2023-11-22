using nextorm.core;

namespace nextorm.sqlite.tests;

public abstract class Repository
{
    public abstract CommandBuilder<SimpleEntity> SimpleEntity { get; }
}
