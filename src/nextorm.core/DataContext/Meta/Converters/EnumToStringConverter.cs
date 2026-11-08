namespace NextORM.Core;

/// <summary>
/// A built-in <see cref="ValueConverter{TModel,TProvider}"/> that stores an enum as its name
/// (<see cref="Enum.ToString()"/>) in a text column and parses it back with
/// <see cref="Enum.Parse{TEnum}(string)"/>. It is the ready-made equivalent of a hand-written
/// <c>ValueConverter&lt;TEnum, string&gt;</c>, so it can be attached with
/// <see cref="ValueConverterAttribute"/> or <c>HasConversion</c> and needs no custom class.
/// </summary>
/// <typeparam name="TEnum">The enum type stored as its name.</typeparam>
public sealed class EnumToStringConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    /// <inheritdoc/>
    public override string? ConvertToProvider(TEnum model) => model.ToString();

    /// <inheritdoc/>
    public override TEnum ConvertFromProvider(string? provider) => Enum.Parse<TEnum>(provider!);
}
