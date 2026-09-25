using System.Data.Common;

namespace NextORM.Core;

/// <summary>
/// Metadata about a command as it moves through the execution lifecycle. Created by the context's
/// execution axis and passed to the interceptors registered for it.
/// </summary>
/// <param name="DataContext">The context that owns the command being executed.</param>
/// <param name="Sql">The command text, or <see langword="null"/> when it is not known.</param>
public readonly record struct CommandEventData(IDataContext DataContext, string? Sql);

/// <summary>
/// Observes the lifecycle of the commands a context executes. The events are raised by the context's
/// execution axis, not by the command: a <see cref="DbCommand"/> is only the event payload and cannot
/// log or profile itself.
/// </summary>
/// <remarks>
/// Register implementations with <c>DataContextBuilder.AddInterceptor</c> (applies to every context the
/// builder creates) or <c>DataContext.AddInterceptor</c> (per context instance). Interceptors are
/// invoked in registration order and must be thread-safe, since a context may execute concurrently.
/// A command created for a cached query plan is reused across executions, so implementations must not
/// change <see cref="DbCommand.CommandText"/>.
/// </remarks>
public interface IQueryInterceptor
{
    /// <summary>
    /// Called when a command has been created and its parameters bound. For a query it is raised once,
    /// by the planning axis, when the plan is built (a cache hit does not raise it again); for a DML
    /// statement it is raised on every execution, because mutation commands are not cached.
    /// </summary>
    /// <param name="eventData">Metadata about the command.</param>
    /// <param name="command">The command that was created.</param>
    void CommandInitialized(CommandEventData eventData, DbCommand command) { }

    /// <summary>Called immediately before the command is executed.</summary>
    /// <param name="eventData">Metadata about the command.</param>
    /// <param name="command">The command about to execute.</param>
    void CommandExecuting(CommandEventData eventData, DbCommand command) { }

    /// <summary>Called after the command has executed successfully.</summary>
    /// <param name="eventData">Metadata about the command.</param>
    /// <param name="command">The command that executed.</param>
    /// <param name="elapsed">The time spent executing the command.</param>
    void CommandExecuted(CommandEventData eventData, DbCommand command, TimeSpan elapsed) { }

    /// <summary>Called when executing the command throws.</summary>
    /// <param name="eventData">Metadata about the command.</param>
    /// <param name="command">The command that failed.</param>
    /// <param name="exception">The exception thrown by the provider.</param>
    void CommandFailed(CommandEventData eventData, DbCommand command, Exception exception) { }
}
