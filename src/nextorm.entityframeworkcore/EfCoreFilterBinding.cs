using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using NextORM.Core;

namespace NextORM.EntityFrameworkCore;

/// <summary>
/// Binds each successfully created nextorm <see cref="IDataContext"/> to the exact EF Core
/// <see cref="DbContext"/> it was created from. An imported EF Core query filter that reads instance
/// members of its context is rewritten to call <see cref="GetOwner"/> with the executing nextorm
/// context, so the filter always evaluates against the live owner rather than the context that built
/// the (cached) EF model.
/// </summary>
/// <remarks>
/// The table is a <see cref="ConditionalWeakTable{TKey,TValue}"/>: it holds no strong reference to
/// either the nextorm context or the EF context, so neither is kept alive by the bridge. The owner is
/// attached by <c>EntityFrameworkCoreExtensions.CreateContext</c> after the bridge metadata was
/// published successfully. An imported filter lives in process-wide metadata, so it also runs on a
/// plain nextorm context created without the bridge; <see cref="GetOwner"/> then fails closed with a
/// documented <see cref="InvalidOperationException"/> naming the missing owner and the bridge
/// requirement, instead of leaving a null owner to throw a raw <see cref="NullReferenceException"/>.
/// </remarks>
internal static class EfCoreFilterBinding
{
    private static readonly ConditionalWeakTable<IDataContext, DbContext> Owners = new();

    /// <summary>
    /// Records <paramref name="owner"/> as the live EF Core context behind <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The successfully created nextorm context.</param>
    /// <param name="owner">The EF Core context the nextorm context was built from.</param>
    public static void Bind(IDataContext context, DbContext owner)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(owner);

        Owners.AddOrUpdate(context, owner);
    }

    /// <summary>
    /// Returns the EF Core context bound to <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The executing nextorm context.</param>
    /// <returns>The bound EF Core context.</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="context"/> was not created through the bridge, so it has no owning
    /// <see cref="DbContext"/>. This happens when a filter imported into process-wide metadata runs on a
    /// plain nextorm context.
    /// </exception>
    public static DbContext GetOwner(IDataContext context)
    {
        if (context is not null && Owners.TryGetValue(context, out var owner))
            return owner;

        throw new InvalidOperationException(
            "The EF Core query filter could not resolve its owning DbContext: the executing NextORM context " +
            "is not bound to an EF Core DbContext. The filter was imported from an EF Core model, so it must " +
            "run on a context created through the EF Core bridge (CreateNextOrmContext, GetNextOrmContext, " +
            "ToNextOrm or AddNextOrmFromDbContext). A plain NextORM context shares the process-wide filter " +
            "metadata but has no owning DbContext for the filter to read.");
    }
}
