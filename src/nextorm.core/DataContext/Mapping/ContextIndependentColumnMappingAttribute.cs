namespace NextORM.Core;

// An engine-owned override can share its compiled accessors when the column shape and effective
// dialect agree. Other overrides can read or capture the context that supplies the accessor.
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
internal sealed class ContextIndependentColumnMappingAttribute : Attribute;
