namespace nextorm.core;

public interface IPreparedQueryCommand
{
    bool IsScalar { get; }
}