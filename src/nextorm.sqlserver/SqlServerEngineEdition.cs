namespace NextORM.SqlServer;

/// <summary>
/// The SQL Server engine edition a <see cref="SqlServerDataContext"/> declares, mirroring the values of
/// <c>SERVERPROPERTY('EngineEdition')</c>. The declaration is immutable and supplied by the caller: nextorm
/// never queries the server for this value, so the emitted SQL stays a pure function of the constructor
/// arguments.
/// <para>
/// Only <see cref="SqlDatabase"/> (5), <see cref="AzureSqlManagedInstance"/> (8) and
/// <see cref="MicrosoftFabricSqlDatabase"/> (12) grant the edition-derived native <c>json</c> capability,
/// because those are the products whose engine has the native <c>json</c> data type while reporting a
/// <c>ProductMajorVersion</c> below 17. <see cref="AzureSynapseServerlessSqlPoolOrMicrosoftFabric"/> (11)
/// does <b>not</b> grant it: the value covers "Azure Synapse serverless SQL pool, or Microsoft Fabric", and
/// the serverless pool has no <c>json</c> column type. Every other value, and an unset edition, grant
/// nothing; the unchanged version gate (major 17 or higher) still applies on its own.
/// </para>
/// </summary>
public enum SqlServerEngineEdition
{
    /// <summary>Personal or Desktop Engine (not available in SQL Server 2005 (9.x) and later).</summary>
    PersonalOrDesktop = 1,

    /// <summary>Standard edition, including Standard Developer, Web and Business Intelligence.</summary>
    Standard = 2,

    /// <summary>Enterprise edition, including Enterprise Developer, Developer and Evaluation.</summary>
    Enterprise = 3,

    /// <summary>Express edition, including Express with Tools and Express with Advanced Services.</summary>
    Express = 4,

    /// <summary>Azure SQL Database; grants the edition-derived native <c>json</c> capability.</summary>
    SqlDatabase = 5,

    /// <summary>Azure Synapse Analytics; grants no edition-derived capability.</summary>
    AzureSynapseAnalytics = 6,

    /// <summary>Azure SQL Managed Instance; grants the edition-derived native <c>json</c> capability.</summary>
    AzureSqlManagedInstance = 8,

    /// <summary>Azure SQL Edge; grants no edition-derived capability.</summary>
    AzureSqlEdge = 9,

    /// <summary>
    /// Azure Synapse serverless SQL pool, or Microsoft Fabric. Deliberately grants no edition-derived
    /// capability: the serverless pool has no native <c>json</c> column type.
    /// </summary>
    AzureSynapseServerlessSqlPoolOrMicrosoftFabric = 11,

    /// <summary>Microsoft Fabric SQL database; grants the edition-derived native <c>json</c> capability.</summary>
    MicrosoftFabricSqlDatabase = 12,
}
