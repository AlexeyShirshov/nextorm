using nextorm.mysql;

namespace nextorm.mariadb;

/// <summary>
/// MariaDB dialect: the MySQL family plus the <c>INTERSECT ALL</c>/<c>EXCEPT ALL</c> set-operation
/// variants, which MariaDB 10.4+ implements and MySQL does not.
/// </summary>
public sealed class MariaDbDialect : MySqlDialect
{
    public static new readonly MariaDbDialect Instance = new();

    public override bool SupportsIntersectExceptAll => true;
}
