using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MoissonMontreal.Api.Data;

/// <summary>
/// Converts a date and time to UTC before it is written, because Npgsql only accepts UTC values for timestamptz columns.
/// Values are read back unchanged, in UTC.
/// </summary>
public class UtcDateTimeOffsetConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>(v => v.ToUniversalTime(), v => v);
