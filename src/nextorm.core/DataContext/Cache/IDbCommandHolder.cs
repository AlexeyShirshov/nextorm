using System.Data.Common;

namespace NextORM.Core;

internal interface IDbCommandHolder
{
    void ResetConnection(DbConnection conn, IDataContext dbContext);
}