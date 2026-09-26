using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Read-only mapping metadata for a single entity property: its CLR member and target column.
/// </summary>
/// <remarks>
/// Renamed from <c>IPropertyMeta</c> for consistency with the surrounding metadata types.
/// See <c>docs/specs/design/API-NAMING-REVIEW.md</c> finding P1-11.
/// </remarks>
public interface IPropertyMetadata
{
    /// <summary>
    /// The column-pair descriptor of a <see cref="Range{T}"/> property stored as two scalar columns,
    /// or <see langword="null"/> when the property maps to a single column. Declared with
    /// <see cref="RangeColumnsAttribute"/> or set fluently with
    /// <see cref="EntityPropertyBuilder{T}.RangeColumns(string, string, bool, bool)"/>. The default
    /// implementation returns <see langword="null"/> so existing external implementations keep
    /// compiling.
    /// </summary>
    RangeColumnsMetadata? RangeColumns => null;

    /// <summary>
    /// The CLR property this mapping describes.
    /// </summary>
    PropertyInfo PropertyInfo { get; }
    /// <summary>
    /// The column that <see cref="PropertyInfo"/> maps to.
    /// </summary>
    string ColumnName { get; }

    /// <summary>
    /// Whether <see cref="ColumnName"/> was derived from the property name rather than declared with
    /// an attribute or a fluent mapping. An active <see cref="INamingConvention"/> is applied only to
    /// auto names. The default implementation returns <see langword="false"/> (treat an unknown
    /// mapping as declared) so existing external implementations keep compiling.
    /// </summary>
    bool IsColumnNameAuto => false;

    /// <summary>
    /// Whether the property is (part of) the entity key. Declared with
    /// <see cref="System.ComponentModel.DataAnnotations.KeyAttribute"/>, inferred from the
    /// <c>Id</c>/<c>&lt;TypeName&gt;Id</c> convention, or set fluently with
    /// <see cref="EntityPropertyBuilder{T}.Key"/>. Used by the DML builders to address a row; the
    /// default implementation returns <see langword="false"/> so existing external implementations
    /// keep compiling.
    /// </summary>
    bool IsKey => false;

    /// <summary>
    /// Whether the database generates the property's value (an identity/auto-increment column). Such a
    /// property is excluded from the values written by an insert. Declared with
    /// <see cref="System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedAttribute"/> or set
    /// fluently with <see cref="EntityPropertyBuilder{T}.Identity"/>. The default implementation
    /// returns <see langword="false"/> so existing external implementations keep compiling.
    /// </summary>
    bool IsIdentity => false;

    /// <summary>
    /// Whether the database generates the property's value and it can never be written (a computed
    /// column). Such a property is excluded from the values written by an insert. Declared with
    /// <see cref="System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedAttribute"/> or set
    /// fluently with <see cref="EntityPropertyBuilder{T}.Computed"/>. The default implementation
    /// returns <see langword="false"/> so existing external implementations keep compiling.
    /// </summary>
    bool IsComputed => false;

    /// <summary>
    /// The unit in which a <see cref="System.TimeSpan"/> property is stored on a provider without a
    /// native duration type, or <see langword="null"/> when the property is not a duration or uses the
    /// provider's native type. Declared with <see cref="DurationAttribute"/> or set fluently with
    /// <see cref="EntityPropertyBuilder{T}.Duration(DurationUnit, int)"/>. The default implementation
    /// returns <see langword="null"/> so existing external implementations keep compiling.
    /// </summary>
    DurationUnit? DurationUnit => null;

    /// <summary>
    /// The fractional-second precision of a native duration type (for example <c>TIME(3)</c>), or zero
    /// for the provider default. Declared with <see cref="DurationAttribute.Precision"/> or the fluent
    /// duration mapping. The default implementation returns zero so existing external implementations
    /// keep compiling.
    /// </summary>
    int DurationPrecision => 0;

    /// <summary>
    /// The provider-native collation declared for the property's column, or <see langword="null"/>
    /// when the column follows the database default. Declared with <see cref="CollationAttribute"/> or
    /// set fluently with <see cref="EntityPropertyBuilder{T}.Collation(string)"/>. The value is applied
    /// to the column in collation-sensitive query operations;
    /// <see cref="ISqlDialect.SupportsCollation"/> must be <see langword="true"/>. The default
    /// implementation returns <see langword="null"/> so existing external implementations keep
    /// compiling.
    /// </summary>
    string? Collation => null;

    /// <summary>
    /// The value converter that maps the property between its CLR model type and the provider
    /// representation, or <see langword="null"/> when the property is stored as its model type.
    /// Declared with <see cref="ValueConverterAttribute"/> or <see cref="JsonColumnAttribute"/>, or set
    /// fluently with <see cref="EntityPropertyBuilder{T}.HasConversion(IPropertyValueConverter)"/> or
    /// <see cref="EntityPropertyBuilder{T}.JsonColumn(Action{JsonColumnOptions}?)"/>. The default
    /// implementation returns <see langword="null"/> so existing external implementations keep
    /// compiling.
    /// </summary>
    IPropertyValueConverter? Converter => null;

    /// <summary>
    /// Whether the property is the entity's dynamic-columns store: it receives the columns of a read
    /// row that are not mapped to a declared member instead of mapping to a column itself. Declared
    /// with <see cref="DynamicColumnsAttribute"/> or set fluently with
    /// <see cref="EntityPropertyBuilder{T}.DynamicColumnsStore"/>. A dynamic-columns store is not part
    /// of <see cref="IEntityMetadata.Properties"/>; it is exposed through
    /// <see cref="IEntityMetadata.DynamicColumnsStore"/>. The default implementation returns
    /// <see langword="false"/> so existing external implementations keep compiling.
    /// </summary>
    bool IsDynamicColumnsStore => false;
}