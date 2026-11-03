namespace NextORM.Core;

/// <summary>
/// The reader flavour a <see cref="RawReaderDescriptor"/> describes: the locator-suppressed
/// multi-column result reader (SQLite rowid path) or the LOB/streaming reader (all other providers).
/// </summary>
internal enum RawReaderFlavor
{
    Result = 0,
    Lob = 1,
}
