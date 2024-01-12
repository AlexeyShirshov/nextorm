namespace nextorm.core;

public interface IPreparedQueryCommand<TResult>
{
    bool IsScalar { get; }
}