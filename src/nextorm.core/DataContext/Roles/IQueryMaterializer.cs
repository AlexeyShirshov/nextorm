namespace nextorm.core;

/// <summary>
/// Narrowest contract for a component that only needs to prepare a query and read its rows
/// (planning + materialization) without running terminal operators. Used as a parameter type
/// so such components do not have to depend on the whole <see cref="IDataContext"/>.
/// </summary>
public interface IQueryMaterializer : IQueryPlanner, IRowReaderFactory
{
}
