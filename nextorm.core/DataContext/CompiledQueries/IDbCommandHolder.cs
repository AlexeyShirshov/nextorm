using System.Data.Common;

namespace nextorm.core;

internal interface IDbCommandHolder
{
    void ResetConnection(DbConnection conn);
    // DbConnection? Connection { get; set; }
}