using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace NextORM.Core;

/// <summary>
/// Process-wide pool of <see cref="StringBuilder"/> instances used as scratch buffers while
/// rendering SQL (and for the verbose query log).
/// <para>
/// It lives outside <c>DataContext</c> so the collaborators that need a buffer — <c>SqlBuilder</c>,
/// the expression visitors and <c>ResultSetEnumerator</c> — depend on a dedicated type instead of
/// reaching into the concrete data context. One pool for all of them also removes the previous
/// pair of identical pools.
/// </para>
/// </summary>
internal static class StringBuilderPool
{
    public static readonly ObjectPool<StringBuilder> Shared =
        new DefaultObjectPoolProvider().Create(new StringBuilderPooledObjectPolicy());
}
