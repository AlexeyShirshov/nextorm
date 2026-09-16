using nextorm.core;

namespace nextorm.integration.tests;

/// <summary>
/// Describes a database provider the integration suite can run against. Each provider owns the
/// schema and seed data, so the shared tests can run unchanged on every supported database.
/// </summary>
public interface ITestProvider
{
    string Name { get; }

    bool IsAvailable { get; }

    string SkipReason { get; }

    /// <summary>
    /// True when AVG over an integer column keeps the fractional result. PostgreSQL and SQLite
    /// return a fractional value, while a SQL Server integer AVG is itself an integer, so the
    /// shared assertion does not apply there.
    /// </summary>
    bool SupportsFractionalAverage { get; }

    void EnsureSeeded();

    IDataContext CreateContext();
}
