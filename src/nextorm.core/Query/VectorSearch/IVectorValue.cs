namespace NextORM.Core;

/// <summary>
/// A vector value accepted by the provider-independent nearest-neighbor API. Provider-specific vector
/// types can implement this contract without making the core assembly depend on a provider.
/// </summary>
public interface IVectorValue
{
    /// <summary>Gets the number of coordinates represented by this vector.</summary>
    int Dimensions { get; }
}
